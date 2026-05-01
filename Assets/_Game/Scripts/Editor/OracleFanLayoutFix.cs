#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Positionne les 6 cartes en éventail centré-bas, 3/4 visibles.
/// Menu : Oracle/FIX — Recentrer cartes éventail
/// </summary>
public static class OracleFanLayoutFix
{
    // ── Paramètres calqués sur l'image de référence ──────────
    const float CardW      = 156f;   // largeur carte
    const float CardH      = 240f;   // hauteur carte
    const float Spread     = 500f;   // écart total gauche-droite
    const float AngleMax   = 14f;    // inclinaison max aux bords
    const float ArcHeight  = 18f;    // bosse parabolique au centre
    const float BaseY      = -130f;  // décalage bas (négatif = 3/4 cachée)
    const float RaiseDelta = 160f;   // montée au hover (sort bien)
    const int   MaxSlots   = 6;

    [MenuItem("Oracle/FIX — Recentrer cartes éventail")]
    public static void Run()
    {
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck == null)
        {
            EditorUtility.DisplayDialog("Oracle Fan Fix", "Aucun DeckUI dans la scène.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(deck.gameObject, "Fan Layout Fix");

        // 1. Supprimer HorizontalLayoutGroup
        var hlg = deck.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) { Undo.DestroyObjectImmediate(hlg); Debug.Log("[FanFix] HLG supprimé."); }

        var le = deck.GetComponent<LayoutElement>();
        if (le != null) Undo.DestroyObjectImmediate(le);

        // 2. Stretch DeckUI sur tout le canvas
        var deckRT = deck.GetComponent<RectTransform>();
        Undo.RecordObject(deckRT, "Stretch DeckUI");
        deckRT.anchorMin = Vector2.zero; deckRT.anchorMax = Vector2.one;
        deckRT.offsetMin = Vector2.zero; deckRT.offsetMax = Vector2.zero;
        deckRT.anchoredPosition = Vector2.zero; deckRT.localScale = Vector3.one;

        // 3. Sync paramètres DeckUI
        Undo.RecordObject(deck, "DeckUI fan params");
        deck.fanMode      = true;
        deck.fanCardW     = CardW;  deck.fanCardH     = CardH;
        deck.fanSpread    = Spread; deck.fanAngleMax  = AngleMax;
        deck.fanArcHeight = ArcHeight; deck.fanBaseY  = BaseY;
        deck.fanRaise     = RaiseDelta;

        // 4. Désactiver les slots > 6
        for (int i = 0; i < deck.slots.Count; i++)
        {
            if (deck.slots[i] == null) continue;
            bool active = i < MaxSlots;
            if (deck.slots[i].gameObject.activeSelf != active)
            {
                Undo.RecordObject(deck.slots[i].gameObject, "Toggle slot");
                deck.slots[i].gameObject.SetActive(active);
            }
        }

        // 5. Collecter les 6 slots actifs
        var active6 = new System.Collections.Generic.List<SpellSlotUI>();
        foreach (var s in deck.slots)
            if (s != null && s.gameObject.activeSelf && active6.Count < MaxSlots)
                active6.Add(s);

        int n = active6.Count;
        if (n == 0) { EditorUtility.DisplayDialog("Oracle Fan Fix", "Aucun slot actif.", "OK"); return; }

        // Ordre sibling : bords derrière, centre devant
        var order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => {
            float da = Mathf.Abs((float)a / Mathf.Max(1, n-1) - 0.5f);
            float db = Mathf.Abs((float)b / Mathf.Max(1, n-1) - 0.5f);
            return db.CompareTo(da);
        });
        for (int j = 0; j < n; j++) active6[order[j]].transform.SetSiblingIndex(j);

        // 6. Positionner chaque slot
        for (int i = 0; i < n; i++)
        {
            var slot = active6[i];
            var rt   = slot.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fan pos");

            float t     = n > 1 ? (float)i / (n-1) : 0.5f;
            float x     = Mathf.Lerp(-Spread*0.5f, Spread*0.5f, t);
            float norm  = 2f * Mathf.Abs(t - 0.5f);
            float arcY  = ArcHeight * (1f - norm*norm);
            float angle = Mathf.Lerp(AngleMax, -AngleMax, t);

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta        = new Vector2(CardW, CardH);
            rt.anchoredPosition = new Vector2(x, BaseY + arcY);
            rt.localRotation    = Quaternion.Euler(0f, 0f, angle);
            rt.localScale       = Vector3.one;

            var sle = slot.GetComponent<LayoutElement>();
            if (sle != null) Undo.DestroyObjectImmediate(sle);

            Undo.RecordObject(slot, "RestPose");
            slot.SetRestPose(new Vector2(x, BaseY+arcY), angle, new Vector2(CardW, CardH), RaiseDelta);

            // S'assurer que la carte a une Image raycastable pour le hover
            var img = slot.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Raycast");
                img.raycastTarget = true;
            }

            EditorUtility.SetDirty(slot.gameObject);
        }

        // 7. Vérifier GraphicRaycaster sur le Canvas
        var canvas = deck.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                Debug.Log("[FanFix] GraphicRaycaster ajouté au Canvas.");
            }
        }

        EditorUtility.SetDirty(deck.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[FanFix] {n} cartes OK. BaseY={BaseY}, RaiseDelta={RaiseDelta}");
        EditorUtility.DisplayDialog("Oracle Fan Fix",
            $"OK — {n}/6 cartes positionnées !\n\n" +
            $"• Taille : {CardW}x{CardH}px\n" +
            $"• 3/4 visibles (BaseY={BaseY})\n" +
            $"• Hover : monte {RaiseDelta}px + zoom x1.25\n\n" +
            "Ctrl+S puis Play.", "OK");
    }
}
#endif
