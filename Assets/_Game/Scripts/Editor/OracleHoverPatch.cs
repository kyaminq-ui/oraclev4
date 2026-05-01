#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Patch complet hover + éventail.
/// Menu : Oracle/PATCH — Hover & Éventail
/// </summary>
public static class OracleHoverPatch
{
    // ── Paramètres éventail ──────────────────────────────────────
    const float CardW      = 156f;
    const float CardH      = 240f;
    const float Spread     = 560f;
    const float AngleMax   = 18f;
    const float ArcHeight  = 35f;
    const float BaseY      = -110f;
    const float RaiseDelta = 170f;
    const int   MaxSlots   = 6;

    [MenuItem("Oracle/PATCH — Hover & Éventail")]
    public static void Run()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("[OracleHoverPatch] Démarrage…\n");

        // ── 1. EventSystem ───────────────────────────────────────
        var es = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            var esGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGO, "EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            log.AppendLine("✔ EventSystem créé (manquait).");
        }
        else
        {
            log.AppendLine("✔ EventSystem présent.");
        }

        // ── 2. Canvas + GraphicRaycaster ─────────────────────────
        var canvases = Object.FindObjectsOfType<Canvas>();
        int raycasterFixed = 0;
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                var gr = canvas.GetComponent<GraphicRaycaster>();
                if (gr == null)
                {
                    Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
                    raycasterFixed++;
                }
            }
        }
        log.AppendLine(raycasterFixed > 0
            ? $"✔ GraphicRaycaster ajouté sur {raycasterFixed} Canvas."
            : "✔ GraphicRaycaster déjà présent sur tous les Canvas.");

        // ── 3. DeckUI ────────────────────────────────────────────
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck == null)
        {
            EditorUtility.DisplayDialog("Oracle Hover Patch",
                "Aucun DeckUI dans la scène.\nOuvre la scène de combat et relance.", "OK");
            return;
        }

        Undo.RecordObject(deck, "DeckUI fan params");
        deck.fanMode      = true;
        deck.fanCardW     = CardW;
        deck.fanCardH     = CardH;
        deck.fanSpread    = Spread;
        deck.fanAngleMax  = AngleMax;
        deck.fanArcHeight = ArcHeight;
        deck.fanBaseY     = BaseY;
        deck.fanRaise     = RaiseDelta;
        log.AppendLine("✔ DeckUI.fanMode = true, paramètres éventail mis à jour.");

        // Supprimer HorizontalLayoutGroup sur DeckUI
        var hlg = deck.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
        {
            Undo.DestroyObjectImmediate(hlg);
            log.AppendLine("✔ HorizontalLayoutGroup supprimé du DeckUI.");
        }

        // Supprimer LayoutElement sur DeckUI
        var le = deck.GetComponent<LayoutElement>();
        if (le != null) Undo.DestroyObjectImmediate(le);

        // Étirer DeckUI sur tout le Canvas
        var deckRT = deck.GetComponent<RectTransform>();
        Undo.RecordObject(deckRT, "Stretch DeckUI");
        deckRT.anchorMin        = Vector2.zero;
        deckRT.anchorMax        = Vector2.one;
        deckRT.offsetMin        = Vector2.zero;
        deckRT.offsetMax        = Vector2.zero;
        deckRT.anchoredPosition = Vector2.zero;
        deckRT.localScale       = Vector3.one;
        log.AppendLine("✔ DeckUI étiré sur tout le Canvas.");

        // ── 4. Collecter les slots actifs (max 6) ────────────────
        var active = new List<SpellSlotUI>();
        foreach (var s in deck.slots)
            if (s != null && s.gameObject.activeSelf && active.Count < MaxSlots)
                active.Add(s);

        // Désactiver les slots > MaxSlots
        for (int i = MaxSlots; i < deck.slots.Count; i++)
            if (deck.slots[i] != null && deck.slots[i].gameObject.activeSelf)
            {
                Undo.RecordObject(deck.slots[i].gameObject, "Disable extra slot");
                deck.slots[i].gameObject.SetActive(false);
            }

        int n = active.Count;
        log.AppendLine($"✔ {n} slot(s) actifs détectés.");

        if (n == 0)
        {
            EditorUtility.DisplayDialog("Oracle Hover Patch",
                "Aucun SpellSlotUI actif trouvé dans DeckUI.\nVérifie que les slots sont dans la liste.", "OK");
            return;
        }

        // ── 5. Ordre sibling (bords derrière, centre devant) ─────
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => {
            float da = Mathf.Abs((float)a / Mathf.Max(1, n - 1) - 0.5f);
            float db = Mathf.Abs((float)b / Mathf.Max(1, n - 1) - 0.5f);
            return db.CompareTo(da);
        });
        for (int j = 0; j < n; j++)
            active[order[j]].transform.SetSiblingIndex(j);

        // ── 6. Positionner chaque slot ───────────────────────────
        int raycastFixed = 0;
        int leRemoved    = 0;

        for (int i = 0; i < n; i++)
        {
            var slot = active[i];
            Undo.RegisterCompleteObjectUndo(slot.gameObject, "Patch slot");

            var rt = slot.GetComponent<RectTransform>();
            Undo.RecordObject(rt, "Fan RT");

            float t     = n > 1 ? (float)i / (n - 1) : 0.5f;
            float x     = Mathf.Lerp(-Spread * 0.5f, Spread * 0.5f, t);
            float norm  = 2f * Mathf.Abs(t - 0.5f);
            float arcY  = ArcHeight * (1f - norm * norm);
            float angle = Mathf.Lerp(AngleMax, -AngleMax, t);

            // Pivot bas-centre — OBLIGATOIRE pour l'arc
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.sizeDelta        = new Vector2(CardW, CardH);
            rt.anchoredPosition = new Vector2(x, BaseY + arcY);
            rt.localRotation    = Quaternion.Euler(0f, 0f, angle);
            rt.localScale       = Vector3.one;

            // Supprimer LayoutElement sur le slot
            var sle = slot.GetComponent<LayoutElement>();
            if (sle != null) { Undo.DestroyObjectImmediate(sle); leRemoved++; }

            // Initialiser le RestPose (active le hover)
            Undo.RecordObject(slot, "SetRestPose");
            slot.SetRestPose(new Vector2(x, BaseY + arcY), angle,
                             new Vector2(CardW, CardH), RaiseDelta);

            // ── RAYCAST : s'assurer qu'il existe un Image raycastable ──
            // On cherche d'abord une Image directement sur le slot root
            var rootImg = slot.GetComponent<Image>();
            if (rootImg == null)
            {
                // En créer une invisible mais raycastable
                rootImg = Undo.AddComponent<Image>(slot.gameObject);
                rootImg.color         = new Color(0f, 0f, 0f, 0.004f);
                rootImg.raycastTarget = true;
                raycastFixed++;
                log.AppendLine($"  + Image raycastable ajoutée sur {slot.gameObject.name}");
            }
            else
            {
                Undo.RecordObject(rootImg, "Raycast");
                if (!rootImg.raycastTarget)
                {
                    rootImg.raycastTarget = true;
                    raycastFixed++;
                }
            }

            // Désactiver raycastTarget sur tous les enfants pour éviter les conflits
            foreach (var childImg in slot.GetComponentsInChildren<Image>(true))
            {
                if (childImg == rootImg) continue;
                Undo.RecordObject(childImg, "Child raycast off");
                childImg.raycastTarget = false;
            }
            foreach (var tmp in slot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                Undo.RecordObject(tmp, "TMP raycast off");
                tmp.raycastTarget = false;
            }

            EditorUtility.SetDirty(slot.gameObject);
        }

        log.AppendLine(raycastFixed > 0
            ? $"✔ raycastTarget activé / Image ajoutée sur {raycastFixed} slot(s)."
            : "✔ raycastTarget déjà OK sur tous les slots.");
        if (leRemoved > 0)
            log.AppendLine($"✔ LayoutElement supprimé sur {leRemoved} slot(s).");

        // ── 7. hoverScale + masquer les labels hotkey ────────────
        foreach (var s in active)
        {
            Undo.RecordObject(s, "hoverScale");
            if (s.hoverScale < 1.1f) s.hoverScale = 1.35f;
            // Cacher le label hotkey clavier (on passe en sélection souris)
            if (s.hotkeyText != null)
            {
                Undo.RecordObject(s.hotkeyText.gameObject, "HideHotkey");
                s.hotkeyText.enabled = false;
            }
        }
        log.AppendLine("✔ hoverScale ≥ 1.35, labels hotkey masqués.");

        // ── 8. Sauvegarder ──────────────────────────────────────
        EditorUtility.SetDirty(deck.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Oracle Hover Patch — OK",
            $"Patch appliqué sur {n}/6 cartes.\n\n" +
            "Ce qui a été fait :\n" +
            "• EventSystem vérifié\n" +
            "• GraphicRaycaster vérifié sur tous les Canvas\n" +
            "• HorizontalLayoutGroup supprimé\n" +
            "• Pivot bas-centre (0.5, 0) forcé sur chaque carte\n" +
            "• Image raycastable garantie sur chaque carte root\n" +
            "• raycastTarget = false sur tous les enfants\n" +
            "• RestPose initialisé\n" +
            "• DeckUI étiré plein Canvas\n" +
            "• Labels hotkey clavier masqués\n\n" +
            "Le hover + clic sont maintenant 100% manuels (RectTransformUtility)\n" +
            "→ plus de dépendance à l'EventSystem.\n\n" +
            "→ Ctrl+S puis lance le Play.", "OK");
    }
}
#endif
