using UnityEngine;

/// <summary>
/// Paramètres de génération d'une arène 1v1.
/// Créer via : Clic droit → Create → Arena → Arena Configuration
/// Un seul asset suffit pour le MVP ; duplique-le pour tester des variantes.
/// </summary>
[CreateAssetMenu(fileName = "ArenaConfig_1v1", menuName = "Arena/Arena Configuration")]
public class ArenaConfig : ScriptableObject
{
    // =========================================================
    // DIMENSIONS
    // =========================================================

    [Header("=== DIMENSIONS ===")]
    [Range(9, 21)]
    [Tooltip("Nombre de colonnes (axe X). " +
             "Doit être impair pour avoir une colonne centrale. " +
             "Minimum 9 avec spawnZoneDepth=2 (zone de combat). Arènes compactes : 9 ou 11.")]
    public int arenaWidth = 11;

    [Range(7, 17)]
    [Tooltip("Nombre de lignes (axe Y). " +
             "Doit être impair pour avoir une ligne centrale. " +
             "Pour petites cartes équilibrées : 9 ou 11.")]
    public int arenaHeight = 9;

    // =========================================================
    // ZONES DE SPAWN
    // =========================================================

    [Header("=== ZONES DE SPAWN ===")]
    [Range(1, 4)]
    [Tooltip("Profondeur (en colonnes) de la zone de spawn de chaque équipe. " +
             "Équipe 1 = colonnes 0 à depth-1. " +
             "Équipe 2 = colonnes (width-depth) à (width-1).")]
    public int spawnZoneDepth = 2;

    // =========================================================
    // OBSTACLES
    // =========================================================

    [Header("=== OBSTACLES ===")]
    [Range(0f, 0.30f)]
    [Tooltip("Densité d'obstacles dans la zone de combat centrale. " +
             "0 = aucune. ~0.12 convient aux petites cartes lisibles.")]
    public float obstacleDensity = 0.12f;

    [Tooltip("Activer la symétrie miroir X des obstacles. " +
             "FORTEMENT recommandé pour un jeu 1v1 équilibré.")]
    public bool mirrorSymmetry = true;

    [Range(0, 3)]
    [Tooltip("Distance minimale (en cases) entre un obstacle et le bord d'une zone de spawn. " +
             "Évite de bloquer l'accès à la zone de combat dès la sortie du spawn.")]
    public int minClearanceFromSpawn = 1;

    [Range(1, 3)]
    [Tooltip("Aucune case d'obstacle à moins de N cases du bord extérieur de l'arène complète " +
             "(x=0, y=0, derniers X/Y). Valeur recommandée : 1 = pas d'obstacles au pourtour de la carte.")]
    public int obstacleBorderMargin = 1;

    // =========================================================
    // VARIANTES DE TERRAIN
    // =========================================================

    [Header("=== VARIANTES DE TERRAIN ===")]
    [Range(0f, 0.12f)]
    [Tooltip("Probabilité qu'une case de sol devienne une variante sang (GROUNDBLOOD). Réduire sur petites cartes.")]
    public float bloodTileChance = 0.04f;

    [Range(0f, 0.12f)]
    [Tooltip("Probabilité qu'une case de sol devienne une variante herbe / rune (GROUNDGRASS).")]
    public float grassTileChance = 0.04f;

    [Header("=== PATCHS DE TERRAIN ===")]
    [Tooltip("Regroupe sang / herbe en taches (Perlin + lissage). Désactiver = ancien tirage cellule par cellule.")]
    public bool useTerrainPatches = true;

    [Range(0.02f, 0.40f)]
    [Tooltip("Échelle spatiale du bruit (plus bas = grosses taches, plus haut = motifs plus fins).")]
    public float terrainNoiseScale = 0.11f;

    [Range(0, 6)]
    [Tooltip("Passes de lissage du champ de bruit avant assignation (0 = pic net, 4+ = blobs doux).")]
    public int terrainSmoothingPasses = 2;

