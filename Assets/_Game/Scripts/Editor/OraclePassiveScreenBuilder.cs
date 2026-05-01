#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.IO;

/// <summary>
/// Construit l'intégralité du PassiveSelectionScreen en une seule passe.
/// Menu : Oracle > Build Passive Selection Screen
/// </summary>
public static class OraclePassiveScreenBuilder
{
    private const int   CARD_COUNT       = 5;
    private const float CARD_WIDTH       = 182f;
    private const float CARD_HEIGHT      = 236f;
    private const float CARD_SPACING     = 16f;
    private const string PASSIVE_POOL_PATH = "Assets/_Game/ScriptableObjects/Spells/Passifs";
    private const string POOL_ASSET_PATH   = "Assets/_Game/ScriptableObjects/AllPassivesPool.asset";

    [MenuItem("Oracle/Build Passive Selection Screen")]
    public static void Build()
    {
        Canvas canvas = EnsureCanvas();

        var existing = FindChild(canvas.transform, "PassiveSelectionScreen");
        if (existing != null)
        {
            bool rebuild = EditorUtility.DisplayDialog(
                "Oracle — Passive Screen",
                "Un PassiveSelectionScreen existe déjà.\nVeut-on le reconstruire entièrement ?",
                "Oui, reconstruire", "Annuler");
            if (!rebuild) return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject root = MakePanel(canvas.transform, "PassiveSelectionScreen",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(root.GetComponent<RectTransform>());
        root.SetActive(false);
        var pss = root.AddComponent<PassiveSelectionScreen>();
        Undo.RegisterCreatedObjectUndo(root, "Build PassiveSelectionScreen");

        var bg = MakePanel(root.transform, "Background",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        bg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        StretchFull(bg.GetComponent<RectTransform>());

        var title = MakeTMP(root.transform, "Title",
            new Vector2(0f, -70f), new Vector2(700f, 55f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        title.text      = "Choisis ton passif";
        title.fontSize  = 28f;
        title.fontStyle = FontStyles.Bold;
        title.color     = new Color(0.79f, 0.66f, 0.30f);
        title.alignment = TextAlignmentOptions.Center;

        float totalW = CARD_COUNT * CARD_WIDTH + (CARD_COUNT - 1) * CARD_SPACING;
        var container = MakePanel(root.transform, "CardsContainer",
            new Vector2(0f, -20f),
            new Vector2(totalW, CARD_HEIGHT),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        container.AddComponent<Image>().color = Color.clear;
        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = CARD_SPACING;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;

        var cardList = new System.Collections.Generic.List<PassiveCardUI>();
        for (int i = 0; i < CARD_COUNT; i++)
            cardList.Add(BuildCard(container.transform, i));
        pss.cards = cardList;

        // Timer
        var timerRoot = MakePanel(root.transform, "TimerContainer",
            new Vector2(-60f, -60f), new Vector2(80f, 80f),
            new Vector2(1f, 1f), new Vector2(1f, 1f));
        timerRoot.AddComponent<Image>().color = Color.clear;

        var timerIconGO = MakePanel(timerRoot.transform, "TimerIcon",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(timerIconGO.GetComponent<RectTransform>());
        var timerIconImg = timerIconGO.AddComponent<Image>();
        var iconSp = OracleUIMajTextureSetup.TryLoadSprite("timer_icon_hud");
        if (iconSp != null) timerIconImg.sprite = iconSp;
        timerIconImg.preserveAspect = true;

        var timerFillGO = MakePanel(timerRoot.transform, "TimerFill",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(timerFillGO.GetComponent<RectTransform>());
        var timerFillImg           = timerFillGO.AddComponent<Image>();
        timerFillImg.type          = Image.Type.Filled;
        timerFillImg.fillMethod    = Image.FillMethod.Radial360;
        timerFillImg.fillOrigin    = (int)Image.Origin360.Top;
        timerFillImg.fillClockwise = true;
        timerFillImg.fillAmount    = 1f;
        timerFillImg.color         = new Color(0.788f, 0.659f, 0.298f, 0.45f);
        pss.timerIcon = timerIconImg;
        pss.timerFill = timerFillImg;

        var timerTextGO = MakePanel(timerRoot.transform, "TimerText",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(timerTextGO.GetComponent<RectTransform>());
        var timerTMP       = timerTextGO.AddComponent<TextMeshProUGUI>();
        timerTMP.text      = "30";
        timerTMP.fontSize  = 22f;
        timerTMP.fontStyle = FontStyles.Bold;
        timerTMP.color     = Color.white;
        timerTMP.alignment = TextAlignmentOptions.Center;
        pss.timerText = timerTMP;
        OracleEditorAsepriteFont.AssignIfAvailable(timerTMP);

        // Confirm button
        var btnGO = MakePanel(root.transform, "ConfirmButton",
            new Vector2(0f, 70f), new Vector2(200f, 50f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.79f, 0.66f, 0.30f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var btnBlock              = btn.colors;
        btnBlock.normalColor      = new Color(0.79f, 0.66f, 0.30f);
        btnBlock.highlightedColor = new Color(0.95f, 0.80f, 0.40f);
        btnBlock.pressedColor     = new Color(0.60f, 0.48f, 0.18f);
        btnBlock.disabledColor    = new Color(0.35f, 0.35f, 0.35f);
        btn.colors       = btnBlock;
        btn.interactable = false;

        var btnLabelGO = MakePanel(btnGO.transform, "Label",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(btnLabelGO.GetComponent<RectTransform>());
        var btnTMP       = btnLabelGO.AddComponent<TextMeshProUGUI>();
        btnTMP.text      = "Confirmer";
        btnTMP.fontSize  = 18f;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.color     = new Color(0.10f, 0.07f, 0.02f);
        btnTMP.alignment = TextAlignmentOptions.Center;
        pss.confirmButton = btn;
        OracleEditorAsepriteFont.AssignIfAvailable(btnTMP);

        // Recap panel
        var recap = MakePanel(root.transform, "RecapPanel",
            new Vector2(-200f, -60f), new Vector2(400f, 120f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        recap.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.18f, 0.93f);
        recap.SetActive(false);
        pss.recapPanel = recap;

        var recapTextGO = MakePanel(recap.transform, "RecapText",
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
        StretchFull(recapTextGO.GetComponent<RectTransform>());
        var recapTMP       = recapTextGO.AddComponent<TextMeshProUGUI>();
        recapTMP.text      = "Passif choisi : —";
        recapTMP.fontSize  = 18f;
        recapTMP.color     = Color.white;
        recapTMP.alignment = TextAlignmentOptions.Center;
        pss.recapText = recapTMP;

        TryAssignPassivePool(pss);

        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("[OraclePassiveScreenBuilder] PassiveSelectionScreen construit avec succès !");

        EditorUtility.DisplayDialog(
            "Oracle — Passive Screen",
            "PassiveSelectionScreen construit !\n\n" +
            (pss.passivePool != null
                ? $"PassivePool assigné : {pss.passivePool.allPassives.Count} passifs trouvés."
                : "PassivePool non trouvé — crée Assets/_Game/ScriptableObjects/AllPassivesPool.asset\n" +
                  "et glisse les passifs dedans."),
            "OK");
    }

    // =========================================================
    // CONSTRUCTION D'UNE CARTE (sans cardOrnament — style minimaliste v2)
    // =========================================================
    static PassiveCardUI BuildCard(Transform parent, int index)
    {
        var cardGO = MakePanel(parent, $"Card_{index}",
            Vector2.zero, new Vector2(CARD_WIDTH, CARD_HEIGHT),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        var backing = cardGO.AddComponent<Image>();
        backing.color = new Color(0.08f, 0.08f, 0.13f, 0.95f);

        var btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = backing;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.14f, 0.14f, 0.20f, 0.97f);
        btn.colors = colors;

        var pcu = cardGO.AddComponent<PassiveCardUI>();
        pcu.cardBackground = backing;

        // SelectionBorder
        var borderGO  = MakeAnchoredPanel(cardGO.transform, "SelectionBorder", Vector2.zero, Vector2.one);
        StretchFull(borderGO.GetComponent<RectTransform>());
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color        = new Color(0f, 0f, 0f, 0f);
        borderImg.raycastTarget = false;
        pcu.selectionBorder = borderImg;

        // Icon
        var iconHost = MakeAnchoredPanel(cardGO.transform, "IconHost",
            new Vector2(0.20f, 0.52f), new Vector2(0.80f, 0.92f));
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(iconHost.transform, false);
        StretchFull(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color           = Color.white;
        iconImg.preserveAspect  = true;
        iconImg.enabled         = false;
        iconImg.raycastTarget   = false;
        pcu.iconImage = iconImg;

        // Name
        var nameHost = MakeAnchoredPanel(cardGO.transform, "NameHost",
            new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.50f));
        var nameTMP = nameHost.AddComponent<TextMeshProUGUI>();
        nameTMP.text              = $"Passif {index + 1}";
        nameTMP.fontSize          = 12f;
        nameTMP.fontStyle         = FontStyles.Bold;
        nameTMP.color             = new Color(0.96f, 0.85f, 0.38f);
        nameTMP.alignment         = TextAlignmentOptions.Center;
        nameTMP.enableWordWrapping = true;
        nameTMP.overflowMode      = TextOverflowModes.Ellipsis;
        nameTMP.raycastTarget     = false;
        pcu.nameText = nameTMP;

        // Description
        var descHost = MakeAnchoredPanel(cardGO.transform, "DescHost",
            new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.34f));
        var descTMP = descHost.AddComponent<TextMeshProUGUI>();
        descTMP.text              = "—";
        descTMP.fontSize          = 9.5f;
        descTMP.lineSpacing       = -2f;
        descTMP.color             = new Color(0.67f, 0.67f, 0.67f);
        descTMP.alignment         = TextAlignmentOptions.Center;
        descTMP.enableWordWrapping = true;
        descTMP.raycastTarget     = false;
        pcu.descriptionText = descTMP;

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

    // =========================================================
    // PASSIVE POOL AUTO-ASSIGN
    // =========================================================
    static void TryAssignPassivePool(PassiveSelectionScreen pss)
    {
        var pool = AssetDatabase.LoadAssetAtPath<PassivePool>(POOL_ASSET_PATH);
        if (pool == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/ScriptableObjects")) return;
            pool = ScriptableObject.CreateInstance<PassivePool>();
            AssetDatabase.CreateAsset(pool, POOL_ASSET_PATH);
        }
        if (pool.allPassives.Count == 0 && AssetDatabase.IsValidFolder(PASSIVE_POOL_PATH))
        {
            string[] guids = AssetDatabase.FindAssets("t:PassiveData", new[] { PASSIVE_POOL_PATH });
            foreach (var guid in guids)
            {
                var passive = AssetDatabase.LoadAssetAtPath<PassiveData>(AssetDatabase.GUIDToAssetPath(guid));
                if (passive != null && !pool.allPassives.Contains(passive))
                    pool.allPassives.Add(passive);
            }
            EditorUtility.SetDirty(pool);
        }
        pss.passivePool = pool;
        Debug.Log($"[OraclePassiveScreenBuilder] PassivePool : {pool.allPassives.Count} passifs assignés.");
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================
    static Canvas EnsureCanvas()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null) return canvas;
        var go = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static GameObject MakePanel(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
        return go;
    }

    static TextMeshProUGUI MakeTMP(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = MakePanel(parent, name, anchoredPos, sizeDelta, anchorMin, anchorMax);
        return go.AddComponent<TextMeshProUGUI>();
    }

    static GameObject MakeAnchoredPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
            if (child.name == name) return child;
        return null;
    }
}
#endif
