#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Oracle Setup Wizard — Étape 2.5
/// Menu : Oracle > Setup Wizard
/// Automatise l'injection et le câblage de tous les composants
/// combat sur les GameObjects de la scène.
/// </summary>
public class OracleSetupWizard : EditorWindow
{
    // =========================================================
    // ÉTAT DE LA FENÊTRE
    // =========================================================
    private CharacterStats selectedStats;
    private DeckData       selectedDeck;
    private string         playerGoName = "Player";

    private Vector2 scrollPos;
    private bool    showVerification = true;

    // Palette dark fantasy
    private static readonly Color ColorHeader  = new Color(0.12f, 0.08f, 0.04f, 1f);
    private static readonly Color ColorSection = new Color(0.18f, 0.12f, 0.06f, 1f);
    private static readonly Color ColorGold    = new Color(0.79f, 0.66f, 0.30f, 1f);
    private static readonly Color ColorGreen   = new Color(0.20f, 0.75f, 0.20f, 1f);
    private static readonly Color ColorRed     = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color ColorOrange  = new Color(0.90f, 0.50f, 0.10f, 1f);

    // =========================================================
    // OUVERTURE
    // =========================================================
    [MenuItem("Oracle/Setup Wizard")]
    public static void OpenWindow()
    {
        var window = GetWindow<OracleSetupWizard>("Oracle — Setup Wizard");
        window.minSize = new Vector2(420f, 600f);
        window.Show();
    }

    // =========================================================
    // GUI PRINCIPAL
    // =========================================================
    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();
        GUILayout.Space(6f);

        DrawSectionPersonnage();
        GUILayout.Space(6f);

        DrawSectionUI();
        GUILayout.Space(6f);

        DrawSectionCombat();
        GUILayout.Space(6f);

        DrawSectionOneClick();
        GUILayout.Space(10f);

        DrawSectionVerification();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // HEADER
    // =========================================================
    void DrawHeader()
    {
        var bg = MakeTex(1, 1, ColorHeader);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.fontSize = 16;
        headerStyle.normal.textColor = ColorGold;
        headerStyle.normal.background = bg;
        headerStyle.padding = new RectOffset(0, 0, 10, 10);
        GUILayout.Label("⚔  ORACLE — SETUP WIZARD  ⚔", headerStyle);
        GUILayout.Label("Injection automatique des composants combat", CenteredLabel());
    }

    // =========================================================
    // SECTION — PERSONNAGE
    // =========================================================
    void DrawSectionPersonnage()
    {
        DrawSectionTitle("👤  PERSONNAGE JOUEUR");

        playerGoName  = EditorGUILayout.TextField("Nom du GameObject", playerGoName);
        selectedStats = (CharacterStats)EditorGUILayout.ObjectField(
            "CharacterStats (SO)", selectedStats, typeof(CharacterStats), false);
        selectedDeck  = (DeckData)EditorGUILayout.ObjectField(
            "DeckData (SO)", selectedDeck, typeof(DeckData), false);

        GUILayout.Space(4f);
        DrawHelpBox("Crée un GameObject '" + playerGoName + "' avec TacticalCharacter," +
                    " SpellCaster, PassiveManager et SpriteRenderer.");

        if (GoldButton("Créer personnage joueur"))
            SetupPlayer();
    }

    // =========================================================
    // SECTION — UI COMBAT
    // =========================================================
    void DrawSectionUI()
    {
        DrawSectionTitle("🖥  UI DE COMBAT");
        DrawHelpBox("Crée ou complète le Canvas avec DeckUI (jusqu'à 8 slots, QWERASDF)," +
                    " SpellTooltip, TimerUI et PassiveSelectionScreen.");

        if (GoldButton("Setup UI Combat"))
            SetupCombatUI();

        GUILayout.Space(4f);
        DrawHelpBox("4.2.1 — Layout complet : barres HP + timer haut-droite + bas (passif, PA/PM, Deck, Fin de tour). " +
                    "Ré-parente DeckUI et TimerUI.");
        if (GoldButton("Build Combat HUD (4.2.1)"))
            OracleCombatHUDBuilder.Build();
    }

