using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gestion complète des clics souris sur la grille isométrique.
///
/// COMPORTEMENT PAR PHASE :
///   Placement → clic gauche sur case bleue = placement du personnage
///   Combat    → clic gauche sur case = déplacement OU lancer de sort selon contexte
///               clic droit            = annuler le sort sélectionné
///               survol                = preview de la zone AoE
///
/// SETUP INSPECTOR :
///   - Glisser le TacticalCharacter du joueur local
///   - Glisser la caméra principale (ou laisser vide = Camera.main)
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    // =========================================================
    // RÉFÉRENCES INSPECTOR
    // =========================================================
    [Header("Personnage local")]
    public TacticalCharacter character;

    [Header("Caméra (laisse vide = Camera.main)")]
    public Camera cam;

    // =========================================================
    // ÉTAT INTERNE
    // =========================================================
    private SpellCaster spellCaster;
    private Cell        lastHoveredCell;
    private TacticalCharacter _subscribedPawn;

    TacticalCharacter ControlledPawn
    {
        get
        {
            if (OracleCombatNetBridge.Instance != null &&
                OracleCombatNetBridge.Instance.ShouldSendCommandsOverNetwork)
                return OracleCombatNetBridge.Instance.GetLocalControlledCharacter();
            return character;
        }
    }

    void SyncPawnSubscription()
    {
        var p = ControlledPawn;
        if (p == _subscribedPawn) return;
        if (_subscribedPawn != null)
            _subscribedPawn.OnStateChanged -= OnCharacterStateChanged;
        _subscribedPawn = p;
        if (_subscribedPawn != null)
        {
            _subscribedPawn.OnStateChanged += OnCharacterStateChanged;
            spellCaster = _subscribedPawn.GetComponent<SpellCaster>();
        }
        else
            spellCaster = null;
    }

    // =========================================================
    // INITIALISATION
    // =========================================================
    void Start()
    {
        if (cam == null)
            cam = Camera.main;

        SyncPawnSubscription();
    }

    void LateUpdate()
    {
        SyncPawnSubscription();
    }

    void OnDestroy()
    {
        if (_subscribedPawn != null)
            _subscribedPawn.OnStateChanged -= OnCharacterStateChanged;
    }

    // Recalcule les highlights dès que le personnage revient Idle (fin de déplacement ou de sort).
    // À ce moment currentCell est déjà mis à jour → le dégradé PM est centré sur la bonne case.
    void OnCharacterStateChanged(CharacterState state)
    {
        if (state != CharacterState.Idle) return;
        if (TurnManager.Instance?.CurrentCharacter != ControlledPawn) return;
        if (spellCaster != null && spellCaster.HasSpellSelected) return;
        HighlightReachableCells();
    }

    // =========================================================
    // UPDATE — LECTURE DES INPUTS
    // =========================================================
    void Update()
    {
        if (cam == null || GridManager.Instance == null) return;

        Cell hoveredCell = GetCellUnderMouse();

        HandleHover(hoveredCell);
        HandleLeftClick(hoveredCell);
        HandleRightClick();

        lastHoveredCell = hoveredCell;
    }

    // =========================================================
    // SURVOL — preview AoE
    // =========================================================
    void HandleHover(Cell cell)
    {
        if (cell == lastHoveredCell) return;

        if (spellCaster != null && spellCaster.HasSpellSelected)
        {
            if (cell != null)
                spellCaster.PreviewAoE(cell);
            else
                spellCaster.ClearPreview();
        }

        // Hover visuel sur la grille
        if (cell != null)
            GridManager.Instance.SetHoveredCell(cell.GridX, cell.GridY);
    }

    // =========================================================
    // CLIC GAUCHE
    // =========================================================
    void HandleLeftClick(Cell cell)
    {
        if (!Input.GetMouseButtonDown(0)) return;
        // Ne pas traiter le clic grille si la souris est au-dessus d'un élément UI (DeckUI, boutons…)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (cell == null) return;

        var combatPhase = CombatInitializer.Instance != null
            ? CombatInitializer.Instance.CurrentPhase
            : CombatInitializer.CombatPhase.Combat;

        switch (combatPhase)
        {
            // ── Phase placement ──────────────────────────────
            case CombatInitializer.CombatPhase.Placement:
                CombatInitializer.Instance.OnCellClickedDuringPlacement(cell);
                break;

            // ── Phase combat ─────────────────────────────────
            case CombatInitializer.CombatPhase.Combat:
                HandleCombatClick(cell);
                break;
        }
    }

    void HandleCombatClick(Cell cell)
    {
        var pawn = ControlledPawn;
        if (pawn == null) return;

        // Pas le tour du joueur → ignorer
        if (TurnManager.Instance != null &&
            TurnManager.Instance.CurrentCharacter != pawn)
            return;

        // Un sort est sélectionné → tenter de le lancer
        if (spellCaster != null && spellCaster.HasSpellSelected)
        {
            if (OracleCombatNetBridge.Instance != null &&
                OracleCombatNetBridge.Instance.TrySubmitCast(pawn, cell, spellCaster.SelectedSpell))
                return;
            spellCaster.TryCast(cell);
            return;
        }

        // Pas de sort → tenter de se déplacer
        if (pawn.CanMoveTo(cell))
        {
            if (OracleCombatNetBridge.Instance != null &&
                OracleCombatNetBridge.Instance.TrySubmitMove(pawn, cell))
                return;
            pawn.MoveToCell(cell);
        }
    }

    // =========================================================
    // CLIC DROIT — annuler le sort sélectionné
    // =========================================================
    void HandleRightClick()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        if (spellCaster != null && spellCaster.HasSpellSelected)
        {
            spellCaster.CancelSpell();

            // Remettre les highlights de déplacement
            var pawn = ControlledPawn;
            if (pawn != null && TurnManager.Instance?.CurrentCharacter == pawn)
                HighlightReachableCells();
        }
    }

    // =========================================================
    // HIGHLIGHTS DE DÉPLACEMENT
    // =========================================================

    /// <summary>
    /// Affiche les cases accessibles au personnage actif.
    /// Appelé automatiquement au début du tour si ce personnage joue.
    /// </summary>
    public void HighlightReachableCells()
    {
        var pawn = ControlledPawn;
        if (pawn == null) return;

        GridManager.Instance.ClearAllHighlights();

        if (!pawn.IsAlive || pawn.CurrentCell == null) return;
        if (pawn.CurrentPM <= 0) return; // 0 PM = aucun highlight

        var cellsByDist = new Pathfinding().GetReachableCellsWithDistance(
            pawn.CurrentCell,
            pawn.CurrentPM
        );

        Color baseColor = GridManager.Instance.config.moveColor;
        int   maxPM     = pawn.CurrentPM;

        foreach (var kvp in cellsByDist)
        {
            // distance 1 → alpha plein, distance maxPM → alpha 35 %
            float t     = (maxPM > 1) ? (float)(kvp.Value - 1) / (maxPM - 1) : 0f;
            float alpha = Mathf.Lerp(1f, 0.35f, t) * baseColor.a;
            Color c     = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            GridManager.Instance.HighlightCell(kvp.Key, c, pulse: true);
        }
    }

    // =========================================================
    // UTILITAIRE — CELLULE SOUS LA SOURIS
    // =========================================================
    Cell GetCellUnderMouse()
    {
        return GridManager.Instance.GetCellAtScreenPosition(cam, Input.mousePosition);
    }

    // =========================================================
    // ABONNEMENT AUX ÉVÉNEMENTS DE TOUR
    // =========================================================
    void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart += OnTurnStart;
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart -= OnTurnStart;
    }

    void OnTurnStart(TacticalCharacter who)
    {
        SyncPawnSubscription();

        var pawn = ControlledPawn;
        if (who == pawn)
            HighlightReachableCells();
        else if (GridManager.Instance != null)
            GridManager.Instance.ClearAllHighlights();
    }
}
