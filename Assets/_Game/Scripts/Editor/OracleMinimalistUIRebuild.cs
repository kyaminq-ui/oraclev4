#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Oracle — Refonte UI Minimaliste
/// Menus :
///   Oracle/Minimalist UI — Rebuild Passive Cards
///   Oracle/Minimalist UI — Rebuild Passive Screen
///   Oracle/Minimalist UI — Rebuild Combat HUD
///   Oracle/Minimalist UI — Rebuild Deck Slots
/// </summary>
public static class OracleMinimalistUIRebuild
{
    // ── Palette ──────────────────────────────────────────────────────────────
    static readonly Color C_CARD_BG    = new Color(0.08f, 0.08f, 0.13f, 0.95f);
    static readonly Color C_GOLD       = new Color(0.96f, 0.85f, 0.38f, 1.00f);
    static readonly Color C_DARK_BG    = new Color(0.06f, 0.06f, 0.09f, 0.93f);
    static readonly Color C_TEXT_WHITE = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    static readonly Color C_TEXT_MUTED = new Color(0.67f, 0.67f, 0.67f, 1.00f);
    static readonly Color C_HP_BAR     = new Color(0.91f, 0.28f, 0.33f, 1.00f);
    static readonly Color C_AP         = new Color(0.31f, 0.80f, 0.77f, 1.00f);
    static readonly Color C_MP         = new Color(0.58f, 0.64f, 1.00f, 1.00f);
    static readonly Color C_CLEAR      = new Color(0f, 0f, 0f, 0f);
    static readonly Color C_OVERLAY    = new Color(0f, 0f, 0f, 0.60f);

    const float CARD_W = 162f, CARD_H = 220f, CARD_SPACING = 24f;
    const int   CARD_COUNT = 5;
    const float SLOT_W = 96f, SLOT_H = 128f;
    const int   SLOT_N = 8;
    static readonly string[] HOTKEYS = { "Q","W","E","R","A","S","D","F" };

    // =========================================================================
    //  PASSIVE CARDS ONLY
    // =========================================================================
    [MenuItem("Oracle/Minimalist UI — Rebuild Passive Cards")]
    static void RebuildPassiveCards()
    {
        var pss = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        if (pss == null) { Msg("Aucun PassiveSelectionScreen dans la scène."); return; }

        var container = pss.transform.Find("CardsContainer");
        if (container == null) { Msg("CardsContainer introuvable."); return; }

        if (!Confirm("Détruire les cartes existantes et créer 5 cartes minimalistes ?")) return;

        for (int i = container.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);

        pss.cards.Clear();
        for (int i = 0; i < CARD_COUNT; i++)
            pss.cards.Add(BuildPassiveCard(container, i));

        EditorUtility.SetDirty(pss);
        Debug.Log("[OracleMinimalistUI] 5 cartes passif reconstruites.");
        EditorUtility.DisplayDialog("Oracle", "Cartes passif reconstruites !", "OK");
    }

