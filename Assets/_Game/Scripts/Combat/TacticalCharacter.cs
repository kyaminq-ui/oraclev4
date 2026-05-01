using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CharacterState { Idle, Moving, Casting, Dead }

public enum FacingDirection { SouthEast, SouthWest, NorthEast, NorthWest }

public class TacticalCharacter : MonoBehaviour
{
    // =========================================================
    // EVENTS
    // =========================================================
    public event System.Action<int, int> OnPAChanged;
    public event System.Action<int, int> OnPMChanged;
    public event System.Action<int, int> OnHPChanged;
    public event System.Action<CharacterState> OnStateChanged;
    public event System.Action<FacingDirection> OnFacingChanged;
    public event System.Action<int>             OnMoveStarted;   // nb de cases parcourues
    public event System.Action OnDeath;
    public event System.Action<SpellData> OnSpellCast;
    public event System.Action OnTurnStart_Passive;
    public event System.Action OnTurnEnd_Passive;

    // =========================================================
    // CONFIGURATION
    // =========================================================
    [Header("Données")]
    public CharacterStats stats;
    public DeckData deck;

    [Tooltip("Rempli au runtime par CombatInitializer si un SpellDeckPool est utilisé ; sinon lecture de deck.Spells.")]
    [SerializeField] List<SpellData> runtimeSpells = new List<SpellData>();

    [Header("Visuel")]
    public SpriteRenderer spriteRenderer;

    [Header("Vitesse de déplacement")]
    [Tooltip("Vitesse pour 3+ PM (marche normale)")]
    [SerializeField] private float moveSpeedNormal = 6f;
    [Tooltip("Vitesse pour 1-2 PM (marche lente)")]
    [SerializeField] private float moveSpeedSlow   = 3.5f;

    // =========================================================
    // ÉTAT EN COMBAT
    // =========================================================
    private int currentHP;
    private int currentPA;
    private int currentPM;
    private int nextTurnBonusPA;
    private int bonusRange;
    private CharacterState _state = CharacterState.Idle;
    private Cell currentCell;
    private FacingDirection facing = FacingDirection.SouthEast;

    private Dictionary<SpellData, int> spellCooldowns = new Dictionary<SpellData, int>();
    private List<StatusEffect> activeEffects = new List<StatusEffect>();

    static readonly SpellData[] NoSpellsFallback = new SpellData[0];

    // =========================================================
    // PROPRIÉTÉS PUBLIQUES
    // =========================================================
    public CharacterState State
    {
        get => _state;
        private set { if (_state == value) return; _state = value; OnStateChanged?.Invoke(_state); }
    }
    public Cell CurrentCell     => currentCell;
    public int CurrentHP        => currentHP;
    public int CurrentPA        => currentPA;
    public int CurrentPM        => currentPM;
    public FacingDirection Facing => facing;
    public bool IsAlive         => currentHP > 0;
    public int ShieldHP         => GetStatusEffectValue(StatusEffectType.Shield);

    /// <summary>Sorts utilisés pour l’UI / l’IA / le réseau : deck runtime si défini, sinon asset <see cref="deck"/>.</summary>
    public IReadOnlyList<SpellData> ActiveSpells =>
        runtimeSpells.Count > 0 ? runtimeSpells : (deck != null ? deck.Spells : NoSpellsFallback);

    public void ClearRuntimeSpellDeck() => runtimeSpells.Clear();

    /// <summary>Jusqu’à <see cref="DeckData.MaxSpells"/> entrées ; ignore les null.</summary>
    public void SetRuntimeSpellDeck(IReadOnlyList<SpellData> picks)
    {
        runtimeSpells.Clear();
        if (picks == null) return;
        for (int i = 0; i < picks.Count && runtimeSpells.Count < DeckData.MaxSpells; i++)
            if (picks[i] != null) runtimeSpells.Add(picks[i]);
    }

    // =========================================================
    // INITIALISATION
    // =========================================================
    public void Initialize(Cell startCell)
    {
        if (stats == null) { Debug.LogError($"TacticalCharacter '{name}' : stats manquants !"); return; }
        currentHP = stats.maxHP;
        currentPA = stats.maxPA;
        currentPM = stats.maxPM;
        currentCell = startCell;
        startCell.SetOccupant(gameObject);
        transform.position = GridManager.Instance != null
            ? GridManager.Instance.GridToWorldFace(startCell.GridX, startCell.GridY)
            : startCell.WorldPosition;
        UpdateSortingOrder();
        OnHPChanged?.Invoke(currentHP, stats.maxHP);
    }

    // =========================================================
    // GESTION DU TOUR
    // =========================================================
    public void OnTurnStart()
    {
        if (!IsAlive) return;
        currentPA = stats.maxPA + nextTurnBonusPA;
        nextTurnBonusPA = 0;
        currentPM = stats.maxPM;
        OnPAChanged?.Invoke(currentPA, stats.maxPA);
        OnPMChanged?.Invoke(currentPM, stats.maxPM);
        TickCooldowns();
        OnTurnStart_Passive?.Invoke();
        ProcessTurnStartEffects();
    }