    [Header("=== LISIBILITÉ BORDS SPAWN (sol) ===")]
    [Tooltip("Après les obstacles, peut retirer sang / herbe près des bases pour des couloirs plus clairs.")]
    public bool cleanupVariantTilesNearSpawnBand = true;

    [Range(0, 6)]
    [Tooltip("Nombre de colonnes hors des zones de spawn (vers le centre) à adoucir.")]
    public int cleanupBandWidthFromSpawn = 2;

    [Range(0f, 1f)]
    [Tooltip("Probabilité qu'une case sang/herbe dans cette bande redevienne du sol nu.")]
    public float cleanupBandVariantToGroundChance = 0.78f;

    [Header("=== RENDU — PIÈCE MAÎTRESSE CENTRALE (ground_center_arena) ===")]
    [Tooltip("Réserve TileSpriteRegistry.centerArenaFloorTile pour la case géométrique centrale de la grille (une seule), " +
             "jamais dans le tirage du sol générique.")]
    public bool useExclusiveCenterArenaFloor = true;

    [Range(0f, 1f)]
    [Tooltip("Réduction des décors sur les bandes de combat près des spawns (lisibilité déploiement).")]
    public float decorationSpawnAdjacencyScale = 0.18f;

    [Range(1f, 2.35f)]
    [Tooltip("Multiplicateur de chance de décor sur les 4 voisins orthogonaux de la case centre (cadrage visuel sans encombre le cœur).")]
    public float decorationCenterOrthoRingBoost = 1.28f;

    [Range(0, 8)]
    [Tooltip("Espacement minimum : pas de décor si un voisin cardinal a déjà un décor (≤0 = désactivé).")]
    public int decorationNeighborCardinalMinDistance = 3;

    [Header("=== OBSTACLES — GROUPEMENT ===")]
    [Range(0f, 1f)]
    [Tooltip("Tendance à former des groupements voisins (amisos). Plus bas = lignes plus lâches.")]
    public float obstacleClusterBias = 0.32f;

    // =========================================================
    // GÉNÉRATION PROCÉDURALE
    // =========================================================

    [Header("=== GÉNÉRATION ===")]
    [Tooltip("Graine de génération aléatoire. " +
             "-1 = nouvelle seed aléatoire à chaque génération. " +
             "Toute valeur ≥ 0 = résultat identique et reproductible.")]
    public int seed = -1;

    [Range(50, 500)]
    [Tooltip("Nombre maximum de tentatives pour placer les obstacles. " +
             "Augmente si la densité est élevée mais que peu d'obstacles sont placés.")]
    public int maxObstaclePlacementAttempts = 300;

    // =========================================================
    // RÉFÉRENCES
    // =========================================================

    [Header("=== RÉFÉRENCES ===")]
    [Tooltip("Registre des sprites de tiles (GROUND, OBSTACLE, etc.). " +
             "Créer un TileSpriteRegistry et l'assigner ici.")]
    public TileSpriteRegistry tileRegistry;

    [Header("=== BORDURES PÉRIMÈTRIQUES ===")]
    [Tooltip("Affiche les EDGE1–EDGE12 sur le contour de la grille.")]
    public bool renderPerimeterEdges = true;

    // =========================================================
    // VALIDATION
    // =========================================================

    /// <summary>Retourne true si la configuration est utilisable pour générer une arène.</summary>
    public bool IsValid(out string errorMessage)
    {
        if (tileRegistry == null)
        {
            errorMessage = "TileSpriteRegistry non assigné dans ArenaConfig !";
            return false;
        }

        if (arenaWidth <= spawnZoneDepth * 2 + 2)
        {
            errorMessage = $"arenaWidth ({arenaWidth}) trop petit pour 2 zones de spawn " +
                           $"de profondeur {spawnZoneDepth} avec une zone de combat.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