    // =========================================================================
    //  PASSIVE SCREEN COMPLET
    // =========================================================================
    [MenuItem("Oracle/Minimalist UI — Rebuild Passive Screen")]
    static void RebuildPassiveScreen()
    {
        Canvas canvas = EnsureCanvas();

        var existing = canvas.transform.Find("PassiveSelectionScreen");
        if (existing != null)
        {
            if (!Confirm("PassiveSelectionScreen existe déjà. Reconstruire ?")) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var rootGO = GO("PassiveSelectionScreen", canvas.transform);
        Undo.RegisterCreatedObjectUndo(rootGO, "PSS Minimalist");
        Stretch(rootGO.AddComponent<RectTransform>());
        rootGO.SetActive(false);

        var pss = rootGO.AddComponent<PassiveSelectionScreen>();
        var cv  = rootGO.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 5000;
        rootGO.AddComponent<GraphicRaycaster>();

        // Overlay
        Img(GO("Background", rootGO.transform), C_OVERLAY, stretch: true);

        // Titre
        var titleRT = Rt(GO("Title", rootGO.transform));
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -52f);
        titleRT.sizeDelta        = new Vector2(0f, 44f);
        Tmp(titleRT.gameObject, "Choisis ton passif", 26f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Center);

        // Timer (coin haut droit) — icône + radial + chiffre centré
        var timerCornerRT = Rt(GO("TimerCorner", rootGO.transform));
        timerCornerRT.anchorMin        = timerCornerRT.anchorMax = new Vector2(1f, 1f);
        timerCornerRT.pivot            = new Vector2(1f, 1f);
        timerCornerRT.anchoredPosition = new Vector2(-16f, -12f);
        timerCornerRT.sizeDelta        = new Vector2(56f, 56f);
        var timerIgnore = timerCornerRT.gameObject.AddComponent<LayoutElement>();
        timerIgnore.ignoreLayout = true;

        var tiRT = Rt(GO("TimerIcon", timerCornerRT.transform));
        Stretch(tiRT);
        var tiImg = tiRT.gameObject.AddComponent<Image>();
        var tiSp  = OracleUIMajTextureSetup.TryLoadSprite("timer_icon_hud");
        if (tiSp != null) tiImg.sprite = tiSp;
        tiImg.preserveAspect = true;
        pss.timerIcon = tiImg;

        var tfRT = Rt(GO("TimerFill", timerCornerRT.transform));
        Stretch(tfRT);
        var tfImg        = tfRT.gameObject.AddComponent<Image>();
        tfImg.color               = new Color(0.96f, 0.85f, 0.38f, 0.42f);
        tfImg.type                = Image.Type.Filled;
        tfImg.fillMethod          = Image.FillMethod.Radial360;
        tfImg.fillOrigin          = (int)Image.Origin360.Top;
        tfImg.fillClockwise       = false;
        tfImg.fillAmount          = 1f;
        tfImg.raycastTarget       = false;
        pss.timerFill             = tfImg;

        var ttRT = Rt(GO("TimerText", timerCornerRT.transform));
        Stretch(ttRT);
        pss.timerText = Tmp(ttRT.gameObject, "30", 22f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Center);
        OracleEditorAsepriteFont.AssignIfAvailable(pss.timerText);

        // Cards container
        float totalW = CARD_COUNT * CARD_W + (CARD_COUNT - 1) * CARD_SPACING;
        var contRT = Rt(GO("CardsContainer", rootGO.transform));
        contRT.anchorMin        = new Vector2(0.5f, 0.5f);
        contRT.anchorMax        = new Vector2(0.5f, 0.5f);
        contRT.pivot            = new Vector2(0.5f, 0.5f);
        contRT.anchoredPosition = new Vector2(0f, 10f);
        contRT.sizeDelta        = new Vector2(totalW, CARD_H);
        var hlg = contRT.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = CARD_SPACING;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;

        pss.cards.Clear();
        for (int i = 0; i < CARD_COUNT; i++)
            pss.cards.Add(BuildPassiveCard(contRT.transform, i));

        // Confirm button
        var btnRT = Rt(GO("ConfirmButton", rootGO.transform));
        btnRT.anchorMin        = new Vector2(0.5f, 0f);
        btnRT.anchorMax        = new Vector2(0.5f, 0f);
        btnRT.pivot            = new Vector2(0.5f, 0f);
        btnRT.anchoredPosition = new Vector2(0f, 60f);
        btnRT.sizeDelta        = new Vector2(200f, 46f);
        var btnImg = btnRT.gameObject.AddComponent<Image>();
        btnImg.color = C_CLEAR;
        var outline = btnRT.gameObject.AddComponent<Outline>();
        outline.effectColor    = C_GOLD;
        outline.effectDistance = new Vector2(1f, -1f);
        var btn = btnRT.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var bc = btn.colors;
        bc.normalColor      = C_CLEAR;
        bc.highlightedColor = new Color(0.96f, 0.85f, 0.38f, 0.15f);
        bc.pressedColor     = new Color(0.96f, 0.85f, 0.38f, 0.30f);
        bc.disabledColor    = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        btn.colors       = bc;
        btn.interactable = false;
        pss.confirmButton = btn;
        var lblRT = Rt(GO("Label", btnRT.transform));
        Stretch(lblRT);
        var confirmTmp = Tmp(lblRT.gameObject, "Confirmer", 16f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Center);
        OracleEditorAsepriteFont.AssignIfAvailable(confirmTmp);

        // Recap
        var recapRT = Rt(GO("RecapPanel", rootGO.transform));
        recapRT.anchorMin        = new Vector2(0.5f, 0.5f);
        recapRT.anchorMax        = new Vector2(0.5f, 0.5f);
        recapRT.pivot            = new Vector2(0.5f, 0.5f);
        recapRT.anchoredPosition = Vector2.zero;
        recapRT.sizeDelta        = new Vector2(360f, 80f);
        Img(recapRT.gameObject, new Color(0.08f, 0.08f, 0.13f, 0.97f));
        recapRT.gameObject.SetActive(false);
        pss.recapPanel = recapRT.gameObject;
        var recapTxtRT = Rt(GO("RecapText", recapRT.transform));
        Stretch(recapTxtRT);
        pss.recapText = Tmp(recapTxtRT.gameObject, "Passif choisi : —", 16f, FontStyles.Normal, C_TEXT_WHITE, TextAlignmentOptions.Center);

        TryAssignPassivePool(pss);
        EditorUtility.SetDirty(rootGO);
        Selection.activeGameObject = rootGO;

        EditorUtility.DisplayDialog("Oracle",
            "PassiveSelectionScreen reconstruit !\n\n" +
            (pss.passivePool != null ? $"PassivePool : {pss.passivePool.allPassives.Count} passifs." : "PassivePool non trouvé — assigne-le manuellement."), "OK");
    }

