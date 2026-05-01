using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// IA adversaire pour les tests solo.
/// S'active automatiquement si la partie N'EST PAS un duel en réseau.
/// 
/// SETUP : ajouter ce composant sur le même GameObject que TacticalCharacter
/// de l'adversaire (ou sur CombatInitializer — voir champ opponentCharacter).
/// 
/// Comportement par tour :
///   1. Attend un court délai (simule la "réflexion")
///   2. Se déplace vers le joueur
///   3. Lance le premier sort disponible sur le joueur (ou à portée)
///   4. Fin de tour
/// </summary>
[RequireComponent(typeof(TacticalCharacter))]
public class OpponentAI : MonoBehaviour
{
    [Header("Délais IA (secondes)")]
    [Tooltip("Pause avant d'agir (simule une réflexion).")]
    public float thinkDelay   = 0.6f;
    [Tooltip("Délai entre le déplacement et le lancer de sort.")]
    public float actionDelay  = 0.4f;
    [Tooltip("Délai avant d'appeler Fin de tour.")]
    public float endTurnDelay = 0.5f;

    private TacticalCharacter _self;
    private SpellCaster       _caster;
    private bool              _myTurn;

    void Awake()
    {
        _self   = GetComponent<TacticalCharacter>();
        _caster = GetComponent<SpellCaster>();
    }

    void Start()
    {
        // Ne s'abonner que si on est en mode solo (pas de duel réseau)
        if (IsNetworkDuel()) return;

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart += OnTurnStart;
            TurnManager.Instance.OnCombatEnd += OnCombatEnd;
        }
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart -= OnTurnStart;
            TurnManager.Instance.OnCombatEnd -= OnCombatEnd;
        }
    }

    // =========================================================
    // CALLBACKS TURN MANAGER
    // =========================================================
    private void OnTurnStart(TacticalCharacter character)
    {
        if (character != _self) return;
        if (_myTurn) return;
        _myTurn = true;
        StartCoroutine(PlayTurn());
    }

    private void OnCombatEnd(int winnerTeamId)
    {
        StopAllCoroutines();
        _myTurn = false;
    }

    // =========================================================
    // DÉROULEMENT DU TOUR IA
    // =========================================================
    private IEnumerator PlayTurn()
    {
        yield return new WaitForSeconds(thinkDelay);

        // ── 1. Trouver le personnage cible (l'équipe du joueur) ──────
        TacticalCharacter target = FindPlayerCharacter();

        // ── 2. Se déplacer vers la cible ─────────────────────────────
        if (target != null && _self.CurrentPM > 0)
        {
            Cell moveTarget = FindBestMoveCell(target);
            if (moveTarget != null)
            {
                _self.MoveToCell(moveTarget);
                // Attendre la fin du déplacement
                float timeout = 8f;
                while (_self.State == CharacterState.Moving && timeout > 0f)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }
        }

        yield return new WaitForSeconds(actionDelay);

        // ── 3. Lancer un sort ────────────────────────────────────────
        if (target != null && _caster != null && _self.CurrentPA > 0)
            yield return StartCoroutine(TryCastSpell(target));

        yield return new WaitForSeconds(endTurnDelay);

        // ── 4. Fin de tour ───────────────────────────────────────────
        _myTurn = false;
        if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive)
            TurnManager.Instance.EndTurn();
    }

    // =========================================================
    // LOGIQUE DE DÉPLACEMENT
    // =========================================================
    private Cell FindBestMoveCell(TacticalCharacter target)
    {
        if (_self.CurrentCell == null || target.CurrentCell == null) return null;

        // Toutes les cases atteignables avec les PM restants
        var reachable = new Pathfinding().GetReachableCells(_self.CurrentCell, _self.CurrentPM);
        if (reachable == null || reachable.Count == 0) return null;

        // Prendre la case la plus proche de la cible (hors case occupée)
        Cell best = null;
        int bestDist = int.MaxValue;
        foreach (var cell in reachable)
        {
            if (cell.IsOccupied && cell != _self.CurrentCell) continue;
            int d = ManhattanDist(cell, target.CurrentCell);
            if (d < bestDist)
            {
                bestDist = d;
                best     = cell;
            }
        }

        // Inutile de bouger si on est déjà sur la meilleure case
        if (best == _self.CurrentCell) return null;
        return best;
    }

    private static int ManhattanDist(Cell a, Cell b)
        => Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);

    // =========================================================
    // LOGIQUE DE SORT
    // =========================================================
    private IEnumerator TryCastSpell(TacticalCharacter target)
    {
        if (_self.deck == null) yield break;

        foreach (var spell in _self.deck.Spells)
        {
            if (spell == null) continue;
            if (!_self.CanCastSpell(spell)) continue;

            // Choisir une case cible valide
            Cell castTarget = PickCastTarget(spell, target);
            if (castTarget == null) continue;

            // Lancer via SpellCaster
            if (_caster.SelectSpell(spell))
            {
                bool cast = _caster.TryCast(castTarget);
                if (cast)
                {
                    // Attendre la fin du cast (animation)
                    float timeout = 3f;
                    while (_self.State == CharacterState.Casting && timeout > 0f)
                    {
                        timeout -= Time.deltaTime;
                        yield return null;
                    }
                    yield break;  // un sort suffit par tour
                }
                else
                {
                    _caster.CancelSpell();
                }
            }
        }
    }

    private Cell PickCastTarget(SpellData spell, TacticalCharacter target)
    {
        if (_self.CurrentCell == null) return null;

        // Self/boost : case propre
        if (spell.zoneType == ZoneType.Self || spell.zoneType == ZoneType.Boost)
            return _self.CurrentCell;

        // Essayer d'abord la case du joueur
        if (target != null && target.CurrentCell != null)
        {
            if (_caster.WouldAcceptCast(spell, target.CurrentCell))
                return target.CurrentCell;
        }

        // Sinon chercher n'importe quelle case à portée occupée par un ennemi
        int effectiveMax = spell.rangeMax + _self.GetBonusRange();
        Cell origin = _self.CurrentCell;
        for (int dx = -effectiveMax; dx <= effectiveMax; dx++)
        for (int dy = -effectiveMax; dy <= effectiveMax; dy++)
        {
            int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (dist < spell.rangeMin || dist > effectiveMax) continue;
            var cell = GridManager.Instance.GetCell(origin.GridX + dx, origin.GridY + dy);
            if (cell == null) continue;
            if (_caster.WouldAcceptCast(spell, cell))
                return cell;
        }
        return null;
    }

    // =========================================================
    // HELPERS
    // =========================================================
    private TacticalCharacter FindPlayerCharacter()
    {
        var ci = CombatInitializer.Instance;
        if (ci != null && ci.player != null && ci.player.IsAlive)
            return ci.player;

        // Fallback : premier TacticalCharacter vivant qui n'est pas nous
        foreach (var tc in FindObjectsByType<TacticalCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (tc != _self && tc.IsAlive) return tc;
        return null;
    }

    private static bool IsNetworkDuel()
    {
        if (OracleCombatNetBridge.Instance == null) return false;
        return OracleCombatNetBridge.Instance.ShouldSendCommandsOverNetwork;
    }
}
