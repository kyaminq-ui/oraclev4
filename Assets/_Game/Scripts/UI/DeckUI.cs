using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Hotbar de sorts en éventail — 6 slots max.
/// Hover et clic gérés manuellement (RectTransformUtility) pour s'affranchir
/// des problèmes de GraphicRaycaster / EventSystem.
/// </summary>
public class DeckUI : MonoBehaviour
{
    // =========================================================
    // CONFIGURATION
    // =========================================================
    [Header("Slots (6 max)")]
    public List<SpellSlotUI> slots = new List<SpellSlotUI>();

    [Header("Tooltip")]
    public SpellTooltip tooltip;

    [Header("Fan Layout")]
    public bool  fanMode      = true;
    public float fanCardW     = 156f;
    public float fanCardH     = 240f;   // cartes pleines GIF — lisibilité du texte
    [Tooltip("Référence de hauteur pour l’échelle du contour de sélection sur les cartes (SpellSlotUI). Souvent = fanCardH.")]
    public float spellSlotHudReferenceHeight = 240f;
    public float fanSpread    = 230f;   // cartes très serrées
    public float fanAngleMax  = 18f;
    public float fanArcHeight = 35f;
    public float fanBaseY     = -80f;
    public float fanRaise     = 28f;   // léger détachement au survol (lisible dans l'éventail)

    const int MaxVisibleSlots = 6;

    // =========================================================
    // ÉTAT
    // =========================================================
    private TacticalCharacter activeCharacter;
    private SpellCaster       activeCaster;
    private int               selectedSlotIndex = -1;

    // Hover manuel
    private SpellSlotUI _hoveredSlot;
    private Camera      _uiCamera;

    // =========================================================
    // LIAISON AVEC UN PERSONNAGE
    // =========================================================
    public void BindCharacter(TacticalCharacter character)
    {
        if (activeCharacter != null)
        {
            activeCharacter.OnPAChanged -= OnResourceChanged;
            activeCharacter.OnStateChanged -= OnCharacterStateChanged;
        }

        activeCharacter = character;
        activeCaster    = character != null ? character.GetComponent<SpellCaster>() : null;

        if (activeCharacter != null)
        {
            activeCharacter.OnPAChanged += OnResourceChanged;
            activeCharacter.OnStateChanged += OnCharacterStateChanged;
        }

        RebuildSlots();
    }

    public void UnbindCharacter()
    {
        BindCharacter(null);
        ClearSelection();
    }