    // =========================================================
    // SECTION — GRILLE & COMBAT
    // =========================================================
    void DrawSectionCombat()
    {
        DrawSectionTitle("🗺  GRILLE & COMBAT");
        DrawHelpBox("Ajoute GridManager et TurnManager dans la scène s'ils sont absents.");

        if (GoldButton("Setup Grille & Combat"))
            SetupGridAndCombat();
    }

    // =========================================================
    // SECTION — ONE-CLICK
    // =========================================================
    void DrawSectionOneClick()
    {
        DrawSectionTitle("⚡  TOUT INJECTER");
        DrawHelpBox("Exécute les trois étapes ci-dessus dans l'ordre.");

        GUI.backgroundColor = ColorOrange;
        var bigBtn = new GUIStyle(GUI.skin.button);
        bigBtn.fontSize = 13;
        bigBtn.fontStyle = FontStyle.Bold;
        bigBtn.fixedHeight = 36f;
        if (GUILayout.Button("▶  TOUT INJECTER  ◀", bigBtn))
        {
            SetupPlayer();
            SetupCombatUI();
            SetupGridAndCombat();
            Debug.Log("[OracleSetupWizard] Injection complète terminée.");
        }
        GUI.backgroundColor = Color.white;
    }

    // =========================================================
    // SECTION — VÉRIFICATION
    // =========================================================
    void DrawSectionVerification()
    {
        showVerification = EditorGUILayout.Foldout(showVerification, "🔍  VÉRIFICATION DE LA SCÈNE", true);
        if (!showVerification) return;

        EditorGUILayout.BeginVertical(GUI.skin.box);

        var issues = CollectIssues();
        if (issues.Count == 0)
        {
            var ok = new GUIStyle(EditorStyles.label);
            ok.normal.textColor = ColorGreen;
            GUILayout.Label("✔  Aucun problème détecté — scène prête !", ok);
        }
        else
        {
            var warn = new GUIStyle(EditorStyles.label);
            warn.normal.textColor = ColorRed;
            warn.wordWrap = true;
            foreach (var issue in issues)
                GUILayout.Label("✖  " + issue, warn);
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("Rafraîchir"))
            Repaint();

        EditorGUILayout.EndVertical();
    }

    // =========================================================
    // LOGIQUE — SETUP PERSONNAGE
    // =========================================================
    void SetupPlayer()
    {
        // Chercher ou créer le GO
        GameObject go = GameObject.Find(playerGoName);
        if (go == null)
        {
            go = new GameObject(playerGoName);
            Undo.RegisterCreatedObjectUndo(go, "Create Player GO");
            Debug.Log($"[OracleSetupWizard] GameObject '{playerGoName}' créé.");
        }

        // TacticalCharacter
        var tc = EnsureComponent<TacticalCharacter>(go, "TacticalCharacter");
        if (selectedStats != null && tc.stats == null)
        {
            tc.stats = selectedStats;
            EditorUtility.SetDirty(tc);
        }
        if (selectedDeck != null && tc.deck == null)
        {
            tc.deck = selectedDeck;
            EditorUtility.SetDirty(tc);
        }

        // SpriteRenderer
        var sr = EnsureComponent<SpriteRenderer>(go, "SpriteRenderer");
        if (tc.spriteRenderer == null)
        {
            tc.spriteRenderer = sr;
            EditorUtility.SetDirty(tc);
        }

        // SpellCaster
        EnsureComponent<SpellCaster>(go, "SpellCaster");

        EnsureComponent<SpellAnimator>(go, "SpellAnimator (optionnel VFX)");

        // PassiveManager
        EnsureComponent<PassiveManager>(go, "PassiveManager");

        Selection.activeGameObject = go;
        Debug.Log($"[OracleSetupWizard] Personnage '{playerGoName}' configuré avec succès.");
    }

