using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// Chef d'orchestre de la scène de combat.
///
/// PIPELINE COMPLET :
///   Phase 0 : L'ArenaGenerator génère la map (generateOnStart = true)
///   Phase 1 : Sélection des passifs — PassiveSelectionScreen pour chaque joueur
///   Phase 2 : Placement — le joueur clique sur une case de spawn pour placer son perso
///   Phase 3 : Combat — TurnManager.StartCombat(), DeckUI bind sur chaque début de tour
///   Phase 4 : Fin de combat — affichage résultat
///
/// SETUP INSPECTOR :
///   - Glisser les deux TacticalCharacter (ils doivent être DÉSACTIVÉS au départ)
///   - Glisser le DeckUI, le PassiveSelectionScreen, l'ArenaGenerator
///   - (Optionnel) Glisser le panneau résultat
/// </summary>
public class CombatInitializer : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================
    public static CombatInitializer Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================================================
    // PHASE PUBLIQUE (lue par PlayerInputHandler)
    // =========================================================
    public enum CombatPhase { WaitingForArena, PassiveSelection, Placement, Combat, End }
    public CombatPhase CurrentPhase => phase;

    // =========================================================
    // RÉFÉRENCES INSPECTOR
    // =========================================================
    [Header("Personnages (désactivés au départ)")]
    public TacticalCharacter player;       // Équipe 1 — contrôlé localement
    public TacticalCharacter opponent;     // Équipe 2 — IA ou second joueur

    [Header("Composants scène")]
    public ArenaGenerator    arenaGenerator;
    public DeckUI            deckUI;
    public PassiveSelectionScreen passiveSelectionScreen;

    [Header("UI Résultat (optionnel)")]
    public GameObject victoryPanel;
    public GameObject defeatPanel;

    [Header("Options")]
    [Tooltip("Si true, l'adversaire est placé automatiquement sur une case de spawn aléatoire.")]
    public bool autoPlaceOpponent = true;
    [Tooltip("Délai (secondes) après la sélection de passif avant le placement.")]
    public float delayAfterPassiveSelection = 1.5f;

    [Header("UI Placement")]
    [Tooltip("Chrono + icône timer_icon_hud pendant le placement. Créé sous le Canvas si absent.")]
    public PlacementCountdownUI placementCountdownUi;
    [Tooltip("0 = pas de limite ni affichage du chrono placement. > 0 = secondes max (solo : placement auto sur une case libre si le temps expire).")]
    public float placementTimeLimitSeconds = 120f;

    [Header("Mode Test (Solo)")]
    [Tooltip("Skip la sélection de passif : un passif aléatoire est choisi instantanément. Pratique pour tester le combat rapidement.")]
    public bool skipPassiveSelection = false;

    [Header("Deck de sorts")]
    [Tooltip("Optionnel ; si vide → charge Resources OracleSpellPools/AllCombatSpellsPool.")]
    public SpellDeckPool spellDeckPool;
    [Tooltip("À chaque match : pioche DeckData.MaxSpells sorts distincts depuis le pool (joueur et adversaire : tirages indépendants).")]
    public bool randomizeSpellDeckEachMatch = true;

    // =========================================================
    // ÉTAT INTERNE
    // =========================================================
    private CombatPhase phase = CombatPhase.WaitingForArena;

    private List<Cell> spawnCellsTeam1;
    private List<Cell> spawnCellsTeam2;

    private bool playerPlaced = false;
    private bool netPlacement0Done = false;
    private bool netPlacement1Done = false;

    bool IsNetworkDuel =>
        OracleCombatNetBridge.Instance != null &&
        PhotonNetwork.InRoom &&
        PhotonNetwork.CurrentRoom != null &&
        PhotonNetwork.CurrentRoom.PlayerCount >= 2;

    void ResolveSpellDeckPoolReference()
    {
        if (spellDeckPool == null)
            spellDeckPool = Resources.Load<SpellDeckPool>("OracleSpellPools/AllCombatSpellsPool");
    }

    void ApplyRandomSpellDecksIfConfigured()
    {
        if (!randomizeSpellDeckEachMatch) return;
        ResolveSpellDeckPoolReference();

        int need = DeckData.MaxSpells;
        if (spellDeckPool == null || spellDeckPool.CandidateCount < need)
        {
            if (spellDeckPool != null)
                Debug.LogWarning($"[CombatInitializer] Tirage aléatoire des sorts désactivé : au moins {need} sorts dans le pool (actuellement {spellDeckPool.CandidateCount}). " +
                                  "Oracle → Spell Deck Pool — créer ou remplir.");
            return;
        }

        player?.ClearRuntimeSpellDeck();
        opponent?.ClearRuntimeSpellDeck();

        int seed = unchecked((int)DateTime.UtcNow.Ticks) ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        var rng = new System.Random(seed);
        if (player != null)
            player.SetRuntimeSpellDeck(spellDeckPool.DrawRandomUnique(rng, need));
        if (opponent != null)
            opponent.SetRuntimeSpellDeck(spellDeckPool.DrawRandomUnique(new System.Random(rng.Next()), need));
    }

    // =========================================================
    // DÉMARRAGE
    // =========================================================
    void Start()
    {
        // L'ArenaGenerator a generateOnStart = true, donc la map est prête en Start()
        // (ArenaGenerator s'exécute en ordre -5, avant CombatInitializer)
        StartCoroutine(InitSequence());
    }

    IEnumerator InitSequence()
    {
        // ── Sécurité : attendre un frame pour être sûr que la grille est initialisée ──
        yield return null;

        // Auto-find de toutes les références non assignées (GOs actifs ET inactifs)
        if (player   == null) player   = FindFirstObjectByType<TacticalCharacter>(FindObjectsInactive.Include);
        if (opponent == null)
        {
            var all = FindObjectsByType<TacticalCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tc in all)
                if (tc != player) { opponent = tc; break; }
        }
        else if (player != null && opponent == player)
        {
            opponent = null;
            var all = FindObjectsByType<TacticalCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tc in all)
                if (tc != player) { opponent = tc; break; }
            if (opponent == null)
                Debug.LogError("[CombatInitializer] player et opponent référencent le même TacticalCharacter — en 1v1 réseau, glisse le perso « Opponent » dans opponent.");
        }
        if (arenaGenerator        == null) arenaGenerator        = FindFirstObjectByType<ArenaGenerator>(FindObjectsInactive.Include);
        if (deckUI                == null) deckUI                = FindFirstObjectByType<DeckUI>(FindObjectsInactive.Include);
        if (passiveSelectionScreen == null) passiveSelectionScreen = FindFirstObjectByType<PassiveSelectionScreen>(FindObjectsInactive.Include);
        if (victoryPanel          == null)
        {
            var go = GameObject.Find("VictoryPanel");
            if (go != null) victoryPanel = go;
        }
        if (defeatPanel == null)
        {
            var go = GameObject.Find("DefeatPanel");
            if (go != null) defeatPanel = go;
        }

        Validate();

        // Masque passif / sorts / fin de tour / PV jusqu’au début du combat (évite un flash si la scène les laisse actifs).
        SetPassiveSelectionCombatUiVisible(false);

        spawnCellsTeam1 = arenaGenerator != null ? arenaGenerator.GetSpawnCells(1) : new List<Cell>();
        spawnCellsTeam2 = arenaGenerator != null ? arenaGenerator.GetSpawnCells(2) : new List<Cell>();

        // ── Phase 1 : Sélection des passifs ─────────────────
        yield return StartCoroutine(RunPassiveSelection());

        // ── Phase 2 : Placement ──────────────────────────────
        yield return StartCoroutine(RunPlacement());

        ApplyRandomSpellDecksIfConfigured();

        // ── Phase 3 : Lancement du combat ───────────────────
        StartCombat();
    }

    // =========================================================
    // PHASE 1 — SÉLECTION DES PASSIFS
    // =========================================================
    IEnumerator RunPassiveSelection()
    {
        phase = CombatPhase.PassiveSelection;

        if (passiveSelectionScreen == null)
        {
            Debug.LogWarning("[CombatInitializer] Pas de PassiveSelectionScreen — passifs ignorés.");
            yield break;
        }

        SetPassiveSelectionCombatUiVisible(false);

        bool selectionDone = false;
        PassiveData chosenPassive = null;

        passiveSelectionScreen.OnPassiveSelected += (passive) =>
        {
            chosenPassive  = passive;
            selectionDone  = true;
        };

        passiveSelectionScreen.Show();

        // Mode test solo : skip instantané (pas en réseau pour ne pas gêner l'adversaire humain)
        if (skipPassiveSelection && !IsNetworkDuel)
            passiveSelectionScreen.AutoSelectInstant();

        // Attendre que le joueur confirme (ou que le timer expire ou le skip)
        yield return new WaitUntil(() => selectionDone);

        // Appliquer le passif au personnage contrôlé localement (équipe A solo / hôte, équipe B invité en ligne)
        if (chosenPassive != null)
        {
            var pawn = IsNetworkDuel
                ? OracleCombatNetBridge.Instance.GetLocalControlledCharacter()
                : player;
            if (pawn != null)
            {
                var pm = pawn.GetComponent<PassiveManager>();
                if (pm != null)
                {
                    pm.SetPassive(chosenPassive);
                    Debug.Log($"[CombatInitializer] Passif (local) : {chosenPassive.passiveName} sur {pawn.name}");
                }
            }
        }

        passiveSelectionScreen.Hide();

        // Chrome combat + deck restent masqués jusqu’à la fin du placement (voir StartCombat).

        // Petit délai avant la phase placement (supprimé en mode test)
        if (!skipPassiveSelection || IsNetworkDuel)
            yield return new WaitForSeconds(delayAfterPassiveSelection);
    }

    /// <summary>
    /// Affiche ou masque cartes de sort + bas de <see cref="CombatHUD"/>
    /// (passif, PA/PM, fin de tour, barres PV). Masqué pendant passif + placement ; activé au début du combat.
    /// </summary>
    void SetPassiveSelectionCombatUiVisible(bool showCombatUi)
    {
        var hud = FindFirstObjectByType<CombatHUD>(FindObjectsInactive.Include);
        if (hud != null)
            hud.SetCombatChromeVisible(showCombatUi);
        if (deckUI != null)
            deckUI.gameObject.SetActive(showCombatUi);
    }

    // =========================================================
    // PHASE 2 — PLACEMENT
    // =========================================================
    IEnumerator RunPlacement()
    {
        phase = CombatPhase.Placement;

        SetPassiveSelectionCombatUiVisible(false);

        if (OracleCombatNetBridge.Instance != null && PhotonNetwork.InRoom)
        {
            float wait = 0f;
            while (PhotonNetwork.CurrentRoom != null &&
                   PhotonNetwork.CurrentRoom.PlayerCount < 2 &&
                   wait < 30f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
        }

        GridManager.Instance.ClearAllHighlights();

        if (IsNetworkDuel)
        {
            // Réinitialiser puis se resynchroniser : des RpcApplyPlacement peuvent arriver
            // pendant PassiveSelection (désync chronologie hôte / invité). Sans ça, un client
            // garde netPlacement* à false et reste bloqué sur WaitUntil avec les highlights.
            netPlacement0Done = false;
            netPlacement1Done = false;
            SyncNetPlacementFlagsFromPawns();

            if (PhotonNetwork.IsMasterClient)
            {
                GridManager.Instance.HighlightCells(spawnCellsTeam1, HighlightType.Move);
                Debug.Log("[CombatInitializer] En ligne — hôte : place l’équipe A sur les cases bleues.");
            }
            else
            {
                GridManager.Instance.HighlightCells(spawnCellsTeam2, HighlightType.Move);
                Debug.Log("[CombatInitializer] En ligne — invité : place l’équipe B sur les cases bleues.");
            }

            if (placementTimeLimitSeconds > 0f)
                ResolvePlacementHud()?.Show(placementTimeLimitSeconds);

            yield return new WaitUntil(() => netPlacement0Done && netPlacement1Done);
            GridManager.Instance.ClearAllHighlights();
            PlacementHudHide();
            yield break;
        }

        // ── Solo / vs IA ─────────────────────────────────────
        GridManager.Instance.HighlightCells(spawnCellsTeam1, HighlightType.Move);

        Debug.Log("[CombatInitializer] Phase placement — clique sur une case bleue pour placer ton personnage.");

        if (autoPlaceOpponent && spawnCellsTeam2.Count > 0)
        {
            Cell opponentCell = spawnCellsTeam2[UnityEngine.Random.Range(0, spawnCellsTeam2.Count)];
            PlaceCharacter(opponent, opponentCell, teamId: 2);
            Debug.Log($"[CombatInitializer] Adversaire placé en {opponentCell.GridX},{opponentCell.GridY}");
        }

        if (placementTimeLimitSeconds > 0f)
        {
            placementDeadlineUnscaled = Time.unscaledTime + placementTimeLimitSeconds;
            ResolvePlacementHud()?.Show(placementTimeLimitSeconds);
        }

        yield return new WaitUntil(() => playerPlaced || (placementTimeLimitSeconds > 0f &&
            Time.unscaledTime >= placementDeadlineUnscaled));
        TryAutoPlacePlayerBeforeCombatIfTimedOut();
        PlacementHudHide();

        GridManager.Instance.ClearAllHighlights();
    }

    float placementDeadlineUnscaled;

    void PlacementHudHide()
    {
        if (placementCountdownUi != null)
            placementCountdownUi.Hide();
        else
            FindFirstObjectByType<PlacementCountdownUI>(FindObjectsInactive.Include)?.Hide();
    }

    PlacementCountdownUI ResolvePlacementHud()
    {
        if (placementCountdownUi != null)
            return placementCountdownUi;
        placementCountdownUi = FindFirstObjectByType<PlacementCountdownUI>(FindObjectsInactive.Include);
        if (placementCountdownUi != null)
            return placementCountdownUi;
        var canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return null;
        var go = new GameObject("PlacementCountdownUI");
        go.transform.SetParent(canvas.transform, false);
        placementCountdownUi = go.AddComponent<PlacementCountdownUI>();
        placementCountdownUi.EnsureBuilt();
        return placementCountdownUi;
    }

    void TryAutoPlacePlayerBeforeCombatIfTimedOut()
    {
        if (playerPlaced || placementTimeLimitSeconds <= 0f) return;
        Cell pick = null;
        foreach (var c in spawnCellsTeam1)
        {
            if (!c.IsOccupied) { pick = c; break; }
        }
        if (pick == null) return;
        PlaceCharacter(player, pick, teamId: 1);
        playerPlaced = true;
        Debug.Log("[CombatInitializer] Placement — temps écoulé, placement automatique sur une case libre.");
    }

    // =========================================================
    // CLIC SUR CASE DE SPAWN (appelé par PlayerInputHandler ou équivalent)
    // =========================================================

    /// <summary>
    /// À appeler depuis ton script de gestion des clics quand le joueur
    /// clique sur une cellule pendant la phase de placement.
    /// </summary>
    public void OnCellClickedDuringPlacement(Cell clickedCell)
    {
        if (phase != CombatPhase.Placement) return;

        if (IsNetworkDuel && OracleCombatNetBridge.Instance != null)
        {
            int slot = PhotonNetwork.IsMasterClient ? 0 : 1;
            var allowed = slot == 0 ? spawnCellsTeam1 : spawnCellsTeam2;
            if (!allowed.Contains(clickedCell) || clickedCell.IsOccupied) return;
            if ((slot == 0 && netPlacement0Done) || (slot == 1 && netPlacement1Done)) return;
            OracleCombatNetBridge.Instance.SubmitPlacement(slot, clickedCell.GridX, clickedCell.GridY);
            return;
        }

        if (playerPlaced) return;
        if (!spawnCellsTeam1.Contains(clickedCell)) return;
        if (clickedCell.IsOccupied) return;

        PlaceCharacter(player, clickedCell, teamId: 1);
        playerPlaced = true;

        Debug.Log($"[CombatInitializer] Joueur placé en {clickedCell.GridX},{clickedCell.GridY}");
    }

    /// <summary>Appelé sur toutes les machines après validation réseau du déploiement.</summary>
    public void OnNetworkPlacementApplied(int combatSlot, Cell cell)
    {
        if (phase == CombatPhase.Combat || phase == CombatPhase.End) return;
        if (combatSlot == 0)
        {
            if (netPlacement0Done) return;
            PlaceCharacter(player, cell, teamId: 1);
            netPlacement0Done = true;
            Debug.Log($"[CombatInitializer] Réseau — équipe A placée {cell.GridX},{cell.GridY}");
        }
        else
        {
            if (netPlacement1Done) return;
            PlaceCharacter(opponent, cell, teamId: 2);
            netPlacement1Done = true;
            Debug.Log($"[CombatInitializer] Réseau — équipe B placée {cell.GridX},{cell.GridY}");
        }
    }

    // =========================================================
    // PHASE 3 — DÉMARRAGE DU COMBAT
    // =========================================================
    void StartCombat()
    {
        phase = CombatPhase.Combat;

        SetPassiveSelectionCombatUiVisible(true);

        // Enregistrer les personnages dans le TurnManager
        TurnManager.Instance.RegisterCharacter(player,   teamId: 1);
        TurnManager.Instance.RegisterCharacter(opponent, teamId: 2);

        // Lier le DeckUI au personnage dont c'est le tour
        TurnManager.Instance.OnTurnStart += OnTurnStarted;

        // Écouter la fin du combat
        TurnManager.Instance.OnCombatEnd += OnCombatEnd;

        // Lancer !
        TurnManager.Instance.StartCombat();

        Debug.Log("[CombatInitializer] Combat démarré !");
    }

    // =========================================================
    // CALLBACKS DE COMBAT
    // =========================================================
    void OnTurnStarted(TacticalCharacter character)
    {
        // Le DeckUI se met à jour pour refléter les sorts du personnage actif
        if (deckUI != null)
            deckUI.BindCharacter(character);

        Debug.Log($"[CombatInitializer] Tour de : {character.name}");
    }

    void OnCombatEnd(int winnerTeamId)
    {
        phase = CombatPhase.End;

        TurnManager.Instance.OnTurnStart -= OnTurnStarted;
        TurnManager.Instance.OnCombatEnd -= OnCombatEnd;

        if (deckUI != null) deckUI.UnbindCharacter();

        int localTeam = IsNetworkDuel ? OracleCombatNetBridge.Instance.GetLocalTeamId() : 1;
        bool playerWon = (winnerTeamId == localTeam);
        Debug.Log($"[CombatInitializer] Combat terminé — {(winnerTeamId == -1 ? "Égalité" : $"Équipe {winnerTeamId} gagne")}");

        if (victoryPanel != null) victoryPanel.SetActive(playerWon);
        if (defeatPanel  != null) defeatPanel.SetActive(!playerWon && winnerTeamId != -1);
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================
    /// <summary>Met à jour netPlacement* si les pions sont déjà posés (p. ex. RPC avant RunPlacement).</summary>
    void SyncNetPlacementFlagsFromPawns()
    {
        if (player != null && player.CurrentCell != null)
            netPlacement0Done = true;
        if (opponent != null && opponent.CurrentCell != null)
            netPlacement1Done = true;
    }

    void PlaceCharacter(TacticalCharacter character, Cell cell, int teamId)
    {
        character.gameObject.SetActive(true);
        character.Initialize(cell);

        // Appliquer le passif du deck si aucun passif manuel n'a été sélectionné
        var pm = character.GetComponent<PassiveManager>();
        if (pm != null && pm.activePassive == null && character.deck?.Passive != null)
        {
            pm.SetPassive(character.deck.Passive);
        }
    }

    void Validate()
    {
        if (player             == null) Debug.LogError("[CombatInitializer] Aucun TacticalCharacter (player) trouvé dans la scène !");
        if (opponent           == null) Debug.LogWarning("[CombatInitializer] Aucun adversaire trouvé — combat en solo uniquement.");
        if (arenaGenerator     == null) Debug.LogWarning("[CombatInitializer] ArenaGenerator absent — zones de spawn vides.");
        if (GridManager.Instance  == null) Debug.LogError("[CombatInitializer] GridManager absent de la scène !");
        if (TurnManager.Instance  == null) Debug.LogError("[CombatInitializer] TurnManager absent de la scène !");
    }
}
