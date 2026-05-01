#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Crée l'écran de sélection de passif (PassiveSelectionScreen) en un clic.
/// Menu : Oracle/Build Passive Selection Screen (v1)
/// </summary>
public static class OraclePassiveSelectionBuilder
{
    static readonly Color DarkBg    = new Color(0.04f, 0.04f, 0.08f, 0.94f);
    static readonly Color CardBg    = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    static readonly Color Accent    = new Color(0.788f, 0.659f, 0.298f, 1f);

    const int   CardCount = 5;
    const float CardW     = 160f;
    const float CardH     = 240f;

    [MenuItem("Oracle/Build Passive Selection Screen (v1)")]
    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Oracle — Passif", "Aucun Canvas dans la scène.", "OK");
            return;
        }

        var oldGO = canvas.transform.Find("PassiveSelectionScreen");
        if (oldGO != null)
        {
            if (!EditorUtility.DisplayDialog("Oracle — Passif",
                    "PassiveSelectionScreen existe déjà. Reconstruire ?", "Oui", "Non"))
                return;
            Undo.DestroyObjectImmediate(oldGO.gameObject);
        }

        // ── Root ─────────────────────────────────────────────────────────────
        var root = new GameObject("PassiveSelectionScreen");
        Undo.RegisterCreatedObjectUndo(root, "PassiveSelectionScreen");
        root.transform.SetParent(canvas.transform, false);
        Stretch(root.AddComponent<RectTransform>());

        var screen = root.AddComponent<PassiveSelectionScreen>();

        // Overlay sombre plein écran
        var bg = root.AddComponent<Image>();
        bg.color = DarkBg;

        // Canvas override pour passer devant tout
        var cv = root.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder    = 5000;
        root.AddComponent<GraphicRaycaster>();

        var vMain = root.AddComponent<VerticalLayoutGroup>();
        vMain.padding            = new RectOffset(40, 40, 32, 32);
        vMain.spacing            = 20f;
        vMain.childAlignment     = TextAnchor.UpperCenter;
        vMain.childControlWidth  = true;
        vMain.childControlHeight = false;
        vMain.childForceExpandWidth  = true;
        vMain.childForceExpandHeight = false;

        // ── Titre ────────────────────────────────────────────────────────────
        AddLabel(root.transform, "Title", "Choisissez un passif", 26f, FontStyles.Bold, Accent, 48f);

        // Timer (coin haut droit, hors flux du layout)
        var timerCorner = new GameObject("TimerCorner");
        Undo.RegisterCreatedObjectUndo(timerCorner, "TimerCorner");
        timerCorner.transform.SetParent(root.transform, false);
        var tRt = timerCorner.AddComponent<RectTransform>();
        tRt.anchorMin = tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot     = new Vector2(1f, 1f);
        tRt.anchoredPosition = new Vector2(-16f, -16f);
        tRt.sizeDelta        = new Vector2(56f, 56f);
        var igLE = timerCorner.AddComponent<LayoutElement>();
        igLE.ignoreLayout = true;

        var iconGO = new GameObject("TimerIcon");
        iconGO.transform.SetParent(timerCorner.transform, false);
        Stretch(iconGO.AddComponent<RectTransform>());
        var tIcon = iconGO.AddComponent<Image>();
        var tSp = OracleUIMajTextureSetup.TryLoadSprite("timer_icon_hud");
        if (tSp != null) tIcon.sprite = tSp;
        tIcon.preserveAspect = true;

        var timerFillGO = new GameObject("TimerFill");
        timerFillGO.transform.SetParent(timerCorner.transform, false);
        Stretch(timerFillGO.AddComponent<RectTransform>());
        var timerFillImg = timerFillGO.AddComponent<Image>();
        timerFillImg.color               = new Color(0.788f, 0.659f, 0.298f, 0.45f);
        timerFillImg.type                = Image.Type.Filled;
        timerFillImg.fillMethod          = Image.FillMethod.Radial360;
        timerFillImg.fillOrigin          = (int)Image.Origin360.Top;
        timerFillImg.fillClockwise       = false;
        timerFillImg.fillAmount          = 1f;
        timerFillImg.raycastTarget       = false;

        var timerTextGO = AddLabel(timerCorner.transform, "TimerText", "30", 20f, FontStyles.Bold, Accent, 56f);
        Stretch(timerTextGO.GetComponent<RectTransform>());
        timerTextGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        OracleEditorAsepriteFont.AssignIfAvailable(timerTextGO.GetComponent<TextMeshProUGUI>());

        // ── Zone cartes ───────────────────────────────────────────────────────
        var cardsRow = MakeLE(MakePanel(root.transform, "CardsRow", CardH + 16f), CardH + 16f);
        var hlg = cardsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 16f;
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.childControlWidth     = false;
        hlg.childControlHeight    = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        Sprite iconRim = OracleUIMajTextureSetup.TryLoadSprite("UI_ICONE_PASSIF");

        for (int i = 0; i < CardCount; i++)
            screen.cards.Add(BuildPassiveCard(cardsRow.transform, i, iconRim));

        // ── Bouton Confirmer (rangée pleine largeur + HLG pour centrer le bouton fixe) ─
        var confirmBtn = BuildConfirmButton(root.transform);
        screen.confirmButton = confirmBtn;

        // ── Panneau récap (masqué par défaut) ────────────────────────────────
        var recap = BuildRecapPanel(root.transform);
        screen.recapPanel = recap.panel;
        screen.recapText  = recap.text;

        // ── Câblage timer ─────────────────────────────────────────────────────
        screen.timerIcon = tIcon;
        screen.timerFill = timerFillImg;
        screen.timerText = timerTextGO.GetComponent<TextMeshProUGUI>();

        root.SetActive(false);

        EditorUtility.SetDirty(screen);
        Selection.activeGameObject = root;
        Debug.Log("[OraclePassiveSelectionBuilder] PassiveSelectionScreen créé (désactivé par défaut).");

        EditorUtility.DisplayDialog("Oracle — Passif",
            "PassiveSelectionScreen créé (inactif par défaut).\n\n" +
            "• Assigne le PassivePool dans l'Inspector.\n" +
            "• Appelle screen.Show() depuis ton code de jeu pour l'afficher.\n" +
            "• Menu Oracle/Configure UI_Maj Sprites si les icônes HUD ne s'affichent pas.", "OK");
    }

    // ── Carte passif ──────────────────────────────────────────────────────────

    static PassiveCardUI BuildPassiveCard(Transform parent, int index, Sprite iconRim)
    {
        var cardGO = new GameObject($"PassiveCard_{index + 1}");
        Undo.RegisterCreatedObjectUndo(cardGO, "PassiveCard");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<RectTransform>().sizeDelta = new Vector2(CardW, CardH);

        var le = cardGO.AddComponent<LayoutElement>();
        le.preferredWidth  = CardW;
        le.preferredHeight = CardH;

        var cardImg = cardGO.AddComponent<Image>();
        cardImg.color = CardBg;

        var btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = cardImg;
        var cs = btn.colors;
        cs.highlightedColor = new Color(0.14f, 0.14f, 0.20f, 0.95f);
        btn.colors = cs;

        var card = cardGO.AddComponent<PassiveCardUI>();
        card.cardBackground = cardImg;

        var vl = cardGO.AddComponent<VerticalLayoutGroup>();
        vl.padding            = new RectOffset(10, 10, 12, 12);
        vl.spacing            = 8f;
        vl.childAlignment     = TextAnchor.UpperCenter;
        vl.childControlWidth  = true;
        vl.childControlHeight = false;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        // Icône avec cadre UI_ICONE_PASSIF
        float iconSize = 96f;
        var iconWrap = new GameObject("IconWrap");
        iconWrap.transform.SetParent(cardGO.transform, false);
        var iwRT = iconWrap.AddComponent<RectTransform>();
        iwRT.sizeDelta = new Vector2(iconSize, iconSize);
        var iwLE = iconWrap.AddComponent<LayoutElement>();
        iwLE.preferredWidth  = iconSize;
        iwLE.preferredHeight = iconSize;

        var plateBg = new GameObject("Plate");
        plateBg.transform.SetParent(iconWrap.transform, false);
        Stretch(plateBg.AddComponent<RectTransform>());
        var plateImg = plateBg.AddComponent<Image>();
        plateImg.color         = new Color(0.14f, 0.14f, 0.18f, 1f);
        plateImg.raycastTarget = false;

        // Frame EN PREMIER (derrière l'icône)
        if (iconRim != null)
        {
            var frameGO  = new GameObject("IconFrame");
            frameGO.transform.SetParent(iconWrap.transform, false);
            Stretch(frameGO.AddComponent<RectTransform>());
            var fImg = frameGO.AddComponent<Image>();
            fImg.sprite         = iconRim;
            fImg.preserveAspect = true;
            fImg.raycastTarget  = false;
        }

        // Icône PAR DESSUS la frame
        var iconHostGO = new GameObject("IconHost");
        iconHostGO.transform.SetParent(iconWrap.transform, false);
        var ihRT = iconHostGO.AddComponent<RectTransform>();
        ihRT.anchorMin = new Vector2(0.12f, 0.12f);
        ihRT.anchorMax = new Vector2(0.88f, 0.88f);
        ihRT.offsetMin = Vector2.zero;
        ihRT.offsetMax = Vector2.zero;

        var iconGO  = new GameObject("Icon");
        iconGO.transform.SetParent(iconHostGO.transform, false);
        Stretch(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color          = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;

        card.iconImage = iconImg;

        // Nom
        var nameGO  = new GameObject("PassiveName");
        nameGO.transform.SetParent(cardGO.transform, false);
        nameGO.AddComponent<RectTransform>().sizeDelta = new Vector2(CardW - 20f, 26f);
        nameGO.AddComponent<LayoutElement>().preferredHeight = 26f;
        var nameTmp = nameGO.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = "Passif";
        nameTmp.fontSize  = 14f;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color     = Accent;
        nameTmp.alignment = TextAlignmentOptions.Center;
        nameTmp.raycastTarget = false;
        card.nameText = nameTmp;

        // Description
        var descGO  = new GameObject("Description");
        descGO.transform.SetParent(cardGO.transform, false);
        descGO.AddComponent<RectTransform>().sizeDelta = new Vector2(CardW - 20f, 72f);
        descGO.AddComponent<LayoutElement>().preferredHeight = 72f;
        var descTmp = descGO.AddComponent<TextMeshProUGUI>();
        descTmp.text      = "";
        descTmp.fontSize  = 12f;
        descTmp.color     = new Color(0.78f, 0.78f, 0.82f, 1f);
        descTmp.alignment = TextAlignmentOptions.Top;
        descTmp.raycastTarget = false;
        card.descriptionText = descTmp;

        // Bordure de sélection
        var borderGO  = new GameObject("SelectionBorder");
        borderGO.transform.SetParent(cardGO.transform, false);
        Stretch(borderGO.AddComponent<RectTransform>());
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color         = Color.clear;
        borderImg.raycastTarget = false;
        card.selectionBorder = borderImg;

        return card;
    }

    // ── Bouton confirmer ──────────────────────────────────────────────────────

    static Button BuildConfirmButton(Transform parent)
    {
        var row = new GameObject("ConfirmRow");
        Undo.RegisterCreatedObjectUndo(row, "ConfirmRow");
        row.transform.SetParent(parent, false);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 52f;
        rowLE.flexibleWidth   = 1f;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                   = new RectOffset(0, 0, 0, 0);
        hlg.spacing                   = 0f;
        hlg.childAlignment            = TextAnchor.MiddleCenter;
        hlg.childControlWidth         = false;
        hlg.childControlHeight        = true;
        hlg.childForceExpandWidth     = false;
        hlg.childForceExpandHeight    = true;

        var btnGO = new GameObject("ConfirmButton");
        Undo.RegisterCreatedObjectUndo(btnGO, "ConfirmButton");
        btnGO.transform.SetParent(row.transform, false);
        var btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.sizeDelta = new Vector2(200f, 52f);
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth  = 200f;
        btnLE.preferredHeight = 52f;

        var img = btnGO.AddComponent<Image>();
        img.color = Accent;
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable  = false;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(btnGO.transform, false);
        Stretch(txtGO.AddComponent<RectTransform>());
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Confirmer";
        tmp.fontSize  = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = new Color(0.1f, 0.07f, 0.02f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        OracleEditorAsepriteFont.AssignIfAvailable(tmp);

        return btn;
    }

    // ── Panneau récap ─────────────────────────────────────────────────────────

    struct RecapW { public GameObject panel; public TextMeshProUGUI text; }

    static RecapW BuildRecapPanel(Transform parent)
    {
        var panelGO = new GameObject("RecapPanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "RecapPanel");
        panelGO.transform.SetParent(parent, false);
        var pRT = panelGO.AddComponent<RectTransform>();
        pRT.sizeDelta = new Vector2(400f, 100f);
        panelGO.AddComponent<LayoutElement>().preferredHeight = 100f;
        var pImg = panelGO.AddComponent<Image>();
        pImg.color = new Color(0.06f, 0.06f, 0.10f, 0.96f);

        var txtGO = new GameObject("RecapText");
        txtGO.transform.SetParent(panelGO.transform, false);
        Stretch(txtGO.AddComponent<RectTransform>());
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "";
        tmp.fontSize  = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Accent;
        tmp.alignment = TextAlignmentOptions.Center;

        panelGO.SetActive(false);
        return new RecapW { panel = panelGO, text = tmp };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RectTransform MakePanel(Transform parent, string name, float preferredH)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, preferredH);
        return rt;
    }

    static RectTransform MakeLE(RectTransform rt, float preferredH)
    {
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferredH;
        le.minHeight       = preferredH;
        return rt;
    }

    static GameObject AddLabel(Transform parent, string name, string text,
        float size, FontStyles style, Color col, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, height);
        go.AddComponent<LayoutElement>().preferredHeight = height;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = col;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return go;
    }

    static void Stretch(RectTransform rt)
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