    // =========================================================
    // LOGIQUE — SETUP UI COMBAT
    // =========================================================
    void SetupCombatUI()
    {
        // Canvas — chercher ou créer
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            Debug.Log("[OracleSetupWizard] Canvas créé.");
        }

        // ── DeckUI ──────────────────────────────────────────
        DeckUI deckUI = FindObjectOfType<DeckUI>(true);
        if (deckUI == null)
        {
            var deckGO = CreateUIPanel(canvas.transform, "DeckUI",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 80f));
            deckUI = deckGO.AddComponent<DeckUI>();
            Undo.RegisterCreatedObjectUndo(deckGO, "Create DeckUI");
            Debug.Log("[OracleSetupWizard] DeckUI créé.");

            // 6 SpellSlotUI
            for (int i = 0; i < 6; i++)
            {
                var slotGO = CreateUIPanel(deckGO.transform, $"SpellSlot_{i + 1}",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(64f, 64f));
                var rt = slotGO.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(8f + i * 72f, 8f);
                var slot = slotGO.AddComponent<SpellSlotUI>();

                // Icône principale
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(slotGO.transform, false);
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.color = Color.white;
                StretchFull(iconGO.GetComponent<RectTransform>());
                slot.iconImage = iconImg;

                // Overlay dim
                var dimGO = new GameObject("DimOverlay");
                dimGO.transform.SetParent(slotGO.transform, false);
                var dimImg = dimGO.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.55f);
                dimImg.enabled = false;
                StretchFull(dimGO.GetComponent<RectTransform>());
                slot.dimOverlay = dimImg;

                // Texte PA
                var paGO = CreateTMPLabel(slotGO.transform, "PACost", $"");
                paGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(4f, 46f);
                slot.paCostText = paGO.GetComponent<TextMeshProUGUI>();

                // Texte hotkey
                var hkGO = CreateTMPLabel(slotGO.transform, "Hotkey", $"{i + 1}");
                hkGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(4f, 4f);
                slot.hotkeyText = hkGO.GetComponent<TextMeshProUGUI>();

