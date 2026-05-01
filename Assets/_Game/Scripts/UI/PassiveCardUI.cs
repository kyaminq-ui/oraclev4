using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Carte de passif — style minimaliste aéré (Oracle v2 redesign).
/// Pas de cadre chargé : fond sombre léger + bordure dorée sur sélection.
/// Hover et sélection gérés en code pur (pas besoin de DOTween).
/// </summary>
public class PassiveCardUI : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  Références Inspector
    // ──────────────────────────────────────────────
    [Header("Visuel")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    [Header("Fond & Bordure")]
    public Image cardBackground;
    [Tooltip("Image fine tout autour de la carte — alpha 0 par défaut, visible si sélectionné.")]
    public Image selectionBorder;

    // ──────────────────────────────────────────────
    //  Couleurs
    // ──────────────────────────────────────────────
    [Header("Couleurs")]
    public Color normalBg   = new Color(0.08f, 0.08f, 0.12f, 0.92f);
    public Color hoverBg    = new Color(0.14f, 0.14f, 0.20f, 0.95f);
    public Color selectedBg = new Color(0.10f, 0.10f, 0.16f, 0.98f);

    public Color borderSelected  = new Color(0.96f, 0.85f, 0.38f, 1f);   // doré
    public Color borderNormal    = new Color(0f, 0f, 0f, 0f);

    // ──────────────────────────────────────────────
    //  State
    // ──────────────────────────────────────────────
    private PassiveData _data;
    private bool _selected;

    public PassiveData Data       => _data;
    public bool        IsSelected => _selected;

    // ──────────────────────────────────────────────
    //  Init
    // ──────────────────────────────────────────────
    public void Setup(PassiveData passive)
    {
        _data     = passive;
        _selected = false;

        if (iconImage != null)
        {
            iconImage.preserveAspect = true;
            iconImage.sprite  = passive.icon;
            iconImage.enabled = passive.icon != null;
        }

        if (nameText != null)        nameText.text        = passive.passiveName;
        if (descriptionText != null) descriptionText.text = passive.description;

        ApplyColors(normalBg, borderNormal);
    }

    // ──────────────────────────────────────────────
    //  Sélection
    // ──────────────────────────────────────────────
    public void SetSelected(bool value)
    {
        _selected = value;
        ApplyColors(value ? selectedBg : normalBg,
                    value ? borderSelected : borderNormal);
    }

    // ──────────────────────────────────────────────
    //  Hover (appelé depuis un EventTrigger Unity)
    // ──────────────────────────────────────────────
    public void OnPointerEnter()
    {
        if (!_selected) ApplyColors(hoverBg, borderNormal);
    }

    public void OnPointerExit()
    {
        if (!_selected) ApplyColors(normalBg, borderNormal);
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────
    private void ApplyColors(Color bg, Color border)
    {
        if (cardBackground   != null) cardBackground.color   = bg;
        if (selectionBorder  != null) selectionBorder.color  = border;
    }
}
