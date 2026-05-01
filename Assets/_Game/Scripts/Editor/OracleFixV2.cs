#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Oracle — Fix v2
/// 1. Assigne AllPassivesPool (le bon, avec les 10 passifs) sur PassiveSelectionScreen
/// 2. Crée un DeckUI dans DeckHost si absent, le branche sur CombatInitializer
///
/// Menu : Oracle/Fix v2 — PassivePool + DeckUI
/// </summary>
public static class OracleFixV2
{
    const string ALL_POOL = "Assets/_Game/ScriptableObjects/AllPassivesPool.asset";

    [MenuItem("Oracle/Fix v2 — PassivePool + DeckUI")]
    static void Run()
    {
        int fixes = 0;

        // ══════════════════════════════════════════════════════
        //  1. PASSIVE POOL — forcer AllPassivesPool
        // ══════════════════════════════════════════════════════
        var pss = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        if (pss == null)
            Debug.LogWarning("[OracleFixV2] Aucun PassiveSelectionScreen dans la scène.");
        else
        {
            var pool = AssetDatabase.LoadAssetAtPath<PassivePool>(ALL_POOL);
            if (pool == null)
            {
                // Chercher le pool avec le plus de passifs dans tout le projet
                PassivePool best = null;
                int bestCount = -1;
                foreach (var guid in AssetDatabase.FindAssets("t:PassivePool"))
                {
                    var p = AssetDatabase.LoadAssetAtPath<PassivePool>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p != null && p.allPassives.Count > bestCount)
                    {
                        best = p;
                        bestCount = p.allPassives.Count;
                    }
                }
                pool = best;
            }

            if (pool == null)
                Debug.LogError("[OracleFixV2] Aucun PassivePool trouvé dans le projet !");
            else if (pool.allPassives.Count == 0)
                Debug.LogError($"[OracleFixV2] {pool.name} est vide — ajoute les PassiveData manuellement dans l'Inspector.");
            else
            {
                Undo.RecordObject(pss, "Fix PassivePool");
                pss.passivePool = pool;
                EditorUtility.SetDirty(pss);
                fixes++;
                Debug.Log($"[OracleFixV2] PassivePool '{pool.name}' assigné ({pool.allPassives.Count} passifs).");
            }
        }

        // ══════════════════════════════════════════════════════
        //  2. DECK UI — créer si absent, placer dans DeckHost
        // ══════════════════════════════════════════════════════
        var existingDeck = Object.FindObjectOfType<DeckUI>(true);

        if (existingDeck != null)
        {
            // DeckUI existe — vérifier qu'il est bien dans DeckHost
            var deckHost = GameObject.Find("DeckHost");
            if (deckHost != null && existingDeck.transform.parent != deckHost.transform)
            {
                Undo.SetTransformParent(existingDeck.transform, deckHost.transform, "Move DeckUI to DeckHost");
                var drt = existingDeck.GetComponent<RectTransform>();
                StretchFull(drt);
                fixes++;
                Debug.Log("[OracleFixV2] DeckUI re-parenté dans DeckHost.");
            }
            else
                Debug.Log($"[OracleFixV2] DeckUI trouvé : '{existingDeck.name}'. OK.");
        }
        else
        {
            // DeckUI absent — le créer dans DeckHost
            var deckHost = GameObject.Find("DeckHost");
            if (deckHost == null)
            {
                Debug.LogError("[OracleFixV2] DeckHost introuvable — lance d'abord 'Minimalist UI — Rebuild Combat HUD'.");
            }
            else
            {
                var dGO = new GameObject("DeckUI");
                Undo.RegisterCreatedObjectUndo(dGO, "Create DeckUI");
                dGO.transform.SetParent(deckHost.transform, false);
                var drt = dGO.AddComponent<RectTransform>();
                StretchFull(drt);

                var deck = dGO.AddComponent<DeckUI>();

                // HorizontalLayoutGroup
                var hlg = dGO.AddComponent<HorizontalLayoutGroup>();
                hlg.padding                = new RectOffset(8, 8, 8, 8);
                hlg.spacing                = 10f;
                hlg.childAlignment         = TextAnchor.MiddleCenter;
                hlg.childControlWidth      = false;
                hlg.childControlHeight     = false;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = false;

                // Créer 8 SpellSlots basiques
                var hotkeys = new[] { "Q","W","E","R","A","S","D","F" };
                for (int i = 0; i < 8; i++)
                    deck.slots.Add(BuildBasicSlot(dGO.transform, i, hotkeys[i]));

                // Brancher tooltip si présent
                var tooltip = Object.FindObjectOfType<SpellTooltip>(true);
                if (tooltip != null) deck.tooltip = tooltip;

                fixes++;
                EditorUtility.SetDirty(deck);
                Debug.Log("[OracleFixV2] DeckUI créé dans DeckHost avec 8 slots.");

                // Brancher sur CombatInitializer
                var ci = Object.FindObjectOfType<CombatInitializer>(true);
                if (ci != null)
                {
                    Undo.RecordObject(ci, "Assign DeckUI");
                    ci.deckUI = deck;
                    EditorUtility.SetDirty(ci);
                    fixes++;
                    Debug.Log("[OracleFixV2] DeckUI branché sur CombatInitializer.");
                }
            }
        }

