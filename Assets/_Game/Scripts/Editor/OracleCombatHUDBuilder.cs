#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Roadmap 4.2.1 — Layout écran de combat (un clic).
/// Menu : Oracle > Build Combat HUD (4.2.1)
/// Réorganise DeckUI + TimerUI existants dans la structure officielle.
/// </summary>
public static class OracleCombatHUDBuilder
{
    private static readonly Color Accent = new Color(0.788f, 0.659f, 0.298f, 1f);
    private static readonly Color DarkBg  = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    private static readonly Color BarBg   = new Color(0.18f, 0.18f, 0.22f, 1f);
    private static readonly Color HpOk    = new Color(0.25f, 0.75f, 0.35f, 1f);

    const string MENU = "Oracle/Build Combat HUD (4.2.1)";

    [MenuItem(MENU)]
    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Oracle — HUD", "Aucun Canvas dans la scène. Lance d'abord Setup UI Combat.", "OK");
            return;
        }

        // Détruire TOUS les CombatHUD existants (évite l'accumulation lors de rebuilds successifs)
        bool foundOld = false;
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvas.transform.GetChild(i);
            if (child.name == "CombatHUD") foundOld = true;
        }
        if (foundOld)
        {
            if (!EditorUtility.DisplayDialog("OracleCombatHUD",
                    "CombatHUD existe déjà. Reconstruire ?", "Oui", "Non"))
                return;
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = canvas.transform.GetChild(i);
                if (child.name == "CombatHUD")
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        var rootGO = new GameObject("CombatHUD");
        Undo.RegisterCreatedObjectUndo(rootGO, "CombatHUD");
        rootGO.transform.SetParent(canvas.transform, false);
        StretchFull(rootGO.AddComponent<RectTransform>());
        var hud = rootGO.AddComponent<CombatHUD>();
        rootGO.transform.SetAsFirstSibling();

        // ── HP PLAYER : widget flottant haut-gauche ────────────────────────
        var teamABuilt = BuildPlayerHPWidget(rootGO.transform);

        // ── TIMER : hôte flottant haut-centre (entre les barres HP) ───────
        var timerHost = new GameObject("TimerHost").AddComponent<RectTransform>();
        Undo.RegisterCreatedObjectUndo(timerHost.gameObject, "TimerHost");
        timerHost.SetParent(rootGO.transform, false);
        timerHost.anchorMin        = new Vector2(0.5f, 1f);
        timerHost.anchorMax        = new Vector2(0.5f, 1f);
        timerHost.pivot            = new Vector2(0.5f, 1f);
        timerHost.anchoredPosition = new Vector2(0f, -10f);
        timerHost.sizeDelta        = new Vector2(56f, 56f);

        // ── FAN DECK AREA : bande basse (léche l’eventail sans recouvrir tout l’écran) ───
        var fanAreaGO = new GameObject("FanDeckArea");
        Undo.RegisterCreatedObjectUndo(fanAreaGO, "FanDeckArea");
        fanAreaGO.transform.SetParent(rootGO.transform, false);
        var fanAreaRT = fanAreaGO.AddComponent<RectTransform>();
        fanAreaRT.anchorMin        = new Vector2(0f, 0f);
        fanAreaRT.anchorMax        = new Vector2(1f, 0f);
        fanAreaRT.pivot            = new Vector2(0.5f, 0f);
        fanAreaRT.anchoredPosition = Vector2.zero;
        const float deckStripHeightPx = 480f;
        fanAreaRT.offsetMin            = Vector2.zero;
        fanAreaRT.offsetMax            = new Vector2(0f, deckStripHeightPx);

        // DeckUI : re-parent si existe, sinon créer avec fan mode
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck != null)
        {
            Undo.SetTransformParent(deck.transform, fanAreaRT, "Move DeckUI");
            StretchFull(deck.GetComponent<RectTransform>());
        }
        else
        {
            deck = CreateDeckUI(fanAreaRT);
        }
        deck.fanMode      = true;
        deck.fanSpread    = 500f;
        deck.fanCardW     = 156f;
        deck.fanCardH     = 240f;
        deck.fanAngleMax  = 10f;    // courbe réduite
        deck.fanArcHeight = 12f;    // arc réduit
        deck.fanBaseY     = -55f;
        deck.fanRaise     = 22f;


        deck.ApplyFanLayout();

        // ── WIDGET PASSIF : bas-gauche, sans fond, flottant ────────────────
        var passiveWidget = BuildPassiveWidget(rootGO.transform);
        hud.passiveWidget = passiveWidget;

        // ── PA / PM : flottant bas-gauche, à droite du passif ──────────────
        var resBlock = BuildFloatingResources(rootGO.transform);

        // ── FIN DE TOUR : flottant bas-droit ───────────────────────────────
        var endBtn = BuildFloatingEndTurn(rootGO.transform);

        // ── CÂBLAGE ────────────────────────────────────────────────────────
        hud.teamALabel   = teamABuilt.label;
        hud.teamAHpFill  = teamABuilt.fill;
        hud.teamAHpValue = teamABuilt.value;

        hud.paText        = resBlock.pa;
        hud.pmText        = resBlock.pm;
        hud.paIconImage   = resBlock.paIcon;
        hud.pmIconImage   = resBlock.pmIcon;
        hud.endTurnButton = endBtn;

        // TimerUI : TimerHost ; sinon premier Timer hors PassiveSelectionScreen ; sinon création
        PassiveSelectionScreen passiveScr = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        Transform passiveRoot               = passiveScr != null ? passiveScr.transform : null;
        TimerUI combatTimer = timerHost.GetComponentInChildren<TimerUI>(true);
        if (combatTimer != null)
        {
            StretchFull(combatTimer.GetComponent<RectTransform>());
        }
        else
        {
            foreach (var t in Object.FindObjectsOfType<TimerUI>(true))
            {
                if (t == null) continue;
                if (passiveRoot != null && t.transform.IsChildOf(passiveRoot)) continue;
                Undo.SetTransformParent(t.transform, timerHost, "Move TimerUI");
                StretchFull(t.GetComponent<RectTransform>());
                combatTimer = t;
                break;
            }
        }
        if (combatTimer == null)
        {
            combatTimer = CreateTimerUIUnderHost(timerHost);
            Undo.RegisterCreatedObjectUndo(combatTimer.gameObject, "TimerUI");
        }
        hud.combatTurnTimer = combatTimer;

        // ── INFOBULLE HP ────────────────────────────────────────────────────
        BuildHpTooltip(rootGO.transform);

        ReorderCombatHudDrawing(rootGO.transform);

        EditorUtility.SetDirty(hud);
        Selection.activeGameObject = rootGO;
        Debug.Log("[OracleCombatHUDBuilder] HUD généré — cartes en éventail, widget passif bas-gauche.");
        EditorUtility.DisplayDialog("Oracle — HUD",
            "CombatHUD créé.\n\n" +
            "• HP Player (gauche) | Timer (haut centre) | HP Ennemi (droite) + PA/PM + Fin de tour.\n" +
            "• Barre de vie pixel-art (UI_BARRE_DE_VIE) appliquée sur les deux barres HP.\n" +
            "• 8 cartes sorts en éventail (bas centre, 75 % visibles — survol lève la carte).\n" +
            "• Widget passif bas-gauche, survol = infobulle.\n" +
            "• Lance Oracle/Build Passive Selection Screen pour l'écran de passifs.\n" +
            "• Passe les personnages dans l'Inspector si auto-detect insuffisant.", "OK");
    }

    // ── Widget passif bas-gauche ─────────────────────────────────────────────

    static PassiveHUDWidget BuildPassiveWidget(Transform parent)
    {
        Sprite rim = OracleUIMajTextureSetup.TryLoadSprite("UI_ICONE_PASSIF");
        float iconSize = rim != null
            ? Mathf.Clamp(Mathf.Max(rim.rect.width, rim.rect.height) * 2f, 72f, 100f)
            : 80f;

        var widgetGO = new GameObject("PassiveWidget");
        Undo.RegisterCreatedObjectUndo(widgetGO, "PassiveWidget");
        widgetGO.transform.SetParent(parent, false);
        var wRT = widgetGO.AddComponent<RectTransform>();
        wRT.anchorMin        = new Vector2(0f, 0f);
        wRT.anchorMax        = new Vector2(0f, 0f);
        wRT.pivot            = new Vector2(0f, 0f);
        wRT.anchoredPosition = new Vector2(16f, 16f);
        wRT.sizeDelta        = new Vector2(iconSize + 8f, iconSize + 28f);

        var widget = widgetGO.AddComponent<PassiveHUDWidget>();
        var rayGO  = widgetGO.AddComponent<Image>();
        rayGO.color         = new Color(0f, 0f, 0f, 0.001f); // invisible mais raycastable
        rayGO.raycastTarget = true;

        var vl = widgetGO.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(0, 0, 0, 4);
        vl.spacing = 2f;
        vl.childAlignment = TextAnchor.LowerCenter;
        vl.childControlWidth  = true;
        vl.childControlHeight = false;
        vl.childForceExpandWidth = true;

        // Label "Passif"
        var labelGO = new GameObject("PassifLabel");
        labelGO.transform.SetParent(widgetGO.transform, false);
        labelGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
        labelGO.AddComponent<LayoutElement>().preferredHeight = 18f;
        var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.text         = "Passif";
        labelTmp.fontSize     = 11f;
        labelTmp.fontStyle    = FontStyles.Bold;
        labelTmp.color        = Accent;
        labelTmp.alignment    = TextAlignmentOptions.Center;
        labelTmp.raycastTarget = false;

        // Slot icône
        var slotGO = new GameObject("PassiveIconSlot");
        slotGO.transform.SetParent(widgetGO.transform, false);
        var sRT = slotGO.AddComponent<RectTransform>();
        sRT.sizeDelta = new Vector2(iconSize, iconSize);
        slotGO.AddComponent<LayoutElement>().preferredHeight = iconSize;

        var plate = new GameObject("Plate");
        plate.transform.SetParent(slotGO.transform, false);
        StretchFull(plate.AddComponent<RectTransform>());
        var plateImg = plate.AddComponent<Image>();
        plateImg.color         = new Color(0f, 0f, 0f, 0f); // transparent — pas de fond parasite
        plateImg.raycastTarget = false;

        // Frame d'abord, puis l'icône PAR DESSUS
        if (rim != null)
        {
            var frameGO = new GameObject("PassiveFrame");
            frameGO.transform.SetParent(slotGO.transform, false);
            StretchFull(frameGO.AddComponent<RectTransform>());
            var fImg = frameGO.AddComponent<Image>();
            fImg.sprite         = rim;
            fImg.preserveAspect = true;
            fImg.raycastTarget  = false;
        }

        var iconHostGO = new GameObject("IconHost");
        iconHostGO.transform.SetParent(slotGO.transform, false);
        var ihRT = iconHostGO.AddComponent<RectTransform>();
        ihRT.anchorMin = new Vector2(0.14f, 0.14f);
        ihRT.anchorMax = new Vector2(0.86f, 0.86f);
        ihRT.offsetMin = ihRT.offsetMax = Vector2.zero;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(iconHostGO.transform, false);
        StretchFull(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color          = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        iconImg.enabled        = false;
        widget.iconImage = iconImg;

        // Tooltip (masqué, hors du VLG du widget, s'affiche au survol)
        var tooltipGO = new GameObject("PassiveTooltip");
        Undo.RegisterCreatedObjectUndo(tooltipGO, "PassiveTooltip");
        tooltipGO.transform.SetParent(widgetGO.transform, false);
        // RectTransform AVANT LayoutElement — LayoutElement a [RequireComponent(RectTransform)]
        // et AddComponent retourne null si un RT existe déjà.
        var tRT = tooltipGO.AddComponent<RectTransform>();
        tRT.anchorMin        = new Vector2(0f, 1f);
        tRT.anchorMax        = new Vector2(0f, 1f);
        tRT.pivot            = new Vector2(0f, 0f);
        tRT.anchoredPosition = new Vector2(0f, 6f);
        tRT.sizeDelta        = new Vector2(210f, 86f);
        var tLE = tooltipGO.AddComponent<LayoutElement>();
        tLE.ignoreLayout = true;

        var cv = tooltipGO.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 200;

        var tipBg = tooltipGO.AddComponent<Image>();
        tipBg.color = new Color(0.06f, 0.06f, 0.10f, 0.96f);

        var tipVl = tooltipGO.AddComponent<VerticalLayoutGroup>();
        tipVl.padding               = new RectOffset(8, 8, 6, 6);
        tipVl.spacing               = 4f;
        tipVl.childControlWidth     = true;
        tipVl.childControlHeight    = false;
        tipVl.childForceExpandWidth = true;

        var tipName = AddTooltipText(tooltipGO.transform, "TipName", "", 13f, FontStyles.Bold, Accent);
        var tipDesc = AddTooltipText(tooltipGO.transform, "TipDesc", "", 11f, FontStyles.Normal, new Color(0.80f, 0.80f, 0.84f));

        widget.tooltipPanel = tooltipGO;
        widget.tooltipName  = tipName;
        widget.tooltipDesc  = tipDesc;
        tooltipGO.SetActive(false);

        return widget;
    }

    static TextMeshProUGUI AddTooltipText(Transform parent, string name, string text,
        float size, FontStyles style, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, size * 1.6f + 4f);
        go.AddComponent<LayoutElement>().preferredHeight = size * 1.6f + 4f;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.fontStyle     = style;
        tmp.color         = col;
        tmp.alignment     = TextAlignmentOptions.TopLeft;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        return tmp;
    }

    static DeckUI CreateDeckUI(RectTransform parent)
    {
        var deckGO = new GameObject("DeckUI");
        Undo.RegisterCreatedObjectUndo(deckGO, "DeckUI");
        deckGO.transform.SetParent(parent, false);
        StretchFull(deckGO.AddComponent<RectTransform>());
        var dle = deckGO.AddComponent<LayoutElement>();
        dle.flexibleWidth = 1f;
        dle.minHeight     = 80f;

        var hlg = deckGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding               = new RectOffset(6, 6, 6, 6);
        hlg.spacing               = 8f;
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = false;
        hlg.childControlHeight    = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var deck = deckGO.AddComponent<DeckUI>();

        for (int i = 0; i < 6; i++)
            deck.slots.Add(BuildSpellSlot(deckGO.transform, i));

        EditorUtility.SetDirty(deck);
        return deck;
    }

    static readonly string[] SlotHotkeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
    const float SlotW = 156f;
    const float SlotH = 240f;

    static SpellSlotUI BuildSpellSlot(Transform parent, int index)
    {
        var slotGO = new GameObject($"SpellSlot_{index + 1}");
        Undo.RegisterCreatedObjectUndo(slotGO, "SpellSlot");
        slotGO.transform.SetParent(parent, false);
        slotGO.AddComponent<RectTransform>().sizeDelta = new Vector2(SlotW, SlotH);

        var le = slotGO.AddComponent<LayoutElement>();
        le.preferredWidth  = SlotW;
        le.preferredHeight = SlotH;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        var rootHit = slotGO.AddComponent<Image>();
        rootHit.color         = new Color(0f, 0f, 0f, 0.001f);
        rootHit.raycastTarget = true;

        var slot = slotGO.AddComponent<SpellSlotUI>();

        AddChildImage(slotGO.transform, "Backing", new Color(0f, 0f, 0f, 0f));

        var iconArea = AddPanel(slotGO.transform, "IconArea", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));

        var iconGO  = new GameObject("Icon");
        iconGO.transform.SetParent(iconArea.transform, false);
        StretchFull(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color          = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;

        var dimGO  = new GameObject("DimOverlay");
        dimGO.transform.SetParent(iconArea.transform, false);
        StretchFull(dimGO.AddComponent<RectTransform>());
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color         = new Color(0f, 0f, 0f, 0.55f);
        dimImg.enabled       = false;
        dimImg.raycastTarget = false;

        var paGo = AddTmp(slotGO.transform, "PACost", 14f, FontStyles.Bold,
            TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 22f));

        var hkGo = AddTmp(slotGO.transform, "Hotkey", 12f, FontStyles.Bold,
            TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(32f, 22f));
        hkGo.GetComponent<TextMeshProUGUI>().text =
            index < SlotHotkeys.Length ? SlotHotkeys[index] : $"{index + 1}";

        var selGO  = new GameObject("SelectionBorder");
        selGO.transform.SetParent(slotGO.transform, false);
        StretchFull(selGO.AddComponent<RectTransform>());
        var selImg = selGO.AddComponent<Image>();
        selImg.color         = Color.clear;
        selImg.enabled       = false;
        selImg.raycastTarget = false;

        slot.iconImage       = iconImg;
        slot.cardFrameImage  = null;
        slot.dimOverlay      = dimImg;
        slot.paCostText     = paGo.GetComponent<TextMeshProUGUI>();
        slot.hotkeyText     = hkGo.GetComponent<TextMeshProUGUI>();
        slot.selectionBorder = selImg;

        return slot;
    }

    static RectTransform AddChildImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var img = go.AddComponent<Image>();
        img.color         = col;
        img.raycastTarget = false;
        return rt;
    }

    static RectTransform AddPanel(Transform parent, string name, Vector2 ancMin, Vector2 ancMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static GameObject AddTmp(Transform parent, string name, float size, FontStyles style,
        TextAlignmentOptions align, Vector2 ancMin, Vector2 ancMax, Vector2 pos, Vector2 delta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = ancMin;
        rt.anchorMax        = ancMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = delta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = "";
        tmp.fontSize      = size;
        tmp.fontStyle     = style;
        tmp.alignment     = align;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        return go;
    }

    struct HpBuilt
    {
        public RectTransform root;
        public TextMeshProUGUI label, value;
        public Image fill;
    }

    // ── HP Player : widget flottant haut-gauche, bordure dorée en pur code ──
    static HpBuilt BuildPlayerHPWidget(Transform parent)
    {
        var go = new GameObject("PlayerHPWidget");
        Undo.RegisterCreatedObjectUndo(go, "PlayerHPWidget");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(14f, -14f);
        rt.sizeDelta        = new Vector2(240f, 66f);

        var vl = go.AddComponent<VerticalLayoutGroup>();
        vl.padding      = new RectOffset(8, 8, 6, 6);
        vl.spacing      = 4f;
        vl.childAlignment         = TextAnchor.UpperLeft;
        vl.childControlWidth      = true;
        vl.childControlHeight     = false;
        vl.childForceExpandWidth  = true;

        // Fond semi-transparent
        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.08f, 0.78f);
        bgImg.raycastTarget = false;

        // Nom du joueur
        var labelGO = new GameObject("PlayerLabel");
        labelGO.transform.SetParent(go.transform, false);
        labelGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 20f);
        labelGO.AddComponent<LayoutElement>().preferredHeight = 20f;
        var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.text      = "Player";
        labelTmp.fontSize  = 14f;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color     = Accent;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.raycastTarget = false;
        OracleEditorAsepriteFont.AssignIfAvailable(labelTmp);

        // Ligne barre HP
        const float barH = 22f;
        var barRowGO = new GameObject("HpBarRow");
        barRowGO.transform.SetParent(go.transform, false);
        var barRowRT = barRowGO.AddComponent<RectTransform>();
        barRowRT.sizeDelta = new Vector2(0f, barH);
        barRowGO.AddComponent<LayoutElement>().preferredHeight = barH;

        // Bordure dorée (image pleine en fond, légèrement plus grande via offset)
        var borderGO = new GameObject("HpBorder");
        borderGO.transform.SetParent(barRowGO.transform, false);
        var borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero; borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-2f, -2f); borderRT.offsetMax = new Vector2(2f, 2f);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = Accent;
        borderImg.raycastTarget = false;

        // Fond de piste
        var trackGO = new GameObject("HpTrack");
        trackGO.transform.SetParent(barRowGO.transform, false);
        StretchFull(trackGO.AddComponent<RectTransform>());
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = BarBg;
        trackImg.raycastTarget = false;

        // Remplissage HP
        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(trackGO.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0.02f, 0.1f);
        fillRT.anchorMax = new Vector2(0.98f, 0.9f);
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        var fill = fillGO.AddComponent<Image>();
        fill.color      = HpOk;
        fill.type       = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        // Texte valeur HP
        var valGO = new GameObject("HpValue");
        valGO.transform.SetParent(go.transform, false);
        valGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
        valGO.AddComponent<LayoutElement>().preferredHeight = 16f;
        var val = valGO.AddComponent<TextMeshProUGUI>();
        val.text      = "— / —";
        val.fontSize  = 12f;
        val.color     = Color.white;
        val.alignment = TextAlignmentOptions.Left;
        val.raycastTarget = false;

        return new HpBuilt { root = rt, label = labelTmp, fill = fill, value = val };
    }

    // ── PA / PM flottant bas-gauche (à droite du widget passif) ─────────────
    static ResW BuildFloatingResources(Transform parent)
    {
        var go = new GameObject("ResourcesPanel");
        Undo.RegisterCreatedObjectUndo(go, "ResourcesPanel");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(0f, 0f);
        rt.pivot            = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(120f, 20f);
        rt.sizeDelta        = new Vector2(110f, 48f);

        var hl = go.AddComponent<HorizontalLayoutGroup>();
        hl.spacing               = 8f;
        hl.childAlignment        = TextAnchor.MiddleCenter;
        hl.childControlWidth     = false;
        hl.childControlHeight    = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;

        var paW = BuildHudStatIcon(go.transform, "PAStat", "pa_icon_hud", "mana_icon_hud");
        var pmW = BuildHudStatIcon(go.transform, "PMStat", "pm_icon_hud", "mouvement_icon_hud");

        return new ResW
        {
            pa     = paW.txt,
            pm     = pmW.txt,
            paIcon = paW.icon,
            pmIcon = pmW.icon,
            resourcesPanelRoot = go.transform,
        };
    }

    static (Image icon, TextMeshProUGUI txt) BuildHudStatIcon(Transform parent, string name, string primarySpriteKey, string legacySpriteKey)
    {
        var block = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(block, name);
        block.transform.SetParent(parent, false);
        var brt = block.AddComponent<RectTransform>();
        brt.sizeDelta = new Vector2(44f, 44f);
        var le = block.AddComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = 44f;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(block.transform, false);
        StretchFull(iconGO.AddComponent<RectTransform>());
        var img = iconGO.AddComponent<Image>();
        var sp = OracleUIMajTextureSetup.TryLoadSpritePrimaryOrLegacy(primarySpriteKey, legacySpriteKey);
        if (sp != null) img.sprite = sp;
        img.preserveAspect = true;
        img.raycastTarget = false;

        var valGO = new GameObject("Value");
        valGO.transform.SetParent(block.transform, false);
        StretchFull(valGO.AddComponent<RectTransform>());
        var tmp = valGO.AddComponent<TextMeshProUGUI>();
        tmp.text              = "0";
        tmp.fontSize          = 17f;
        tmp.fontStyle         = FontStyles.Bold;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = Color.white;
        tmp.raycastTarget     = false;
        return (img, tmp);
    }

    public static TimerUI CreateTimerUIUnderHost(RectTransform host)
    {
        var timerGO = new GameObject("TimerUI");
        timerGO.transform.SetParent(host, false);
        StretchFull(timerGO.AddComponent<RectTransform>());
        var timerUI = timerGO.AddComponent<TimerUI>();

        var iconGO = new GameObject("TimerIcon");
        iconGO.transform.SetParent(timerGO.transform, false);
        StretchFull(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        var tSp = OracleUIMajTextureSetup.TryLoadSprite("timer_icon_hud");
        if (tSp != null) iconImg.sprite = tSp;
        iconImg.preserveAspect = true;
        timerUI.timerIconImage = iconImg;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(timerGO.transform, false);
        StretchFull(fillGO.AddComponent<RectTransform>());
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Radial360;
        fillImg.fillOrigin = (int)Image.Origin360.Top;
        fillImg.fillClockwise = false;
        fillImg.fillAmount = 1f;
        fillImg.color      = new Color(0.788f, 0.659f, 0.298f, 0.45f);
        timerUI.fillImage = fillImg;

        var timeGO = new GameObject("TimeText");
        timeGO.transform.SetParent(timerGO.transform, false);
        StretchFull(timeGO.AddComponent<RectTransform>());
        var timeTmp = timeGO.AddComponent<TextMeshProUGUI>();
        timeTmp.fontSize      = 20f;
        timeTmp.fontStyle     = FontStyles.Bold;
        timeTmp.alignment     = TextAlignmentOptions.Center;
        timeTmp.color         = Color.white;
        timeTmp.raycastTarget = false;
        timerUI.timeText = timeTmp;
        timerUI.showRadialTimeFill = false;
        if (fillImg != null)
        {
            fillImg.enabled = false;
            fillImg.color = Color.clear;
        }
        OracleEditorAsepriteFont.AssignIfAvailable(timeTmp);

        EditorUtility.SetDirty(timerUI);
        return timerUI;
    }

    // ── Fin de tour flottant bas-droit ───────────────────────────────────────
    static Button BuildFloatingEndTurn(Transform parent)
    {
        var wrapGO = new GameObject("EndTurnPanel");
        Undo.RegisterCreatedObjectUndo(wrapGO, "EndTurnPanel");
        wrapGO.transform.SetParent(parent, false);
        var wRT = wrapGO.AddComponent<RectTransform>();
        wRT.anchorMin        = new Vector2(1f, 0f);
        wRT.anchorMax        = new Vector2(1f, 0f);
        wRT.pivot            = new Vector2(1f, 0f);
        wRT.anchoredPosition = new Vector2(-16f, 16f);
        wRT.sizeDelta        = new Vector2(140f, 52f);

        var img = wrapGO.AddComponent<Image>();
        img.color = Accent;
        var btn = wrapGO.AddComponent<Button>();
        btn.targetGraphic = img;

        var lblGO = TextBlock(wRT, "Label", "Fin de tour", 16f, FontStyles.Bold,
            new Color(0.08f, 0.05f, 0.01f));
        StretchFull(lblGO.GetComponent<RectTransform>());
        lblGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        return btn;
    }

    struct ResW
    {
        public TextMeshProUGUI pa, pm;
        public Image            paIcon, pmIcon;
        public Transform        resourcesPanelRoot;
    }

    static ResW ResourcesBlock(RectTransform parent)
    {
        var block = Panel(parent, "ResourcesBlock",
            Vector2.zero, new Vector2(100f, 80f), Vector2.zero, Vector2.one);
        block.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;
        var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childAlignment = TextAnchor.MiddleCenter;

        var paGO = TextBlock(block, "PA", "PA: —", 16f, FontStyles.Bold, Color.white);
        var pmGO = TextBlock(block, "PM", "PM: —", 16f, FontStyles.Bold, Color.white);
        return new ResW
        {
            pa                 = paGO.GetComponent<TextMeshProUGUI>(),
            pm                 = pmGO.GetComponent<TextMeshProUGUI>(),
            paIcon             = null,
            pmIcon             = null,
            resourcesPanelRoot = block,
        };
    }

    static Button EndTurnButton(RectTransform parent)
    {
        var wrap = Panel(parent, "EndTurnWrap",
            Vector2.zero, new Vector2(130f, 56f), Vector2.zero, Vector2.one);
        wrap.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;

        var btnGO = new GameObject("EndTurnButton");
        btnGO.transform.SetParent(wrap, false);
        var brt = btnGO.AddComponent<RectTransform>();
        StretchFull(brt);
        var img = btnGO.AddComponent<Image>();
        img.color = Accent;
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = TextBlock(brt, "Label", "Fin de tour", 16f, FontStyles.Bold, new Color(0.1f, 0.07f, 0.02f));
        StretchFull(txtGO.GetComponent<RectTransform>());
        txtGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        return btn;
    }

    static RectTransform Panel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        return rt;
    }

    static RectTransform Panel(RectTransform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        return rt;
    }

    static GameObject TextBlock(RectTransform parent, string name, string text,
        float size, FontStyles style, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(100f, 24f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = text;
        tmp.fontSize   = size;
        tmp.fontStyle  = style;
        tmp.color      = col;
        tmp.alignment  = TextAlignmentOptions.Center;
        OracleEditorAsepriteFont.AssignIfAvailable(tmp);
        return go;
    }

    /// <summary>
    /// Ordre de sibling : jeu de cartes d’abord, puis overlays (timer au-dessus de la FanDeckArea).
    /// </summary>
    static void ReorderCombatHudDrawing(Transform hudRoot)
    {
        int i = 0;
        void Put(Transform tr)
        {
            if (tr == null) return;
            tr.SetSiblingIndex(i++);
        }

        Transform N(string path) => hudRoot.Find(path);
        Put(N("PlayerHPWidget"));
        Put(N("FanDeckArea"));
        Put(N("PassiveWidget"));
        Put(N("ResourcesPanel"));
        Put(N("TimerHost"));
        Put(N("EndTurnPanel"));
        Put(N("HpTooltip"));
    }

    // ── Infobulle HP au survol personnage ────────────────────────────────────
    static void BuildHpTooltip(Transform parent)
    {
        var go = new GameObject("HpTooltip");
        Undo.RegisterCreatedObjectUndo(go, "HpTooltip");
        go.transform.SetParent(parent, false);
        StretchFull(go.AddComponent<RectTransform>());

        var hpRootCg = go.AddComponent<CanvasGroup>();
        hpRootCg.alpha               = 1f;
        hpRootCg.blocksRaycasts      = false;
        hpRootCg.interactable        = false;

        var widget = go.AddComponent<HpTooltipWidget>();

        // Caméra principale
        var camGO = GameObject.FindGameObjectWithTag("MainCamera");
        if (camGO != null) widget.cam = camGO.GetComponent<Camera>();
        if (widget.cam == null) widget.cam = Object.FindObjectOfType<Camera>(true);

        // Panel tooltip (Canvas ajoute déjà RectTransform — un second AddComponent<RectTransform> renvoie null)
        var panelGO = new GameObject("TooltipPanel");
        panelGO.transform.SetParent(go.transform, false);
        var cv = panelGO.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 300;
        var panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(0f, 0f);
        panelRT.pivot            = new Vector2(0f, 0f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(140f, 52f);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.04f, 0.04f, 0.08f, 0.92f);

        var vl = panelGO.AddComponent<VerticalLayoutGroup>();
        vl.padding               = new RectOffset(8, 8, 5, 5);
        vl.spacing               = 2f;
        vl.childControlWidth     = true;
        vl.childControlHeight    = false;
        vl.childForceExpandWidth = true;

        // Ligne nom
        var nameGO = new GameObject("TooltipName");
        nameGO.transform.SetParent(panelGO.transform, false);
        nameGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
        nameGO.AddComponent<LayoutElement>().preferredHeight = 18f;
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = "";
        nameTmp.fontSize  = 13f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color     = Accent;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.raycastTarget = false;

        // Ligne HP
        var hpGO = new GameObject("TooltipHP");
        hpGO.transform.SetParent(panelGO.transform, false);
        hpGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);
        hpGO.AddComponent<LayoutElement>().preferredHeight = 16f;
        var hpTmp = hpGO.AddComponent<TextMeshProUGUI>();
        hpTmp.text      = "";
        hpTmp.fontSize  = 12f;
        hpTmp.color     = Color.white;
        hpTmp.alignment = TextAlignmentOptions.Left;
        hpTmp.raycastTarget = false;

        widget.panel    = panelRT;
        widget.nameText = nameTmp;
        widget.hpText   = hpTmp;

        EditorUtility.SetDirty(widget);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.localScale       = Vector3.one;
    }
}
#endif
