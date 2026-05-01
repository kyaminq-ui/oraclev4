using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

/// <summary>
/// HUD de combat — roadmap 4.2.1
/// Barre haute : HP équipe A | Timer tour (centré) | HP équipe B
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

    GameObject _resourcesPanelRoot;

    TacticalCharacter _subPA, _subPM, _subHP_A, _subHP_B;

    void Awake()
    {
        AutoFindCharacters();
        if (localPlayerCharacter == null) localPlayerCharacter = teamACharacter;

        EnsureEnemyHpWidgetFromPlayerClone();
        EnsurePaPmIconRows();
        DockResourcesPanelBesidePassive();

        if (combatTurnTimer == null)
            combatTurnTimer = GetComponentInChildren<TimerUI>(true);
        RuntimeEnsureCombatTimer();
        OracleHudRuntimeSprites.ApplyCombatHudIfMissing(paIconImage, pmIconImage, combatTurnTimer);
        HideHpBarDecor(teamAHpFill);
        HideHpBarDecor(teamBHpFill);
    }

    /// <summary>Retire le fond et le contour du widget PlayerHPWidget (oracle HUD builder) tout en gardant le fill.</summary>
    static void HideHpBarDecor(Image hpFillImage)
    {
        if (hpFillImage == null) return;

        Transform track = hpFillImage.transform.parent;
        if (track == null) return;

        var trackImg = track.GetComponent<Image>();
        if (trackImg != null)
        {
            var tc = trackImg.color;
            trackImg.color = new Color(tc.r, tc.g, tc.b, 0f);
        }

        Transform barRow = track.parent;
        if (barRow != null)
        {
            Transform borderTf = barRow.Find("HpBorder");
            if (borderTf != null)
            {
                var borderImg = borderTf.GetComponent<Image>();
                if (borderImg != null)
                    borderImg.enabled = false;
            }

            Transform widget = barRow.parent;
            if (widget != null)
            {
                var rootPlate = widget.GetComponent<Image>();
                if (rootPlate != null &&
                    (widget.name.Contains("HPWidget") ||
                     widget.name.IndexOf("PlayerHP", StringComparison.Ordinal) >= 0 ||
                     widget.name.IndexOf("EnemyHP", StringComparison.Ordinal) >= 0))
                {
                    rootPlate.enabled = false;
                }
            }
        }
    }

    void EnsureEnemyHpWidgetFromPlayerClone()
    {
        if (teamBHpFill != null && teamBLabel != null && teamBHpValue != null)
            return;

        var playerRt = transform.Find("PlayerHPWidget") as RectTransform;
        if (playerRt == null)
            return;

        var enemyRt = transform.Find("EnemyHPWidget") as RectTransform;
        if (enemyRt == null)
        {
            enemyRt = Instantiate(playerRt.gameObject, transform).GetComponent<RectTransform>();
            enemyRt.name = "EnemyHPWidget";
        }

        enemyRt.anchorMin = enemyRt.anchorMax = new Vector2(1f, 1f);
        enemyRt.pivot = new Vector2(1f, 1f);
        enemyRt.anchoredPosition = new Vector2(-14f, -14f);
        enemyRt.sizeDelta = playerRt.sizeDelta;

        var rootVlg = enemyRt.GetComponent<VerticalLayoutGroup>();
        if (rootVlg != null)
            rootVlg.childAlignment = TextAnchor.UpperRight;

        if (teamBLabel == null)
            teamBLabel = enemyRt.Find("PlayerLabel")?.GetComponent<TextMeshProUGUI>();
        if (teamBLabel != null)
        {
            teamBLabel.horizontalAlignment = HorizontalAlignmentOptions.Right;
            teamBLabel.verticalAlignment = VerticalAlignmentOptions.Top;
        }

        if (teamBHpFill == null)
            teamBHpFill = enemyRt.Find("HpBarRow/HpTrack/Fill")?.GetComponent<Image>();
        if (teamBHpValue == null)
            teamBHpValue = enemyRt.Find("HpValue")?.GetComponent<TextMeshProUGUI>();
        if (teamBHpValue != null)
        {
            teamBHpValue.horizontalAlignment = HorizontalAlignmentOptions.Right;
            teamBHpValue.verticalAlignment = VerticalAlignmentOptions.Top;
        }

        HideHpBarDecor(teamBHpFill);

        if (teamBHpFill != null)
            teamBHpFill.color = new Color(0.91f, 0.28f, 0.33f, 1f);
    }

    void EnsurePaPmIconRows()
    {
        TryWrapStatRow("PaRow", ref paText, ref paIconImage, 26f);
        TryWrapStatRow("PmRow", ref pmText, ref pmIconImage, 26f);
    }

    /// <summary>
    /// Le texte PA/PM peut être sur un enfant « Value » / « PAValue » / « PMValue » : les lignes et icônes
    /// doivent être rattachées au cluster (<c>PACluster</c>, <c>PMCluster</c>, <c>PAStat</c>…).
    /// </summary>
    static Transform ResolveStatClusterParent(Transform textTransform)
    {
        if (textTransform == null) return null;
        Transform p = textTransform.parent;
        while (p != null)
        {
            string n = p.name;
            if (n == "PAValue" || n == "PMValue" ||
                string.Equals(n, "Value", StringComparison.OrdinalIgnoreCase))
            {
                p = p.parent;
                continue;
            }
            break;
        }
        return p;
    }

    static void TryWrapStatRow(string rowName, ref TextMeshProUGUI textField, ref Image iconRef, float iconSize)
    {
        if (textField == null)
            return;

        Transform clusterParent = ResolveStatClusterParent(textField.transform);
        if (clusterParent == null)
            return;

        Transform row = clusterParent.Find(rowName);
        if (row == null)
        {
            var rowGo = new GameObject(rowName, typeof(RectTransform));
            row = rowGo.transform;
            row.SetParent(clusterParent, false);
            var rt = rowGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;

            var rowLe = rowGo.AddComponent<LayoutElement>();
            rowLe.preferredHeight = Mathf.Max(22f, iconSize + 2f);
            rowLe.flexibleWidth = 1f;

            var h = rowGo.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleLeft;
            h.spacing = 6f;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.padding = new RectOffset(0, 0, 0, 0);

            Transform existingIconTf = clusterParent.Find("Icon");
            Image existingIcon = existingIconTf != null ? existingIconTf.GetComponent<Image>() : null;
            if (existingIcon != null)
            {
                existingIcon.transform.SetParent(row, false);
                var iconRt = existingIcon.rectTransform;
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);
                iconRt.anchoredPosition = Vector2.zero;
                var iconLe = existingIcon.gameObject.GetComponent<LayoutElement>();
                if (iconLe == null) iconLe = existingIcon.gameObject.AddComponent<LayoutElement>();
                iconLe.preferredWidth = iconLe.preferredHeight = iconSize;
                iconRef = existingIcon;
                iconRef.preserveAspect = true;
                iconRef.raycastTarget = false;
            }
            else
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(row, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);
                var iconLe = iconGo.AddComponent<LayoutElement>();
                iconLe.preferredWidth = iconLe.preferredHeight = iconSize;

                iconRef = iconGo.AddComponent<Image>();
                iconRef.preserveAspect = true;
                iconRef.raycastTarget = false;
            }

            textField.transform.SetParent(row, false);
            var tmpLe = textField.gameObject.GetComponent<LayoutElement>();
            if (tmpLe == null) tmpLe = textField.gameObject.AddComponent<LayoutElement>();
            tmpLe.flexibleWidth = 1f;

            string s = textField.text ?? string.Empty;
            int colon = s.IndexOf(':');
            if (colon >= 0 && colon + 1 < s.Length)
                textField.text = s.Substring(colon + 1).Trim();
            else if (s.StartsWith("PA", StringComparison.OrdinalIgnoreCase) ||
                     s.StartsWith("PM", StringComparison.OrdinalIgnoreCase))
                textField.text = "0";
        }
        else if (iconRef == null)
            iconRef = row.Find("Icon")?.GetComponent<Image>();
    }

    /// <summary>
    /// HUD minimaliste : <c>TimerHost</c> peut exister sans <see cref="TimerUI"/> ni référence assignée.
    /// </summary>
    void RuntimeEnsureCombatTimer()
    {
        if (combatTurnTimer != null) return;

        var host = transform.Find("TimerHost") as RectTransform;
        if (host == null) return;

        combatTurnTimer = host.GetComponentInChildren<TimerUI>(true);
        if (combatTurnTimer != null) return;

        combatTurnTimer = RuntimeCreateTimerUnderHost(host);
    }

    static TimerUI RuntimeCreateTimerUnderHost(RectTransform host)
    {
        var timerGO = new GameObject("TimerUI");
        timerGO.transform.SetParent(host, false);
        var timerRt = timerGO.AddComponent<RectTransform>();
        timerRt.anchorMin = Vector2.zero;
        timerRt.anchorMax = Vector2.one;
        timerRt.offsetMin = timerRt.offsetMax = Vector2.zero;
        timerRt.localScale = Vector3.one;

        void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.anchoredPosition = Vector2.zero;
        }

        var iconGO = new GameObject("TimerIcon");
        iconGO.transform.SetParent(timerGO.transform, false);
        var iconRt = iconGO.AddComponent<RectTransform>();
        Stretch(iconRt);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(timerGO.transform, false);
        var fillRt = fillGO.AddComponent<RectTransform>();
        Stretch(fillRt);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.fillAmount = 1f;
        fillImg.color = new Color(0.788f, 0.659f, 0.298f, 0.45f);

        var timeGO = new GameObject("TimeText");
        timeGO.transform.SetParent(timerGO.transform, false);
        var timeRt = timeGO.AddComponent<RectTransform>();
        Stretch(timeRt);
        var timeTmp = timeGO.AddComponent<TextMeshProUGUI>();
        timeTmp.fontSize = 20f;
        timeTmp.fontStyle = FontStyles.Bold;
        timeTmp.alignment = TextAlignmentOptions.Center;
        timeTmp.color = Color.white;
        timeTmp.raycastTarget = false;
        timeTmp.text = "0";

        var timerUI = timerGO.AddComponent<TimerUI>();
        timerUI.timerIconImage = iconImg;
        timerUI.fillImage = fillImg;
        timerUI.timeText = timeTmp;
        timerUI.showRadialTimeFill = false;

        if (iconImg != null)
            iconImg.preserveAspect = true;
        if (fillImg != null)
        {
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = false;
            fillImg.enabled = false;
            fillImg.color = Color.clear;
        }
        if (timeTmp != null)
            timeTmp.enableWordWrapping = false;

        return timerUI;
    }

    /// <summary>
    /// Après tous les Awake : centre le TimerHost, réapplique les sprites PA/PM (Resources) et réactive les clusters.
    /// </summary>
    void EnsureTimerHostTopCenterAndHudSprites()
    {
        EnsurePaPmIconRows();

        var host = transform.Find("TimerHost") as RectTransform;
        if (host != null)
        {
            host.anchorMin = host.anchorMax = new Vector2(0.5f, 1f);
            host.pivot = new Vector2(0.5f, 1f);
            host.anchoredPosition = new Vector2(0f, -10f);
        }

        if (combatTurnTimer == null)
            combatTurnTimer = GetComponentInChildren<TimerUI>(true);
        if (combatTurnTimer != null)
        {
            combatTurnTimer.timerHostDock = TimerUI.TimerHostDock.TopCenter;
            combatTurnTimer.ApplyHostRectLayout();
        }

        OracleHudRuntimeSprites.ApplyCombatHudIfMissing(paIconImage, pmIconImage, combatTurnTimer);
        EnsureStatIconHierarchyActive();
    }

    void EnsureStatIconHierarchyActive()
    {
        void Up(Image img)
        {
            if (img == null) return;
            for (var t = img.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                    t.gameObject.SetActive(true);
                if (t == transform)
                    break;
            }
        }
        Up(paIconImage);
        Up(pmIconImage);
    }

    void DockResourcesPanelBesidePassive()
    {
        if (paText == null)
            return;

        Transform panelTf = paText.transform.parent;
        while (panelTf != null && panelTf.parent != transform && panelTf.parent != null)
            panelTf = panelTf.parent;

        if (panelTf == null || passiveWidget == null)
            return;

        if (!string.Equals(panelTf.name, "ResourcesPanel", StringComparison.Ordinal))
        {
            var found = transform.GetComponentsInChildren<Transform>(true);
            foreach (var t in found)
            {
                if (t != null && string.Equals(t.name, "ResourcesPanel", StringComparison.Ordinal))
                {
                    panelTf = t;
                    break;
                }
            }
        }

        _resourcesPanelRoot = panelTf.gameObject;

        var panelRt = panelTf as RectTransform;
        var pwRt = passiveWidget.transform as RectTransform;
        if (panelRt == null || pwRt == null)
            return;

        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        float gap = 10f;
        panelRt.anchoredPosition = new Vector2(
            pwRt.anchoredPosition.x + pwRt.sizeDelta.x + gap,
            pwRt.anchoredPosition.y + 2f);
    }

    void Start()
    {
        EnsureTimerHostTopCenterAndHudSprites();
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
            if (_subHP_A == null && teamACharacter != null && teamACharacter.stats != null)
            {
                teamACharacter.OnHPChanged += OnHpA;
                _subHP_A = teamACharacter;
                if (teamALabel != null) teamALabel.text = teamACharacter.name;
            }
            if (_subHP_B == null && teamBCharacter != null && teamBCharacter.stats != null)
            {
                teamBCharacter.OnHPChanged += OnHpB;
                _subHP_B = teamBCharacter;
                if (teamBLabel != null) teamBLabel.text = teamBCharacter.name;
            }

            if (_subHP_A != null)
                RefreshHpBar(teamACharacter, teamAHpFill, teamAHpValue);
            if (_subHP_B != null)
                RefreshHpBar(teamBCharacter, teamBHpFill, teamBHpValue);

            if (_subHP_A != null && _subHP_B != null)
                yield break;
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
        var ci = CombatInitializer.Instance;
        if (ci != null)
        {
            if (teamACharacter == null && ci.player != null)
                teamACharacter = ci.player;
            if (teamBCharacter == null && ci.opponent != null)
                teamBCharacter = ci.opponent;
            if (localPlayerCharacter == null && ci.player != null)
                localPlayerCharacter = ci.player;
        }

        if (teamACharacter != null && teamBCharacter != null)
            return;

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

    void OnHpA(int cur, int max) => RefreshHpBar(cur, max, teamAHpFill, teamAHpValue);
    void OnHpB(int cur, int max) => RefreshHpBar(cur, max, teamBHpFill, teamBHpValue);

    static void RefreshHpBar(int curHp, int maxHp, Image fill, TextMeshProUGUI valueText)
    {
        if (fill != null)
        {
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            float t = maxHp > 0 ? Mathf.Clamp01((float)curHp / maxHp) : 0f;
            fill.fillAmount = t;
        }
        if (valueText != null)
            valueText.text = $"{curHp} / {maxHp}";
    }

    static void RefreshHpBar(TacticalCharacter ch, Image fill, TextMeshProUGUI valueText)
    {
        if (ch?.stats == null) return;
        RefreshHpBar(ch.CurrentHP, ch.stats.maxHP, fill, valueText);
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
        OracleUIImportantFont.Apply(teamBLabel);
        OracleUIImportantFont.Apply(teamAHpValue);
        OracleUIImportantFont.Apply(teamBHpValue);
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

        if (_resourcesPanelRoot != null)
            _resourcesPanelRoot.SetActive(visible);
        else if (paText != null && paText.transform.root == transform.root)
        {
            Transform panel = paText.transform.parent;
            if (panel != null) panel.gameObject.SetActive(visible);
        }

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
