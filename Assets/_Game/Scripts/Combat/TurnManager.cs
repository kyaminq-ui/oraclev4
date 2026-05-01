using UnityEngine;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================
    public static TurnManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // =========================================================
    // EVENTS
    // =========================================================
    public event System.Action<TacticalCharacter> OnTurnStart;
    public event System.Action<TacticalCharacter> OnTurnEnd;
    public event System.Action<int> OnRoundStart;       // numéro du round
    /// <summary>Équipe gagnante (teamId Photon / logique jeu) ; -1 si égalité / aucune.</summary>
    public event System.Action<int> OnCombatEnd;
    public event System.Action OnTimeOut;

    // =========================================================
    // CONFIGURATION
    // =========================================================
    [Header("Configuration")]
    [Tooltip("Durée max du tour en secondes (combat actif).")]
    public float turnDuration = 15f;

    // =========================================================
    // ÉTAT
    // =========================================================
    private List<TacticalCharacter> turnOrder = new List<TacticalCharacter>();
    private Dictionary<TacticalCharacter, int> characterTeams = new Dictionary<TacticalCharacter, int>();
    private int currentIndex = 0;
    private int roundNumber = 0;
    private float timeRemaining = 0f;
    private bool combatActive = false;
    private bool turnActive = false;

    public TacticalCharacter CurrentCharacter =>
        (currentIndex < turnOrder.Count) ? turnOrder[currentIndex] : null;
    public float TimeRemaining => timeRemaining;
    public int RoundNumber => roundNumber;
    public bool IsCombatActive => combatActive;

    // =========================================================
    // ENREGISTREMENT
    // =========================================================
    public void RegisterCharacter(TacticalCharacter character, int teamId)
    {
        if (characterTeams.ContainsKey(character)) return;
        characterTeams[character] = teamId;
        character.OnDeath += () => OnCharacterDied(character);
    }

    // =========================================================
    // DÉMARRAGE DU COMBAT
    // =========================================================
    public void StartCombat()
    {
        turnOrder.Clear();
        foreach (var kvp in characterTeams)
            turnOrder.Add(kvp.Key);

        // Initiative décroissante (spec 2.1.1). Égalités : ordre InstanceID stable.
        turnOrder.Sort(CompareInitiativeThenStable);

        roundNumber = 0;
        currentIndex = 0;
        combatActive = true;
        StartNewRound();
    }

    private static int CompareInitiativeThenStable(TacticalCharacter a, TacticalCharacter b)
    {
        int ia = InitiativeOf(a);
        int ib = InitiativeOf(b);
        int cmp = ib.CompareTo(ia);
        if (cmp != 0) return cmp;
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    private static int InitiativeOf(TacticalCharacter ch)
    {
        if (ch != null && ch.stats != null) return ch.stats.initiative;
        return 0;
    }

    // =========================================================
    // GESTION DES ROUNDS
    // =========================================================
    private void StartNewRound()
    {
        roundNumber++;
        currentIndex = 0;
        OnRoundStart?.Invoke(roundNumber);
        AdvanceToNextTurn();
    }

    private void AdvanceToNextTurn()
    {
        while (currentIndex < turnOrder.Count && !turnOrder[currentIndex].IsAlive)
            currentIndex++;

        if (currentIndex >= turnOrder.Count)
        {
            StartNewRound();
            return;
        }

        TacticalCharacter current = turnOrder[currentIndex];
        timeRemaining = turnDuration;
        turnActive = true;

        current.OnTurnStart();
        OnTurnStart?.Invoke(current);
    }

    // =========================================================
    // FIN DE TOUR
    // =========================================================
    public void EndTurn()
    {
        if (!turnActive) return;
        turnActive = false;

        TacticalCharacter current = CurrentCharacter;
        current?.OnTurnEnd();
        OnTurnEnd?.Invoke(current);

        currentIndex++;
        AdvanceToNextTurn();
    }

    // =========================================================
    // TIMER
    // =========================================================
    void Update()
    {
        if (!combatActive || !turnActive) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            OnTimeOut?.Invoke();
            EndTurn();
        }
    }

    // =========================================================
    // VICTOIRE
    // =========================================================
    private void OnCharacterDied(TacticalCharacter character)
    {
        CheckVictoryCondition();
    }

    private void CheckVictoryCondition()
    {
        Dictionary<int, int> aliveByTeam = new Dictionary<int, int>();
        foreach (var kvp in characterTeams)
        {
            if (!kvp.Key.IsAlive) continue;
            int team = kvp.Value;
            if (!aliveByTeam.ContainsKey(team)) aliveByTeam[team] = 0;
            aliveByTeam[team]++;
        }

        if (aliveByTeam.Count == 1)
        {
            combatActive = false;
            turnActive = false;
            foreach (var kvp in aliveByTeam)
                OnCombatEnd?.Invoke(kvp.Key);
        }
        else if (aliveByTeam.Count == 0)
        {
            combatActive = false;
            turnActive = false;
            OnCombatEnd?.Invoke(-1);
        }
    }
}
