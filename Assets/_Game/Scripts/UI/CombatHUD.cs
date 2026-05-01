using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// HUD de combat — roadmap 4.2.1
/// Barre haute : HP équipe A | HP équipe B (timer du tour : <see cref="TimerUI"/> haut-droite)
/// Barre basse : passif actif | PA / PM (icônes + chiffres) | zone Deck (DeckUI) | Fin de tour
/// </summary>
public class CombatHUD : MonoBehaviour
{
    // =========================================================
    // PERSONNAGES (auto si vides)
    // =========================================================
    [Header("Combatants")]
    public TacticalCharacter teamACharacter;
    public TacticalCharacter teamBCharacter;
    [Tooltip("Celui qui appuie sur Fin de tour (souvent = team A en local).")]
    public TacticalCharacter localPlayerCharacter;

    // =========================================================
    // HAUT — HP
    // =========================================================
    [Header("Équipe A (gauche)")]
    public TextMeshProUGUI teamALabel;
    public Image            teamAHpFill;
    public TextMeshProUGUI  teamAHpValue;

    [Header("Équipe B (droite)")]
    public TextMeshProUGUI teamBLabel;
    public Image            teamBHpFill;
    public TextMeshProUGUI  teamBHpValue;

    // =========================================================
    // BAS — Passif / ressources / fin de tour
    // =========================================================
    [Header("Passif (tour actif)")]
    public Image            passiveIcon;
    public TextMeshProUGUI  passiveNameText;

    [Header("Passif Widget (bas-gauche)")]
    public PassiveHUDWidget passiveWidget;

    [Header("PA / PM (tour actif)")]
    [Tooltip("Icône mana (nombre PA centré par paText).")]
    public Image            paIconImage;
    [Tooltip("Icône déplacement (nombre PM centré par pmText).")]
    public Image            pmIconImage;
    public TextMeshProUGUI  paText;
    public TextMeshProUGUI  pmText;

    [Header("Chrono de tour")]
    [Tooltip("Optionnel. Si vide, recherche automatique dans les enfants (ex. sous TimerHost).")]
    public TimerUI          combatTurnTimer;

    [Header("Actions")]
    public Button endTurnButton;

    // =========================================================
    // COULEURS
    // =========================================================
    public Color accentColor = new Color(0.788f, 0.659f, 0.298f, 1f);

    TacticalCharacter _subPA, _subPM, _subHP_A, _subHP_B;

    void Awake()
    {
        AutoFindCharacters();
        if (localPlayerCharacter == null) localPlayerCharacter = teamACharacter;
        if (combatTurnTimer == null)
            combatTurnTimer = GetComponentInChildren<TimerUI>(true);
        OracleHudRuntimeSprites.ApplyCombatHudIfMissing(paIconImage, pmIconImage, combatTurnTimer);
    }

    void Start()
    {
        WireHpStatic();
        WireEndTurnButton();
        ApplyImportantTypeface();
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart += OnTurnStart;
        OnTurnStart(TurnManager.Instance != null ? TurnManager.Instance.CurrentCharacter : null);

        // Les personnages sont désactivés au départ et initialisés en différé par CombatInitializer.
        // On relance un refresh périodique jusqu'à ce que les deux barres soient alimentées.
        StartCoroutine(LateHpRefresh());
    }