    public void OnTurnEnd()
    {
        bonusRange = 0;
        OnTurnEnd_Passive?.Invoke();
        ProcessTurnEndEffects();
    }

    public void NotifySpellCast(SpellData spell) => OnSpellCast?.Invoke(spell);

    private void TickCooldowns()
    {
        var keys = new List<SpellData>(spellCooldowns.Keys);
        foreach (var spell in keys)
            if (--spellCooldowns[spell] <= 0) spellCooldowns.Remove(spell);
    }

    // =========================================================
    // EFFETS DE STATUT
    // =========================================================
    public void AddStatusEffect(StatusEffect effect)
    {
        // Saignement : cumulable jusqu'à 2 stacks
        if (effect.type == StatusEffectType.Bleed)
        {
            int stacks = CountEffects(StatusEffectType.Bleed);
            if (stacks >= 2) return;
        }
        // Pour les autres, on remplace si déjà présent (refresh durée)
        else
        {
            RemoveStatusEffect(effect.type);
        }
        activeEffects.Add(effect);
    }

    public void RemoveStatusEffect(StatusEffectType type)
    {
        activeEffects.RemoveAll(e => e.type == type);
    }

    public bool HasStatusEffect(StatusEffectType type) =>
        activeEffects.Exists(e => e.type == type);

    public int GetStatusEffectValue(StatusEffectType type)
    {
        int total = 0;
        foreach (var e in activeEffects)
            if (e.type == type) total += e.value;
        return total;
    }

    private int CountEffects(StatusEffectType type)
    {
        int count = 0;
        foreach (var e in activeEffects) if (e.type == type) count++;
        return count;
    }

    public void ClearAllDebuffs() =>
        activeEffects.RemoveAll(e => e.isDebuff);

    private void ProcessTurnStartEffects()
    {
        var toRemove = new List<StatusEffect>();
        foreach (var effect in activeEffects)
        {
            if (effect.type == StatusEffectType.Bleed)
                TakeDamage(effect.value, null);

            effect.Tick();
            if (effect.IsExpired) toRemove.Add(effect);
        }
        foreach (var e in toRemove) activeEffects.Remove(e);
    }

    private void ProcessTurnEndEffects()
    {
        var toRemove = new List<StatusEffect>();
        foreach (var effect in activeEffects)
        {
            if (effect.type == StatusEffectType.DamageReduction ||
                effect.type == StatusEffectType.Thorns ||
                effect.type == StatusEffectType.Shield ||
                effect.type == StatusEffectType.ReducedAttack)
            {
                effect.Tick();
                if (effect.IsExpired) toRemove.Add(effect);
            }
        }
        foreach (var e in toRemove) activeEffects.Remove(e);
    }

    // =========================================================
    // BONUS DE RESSOURCES
    // =========================================================
    public void AddBonusPA(int amount)
    {
        currentPA += amount;
        OnPAChanged?.Invoke(currentPA, stats.maxPA);
    }

    public void AddBonusPM(int amount)
    {
        currentPM += amount;
        OnPMChanged?.Invoke(currentPM, stats.maxPM);
    }

    public void AddNextTurnBonusPA(int amount) => nextTurnBonusPA += amount;

    public void AddBonusRange(int amount, int duration) => bonusRange += amount;

    public int GetBonusRange() => bonusRange;

    public int RemovePM(int amount)
    {
        int actual = Mathf.Min(amount, currentPM);
        currentPM -= actual;
        OnPMChanged?.Invoke(currentPM, stats.maxPM);
        return actual;
    }

    // =========================================================
    // DÉPLACEMENT
    // =========================================================
    public bool CanMoveTo(Cell target)
    {
        if (target == null || !target.IsWalkable || target.IsOccupied) return false;
        if (State != CharacterState.Idle) return false;
        var path = new Pathfinding().FindPath(currentCell, target);
        return path != null && path.Count - 1 <= currentPM;
    }

    public void MoveToCell(Cell target)
    {
        if (!CanMoveTo(target)) return;
        var path = new Pathfinding().FindPath(currentCell, target);
        if (path != null) StartCoroutine(MoveAlongPath(path));
    }

    public void ForceSetCell(Cell cell)
    {
        currentCell = cell;
        UpdateSortingOrder();
    }

