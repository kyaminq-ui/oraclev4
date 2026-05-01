#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Recrée les SpellSlotUI du DeckUI pour cartes pleines (GIF CARTE_SORT) : ratio préservé, texte bas visible.
/// Menu : Oracle/Rebuild Deck Slots — UI Maj (8 slots)
/// </summary>
public static class OracleDeckSlotsMajBuilder
{
    static readonly string[] Hotkeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
    const int SlotCount = 8;
    const float SlotW = 156f;
    const float SlotH = 240f;

    [MenuItem("Oracle/Rebuild Deck Slots — UI Maj (8 slots)")]
    public static void Rebuild()
    {
        var deck = Object.FindObjectOfType<DeckUI>(true);
        if (deck == null)
        {
            EditorUtility.DisplayDialog("Oracle — Deck Maj", "Aucun DeckUI dans la scène.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Oracle — Deck Maj",
                "Détruire les SpellSlot enfants du DeckUI et recréer 8 slots cartes pleines ?\n" +
                "Oracle/Import & Assign CARTE_SORT pour lier les illustrations.",
                "Oui", "Annuler"))
            return;

        Undo.RegisterCompleteObjectUndo(deck.gameObject, "Deck UI Maj");

        for (int i = deck.transform.childCount - 1; i >= 0; i--)
        {
            var ch = deck.transform.GetChild(i);
            if (ch.GetComponent<SpellSlotUI>() != null || ch.name.StartsWith("SpellSlot"))
                Undo.DestroyObjectImmediate(ch.gameObject);
        }

        deck.slots.Clear();

        var hlg = deck.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = Undo.AddComponent<HorizontalLayoutGroup>(deck.gameObject);
        hlg.padding = new RectOffset(6, 6, 6, 6);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        for (int i = 0; i < SlotCount; i++)
            deck.slots.Add(BuildSlot(deck.transform, i));

        EditorUtility.SetDirty(deck);
        Selection.activeGameObject = deck.gameObject;
        Debug.Log("[OracleDeckSlotsMajBuilder] 8 slots carte créés.");
        EditorUtility.DisplayDialog("Oracle — Deck Maj", "Deck reconfiguré (8 slots).", "OK");
    }

    static SpellSlotUI BuildSlot(Transform parent, int index)
    {
        var slotGO = new GameObject($"SpellSlot_{index + 1}");
        Undo.RegisterCreatedObjectUndo(slotGO, "SpellSlot Maj");
        slotGO.transform.SetParent(parent, false);
        slotGO.AddComponent<RectTransform>().sizeDelta = new Vector2(SlotW, SlotH);

        var le = slotGO.AddComponent<LayoutElement>();
        le.preferredWidth = SlotW;
        le.preferredHeight = SlotH;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;

        var rootHit = slotGO.AddComponent<Image>();
        rootHit.color = new Color(0f, 0f, 0f, 0.001f);
        rootHit.raycastTarget = true;

        var slot = slotGO.AddComponent<SpellSlotUI>();

        ChildImage(slotGO.transform, "Backing", new Color(0.07f, 0.07f, 0.10f, 1f));

        var iconArea = Panel(slotGO.transform, "IconArea", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(iconArea.transform, false);
        Stretch(iconGO.AddComponent<RectTransform>());
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        var dimGO = new GameObject("DimOverlay");
        dimGO.transform.SetParent(iconArea.transform, false);
        Stretch(dimGO.AddComponent<RectTransform>());
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.55f);
        dimImg.enabled = false;
        dimImg.raycastTarget = false;

        var paGo = Tmp(slotGO.transform, "PACost", 14f, FontStyles.Bold,
            TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-6f, -6f), new Vector2(40f, 22f));

        var hkGo = Tmp(slotGO.transform, "Hotkey", 12f, FontStyles.Bold,
            TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(8f, 8f), new Vector2(32f, 22f));
        hkGo.GetComponent<TextMeshProUGUI>().text =
            index < Hotkeys.Length ? Hotkeys[index] : $"{index + 1}";

        var selGO = new GameObject("SelectionBorder");
        selGO.transform.SetParent(slotGO.transform, false);
        Stretch(selGO.AddComponent<RectTransform>());
        var selImg = selGO.AddComponent<Image>();
        selImg.color = Color.clear;
        selImg.enabled = false;
        selImg.raycastTarget = false;

        slot.iconImage = iconImg;
        slot.cardFrameImage = null;
        slot.dimOverlay = dimImg;
        slot.paCostText = paGo.GetComponent<TextMeshProUGUI>();
        slot.hotkeyText = hkGo.GetComponent<TextMeshProUGUI>();
        slot.selectionBorder = selImg;

        return slot;
    }

    static RectTransform ChildImage(Transform parent, string name, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        Stretch(rt);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        return rt;
    }

    static RectTransform Panel(Transform parent, string name, Vector2 ancMin, Vector2 ancMax)
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

    static GameObject Tmp(Transform parent, string name, float size, FontStyles style,
        TextAlignmentOptions align, Vector2 ancMin, Vector2 ancMax, Vector2 pos, Vector2 delta)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = delta;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