    IEnumerator LateHpRefresh()
    {
        // Tente de câbler et rafraîchir les barres HP jusqu'à ce qu'elles soient valides.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSeconds(0.25f);

            // Re-chercher les personnages si pas encore trouvés
            if (teamACharacter == null || teamBCharacter == null)
                AutoFindCharacters();

            // Re-câbler si un personnage vient d'être activé / initialisé
            if (_subHP_A == null && teamACharacter != null)
            {
                teamACharacter.OnHPChanged += OnHpA;
                _subHP_A = teamACharacter;
                if (teamALabel != null) teamALabel.text = teamACharacter.name;
            }
            if (_subHP_B == null && teamBCharacter != null)
            {
                teamBCharacter.OnHPChanged += OnHpB;
                _subHP_B = teamBCharacter;
                if (teamBLabel != null) teamBLabel.text = teamBCharacter.name;
            }

            bool aOk = teamACharacter != null && teamACharacter.stats != null && teamACharacter.CurrentHP > 0;
            bool bOk = teamBCharacter != null && teamBCharacter.stats != null && teamBCharacter.CurrentHP > 0;

            if (aOk) RefreshHpBar(teamACharacter, teamAHpFill, teamAHpValue);
            if (bOk) RefreshHpBar(teamBCharacter, teamBHpFill, teamBHpValue);

            if (aOk && bOk) yield break;
        }
    }

    void OnDestroy()
    {
        UnsubscribeAll();
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart -= OnTurnStart;
    }

    void AutoFindCharacters()
    {
        if (teamACharacter != null && teamBCharacter != null) return;
        var all = FindObjectsByType<TacticalCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length >= 1 && teamACharacter == null) teamACharacter = all[0];
        if (all.Length >= 2 && teamBCharacter == null)
        {
            foreach (var c in all)
                if (c != teamACharacter) { teamBCharacter = c; break; }
        }
    }

    void WireHpStatic()
    {
        if (teamACharacter != null)
        {
            teamACharacter.OnHPChanged += OnHpA;
            _subHP_A = teamACharacter;
            if (teamALabel != null) teamALabel.text = teamACharacter.name;
            RefreshHpBar(teamACharacter, teamAHpFill, teamAHpValue);
        }
        if (teamBCharacter != null)
        {
            teamBCharacter.OnHPChanged += OnHpB;
            _subHP_B = teamBCharacter;
            if (teamBLabel != null) teamBLabel.text = teamBCharacter.name;
            RefreshHpBar(teamBCharacter, teamBHpFill, teamBHpValue);
        }
    }

    void OnHpA(int cur, int max) => RefreshHpBar(teamACharacter, teamAHpFill, teamAHpValue);
    void OnHpB(int cur, int max) => RefreshHpBar(teamBCharacter, teamBHpFill, teamBHpValue);

    static void RefreshHpBar(TacticalCharacter ch, Image fill, TextMeshProUGUI valueText)
    {
        if (ch?.stats == null) return;
        int max = ch.stats.maxHP;
        int cur = ch.CurrentHP;
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            float t     = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
            fill.fillAmount = t;
        }
        if (valueText != null) valueText.text = $"{cur} / {max}";
    }

    void WireEndTurnButton()
    {
        if (endTurnButton == null) return;
        endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    /// <summary>Police Aseprite sur le libellé joueur (barre de vie), PA/PM et bouton fin de tour.</summary>
    void ApplyImportantTypeface()
    {
        OracleUIImportantFont.Apply(teamALabel);
        OracleUIImportantFont.Apply(paText);
        OracleUIImportantFont.Apply(pmText);
        if (endTurnButton != null)
            OracleUIImportantFont.Apply(endTurnButton.GetComponentInChildren<TextMeshProUGUI>(true));
    }

    TacticalCharacter ResolvedLocalPawn()
    {
        if (OracleCombatNetBridge.Instance != null &&
            OracleCombatNetBridge.Instance.ShouldSendCommandsOverNetwork)
        {
            var p = OracleCombatNetBridge.Instance.GetLocalControlledCharacter();
            if (p != null) return p;
        }
        return localPlayerCharacter;
    }

    void OnEndTurnClicked()
    {
        if (TurnManager.Instance == null) return;
        var cur = TurnManager.Instance.CurrentCharacter;
        if (cur == null) return;
        var localPawn = ResolvedLocalPawn();
        if (localPawn != null && cur != localPawn) return;
        if (OracleCombatNetBridge.Instance != null &&
            OracleCombatNetBridge.Instance.TrySubmitEndTurn(localPawn))
            return;
        TurnManager.Instance.EndTurn();
    }

    void OnTurnStart(TacticalCharacter active)
    {
        UnsubscribeResources();

        if (active == null) return;

        active.OnPAChanged += OnPAChanged;
        active.OnPMChanged += OnPMChanged;
        _subPA = active;
        _subPM = active;

        OnPAChanged(active.CurrentPA, active.stats != null ? active.stats.maxPA : 8);
        OnPMChanged(active.CurrentPM, active.stats != null ? active.stats.maxPM : 3);

        // Toujours afficher le passif du joueur local, pas celui du tour actif
        RefreshPassiveDisplay(localPlayerCharacter != null ? localPlayerCharacter : active);

        if (endTurnButton != null)
        {
            var lp = ResolvedLocalPawn();
            bool myTurn = lp == null || active == lp;
            endTurnButton.interactable = myTurn && TurnManager.Instance != null && TurnManager.Instance.IsCombatActive;
        }
    }

    void UnsubscribeResources()
    {
        if (_subPA != null) _subPA.OnPAChanged -= OnPAChanged;
        if (_subPM != null) _subPM.OnPMChanged -= OnPMChanged;
        _subPA = _subPM = null;
    }

    void UnsubscribeAll()
    {
        UnsubscribeResources();
        if (_subHP_A != null) _subHP_A.OnHPChanged -= OnHpA;
        if (_subHP_B != null) _subHP_B.OnHPChanged -= OnHpB;
        _subHP_A = _subHP_B = null;
    }

    void OnPAChanged(int cur, int max)
    {
        if (paText != null)
            paText.text = cur.ToString();
    }

    void OnPMChanged(int cur, int max)
    {
        if (pmText != null)
            pmText.text = cur.ToString();
    }

    void RefreshPassiveDisplay(TacticalCharacter ch)
    {
        var pm = ch != null ? ch.GetComponent<PassiveManager>() : null;
        var p  = pm != null ? pm.activePassive : null;

        if (passiveNameText != null)
            passiveNameText.text = p != null ? p.passiveName : "—";

        if (passiveIcon != null)
        {
            passiveIcon.preserveAspect = true;
            passiveIcon.sprite  = p != null ? p.icon : null;
            passiveIcon.enabled = p != null && p.icon != null;
        }

        passiveWidget?.SetPassive(p);
    }

    /// <summary>
    /// Affiche ou masque le chrome de combat (passif, PA/PM, fin de tour ; le DeckUI est géré par <see cref="CombatInitializer"/>).
    /// Masqué pendant sélection de passif et placement ; affiché une fois le combat lancé.
    /// </summary>
    public void SetCombatChromeVisible(bool visible)
    {
        if (passiveWidget != null) passiveWidget.gameObject.SetActive(visible);

        if (passiveIcon != null) passiveIcon.gameObject.SetActive(visible);
        if (passiveNameText != null) passiveNameText.gameObject.SetActive(visible);
        if (paIconImage != null && paIconImage.transform.parent != null)
            paIconImage.transform.parent.gameObject.SetActive(visible);
        if (pmIconImage != null && pmIconImage.transform.parent != null)
            pmIconImage.transform.parent.gameObject.SetActive(visible);
        if (paText != null) paText.gameObject.SetActive(visible);
        if (pmText != null) pmText.gameObject.SetActive(visible);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(visible);

        if (visible)
        {
            OracleHudRuntimeSprites.ApplyCombatHudIfMissing(paIconImage, pmIconImage, combatTurnTimer);
            if (paIconImage != null)
                paIconImage.enabled = paIconImage.sprite != null;
            if (pmIconImage != null)
                pmIconImage.enabled = pmIconImage.sprite != null;

            if (teamALabel != null) teamALabel.gameObject.SetActive(true);
            if (teamAHpFill != null) teamAHpFill.gameObject.SetActive(true);
            if (teamAHpValue != null) teamAHpValue.gameObject.SetActive(true);
            if (teamBLabel != null) teamBLabel.gameObject.SetActive(true);
            if (teamBHpFill != null) teamBHpFill.gameObject.SetActive(true);
            if (teamBHpValue != null) teamBHpValue.gameObject.SetActive(true);
            if (combatTurnTimer != null) combatTurnTimer.gameObject.SetActive(true);
        }
        else
        {
            if (teamALabel != null) teamALabel.gameObject.SetActive(false);
            if (teamAHpFill != null) teamAHpFill.gameObject.SetActive(false);
            if (teamAHpValue != null) teamAHpValue.gameObject.SetActive(false);
            if (teamBLabel != null) teamBLabel.gameObject.SetActive(false);
            if (teamBHpFill != null) teamBHpFill.gameObject.SetActive(false);
            if (teamBHpValue != null) teamBHpValue.gameObject.SetActive(false);
            if (combatTurnTimer != null) combatTurnTimer.gameObject.SetActive(false);
        }
    }
}