    private IEnumerator MoveAlongPath(List<Cell> path)
    {
        int steps = path.Count - 1;
        float speed = steps >= 3 ? moveSpeedNormal : moveSpeedSlow;

        OnMoveStarted?.Invoke(steps);
        State = CharacterState.Moving;

        for (int i = 1; i < path.Count; i++)
        {
            Cell next = path[i];
            currentPM--;
            OnPMChanged?.Invoke(currentPM, stats.maxPM);
            UpdateFacing(currentCell, next);
            currentCell.ClearOccupant();

            Vector3 start = transform.position;
            Vector3 end = GridManager.Instance != null
                ? GridManager.Instance.GridToWorldFace(next.GridX, next.GridY)
                : next.WorldPosition;
            float elapsed = 0f, duration = 1f / speed;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, elapsed / duration);
                UpdateSortingOrder();
                yield return null;
            }
            transform.position = end;
            currentCell = next;
            currentCell.SetOccupant(gameObject);
            UpdateSortingOrder();
        }
        State = CharacterState.Idle;
    }

    private void UpdateFacing(Cell from, Cell to)
    {
        // Grille iso (GridManager.GridToWorld) : un pas (+1,0) ou (0,+1) n'a pas le même
        // vecteur monde. Classer par signe de Δworld pour aligner NE/SE/NO/SO avec les sprites _NE/_SE/_NO/_SO.
        Vector3 a = GridManager.Instance != null
            ? GridManager.Instance.GridToWorld(from.GridX, from.GridY)
            : from.WorldPosition;
        Vector3 b = GridManager.Instance != null
            ? GridManager.Instance.GridToWorld(to.GridX, to.GridY)
            : to.WorldPosition;
        Vector3 d = b - a;
        if (d.sqrMagnitude < 1e-10f) return;

        FacingDirection dir;
        if      (d.x >= 0f && d.y >= 0f) dir = FacingDirection.NorthEast;
        else if (d.x <  0f && d.y >= 0f) dir = FacingDirection.NorthWest;
        else if (d.x >= 0f && d.y <  0f) dir = FacingDirection.SouthEast;
        else                               dir = FacingDirection.SouthWest;
        SetFacing(dir);
    }

    private void SetFacing(FacingDirection dir)
    {
        if (facing == dir) return;
        facing = dir;
        OnFacingChanged?.Invoke(dir);
    }

    // =========================================================
    // SORTS
    // =========================================================
    public bool CanCastSpell(SpellData spell)
    {
        if (spell == null || State != CharacterState.Idle) return false;
        if (currentPA < spell.paCost) return false;
        if (spellCooldowns.ContainsKey(spell)) return false;
        if (HasStatusEffect(StatusEffectType.Silence)) return false;
        return true;
    }

    public void SpendPA(int amount)
    {
        currentPA = Mathf.Max(0, currentPA - amount);
        OnPAChanged?.Invoke(currentPA, stats.maxPA);
    }

    public void StartCooldown(SpellData spell)
    {
        if (spell.cooldown > 0) spellCooldowns[spell] = spell.cooldown;
    }

    public int GetCooldown(SpellData spell) =>
        spellCooldowns.TryGetValue(spell, out int cd) ? cd : 0;

    public void SetCastingState(bool casting) =>
        State = casting ? CharacterState.Casting : CharacterState.Idle;

    // =========================================================
    // DÉGÂTS / SOINS
    // =========================================================
    public void TakeDamage(int amount, TacticalCharacter attacker)
    {
        if (!IsAlive) return;

        // Passifs défensifs (Évasif, Bouclier Hasardeux)
        var pm = GetComponent<PassiveManager>();
        if (pm != null) amount = pm.ModifyIncomingDamage(amount, attacker);
        if (amount <= 0) return;

        // Réduction de dégâts (Peau d'Écorce)
        int reduction = GetStatusEffectValue(StatusEffectType.DamageReduction);
        amount = Mathf.Max(0, amount - reduction);

        // Bouclier (Rempart)
        int shield = GetStatusEffectValue(StatusEffectType.Shield);
        if (shield > 0)
        {
            int absorbed = Mathf.Min(shield, amount);
            amount -= absorbed;
            // Réduire le shield restant
            foreach (var e in activeEffects)
                if (e.type == StatusEffectType.Shield) { e.value -= absorbed; break; }
            if (GetStatusEffectValue(StatusEffectType.Shield) <= 0)
                RemoveStatusEffect(StatusEffectType.Shield);
        }

        // Renvoi de dégâts (Épine)
        int thorns = GetStatusEffectValue(StatusEffectType.Thorns);
        if (thorns > 0 && attacker != null)
            attacker.TakeDamage(thorns, null);

        if (amount <= 0) return;

        // LastBreath (Second Souffle)
        if (HasStatusEffect(StatusEffectType.LastBreath) && currentHP - amount <= 0)
        {
            currentHP = 1;
            RemoveStatusEffect(StatusEffectType.LastBreath);
            OnHPChanged?.Invoke(currentHP, stats.maxHP);
            return;
        }

        currentHP = Mathf.Max(0, currentHP - amount);
        OnHPChanged?.Invoke(currentHP, stats.maxHP);
        if (currentHP <= 0) Die();
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        currentHP = Mathf.Min(stats.maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, stats.maxHP);
    }

    private void Die()
    {
        State = CharacterState.Dead;
        currentCell?.ClearOccupant();
        OnDeath?.Invoke();
    }

    // =========================================================
    // SORTING ORDER ISOMÉTRIQUE
    // =========================================================
    private void UpdateSortingOrder()
    {
        if (spriteRenderer == null) return;
        int bias = (GridManager.Instance != null) ? GridManager.Instance.config.characterSortingBias : 1000;
        spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10) + bias;
    }
}