        // ══════════════════════════════════════════════════════
        //  3. COMBATINITIALIZER — vérifier passiveSelectionScreen
        // ══════════════════════════════════════════════════════
        var combatInit = Object.FindObjectOfType<CombatInitializer>(true);
        if (combatInit != null && combatInit.passiveSelectionScreen == null)
        {
            var pss2 = Object.FindObjectOfType<PassiveSelectionScreen>(true);
            if (pss2 != null)
            {
                Undo.RecordObject(combatInit, "Assign PSS");
                combatInit.passiveSelectionScreen = pss2;
                EditorUtility.SetDirty(combatInit);
                fixes++;
                Debug.Log("[OracleFixV2] PassiveSelectionScreen branché sur CombatInitializer.");
            }
        }

        // ══════════════════════════════════════════════════════
        //  Résumé
        // ══════════════════════════════════════════════════════
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Oracle — Fix v2",
            fixes > 0
                ? $"{fixes} correction(s) appliquée(s) :\n\n" +
                  "• AllPassivesPool assigné sur PassiveSelectionScreen\n" +
                  "• DeckUI créé/repositionné dans DeckHost\n" +
                  "• Références CombatInitializer vérifiées\n\n" +
                  "Lance Play !"
                : "Tout était déjà correct.",
            "OK");
    }

    // ── Slot sort minimal ─────────────────────────────────────
    static SpellSlotUI BuildBasicSlot(Transform parent, int index, string hotkey)
    {
        const float W = 96f, H = 128f;
        var go = new GameObject($"SpellSlot_{index + 1}");
        Undo.RegisterCreatedObjectUndo(go, "SpellSlot");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(W, H);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = W;
        le.preferredHeight = H;
        le.flexibleWidth   = 0f;

        // Zone de clic
        var hit = go.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0.001f);

        var slot = go.AddComponent<SpellSlotUI>();

        // Fond
        var bg = Child(go.transform, "Backing");
        Stretch(bg.AddComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.13f, 0.95f);

        // Icône
        var iconArea = AnchoredChild(go.transform, "IconArea",
            new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.90f));
        var iconGO = Child(iconArea.transform, "Icon");
        Stretch(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        slot.iconImage = iconImg;

        // DimOverlay
        var dim = Child(iconArea.transform, "DimOverlay");
        Stretch(dim.AddComponent<RectTransform>());
        var dimImg = dim.AddComponent<Image>();
        dimImg.color     = new Color(0f, 0f, 0f, 0.55f);
        dimImg.enabled   = false;
        dimImg.raycastTarget = false;
        slot.dimOverlay = dimImg;

        // PACost badge
        var pa = AnchoredChildRT(go.transform, "PACost",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(22f, 22f));
        pa.gameObject.AddComponent<Image>().color = new Color(0.31f, 0.80f, 0.77f);
        var paTmp = pa.gameObject.AddComponent<TextMeshProUGUI>();
        paTmp.fontSize = 11f; paTmp.fontStyle = FontStyles.Bold;
        paTmp.alignment = TextAlignmentOptions.Center;
        paTmp.color = new Color(0.05f, 0.05f, 0.08f);
        paTmp.text = "0"; paTmp.raycastTarget = false;
        slot.paCostText = paTmp;

        // Hotkey
        var hk = AnchoredChildRT(go.transform, "Hotkey",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(5f, 4f), new Vector2(28f, 18f));
        var hkTmp = hk.gameObject.AddComponent<TextMeshProUGUI>();
        hkTmp.fontSize = 10f; hkTmp.fontStyle = FontStyles.Bold;
        hkTmp.alignment = TextAlignmentOptions.Left;
        hkTmp.color = new Color(1f, 1f, 1f, 0.35f);
        hkTmp.text = hotkey; hkTmp.raycastTarget = false;
        slot.hotkeyText = hkTmp;

        // SelectionBorder
        var sel = Child(go.transform, "SelectionBorder");
        Stretch(sel.AddComponent<RectTransform>());
        var selImg = sel.AddComponent<Image>();
        selImg.color = new Color(0, 0, 0, 0);
        selImg.enabled = false; selImg.raycastTarget = false;
        slot.selectionBorder = selImg;

        return slot;
    }

    // ── Helpers ───────────────────────────────────────────────
    static GameObject Child(Transform p, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(p, false);
        return go;
    }

    static GameObject AnchoredChild(Transform p, string name, Vector2 ancMin, Vector2 ancMax)
    {
        var go = Child(p, name);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    static RectTransform AnchoredChildRT(Transform p, string name,
        Vector2 ancMin, Vector2 ancMax, Vector2 pos, Vector2 size)
    {
        var go = Child(p, name);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        rt.pivot = ancMin; // pivot = anchor pour positionnement naturel
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = rt.anchoredPosition = Vector2.zero;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
    }
}
#endif