    // =========================================================================
    //  COMBAT HUD  — positionnement ancré fixe, sans Layout Group sur les textes
    // =========================================================================
    [MenuItem("Oracle/Minimalist UI — Rebuild Combat HUD")]
    static void RebuildCombatHUD()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Msg("Aucun Canvas dans la scène."); return; }

        var old = canvas.transform.Find("CombatHUD");
        if (old != null)
        {
            if (!Confirm("CombatHUD existe. Reconstruire ? (DeckUI et TimerUI seront re-parentés)")) return;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        var rootGO = GO("CombatHUD", canvas.transform);
        Undo.RegisterCreatedObjectUndo(rootGO, "CombatHUD Minimalist");
        Stretch(rootGO.AddComponent<RectTransform>());
        var hud = rootGO.AddComponent<CombatHUD>();
        rootGO.transform.SetAsFirstSibling();

        // ══════════════════════════════════════════════════════════
        //  TOP BAR  (72px, ancré en haut)
        // ══════════════════════════════════════════════════════════
        var topRT = AnchoredRect(rootGO.transform, "TopBar",
            ancMin: new Vector2(0f, 1f), ancMax: new Vector2(1f, 1f),
            pivot:  new Vector2(0.5f, 1f),
            pos:    Vector2.zero, size: new Vector2(0f, 72f));
        Img(topRT.gameObject, C_DARK_BG);

        // — Bloc Player (gauche) —
        // Label
        var pLabelRT = AnchoredRect(topRT.transform, "PlayerLabel",
            ancMin: new Vector2(0f, 1f), ancMax: new Vector2(0.28f, 1f),
            pivot:  new Vector2(0f, 1f),
            pos:    new Vector2(16f, -8f), size: new Vector2(0f, 18f));
        hud.teamALabel = Tmp(pLabelRT.gameObject, "Player", 11f, FontStyles.Bold, C_TEXT_MUTED, TextAlignmentOptions.Left);
        OracleEditorAsepriteFont.AssignIfAvailable(hud.teamALabel);

        // HP track
        var pTrackRT = AnchoredRect(topRT.transform, "PlayerHpTrack",
            ancMin: new Vector2(0f, 1f), ancMax: new Vector2(0.28f, 1f),
            pivot:  new Vector2(0f, 1f),
            pos:    new Vector2(16f, -30f), size: new Vector2(-32f, 8f));
        Img(pTrackRT.gameObject, new Color(0.2f, 0.2f, 0.2f));
        var pFillRT = AnchoredRect(pTrackRT.transform, "Fill",
            ancMin: Vector2.zero, ancMax: Vector2.one,
            pivot:  new Vector2(0f, 0.5f),
            pos:    Vector2.zero, size: Vector2.zero);
        var pFill        = pFillRT.gameObject.AddComponent<Image>();
        pFill.color      = C_HP_BAR;
        pFill.type       = Image.Type.Filled;
        pFill.fillMethod = Image.FillMethod.Horizontal;
        pFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        pFill.fillAmount = 1f;
        hud.teamAHpFill  = pFill;

        // HP value
        var pValRT = AnchoredRect(topRT.transform, "PlayerHpValue",
            ancMin: new Vector2(0f, 1f), ancMax: new Vector2(0.28f, 1f),
            pivot:  new Vector2(0f, 1f),
            pos:    new Vector2(16f, -42f), size: new Vector2(0f, 18f));
        hud.teamAHpValue = Tmp(pValRT.gameObject, "0 / 50", 12f, FontStyles.Normal, C_TEXT_WHITE, TextAlignmentOptions.Left);

        // — Timer (haut centre, entre les barres HP) —
        var timerHostRT = AnchoredRect(rootGO.transform, "TimerHost",
            ancMin: new Vector2(0.5f, 1f), ancMax: new Vector2(0.5f, 1f),
            pivot:  new Vector2(0.5f, 1f),
            pos:    new Vector2(0f, -10f), size: new Vector2(56f, 56f));

        // — Bloc Opponent (droite) —
        var oLabelRT = AnchoredRect(topRT.transform, "OpponentLabel",
            ancMin: new Vector2(0.72f, 1f), ancMax: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f),
            pos:    new Vector2(-16f, -8f), size: new Vector2(0f, 18f));
        hud.teamBLabel = Tmp(oLabelRT.gameObject, "Opponent", 11f, FontStyles.Bold, C_TEXT_MUTED, TextAlignmentOptions.Right);

        var oTrackRT = AnchoredRect(topRT.transform, "OpponentHpTrack",
            ancMin: new Vector2(0.72f, 1f), ancMax: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f),
            pos:    new Vector2(-16f, -30f), size: new Vector2(-32f, 8f));
        Img(oTrackRT.gameObject, new Color(0.2f, 0.2f, 0.2f));
        var oFillRT = AnchoredRect(oTrackRT.transform, "Fill",
            ancMin: Vector2.zero, ancMax: Vector2.one,
            pivot:  new Vector2(1f, 0.5f),
            pos:    Vector2.zero, size: Vector2.zero);
        var oFill        = oFillRT.gameObject.AddComponent<Image>();
        oFill.color      = C_HP_BAR;
        oFill.type       = Image.Type.Filled;
        oFill.fillMethod = Image.FillMethod.Horizontal;
        oFill.fillOrigin = (int)Image.OriginHorizontal.Right;
        oFill.fillAmount = 1f;
        hud.teamBHpFill  = oFill;

        var oValRT = AnchoredRect(topRT.transform, "OpponentHpValue",
            ancMin: new Vector2(0.72f, 1f), ancMax: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f),
            pos:    new Vector2(-16f, -42f), size: new Vector2(0f, 18f));
        hud.teamBHpValue = Tmp(oValRT.gameObject, "0 / 50", 12f, FontStyles.Normal, C_TEXT_WHITE, TextAlignmentOptions.Right);

        // ══════════════════════════════════════════════════════════
        //  BOTTOM BAR  (120px, ancré en bas)
        // ══════════════════════════════════════════════════════════
        var botRT = AnchoredRect(rootGO.transform, "BottomBar",
            ancMin: new Vector2(0f, 0f), ancMax: new Vector2(1f, 0f),
            pivot:  new Vector2(0.5f, 0f),
            pos:    Vector2.zero, size: new Vector2(0f, 120f));
        Img(botRT.gameObject, C_DARK_BG);

        // Passif — icône (carré 52×52, ancré bas-gauche)
        var passiveIconSlotRT = AnchoredRect(botRT.transform, "PassiveIconSlot",
            ancMin: new Vector2(0f, 0.5f), ancMax: new Vector2(0f, 0.5f),
            pivot:  new Vector2(0f, 0.5f),
            pos:    new Vector2(16f, 0f), size: new Vector2(52f, 52f));
        Img(passiveIconSlotRT.gameObject, new Color(0.12f, 0.12f, 0.18f));
        var pIconInnerRT = AnchoredRect(passiveIconSlotRT.transform, "Icon",
            ancMin: new Vector2(0.1f, 0.1f), ancMax: new Vector2(0.9f, 0.9f),
            pivot:  new Vector2(0.5f, 0.5f),
            pos:    Vector2.zero, size: Vector2.zero);
        var pIconImg = pIconInnerRT.gameObject.AddComponent<Image>();
        pIconImg.color          = new Color(0.5f, 0.5f, 0.5f);
        pIconImg.preserveAspect = true;
        pIconImg.raycastTarget  = false;
        hud.passiveIcon = pIconImg;

        // Passif — nom
        var pNameRT = AnchoredRect(botRT.transform, "PassiveName",
            ancMin: new Vector2(0f, 0.5f), ancMax: new Vector2(0f, 0.5f),
            pivot:  new Vector2(0f, 0.5f),
            pos:    new Vector2(76f, 8f), size: new Vector2(130f, 20f));
        hud.passiveNameText = Tmp(pNameRT.gameObject, "Passif", 13f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Left);

        // PA / PM — icônes + chiffre centré
        const float statSz = 44f;
        var paClusterRT = AnchoredRect(botRT.transform, "PACluster",
            ancMin: new Vector2(0f, 0.5f), ancMax: new Vector2(0f, 0.5f),
            pivot:  new Vector2(0f, 0.5f),
            pos:    new Vector2(76f, -12f), size: new Vector2(statSz, statSz));
        var paIconRT = Rt(GO("Icon", paClusterRT.transform));
        Stretch(paIconRT);
        var paIconImg = paIconRT.gameObject.AddComponent<Image>();
        var paSp = OracleUIMajTextureSetup.TryLoadSpritePrimaryOrLegacy("pa_icon_hud", "mana_icon_hud");
        if (paSp != null) paIconImg.sprite = paSp;
        paIconImg.preserveAspect = true;
        hud.paIconImage = paIconImg;
        var paValRT = Rt(GO("PAValue", paClusterRT.transform));
        Stretch(paValRT);
        hud.paText = Tmp(paValRT.gameObject, "0", 17f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        OracleEditorAsepriteFont.AssignIfAvailable(hud.paText);

        var pmClusterRT = AnchoredRect(botRT.transform, "PMCluster",
            ancMin: new Vector2(0f, 0.5f), ancMax: new Vector2(0f, 0.5f),
            pivot:  new Vector2(0f, 0.5f),
            pos:    new Vector2(76f + statSz + 10f, -12f), size: new Vector2(statSz, statSz));
        var pmIconRT = Rt(GO("Icon", pmClusterRT.transform));
        Stretch(pmIconRT);
        var pmIconImg = pmIconRT.gameObject.AddComponent<Image>();
        var pmSp = OracleUIMajTextureSetup.TryLoadSpritePrimaryOrLegacy("pm_icon_hud", "mouvement_icon_hud");
        if (pmSp != null) pmIconImg.sprite = pmSp;
        pmIconImg.preserveAspect = true;
        hud.pmIconImage = pmIconImg;
        var pmValRT = Rt(GO("PMValue", pmClusterRT.transform));
        Stretch(pmValRT);
        hud.pmText = Tmp(pmValRT.gameObject, "0", 17f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        OracleEditorAsepriteFont.AssignIfAvailable(hud.pmText);

        // Deck host (centre, étire pour prendre tout l'espace disponible)
        var deckHostRT = AnchoredRect(botRT.transform, "DeckHost",
            ancMin: new Vector2(0.22f, 0f), ancMax: new Vector2(0.80f, 1f),
            pivot:  new Vector2(0.5f, 0.5f),
            pos:    Vector2.zero, size: Vector2.zero);

        // End Turn (droite)
        var endWrapRT = AnchoredRect(botRT.transform, "EndTurnWrap",
            ancMin: new Vector2(1f, 0.5f), ancMax: new Vector2(1f, 0.5f),
            pivot:  new Vector2(1f, 0.5f),
            pos:    new Vector2(-16f, 0f), size: new Vector2(120f, 48f));
        var endImg = endWrapRT.gameObject.AddComponent<Image>();
        endImg.color = C_CLEAR;
        var endOutline = endWrapRT.gameObject.AddComponent<Outline>();
        endOutline.effectColor    = C_GOLD;
        endOutline.effectDistance = new Vector2(1f, -1f);
        var endBtn = endWrapRT.gameObject.AddComponent<Button>();
        endBtn.targetGraphic = endImg;
        var ec = endBtn.colors;
        ec.normalColor      = C_CLEAR;
        ec.highlightedColor = new Color(0.96f, 0.85f, 0.38f, 0.18f);
        ec.pressedColor     = new Color(0.96f, 0.85f, 0.38f, 0.35f);
        endBtn.colors = ec;
        hud.endTurnButton = endBtn;
        var endLblRT = Rt(GO("Label", endWrapRT.transform));
        Stretch(endLblRT);
        var endTurnTmp = Tmp(endLblRT.gameObject, "Fin de tour", 14f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Center);
        OracleEditorAsepriteFont.AssignIfAvailable(endTurnTmp);

        // ── TimerUI sous TimerHost (évite le Timer du PassiveSelectionScreen ; crée si vide)
        PassiveSelectionScreen passiveScr = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        Transform passiveRoot = passiveScr != null ? passiveScr.transform : null;
        TimerUI combatTimer = timerHostRT.GetComponentInChildren<TimerUI>(true);
        if (combatTimer != null)
        {
            Stretch(combatTimer.GetComponent<RectTransform>());
        }
        else
        {
            foreach (var t in Object.FindObjectsOfType<TimerUI>(true))
            {
                if (t == null) continue;
                if (passiveRoot != null && t.transform.IsChildOf(passiveRoot)) continue;
                Undo.SetTransformParent(t.transform, timerHostRT.transform, "Move TimerUI");
                Stretch(t.GetComponent<RectTransform>());
                combatTimer = t;
                break;
            }
        }
        if (combatTimer == null)
        {
            combatTimer = OracleCombatHUDBuilder.CreateTimerUIUnderHost(timerHostRT);
            Undo.RegisterCreatedObjectUndo(combatTimer.gameObject, "TimerUI");
        }
        hud.combatTurnTimer = combatTimer;

        // ── Re-parent DeckUI dans deckHost
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck != null)
        {
            Undo.SetTransformParent(deck.transform, deckHostRT.transform, "Move DeckUI");
            Stretch(deck.GetComponent<RectTransform>());
        }

        EditorUtility.SetDirty(hud);
        Selection.activeGameObject = rootGO;
        Debug.Log("[OracleMinimalistUI] CombatHUD reconstruit.");
        EditorUtility.DisplayDialog("Oracle", "CombatHUD minimaliste créé !", "OK");
    }

    // =========================================================================
    //  DECK SLOTS
    // =========================================================================
    [MenuItem("Oracle/Minimalist UI — Rebuild Deck Slots")]
    static void RebuildDeckSlots()
    {
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck == null) { Msg("Aucun DeckUI dans la scène."); return; }
        if (!Confirm("Recréer les 8 slots de sorts en style minimaliste ?")) return;

        Undo.RegisterCompleteObjectUndo(deck.gameObject, "Deck Minimalist");

        for (int i = deck.transform.childCount - 1; i >= 0; i--)
        {
            var ch = deck.transform.GetChild(i);
            if (ch.GetComponent<SpellSlotUI>() != null || ch.name.StartsWith("SpellSlot"))
                Undo.DestroyObjectImmediate(ch.gameObject);
        }
        deck.slots.Clear();

        var hlg = deck.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(deck.gameObject);
        hlg.padding                = new RectOffset(8, 8, 8, 8);
        hlg.spacing                = 10f;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        for (int i = 0; i < SLOT_N; i++)
            deck.slots.Add(BuildSpellSlot(deck.transform, i));

        EditorUtility.SetDirty(deck);
        Selection.activeGameObject = deck.gameObject;
        Debug.Log("[OracleMinimalistUI] 8 slots sorts reconstruits.");
        EditorUtility.DisplayDialog("Oracle", "8 slots de sorts créés !", "OK");
    }

    // =========================================================================
    //  BUILD — Carte passif
    // =========================================================================
    static PassiveCardUI BuildPassiveCard(Transform parent, int index)
    {
        var cardGO = GO($"Card_{index}", parent);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(CARD_W, CARD_H);

        var le = cardGO.AddComponent<LayoutElement>();
        le.preferredWidth  = CARD_W;
        le.preferredHeight = CARD_H;
        le.flexibleWidth   = 0f;

        var hitImg = cardGO.AddComponent<Image>();
        hitImg.color = C_CLEAR;
        var btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = hitImg;
        var bc = btn.colors;
        bc.normalColor = bc.highlightedColor = bc.pressedColor = C_CLEAR;
        btn.colors = bc;

        var pcu = cardGO.AddComponent<PassiveCardUI>();

        // Fond
        var bgRT = Rt(GO("Background", cardGO.transform));
        Stretch(bgRT);
        var bgImg = bgRT.gameObject.AddComponent<Image>();
        bgImg.color        = C_CARD_BG;
        bgImg.raycastTarget = false;
        pcu.cardBackground = bgImg;

        // Bordure sélection
        var borderRT = Rt(GO("SelectionBorder", cardGO.transform));
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-1f, -1f);
        borderRT.offsetMax = new Vector2(1f, 1f);
        var borderImg = borderRT.gameObject.AddComponent<Image>();
        borderImg.color        = C_CLEAR;
        borderImg.raycastTarget = false;
        pcu.selectionBorder = borderImg;

        // Icône (centré, zone haute)
        var iconRT = AnchoredRect(cardGO.transform, "Icon",
            ancMin: new Vector2(0.20f, 0.52f), ancMax: new Vector2(0.80f, 0.92f),
            pivot:  new Vector2(0.5f, 0.5f),
            pos:    Vector2.zero, size: Vector2.zero);
        var iconImg = iconRT.gameObject.AddComponent<Image>();
        iconImg.color          = Color.white;
        iconImg.preserveAspect = true;
        iconImg.enabled        = false;
        iconImg.raycastTarget  = false;
        pcu.iconImage = iconImg;

        // Nom
        var nameRT = AnchoredRect(cardGO.transform, "NameText",
            ancMin: new Vector2(0.08f, 0f), ancMax: new Vector2(0.92f, 0f),
            pivot:  new Vector2(0.5f, 0f),
            pos:    new Vector2(0f, 74f), size: new Vector2(0f, 22f));
        var nameTmp = Tmp(nameRT.gameObject, $"Passif {index + 1}", 13f, FontStyles.Bold, C_GOLD, TextAlignmentOptions.Center);
        nameTmp.enableWordWrapping = false;
        nameTmp.overflowMode       = TextOverflowModes.Ellipsis;
        pcu.nameText = nameTmp;

        // Description
        var descRT = AnchoredRect(cardGO.transform, "DescText",
            ancMin: new Vector2(0.08f, 0f), ancMax: new Vector2(0.92f, 0f),
            pivot:  new Vector2(0.5f, 0f),
            pos:    new Vector2(0f, 10f), size: new Vector2(0f, 60f));
        var descTmp = Tmp(descRT.gameObject, "—", 10f, FontStyles.Normal, C_TEXT_MUTED, TextAlignmentOptions.Center);
        descTmp.enableWordWrapping = true;
        descTmp.overflowMode       = TextOverflowModes.Ellipsis;
        pcu.descriptionText = descTmp;

        // EventTrigger hover
        var et = cardGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        var enter = new UnityEngine.EventSystems.EventTrigger.Entry();
        enter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enter.callback.AddListener(_ => pcu.OnPointerEnter());
        et.triggers.Add(enter);
        var exit = new UnityEngine.EventSystems.EventTrigger.Entry();
        exit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exit.callback.AddListener(_ => pcu.OnPointerExit());
        et.triggers.Add(exit);

        return pcu;
    }

    // =========================================================================
    //  BUILD — Slot sort
    // =========================================================================
    static SpellSlotUI BuildSpellSlot(Transform parent, int index)
    {
        var slotGO = GO($"SpellSlot_{index + 1}", parent);
        Undo.RegisterCreatedObjectUndo(slotGO, "SpellSlot Minimal");
        slotGO.AddComponent<RectTransform>().sizeDelta = new Vector2(SLOT_W, SLOT_H);

        var le = slotGO.AddComponent<LayoutElement>();
        le.preferredWidth  = SLOT_W;
        le.preferredHeight = SLOT_H;
        le.flexibleWidth   = 0f;

        var hitImg = slotGO.AddComponent<Image>();
        hitImg.color = new Color(0f, 0f, 0f, 0.001f);

        var slot = slotGO.AddComponent<SpellSlotUI>();

        // Fond
        var bgRT = Rt(GO("Backing", slotGO.transform));
        Stretch(bgRT);
        Img(bgRT.gameObject, C_CARD_BG);

        // Zone icône
        var iconAreaRT = AnchoredRect(slotGO.transform, "IconArea",
            ancMin: new Vector2(0.10f, 0.22f), ancMax: new Vector2(0.90f, 0.90f),
            pivot:  new Vector2(0.5f, 0.5f), pos: Vector2.zero, size: Vector2.zero);

        var iconRT = Rt(GO("Icon", iconAreaRT.transform));
        Stretch(iconRT);
        var iconImg = iconRT.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        slot.iconImage = iconImg;

        var dimRT = Rt(GO("DimOverlay", iconAreaRT.transform));
        Stretch(dimRT);
        var dimImg = dimRT.gameObject.AddComponent<Image>();
        dimImg.color        = new Color(0f, 0f, 0f, 0.58f);
        dimImg.enabled      = false;
        dimImg.raycastTarget = false;
        slot.dimOverlay = dimImg;

        // PA badge
        var paRT = AnchoredRect(slotGO.transform, "PACost",
            ancMin: new Vector2(1f, 1f), ancMax: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f), pos: new Vector2(-4f, -4f), size: new Vector2(22f, 22f));
        Img(paRT.gameObject, C_AP);
        slot.paCostText = Tmp(paRT.gameObject, "0", 11f, FontStyles.Bold, new Color(0.05f, 0.05f, 0.08f), TextAlignmentOptions.Center);

        // Hotkey
        var hkRT = AnchoredRect(slotGO.transform, "Hotkey",
            ancMin: new Vector2(0f, 0f), ancMax: new Vector2(0f, 0f),
            pivot:  new Vector2(0f, 0f), pos: new Vector2(5f, 4f), size: new Vector2(28f, 18f));
        slot.hotkeyText = Tmp(hkRT.gameObject, index < HOTKEYS.Length ? HOTKEYS[index] : $"{index+1}",
            10f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);

        // Bordure sélection
        var selRT = Rt(GO("SelectionBorder", slotGO.transform));
        Stretch(selRT);
        var selImg = selRT.gameObject.AddComponent<Image>();
        selImg.color        = C_CLEAR;
        selImg.enabled      = false;
        selImg.raycastTarget = false;
        slot.selectionBorder = selImg;

        return slot;
    }

    // =========================================================================
    //  PASSIVE POOL
    // =========================================================================
    const string PASSIVE_POOL_PATH = "Assets/_Game/ScriptableObjects/Spells/Passifs";
    const string POOL_ASSET_PATH   = "Assets/_Game/ScriptableObjects/AllPassivesPool.asset";

    static void TryAssignPassivePool(PassiveSelectionScreen pss)
    {
        var pool = AssetDatabase.LoadAssetAtPath<PassivePool>(POOL_ASSET_PATH);
        if (pool == null && AssetDatabase.IsValidFolder("Assets/_Game/ScriptableObjects"))
        {
            pool = ScriptableObject.CreateInstance<PassivePool>();
            AssetDatabase.CreateAsset(pool, POOL_ASSET_PATH);
        }
        if (pool != null && pool.allPassives.Count == 0 && AssetDatabase.IsValidFolder(PASSIVE_POOL_PATH))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:PassiveData", new[] { PASSIVE_POOL_PATH }))
            {
                var p = AssetDatabase.LoadAssetAtPath<PassiveData>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && !pool.allPassives.Contains(p)) pool.allPassives.Add(p);
            }
            EditorUtility.SetDirty(pool);
        }
        if (pool != null) pss.passivePool = pool;
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================
    static Canvas EnsureCanvas()
    {
        var c = Object.FindObjectOfType<Canvas>();
        if (c != null) return c;
        var go = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // Crée un GameObject enfant
    static GameObject GO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // Ajoute et retourne un RectTransform
    static RectTransform Rt(GameObject go) => go.AddComponent<RectTransform>();

    // Rect ancré avec position et taille explicites
    static RectTransform AnchoredRect(Transform parent, string name,
        Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = GO(name, parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = ancMin;
        rt.anchorMax        = ancMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    // Stretch full parent
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    // Ajoute une Image
    static Image Img(GameObject go, Color col, bool stretch = false)
    {
        if (stretch)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            Stretch(rt);
        }
        var img = go.AddComponent<Image>();
        img.color = col;
        return img;
    }

    // Ajoute un TextMeshProUGUI
    static TextMeshProUGUI Tmp(GameObject go, string text, float size, FontStyles style, Color col, TextAlignmentOptions align)
    {
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = col;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Msg(string s) =>
        EditorUtility.DisplayDialog("Oracle — Minimalist UI", s, "OK");

    static bool Confirm(string s) =>
        EditorUtility.DisplayDialog("Oracle — Minimalist UI", s, "Oui", "Annuler");
}
#endif