    // =========================================================
    // CONSTRUCTION DE LA HOTBAR
    // =========================================================
    private void RebuildSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i >= MaxVisibleSlots)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            SpellData spell = null;
            if (activeCharacter != null && activeCharacter.ActiveSpells.Count > 0)
            {
                var spells = activeCharacter.ActiveSpells;
                spell = i < spells.Count ? spells[i] : null;
            }

            slots[i].Setup(spell, activeCharacter, this, i);
            slots[i].gameObject.SetActive(activeCharacter != null);
        }

        ClearSelection();
        if (fanMode) ApplyFanLayout();
    }

    // =========================================================
    // FAN LAYOUT
    // =========================================================
    public void ApplyFanLayout()
    {
        // Supprimer le HorizontalLayoutGroup — il écrase les positions manuelles
        var hlg = GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.DestroyObjectImmediate(hlg);
            else
#endif
            Destroy(hlg);
        }

        // DeckUI couvre tout le canvas → positions absolues depuis le centre-bas
        var deckRT = GetComponent<RectTransform>();
        if (deckRT != null)
        {
            deckRT.anchorMin        = Vector2.zero;
            deckRT.anchorMax        = Vector2.one;
            deckRT.offsetMin        = Vector2.zero;
            deckRT.offsetMax        = Vector2.zero;
            deckRT.anchoredPosition = Vector2.zero;
        }

        var visible = new List<SpellSlotUI>();
        for (int i = 0; i < slots.Count && visible.Count < MaxVisibleSlots; i++)
            if (slots[i] != null && slots[i].gameObject.activeSelf)
                visible.Add(slots[i]);

        int n = visible.Count;
        if (n == 0) return;

        // Ordre sibling : bords derrière, centre devant
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            float da = Mathf.Abs((float)a / Mathf.Max(1, n - 1) - 0.5f);
            float db = Mathf.Abs((float)b / Mathf.Max(1, n - 1) - 0.5f);
            return db.CompareTo(da);
        });
        for (int j = 0; j < order.Count; j++)
            visible[order[j]].transform.SetSiblingIndex(j);

        for (int i = 0; i < n; i++)
        {
            float t     = n > 1 ? (float)i / (n - 1) : 0.5f;
            float x     = Mathf.Lerp(-fanSpread * 0.5f, fanSpread * 0.5f, t);
            float norm  = 2f * Mathf.Abs(t - 0.5f);
            float arcY  = fanArcHeight * (1f - norm * norm);
            float angle = Mathf.Lerp(fanAngleMax, -fanAngleMax, t);

            // Pivot bas-centre obligatoire pour l'arc
            var rt = visible[i].GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);

            visible[i].hudReferenceCardHeight = Mathf.Max(1f, spellSlotHudReferenceHeight);
            visible[i].SetRestPose(new Vector2(x, fanBaseY + arcY),
                                   angle,
                                   new Vector2(fanCardW, fanCardH),
                                   fanRaise);
        }
    }

    public void RestoreSiblingOrder()
    {
        if (!fanMode) return;
        var visible = new List<SpellSlotUI>();
        for (int i = 0; i < slots.Count && visible.Count < MaxVisibleSlots; i++)
            if (slots[i] != null && slots[i].gameObject.activeSelf)
                visible.Add(slots[i]);

        int n = visible.Count;
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            float da = Mathf.Abs((float)a / Mathf.Max(1, n - 1) - 0.5f);
            float db = Mathf.Abs((float)b / Mathf.Max(1, n - 1) - 0.5f);
            return db.CompareTo(da);
        });
        for (int j = 0; j < order.Count; j++)
            visible[order[j]].transform.SetSiblingIndex(j);
    }

    // =========================================================
    // UPDATE — hover et clic 100 % manuels
    // =========================================================
    void Awake()
    {
        var canvas = GetComponentInParent<Canvas>();
        _uiCamera  = canvas != null ? canvas.worldCamera : null;
    }

    void Update()
    {
        if (activeCharacter == null) return;
        HandleMouseHover();
        HandleMouseClick();
    }

    private void HandleMouseHover()
    {
        Vector2 mp = Input.mousePosition;

        // Collecter les slots visibles triés du dernier sibling (dessus) au premier
        var visible = new List<SpellSlotUI>();
        for (int i = 0; i < Mathf.Min(slots.Count, MaxVisibleSlots); i++)
            if (slots[i] != null && slots[i].gameObject.activeSelf)
                visible.Add(slots[i]);
        visible.Sort((a, b) =>
            b.transform.GetSiblingIndex().CompareTo(a.transform.GetSiblingIndex()));

        SpellSlotUI hit = null;
        foreach (var s in visible)
        {
            var rt = s.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mp, _uiCamera))
            {
                hit = s;
                break;
            }
        }

        if (hit == _hoveredSlot) return;

        _hoveredSlot?.ForceExit();
        _hoveredSlot = hit;
        _hoveredSlot?.ForceEnter();
    }

    private void HandleMouseClick()
    {
        // Clic GAUCHE = sélectionner un sort
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 mp = Input.mousePosition;
        var visible = new List<SpellSlotUI>();
        for (int i = 0; i < Mathf.Min(slots.Count, MaxVisibleSlots); i++)
            if (slots[i] != null && slots[i].gameObject.activeSelf)
                visible.Add(slots[i]);
        visible.Sort((a, b) => b.transform.GetSiblingIndex().CompareTo(a.transform.GetSiblingIndex()));

        foreach (var s in visible)
        {
            var rt = s.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mp, _uiCamera))
            {
                SelectSlot(s.SlotIndex);
                return;
            }
        }
    }

    // =========================================================
    // REFRESH
    // =========================================================
    private void OnResourceChanged(int current, int max) => RefreshAll();

    /// <summary>
    /// Les PM (déplacement) ne changent pas la jouabilité des sorts — on ne rafraîchit pas sur OnPMChanged
    /// pour éviter l’assombrissement pendant le mouvement (<see cref="TacticalCharacter.CanCastSpell"/> exige Idle).
    /// </summary>
    void OnCharacterStateChanged(CharacterState state)
    {
        if (state == CharacterState.Idle)
            RefreshAll();
    }

    public void RefreshAll()
    {
        for (int i = 0; i < Mathf.Min(slots.Count, MaxVisibleSlots); i++)
            if (slots[i] != null) slots[i].Refresh();
    }

    // =========================================================
    // SÉLECTION
    // =========================================================
    public void SelectSlot(int index)
    {
        if (activeCaster == null || activeCharacter == null) return;
        if (index < 0 || index >= Mathf.Min(slots.Count, MaxVisibleSlots)) return;

        SpellSlotUI slot = slots[index];
        if (slot == null || !slot.HasSpell) return;

        if (selectedSlotIndex == index)
        {
            ClearSelection();
            activeCaster.CancelSpell();
            return;
        }

        bool ok = activeCaster.SelectSpell(slot.Spell);
        if (!ok) return;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < slots.Count)
            slots[selectedSlotIndex].SetSelected(false);

        selectedSlotIndex = index;
        slot.SetSelected(true);
    }

    public void ClearSelection()
    {
        if (selectedSlotIndex >= 0 && selectedSlotIndex < slots.Count)
            slots[selectedSlotIndex]?.SetSelected(false);
        selectedSlotIndex = -1;
    }

    // =========================================================
    // TOOLTIP
    // =========================================================
    public void ShowTooltip(SpellData spell, Vector3 anchorWorldPos)
    {
        if (tooltip != null) tooltip.Show(spell, anchorWorldPos);
    }

    public void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
    }
}