                deckUI.slots.Add(slot);
                Undo.RegisterCreatedObjectUndo(slotGO, $"Create SpellSlot_{i + 1}");
            }
        }

        // ── SpellTooltip ─────────────────────────────────────
        SpellTooltip tooltip = FindObjectOfType<SpellTooltip>(true);
        if (tooltip == null)
        {
            var ttGO = CreateUIPanel(canvas.transform, "SpellTooltip",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-120f, 90f), new Vector2(240f, 160f));
            tooltip = ttGO.AddComponent<SpellTooltip>();
            ttGO.SetActive(false);
            tooltip.tooltipPanel = ttGO.GetComponent<RectTransform>();
            tooltip.rootCanvas    = canvas;

            tooltip.spellNameText  = AddTMPToPanel(ttGO.transform, "SpellName",   new Vector2(0f, 120f), 14, FontStyles.Bold);
            tooltip.paCostText     = AddTMPToPanel(ttGO.transform, "PACost",      new Vector2(0f, 100f), 11, FontStyles.Normal);
            tooltip.rangeText      = AddTMPToPanel(ttGO.transform, "Range",       new Vector2(0f,  80f), 11, FontStyles.Normal);
            tooltip.cooldownText   = AddTMPToPanel(ttGO.transform, "Cooldown",    new Vector2(0f,  60f), 11, FontStyles.Normal);
            tooltip.descriptionText= AddTMPToPanel(ttGO.transform, "Description", new Vector2(0f,  30f), 10, FontStyles.Normal);
            tooltip.synergyText    = AddTMPToPanel(ttGO.transform, "Synergy",     new Vector2(0f,  10f), 10, FontStyles.Italic);

            if (deckUI != null) deckUI.tooltip = tooltip;
            Undo.RegisterCreatedObjectUndo(ttGO, "Create SpellTooltip");
            Debug.Log("[OracleSetupWizard] SpellTooltip créé.");
        }

        // ── TimerUI ──────────────────────────────────────────
        TimerUI timerUI = FindObjectOfType<TimerUI>(true);
        if (timerUI == null)
        {
            var timerGO = CreateUIPanel(canvas.transform, "TimerUI",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -10f), new Vector2(56f, 56f));
            var timerRtSetup = timerGO.GetComponent<RectTransform>();
            timerRtSetup.pivot = new Vector2(1f, 1f);
            timerUI = timerGO.AddComponent<TimerUI>();

            var iconGO = new GameObject("TimerIcon");
            iconGO.transform.SetParent(timerGO.transform, false);
            StretchFull(iconGO.AddComponent<RectTransform>());
            var iconImg = iconGO.AddComponent<Image>();
            var tSp = OracleUIMajTextureSetup.TryLoadSprite("timer_icon_hud");
            if (tSp != null) iconImg.sprite = tSp;
            iconImg.preserveAspect = true;
            timerUI.timerIconImage = iconImg;

            var fillGO  = new GameObject("Fill");
            fillGO.transform.SetParent(timerGO.transform, false);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = false;
            fillImg.fillAmount = 1f;
            fillImg.color      = new Color(0.20f, 0.80f, 0.20f, 0.45f);
            StretchFull(fillGO.GetComponent<RectTransform>());
            timerUI.fillImage = fillImg;

            var timeGO = new GameObject("TimeText");
            timeGO.transform.SetParent(timerGO.transform, false);
            StretchFull(timeGO.AddComponent<RectTransform>());
            var timeTmp = timeGO.AddComponent<TextMeshProUGUI>();
            timeTmp.fontSize     = 18f;
            timeTmp.fontStyle    = FontStyles.Bold;
            timeTmp.alignment    = TextAlignmentOptions.Center;
            timeTmp.color        = Color.white;
            timeTmp.raycastTarget = false;
            timerUI.timeText = timeTmp;

            Undo.RegisterCreatedObjectUndo(timerGO, "Create TimerUI");
            Debug.Log("[OracleSetupWizard] TimerUI créé.");
        }

        // ── PassiveSelectionScreen ───────────────────────────
        PassiveSelectionScreen pss = FindObjectOfType<PassiveSelectionScreen>(true);
        if (pss == null)
        {
            var pssGO = CreateUIPanel(canvas.transform, "PassiveSelectionScreen",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            pssGO.SetActive(false);
            pss = pssGO.AddComponent<PassiveSelectionScreen>();

            pss.timerFill = null;
            pss.timerText = null;

            Undo.RegisterCreatedObjectUndo(pssGO, "Create PassiveSelectionScreen");
            Debug.Log("[OracleSetupWizard] PassiveSelectionScreen créé (références à câbler manuellement).");
        }

        Debug.Log("[OracleSetupWizard] UI Combat configurée avec succès.");
    }

    // =========================================================
    // LOGIQUE — SETUP GRILLE & COMBAT
    // =========================================================
    void SetupGridAndCombat()
    {
        // GridManager
        if (FindObjectOfType<GridManager>() == null)
        {
            var go = new GameObject("GridManager");
            go.AddComponent<GridManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create GridManager");
            Debug.Log("[OracleSetupWizard] GridManager ajouté — assigne un GridConfig dans l'Inspector !");
        }
        else
        {
            Debug.Log("[OracleSetupWizard] GridManager déjà présent, ignoré.");
        }

        // TurnManager
        if (FindObjectOfType<TurnManager>() == null)
        {
            var go = new GameObject("TurnManager");
            go.AddComponent<TurnManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create TurnManager");
            Debug.Log("[OracleSetupWizard] TurnManager ajouté.");
        }
        else
        {
            Debug.Log("[OracleSetupWizard] TurnManager déjà présent, ignoré.");
        }
    }

    // =========================================================
    // VÉRIFICATION DE LA SCÈNE
    // =========================================================
    List<string> CollectIssues()
    {
        var issues = new List<string>();

        if (FindObjectOfType<GridManager>() == null)
            issues.Add("GridManager absent de la scène.");
        else if (FindObjectOfType<GridManager>().config == null)
            issues.Add("GridManager présent mais GridConfig non assigné.");

        if (FindObjectOfType<TurnManager>() == null)
            issues.Add("TurnManager absent de la scène.");

        if (FindObjectOfType<Canvas>() == null)
            issues.Add("Aucun Canvas dans la scène.");

        if (FindObjectOfType<DeckUI>(true) == null)
            issues.Add("DeckUI absent de la scène.");
        else
        {
            var deckUI = FindObjectOfType<DeckUI>(true);
            if (deckUI.slots == null || deckUI.slots.Count == 0)
                issues.Add("DeckUI : aucun slot SpellSlotUI assigné.");
            if (deckUI.tooltip == null)
                issues.Add("DeckUI : référence SpellTooltip manquante.");
        }

        if (FindObjectOfType<TimerUI>(true) == null)
            issues.Add("TimerUI absent de la scène.");

        if (FindObjectOfType<PassiveSelectionScreen>(true) == null)
            issues.Add("PassiveSelectionScreen absent de la scène.");

        // Personnages
        var chars = FindObjectsOfType<TacticalCharacter>();
        if (chars.Length == 0)
        {
            issues.Add("Aucun TacticalCharacter dans la scène.");
        }
        else
        {
            foreach (var tc in chars)
            {
                if (tc.stats == null)
                    issues.Add($"TacticalCharacter '{tc.name}' : CharacterStats non assigné.");
                if (tc.spriteRenderer == null)
                    issues.Add($"TacticalCharacter '{tc.name}' : SpriteRenderer non référencé.");
                if (tc.GetComponent<SpellCaster>() == null)
                    issues.Add($"'{tc.name}' : SpellCaster manquant.");
                if (tc.GetComponent<PassiveManager>() == null)
                    issues.Add($"'{tc.name}' : PassiveManager manquant.");
            }
        }

        return issues;
    }

    // =========================================================
    // UTILITAIRES — COMPOSANTS
    // =========================================================
    static T EnsureComponent<T>(GameObject go, string label) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null)
        {
            c = Undo.AddComponent<T>(go);
            Debug.Log($"[OracleSetupWizard] {label} ajouté sur '{go.name}'.");
        }
        return c;
    }

    // =========================================================
    // UTILITAIRES — UI
    // =========================================================
    static GameObject CreateUIPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin       = anchorMin;
        rt.anchorMax       = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta       = sizeDelta;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
    }

    static GameObject CreateTMPLabel(Transform parent, string name, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 20f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 10f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static TextMeshProUGUI AddTMPToPanel(Transform parent, string name,
        Vector2 anchoredPos, float fontSize, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta       = new Vector2(220f, 22f);
        rt.anchoredPosition = anchoredPos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize   = fontSize;
        tmp.fontStyle  = style;
        tmp.color      = Color.white;
        tmp.alignment  = TextAlignmentOptions.Left;
        return tmp;
    }

    // =========================================================
    // UTILITAIRES — STYLE GUI
    // =========================================================
    void DrawSectionTitle(string title)
    {
        var bg = MakeTex(1, 1, ColorSection);
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = ColorGold;
        style.normal.background = bg;
        style.padding = new RectOffset(6, 0, 6, 6);
        style.fontSize = 12;
        GUILayout.Label(title, style);
    }

    static void DrawHelpBox(string msg)
    {
        EditorGUILayout.HelpBox(msg, MessageType.None);
    }

    static bool GoldButton(string label)
    {
        GUI.backgroundColor = ColorGold;
        bool pressed = GUILayout.Button(label, GUILayout.Height(28f));
        GUI.backgroundColor = Color.white;
        return pressed;
    }

    static GUIStyle CenteredLabel()
    {
        var s = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        s.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        return s;
    }

    static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
#endif
