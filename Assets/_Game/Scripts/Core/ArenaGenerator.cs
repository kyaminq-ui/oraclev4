using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// Génère procéduralement une arène de combat 1v1 isométrique.
///
/// PIPELINE DE GÉNÉRATION :
///   Awake  → synchronise les dimensions ArenaConfig → GridConfig (avant GridManager)
///   Start  → Generate() si generateOnStart est activé
///
/// ALGORITHME :
///   1. Remplit le sol avec des patchs corrélés ou un tirage aléatoire (config)
///   2. Définit les zones de spawn (gauche = T1, droite = T2)
///   3. Place des obstacles (biais groupement voisin), symétrie miroir, BFS
///   3b. Nettoie sang/herbe trop près des spawns dans la zone de combat
///   4. Applique IsWalkable sur chaque Cell via GridManager
///   5. Crée les GameObjects visuels pour chaque tile
///   6. Colorie les zones de spawn via le système de highlight existant
///
/// SETUP UNITY :
///   - Ce MonoBehaviour doit être dans la scène avec GridManager
///   - Assigner ArenaConfig et GridConfig dans l'Inspector
///   - Créer les assets ScriptableObject : ArenaConfig + TileSpriteRegistry
/// </summary>
[DefaultExecutionOrder(-5)]  // S'exécute avant GridManager (order 0) pour ajuster les dimensions
public class ArenaGenerator : MonoBehaviour
{
    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("=== CONFIGURATION ===")]
    [Tooltip("Asset ArenaConfig contenant tous les paramètres de génération.")]
    public ArenaConfig arenaConfig;

    [Tooltip("Asset GridConfig partagé avec GridManager. " +
             "ArenaGenerator y écrit les dimensions avant que GridManager n'initialise la grille.")]
    public GridConfig gridConfig;

    [Header("=== OPTIONS ===")]
    [Tooltip("Lancer la génération automatiquement au démarrage de la scène.")]
    public bool generateOnStart = true;

    [Tooltip("Afficher des gizmos de debug procéduraux (spawn, obstacles) dans la Scene view.")]
    public bool showDebugGizmos = true;

    // =========================================================
    // DONNÉES INTERNES
    // =========================================================

    private CellTileType[,] arenaData;
    private List<Cell>       spawnCellsTeam1 = new List<Cell>();
    private List<Cell>       spawnCellsTeam2 = new List<Cell>();
    private Transform        tileContainer;
    private System.Random    rng;
    private int              effectiveSeed;

    // Directions cardinales pour le BFS de connectivité
    private static readonly int[] DX = {  0,  0,  1, -1 };
    private static readonly int[] DY = {  1, -1,  0,  0 };

    // =========================================================
    // CYCLE DE VIE
    // =========================================================

    void Awake()
    {
        // Même fichier GridConfig que GridManager : dimensions écrites ici avant son InitializeGrid().
        SyncArenaDimensionsIntoGridConfig();
    }

    /// <summary>Copie ArenaConfig.width/height vers gridConfig pour que GridManager construise le bon tableau de Cell.</summary>
    void SyncArenaDimensionsIntoGridConfig()
    {
        if (arenaConfig == null || gridConfig == null) return;
        gridConfig.width  = arenaConfig.arenaWidth;
        gridConfig.height = arenaConfig.arenaHeight;
    }

    void Start()
    {
        if (generateOnStart)
            Generate();
    }

    // =========================================================
    // POINT D'ENTRÉE PUBLIC
    // =========================================================

    /// <summary>
    /// Génère (ou régénère) l'arène.
    /// Peut être appelé plusieurs fois en cours de partie pour créer une nouvelle carte.
    /// Pour repartir à zéro après changement de taille sans Play, préférez <see cref="RegenerateArena"/>.
    /// </summary>
    public void Generate()
    {
        if (!ValidateSetup()) return;

        effectiveSeed = arenaConfig.seed < 0
            ? Random.Range(0, int.MaxValue)
            : arenaConfig.seed;

        rng = new System.Random(effectiveSeed);

        Debug.Log($"[ArenaGenerator] Génération {arenaConfig.arenaWidth}x{arenaConfig.arenaHeight}" +
                  $" | Seed : {effectiveSeed}" +
                  $" | Densité obstacles : {arenaConfig.obstacleDensity:P0}");

        ClearPreviousArena();

        // Pipeline
        Step1_InitializeArenaData();
        Step2_SetupSpawnZones();
        Step3_GenerateObstacles();
        Step3b_CleanupVariantsNearSpawn();
        Step4_ApplyToGrid();
        Step5_RenderTiles();
        Step6_HighlightSpawnZones();

        Debug.Log($"[ArenaGenerator] Arène prête ! " +
                  $"Spawn T1 : {spawnCellsTeam1.Count} cases | " +
                  $"Spawn T2 : {spawnCellsTeam2.Count} cases");
    }

    /// <summary>
    /// Synchronise les dimensions depuis <see cref="ArenaConfig"/>, appelle <see cref="GridManager.RegenerateGrid"/>,
    /// puis régénère l'arène complète. À utiliser après un changement de taille dans l'Inspector ou via le menu contextuel du composant.
    /// </summary>
    [ContextMenu("Regenerate Arena")]
    public void RegenerateArena()
    {
        if (!ValidateSetup()) return;

        SyncArenaDimensionsIntoGridConfig();
#if UNITY_EDITOR
        if (gridConfig != null)
            EditorUtility.SetDirty(gridConfig);
#endif

        GridManager.Instance.RegenerateGrid();
        Generate();
    }

    // =========================================================
    // ÉTAPE 1 — Données de sol
    // =========================================================

    void Step1_InitializeArenaData()
    {
        int w = arenaConfig.arenaWidth;
        int h = arenaConfig.arenaHeight;
        arenaData = new CellTileType[w, h];

        if (!arenaConfig.useTerrainPatches || w * h <= 1)
        {
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                arenaData[x, y] = RollTerrainIidCell();
        }
        else
        {
            float[,] field = SampleTerrainNoiseField(w, h);
            for (int pass = 0; pass < arenaConfig.terrainSmoothingPasses; pass++)
                BoxBlurFloatField(field, w, h);

            var sorted = BuildSortedTerrainCells(field, w, h);
            FillArenaFromTerrainOrder(sorted, w, h);
        }

        Step1_EnsureNeutralLogicalCenterGround();
    }

    void Step1_EnsureNeutralLogicalCenterGround()
    {
        int cx = TileSpriteRegistry.LogicalCenterX(arenaConfig.arenaWidth);
        int cy = TileSpriteRegistry.LogicalCenterY(arenaConfig.arenaHeight);
        arenaData[cx, cy] = CellTileType.Ground;
    }

    CellTileType RollTerrainIidCell()
    {
        float roll = (float)rng.NextDouble();
        if (roll < arenaConfig.bloodTileChance)
            return CellTileType.GroundBlood;
        if (roll < arenaConfig.bloodTileChance + arenaConfig.grassTileChance)
            return CellTileType.GroundGrass;
        return CellTileType.Ground;
    }

    float[,] SampleTerrainNoiseField(int w, int h)
    {
        float[,] field = new float[w, h];
        float ox = (float)(rng.NextDouble() * 9000.0);
        float oy = (float)(rng.NextDouble() * 9000.0);
        float sc = Mathf.Max(0.02f, arenaConfig.terrainNoiseScale);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.PerlinNoise(ox + x * sc, oy + y * sc);
                float b = Mathf.PerlinNoise(oy + x * sc * 1.37f + 251f, ox + y * sc * 1.11f + 503f);
                field[x, y] = Mathf.Clamp01((a + b) * 0.5f);
            }
        }
        return field;
    }

    static void BoxBlurFloatField(float[,] field, int w, int h)
    {
        float[,] next = new float[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float sum = field[x, y];
                int    cnt = 1;
                if (x > 0) { sum += field[x - 1, y]; cnt++; }
                if (x < w - 1) { sum += field[x + 1, y]; cnt++; }
                if (y > 0) { sum += field[x, y - 1]; cnt++; }
                if (y < h - 1) { sum += field[x, y + 1]; cnt++; }

                next[x, y] = sum / cnt;
            }
        }
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            field[x, y] = Mathf.Clamp01(next[x, y]);
    }

    struct TerrainCellOrder
    {
        public float coherence;
        public int x, y;
    }

    List<TerrainCellOrder> BuildSortedTerrainCells(float[,] field, int w, int h)
    {
        var cells = new List<TerrainCellOrder>(w * h);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
                cells.Add(new TerrainCellOrder { coherence = field[x, y], x = x, y = y });
        }
        cells.Sort((a, b) => a.coherence.CompareTo(b.coherence));
        return cells;
    }

    void FillArenaFromTerrainOrder(List<TerrainCellOrder> sorted, int w, int h)
    {
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            arenaData[x, y] = CellTileType.Ground;

        int total       = Mathf.Max(1, w * h);
        int bloodMarked = Mathf.Clamp(Mathf.RoundToInt(total * arenaConfig.bloodTileChance), 0, total);
        int grassMarked = Mathf.Clamp(Mathf.RoundToInt(total * arenaConfig.grassTileChance), 0, total - bloodMarked);

        int i = 0;
        for (int b = 0; b < bloodMarked && i < sorted.Count; b++, i++)
            arenaData[sorted[i].x, sorted[i].y] = CellTileType.GroundBlood;
        for (int g = 0; g < grassMarked && i < sorted.Count; g++, i++)
            arenaData[sorted[i].x, sorted[i].y] = CellTileType.GroundGrass;
    }

    // =========================================================
    // ÉTAPE 2 — Zones de spawn
    // =========================================================

    void Step2_SetupSpawnZones()
    {
        int w     = arenaConfig.arenaWidth;
        int h     = arenaConfig.arenaHeight;
        int depth = arenaConfig.spawnZoneDepth;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < depth; x++)
                arenaData[x, y] = CellTileType.SpawnTeam1;

            for (int x = w - depth; x < w; x++)
                arenaData[x, y] = CellTileType.SpawnTeam2;
        }
    }

    // =========================================================
    // ÉTAPE 3 — Obstacles symétriques avec vérification de connectivité
    // =========================================================

    void Step3_GenerateObstacles()
    {
        int w         = arenaConfig.arenaWidth;
        int h         = arenaConfig.arenaHeight;
        int depth     = arenaConfig.spawnZoneDepth;
        int clearance = arenaConfig.minClearanceFromSpawn;
        int borderM   = Mathf.Clamp(arenaConfig.obstacleBorderMargin, 0, 4);

        // Zone de combat accessible aux obstacles
        int combatXMin = Mathf.Max(depth + clearance, borderM);
        int combatXMax = Mathf.Min(w - depth - clearance - 1, w - 1 - borderM);

        if (combatXMin > combatXMax)
        {
            Debug.LogWarning("[ArenaGenerator] Zone de combat trop étroite pour placer des obstacles " +
                             "(augmente arenaWidth ou réduis spawnZoneDepth / minClearanceFromSpawn).");
            return;
        }

        // On ne travaille que sur la moitié gauche, puis on miroire sur la droite
        int halfXMax = (combatXMin + combatXMax) / 2;

        // Construire et mélanger la liste des candidats
        int yMin = borderM;
        int yMax = h - 1 - borderM;
        if (yMin > yMax)
        {
            Debug.LogWarning("[ArenaGenerator] obstacleBorderMargin supprime toute place verticale pour les obstacles.");
            return;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        for (int x = combatXMin; x <= halfXMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                if (IsReservedExclusiveArenaCenterCell(x, y, w, h))
                    continue;
                candidates.Add(new Vector2Int(x, y));
            }
        }

        ShuffleList(candidates);

        int totalCombatCells = (combatXMax - combatXMin + 1) * (yMax - yMin + 1);
        int targetCount      = Mathf.RoundToInt(totalCombatCells * arenaConfig.obstacleDensity);
        // Moitié des obstacles, car chacun est mirrored (sauf la colonne centrale)
        int targetHalf = Mathf.CeilToInt(targetCount / 2f);

        int placed   = 0;
        int attempts = 0;
        int candIdx  = 0;

        while (placed < targetHalf && attempts < arenaConfig.maxObstaclePlacementAttempts)
        {
            attempts++;

            bool tryCluster =
                arenaConfig.obstacleClusterBias > 0f &&
                placed > 0 &&
                rng.NextDouble() < arenaConfig.obstacleClusterBias;

            Vector2Int posPick = default;

            bool gotCluster = tryCluster &&
                              TryPickClusterObstacleCandidate(w, h, combatXMin, halfXMax, yMin, yMax, out posPick);

            if (!gotCluster)
            {
                while (candIdx < candidates.Count &&
                       !IsTerrainTileForObstaclePlacement(arenaData[candidates[candIdx].x, candidates[candIdx].y]))
                    candIdx++;

                if (candIdx >= candidates.Count)
                    break;

                posPick = candidates[candIdx++];
            }

            Vector2Int pos = posPick;
            int        mirrorX         = (w - 1) - pos.x;
            bool       isCenterColumn = (pos.x == mirrorX);

            // Sauvegarder pour rollback
            CellTileType savedLeft   = arenaData[pos.x, pos.y];
            CellTileType savedRight  = isCenterColumn ? CellTileType.Ground : arenaData[mirrorX, pos.y];

            // Placer
            arenaData[pos.x, pos.y] = CellTileType.Obstacle;
            if (arenaConfig.mirrorSymmetry && !isCenterColumn)
                arenaData[mirrorX, pos.y] = CellTileType.Obstacle;

            // Vérifier connectivité T1 → T2
            if (!CheckConnectivity())
            {
                arenaData[pos.x, pos.y] = savedLeft;
                if (arenaConfig.mirrorSymmetry && !isCenterColumn)
                    arenaData[mirrorX, pos.y] = savedRight;
                continue;
            }

            placed++;
        }

        Debug.Log($"[ArenaGenerator] Obstacles placés : {placed * (arenaConfig.mirrorSymmetry ? 2 : 1)}" +
                  $" / cible {targetCount} | Tentatives : {attempts}");
    }

    static bool IsTerrainTileForObstaclePlacement(CellTileType t)
    {
        return t == CellTileType.Ground ||
               t == CellTileType.GroundBlood ||
               t == CellTileType.GroundGrass;
    }

    bool IsReservedExclusiveArenaCenterCell(int x, int y, int arenaW, int arenaH)
    {
        if (!arenaConfig.useExclusiveCenterArenaFloor) return false;
        return x == TileSpriteRegistry.LogicalCenterX(arenaW) &&
               y == TileSpriteRegistry.LogicalCenterY(arenaH);
    }

    bool TryPickClusterObstacleCandidate(int w, int h, int combatXMin, int halfXMax, int yMin, int yMax, out Vector2Int pos)
    {
        pos = default;

        List<Vector2Int> pool = new List<Vector2Int>(24);
        for (int x = combatXMin; x <= halfXMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                if (IsReservedExclusiveArenaCenterCell(x, y, w, h)) continue;
                if (!IsTerrainTileForObstaclePlacement(arenaData[x, y])) continue;
                if (!HasOrthogonalObstacleNeighbor(x, y, w, h))
                    continue;
                pool.Add(new Vector2Int(x, y));
            }
        }

        if (pool.Count == 0) return false;

        pos = pool[rng.Next(pool.Count)];
        return true;
    }

    bool HasOrthogonalObstacleNeighbor(int x, int y, int w, int h)
    {
        for (int d = 0; d < 4; d++)
        {
            int nx = x + DX[d];
            int ny = y + DY[d];
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            if (arenaData[nx, ny] == CellTileType.Obstacle)
                return true;
        }

        return false;
    }

    void Step3b_CleanupVariantsNearSpawn()
    {
        if (!arenaConfig.cleanupVariantTilesNearSpawnBand) return;

        int w     = arenaConfig.arenaWidth;
        int h     = arenaConfig.arenaHeight;
        int depth = arenaConfig.spawnZoneDepth;
        int band  = Mathf.Max(0, arenaConfig.cleanupBandWidthFromSpawn);

        if (band == 0) return;

        for (int x = depth; x < w - depth; x++)
        {
            bool nearSpawnOpening =
                (band > 0 && x >= depth && x < depth + band) ||
                (band > 0 && x >= w - depth - band && x < w - depth);

            if (!nearSpawnOpening) continue;

            for (int y = 0; y < h; y++)
            {
                CellTileType t = arenaData[x, y];
                if (t != CellTileType.GroundBlood && t != CellTileType.GroundGrass)
                    continue;

                if ((float)rng.NextDouble() >= arenaConfig.cleanupBandVariantToGroundChance)
                    continue;

                arenaData[x, y] = CellTileType.Ground;
            }
        }
    }

    // =========================================================
    // CONNECTIVITÉ — BFS sur arenaData (sans GridManager)
    // =========================================================

    bool CheckConnectivity()
    {
        int w = arenaConfig.arenaWidth;
        int h = arenaConfig.arenaHeight;

        // Point de départ : première case marchable de la colonne 0 (spawn T1)
        Vector2Int start = new Vector2Int(-1, -1);
        for (int y = 0; y < h; y++)
        {
            if (IsWalkableInData(0, y))
            {
                start = new Vector2Int(0, y);
                break;
            }
        }
        if (start.x < 0) return false;

        // BFS
        bool[,]             visited = new bool[w, h];
        Queue<Vector2Int>   queue   = new Queue<Vector2Int>();

        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cur = queue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + DX[d];
                int ny = cur.y + DY[d];

                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (visited[nx, ny]) continue;
                if (!IsWalkableInData(nx, ny)) continue;

                visited[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Vérifier qu'au moins une case marchable dans la colonne w-1 (spawn T2) est atteinte
        for (int y = 0; y < h; y++)
            if (IsWalkableInData(w - 1, y) && visited[w - 1, y])
                return true;

        return false;
    }

    bool IsWalkableInData(int x, int y) =>
        arenaData[x, y] != CellTileType.Obstacle;

    // =========================================================
    // ÉTAPE 4 — Application sur les Cell de GridManager
    // =========================================================

    void Step4_ApplyToGrid()
    {
        int w = arenaConfig.arenaWidth;
        int h = arenaConfig.arenaHeight;

        spawnCellsTeam1.Clear();
        spawnCellsTeam2.Clear();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Cell cell = GridManager.Instance.GetCell(x, y);
                if (cell == null) continue;

                CellTileType type = arenaData[x, y];
                cell.TileType  = type;
                cell.IsWalkable = type != CellTileType.Obstacle;

                if (type == CellTileType.SpawnTeam1) spawnCellsTeam1.Add(cell);
                else if (type == CellTileType.SpawnTeam2) spawnCellsTeam2.Add(cell);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int blocked = 0;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            var c = GridManager.Instance.GetCell(x, y);
            if (c != null && !c.IsWalkable) blocked++;
        }
        Debug.Log($"[ArenaGenerator] Cases non traversables sur la grille : {blocked}");
#endif
    }

    // =========================================================
    // ÉTAPE 5 — Rendu des sprites de tiles
    // =========================================================

    /// <remarks>
    /// Les tuiles utilisent plusieurs <see cref="SpriteRenderer"/> par case :
    /// le rendu n'est pas fait via Tilemap Unity — pas de <c>SetTiles</c> batch ici ;
    /// le coût est linéaire en w×h, acceptable pour des arènes &lt;~25×25.
    /// </remarks>
    void Step5_RenderTiles()
    {
        TileSpriteRegistry registry = arenaConfig.tileRegistry;
        if (registry == null)
        {
            Debug.LogWarning("[ArenaGenerator] TileSpriteRegistry non assigné dans ArenaConfig — " +
                             "aucun tile visuel ne sera créé.");
            return;
        }

        var gridMgr = GridManager.Instance;
        Transform gridRoot = gridMgr.GridVisualRoot != null
            ? gridMgr.GridVisualRoot
            : gridMgr.transform;

        tileContainer = new GameObject("=== ARENA TILES ===").transform;
        tileContainer.SetParent(gridRoot);
        tileContainer.localPosition = Vector3.zero;
        tileContainer.localRotation = Quaternion.identity;
        tileContainer.localScale    = Vector3.one;

        int w = arenaConfig.arenaWidth;
        int h = arenaConfig.arenaHeight;
        Vector3 spriteOffset =
            gridConfig != null ? gridConfig.arenaTileSpriteWorldOffset : Vector3.zero;
        int orderBias =
            gridConfig != null ? gridConfig.arenaTileSortingOrderBias : 0;

        System.Random renderRng = new System.Random(effectiveSeed + 7919);
        var         decorAnchors = new List<Vector2Int>(128);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Cell cell = gridMgr.GetCell(x, y);
                if (cell == null) continue;

                CellTileType type   = arenaData[x, y];

                Sprite sprite = registry.centerArenaFloorTile != null &&
                                arenaConfig.useExclusiveCenterArenaFloor &&
                                type != CellTileType.Obstacle &&
                                IsReservedExclusiveArenaCenterCell(x, y, w, h)
                    ? registry.centerArenaFloorTile
                    : PickSprite(x, y, type, registry, renderRng);

                if (sprite == null) continue;

                GameObject tileGO = new GameObject($"Tile_{x}_{y}");
                tileGO.transform.SetParent(tileContainer, worldPositionStays: false);
                tileGO.transform.position = cell.WorldPosition + spriteOffset;

                SpriteRenderer sr = tileGO.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                ApplyArenaTileSorting(sr);

                // Tri pseudo-isométrique : plus la case est « bas / droite », plus elle est dessinée devant.
                bool isObstacle = type == CellTileType.Obstacle;
                int  baseOrder  = -(y * w + x);
                sr.sortingOrder = orderBias + (isObstacle ? baseOrder + w * h : baseOrder - w * h);

                if (!isObstacle && !(arenaConfig.useExclusiveCenterArenaFloor && IsReservedExclusiveArenaCenterCell(x, y, w, h)))
                {
                    float          mult  = DecorationChanceMultiplier(x, y, w, h);
                    CellTileType   theme = DecorationThemeForProps(type);
                    Sprite         deco  = registry.MaybeGetDecorationOverlayForCell(theme, renderRng, mult);
                    if (deco != null &&
                        !IsDecorTooClose(decorAnchors, x, y, arenaConfig.decorationNeighborCardinalMinDistance))
                    {
                        var decoGo = new GameObject("Decoration");
                        decoGo.transform.SetParent(tileGO.transform, false);
                        decoGo.transform.localPosition = Vector3.zero;
                        var decoSr = decoGo.AddComponent<SpriteRenderer>();
                        decoSr.sprite = deco;
                        ApplyArenaTileSorting(decoSr);
                        decoSr.sortingOrder = sr.sortingOrder + 1;
                        decorAnchors.Add(new Vector2Int(x, y));
                    }
                }

                if (arenaConfig.renderPerimeterEdges)
                {
                    int peri = TileSpriteRegistry.GetPerimeterStep(x, y, w, h);
                    if (peri >= 0)
                    {
                        Sprite edge = registry.GetEdgeOverlaySprite(peri);
                        if (edge != null)
                        {
                            var edgeGo = new GameObject("Edge");
                            edgeGo.transform.SetParent(tileGO.transform, false);
                            edgeGo.transform.localPosition = Vector3.zero;
                            var edgeSr = edgeGo.AddComponent<SpriteRenderer>();
                            edgeSr.sprite = edge;
                            ApplyArenaTileSorting(edgeSr);
                            edgeSr.sortingOrder = sr.sortingOrder + registry.edgeSortingOrderBoost;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Sorting layer commun pour les sprites d'arène (optionnel, défini dans GridConfig).</summary>
    void ApplyArenaTileSorting(SpriteRenderer sr)
    {
        if (gridConfig == null || string.IsNullOrEmpty(gridConfig.arenaTileSortingLayerName)) return;
        sr.sortingLayerName = gridConfig.arenaTileSortingLayerName;
    }

    Sprite PickSprite(int gx, int gy, CellTileType type, TileSpriteRegistry registry, System.Random renderRng)
    {
        switch (type)
        {
            case CellTileType.Obstacle:
                return registry.GetRandomObstacleTile(renderRng);

            case CellTileType.GroundBlood:
            case CellTileType.GroundGrass:
            case CellTileType.Ground:
            case CellTileType.SpawnTeam1:
            case CellTileType.SpawnTeam2:
                return registry.PickArenaGroundSprite(type, renderRng, gx, gy, arenaConfig.arenaWidth,
                                                       arenaConfig.arenaHeight);

            default:
                return registry.PickArenaGroundSprite(CellTileType.Ground, renderRng, gx, gy, arenaConfig.arenaWidth,
                                                        arenaConfig.arenaHeight);
        }
    }

    static CellTileType DecorationThemeForProps(CellTileType type)
    {
        if (type == CellTileType.SpawnTeam1 || type == CellTileType.SpawnTeam2)
            return CellTileType.Ground;
        return type;
    }

    float DecorationChanceMultiplier(int x, int y, int w, int h)
    {
        float m       = 1f;
        int   depth   = arenaConfig.spawnZoneDepth;
        int   band    = Mathf.Max(arenaConfig.cleanupBandWidthFromSpawn, 3);
        float spawnT  = arenaConfig.decorationSpawnAdjacencyScale;

        bool nearSpawnOpening =
            spawnT < 1f &&
            depth > 0 &&
            ((x >= depth && x < Mathf.Min(depth + band, w - depth)) ||
             (x < w - depth && x >= Mathf.Max(w - depth - band, depth)));

        if (nearSpawnOpening)
            m *= spawnT;

        int cx           = TileSpriteRegistry.LogicalCenterX(w);
        int cy           = TileSpriteRegistry.LogicalCenterY(h);
        int manhattanCtr = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);

        float ringBoost = arenaConfig.decorationCenterOrthoRingBoost;
        if (ringBoost > 1f &&
            arenaConfig.useExclusiveCenterArenaFloor &&
            manhattanCtr == 1)
            m *= ringBoost;

        return Mathf.Clamp(m, 0f, 4f);
    }

    static bool IsDecorTooClose(List<Vector2Int> anchors, int ax, int ay, int minManhattan)
    {
        if (minManhattan <= 0 || anchors == null) return false;

        foreach (Vector2Int p in anchors)
        {
            int md = Mathf.Abs(ax - p.x) + Mathf.Abs(ay - p.y);
            if (md > 0 && md < minManhattan)
                return true;
        }

        return false;
    }

    // =========================================================
    // ÉTAPE 6 — Highlight des zones de spawn
    // =========================================================

    void Step6_HighlightSpawnZones()
    {
        // Réutilise le système de highlight existant de GridManager
        // Move (bleu) = T1 | Attack (rouge) = T2
        // Ces highlights seront effacés par CombatManager au début du combat
        GridManager.Instance.HighlightCells(spawnCellsTeam1, HighlightType.Move);
        GridManager.Instance.HighlightCells(spawnCellsTeam2, HighlightType.Attack);
    }

    // =========================================================
    // NETTOYAGE
    // =========================================================

    void ClearPreviousArena()
    {
        if (tileContainer != null)
            Destroy(tileContainer.gameObject);

        if (GridManager.Instance != null)
            GridManager.Instance.ClearAllHighlights();

        spawnCellsTeam1.Clear();
        spawnCellsTeam2.Clear();
        arenaData = null;
    }

    // =========================================================
    // ACCÈS PUBLIC
    // =========================================================

    /// <summary>Retourne les cases de spawn de l'équipe donnée (1 ou 2).</summary>
    public List<Cell> GetSpawnCells(int team)
    {
        return team == 1 ? spawnCellsTeam1
             : team == 2 ? spawnCellsTeam2
             : new List<Cell>();
    }

    /// <summary>Retourne la seed effectivement utilisée pour la dernière génération.</summary>
    public int GetEffectiveSeed() => effectiveSeed;

    /// <summary>Retourne true si la case (x,y) est dans la zone de spawn de l'équipe donnée.</summary>
    public bool IsSpawnCell(int x, int y, int team)
    {
        if (arenaData == null) return false;
        if (x < 0 || x >= arenaConfig.arenaWidth || y < 0 || y >= arenaConfig.arenaHeight) return false;
        CellTileType type = arenaData[x, y];
        return team == 1 ? type == CellTileType.SpawnTeam1
             : team == 2 ? type == CellTileType.SpawnTeam2
             : false;
    }

    // =========================================================
    // UTILITAIRES INTERNES
    // =========================================================

    bool ValidateSetup()
    {
        if (arenaConfig == null)
        {
            Debug.LogError("[ArenaGenerator] ArenaConfig non assigné dans l'Inspector !");
            return false;
        }
        if (gridConfig == null)
        {
            Debug.LogError("[ArenaGenerator] GridConfig non assigné dans l'Inspector !");
            return false;
        }
        if (GridManager.Instance == null)
        {
            Debug.LogError("[ArenaGenerator] GridManager.Instance est null ! " +
                           "Assure-toi que GridManager est présent dans la scène.");
            return false;
        }

        if (GridManager.Instance.config != gridConfig)
        {
            Debug.LogError("[ArenaGenerator] GridManager doit référencer le même GridConfig que cet ArenaGenerator. " +
                           "Sinon les dimensions / données peuvent diverger et les obstacles restent « marchables » côté grille.");
            return false;
        }

        if (!arenaConfig.IsValid(out string err))
        {
            Debug.LogError($"[ArenaGenerator] ArenaConfig invalide : {err}");
            return false;
        }

        return true;
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j    = rng.Next(i + 1);
            T   tmp  = list[i];
            list[i]  = list[j];
            list[j]  = tmp;
        }
    }

    // =========================================================
    // GIZMOS EDITOR
    // =========================================================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || arenaData == null || GridManager.Instance == null) return;

        int w = arenaConfig.arenaWidth;
        int h = arenaConfig.arenaHeight;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Vector3 pos = GridManager.Instance.GridToWorld(x, y);

                switch (arenaData[x, y])
                {
                    case CellTileType.Obstacle:
                        Gizmos.color = new Color(0.9f, 0.15f, 0.15f, 0.55f);
                        Gizmos.DrawCube(pos, new Vector3(0.35f, 0.20f, 0.01f));
                        break;

                    case CellTileType.SpawnTeam1:
                        Gizmos.color = new Color(0.25f, 0.45f, 1f, 0.30f);
                        Gizmos.DrawCube(pos, new Vector3(0.30f, 0.12f, 0.01f));
                        break;

                    case CellTileType.SpawnTeam2:
                        Gizmos.color = new Color(1f, 0.30f, 0.20f, 0.30f);
                        Gizmos.DrawCube(pos, new Vector3(0.30f, 0.12f, 0.01f));
                        break;
                }
            }
        }

        // Étiquette seed dans la Scene view
        UnityEditor.Handles.Label(
            GridManager.Instance.GridToWorld(0, h + 1),
            $"Arena {w}x{h} | Seed {effectiveSeed}"
        );
    }
#endif
}
