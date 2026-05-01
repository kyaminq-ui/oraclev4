using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Widget passif en bas à gauche — icône + label "Passif", tooltip au survol.
/// </summary>
public class PassiveHUDWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Icône")]
    public Image iconImage;

    [Header("Tooltip")]
    public GameObject      tooltipPanel;
    public TextMeshProUGUI tooltipName;
    public TextMeshProUGUI tooltipDesc;

    PassiveData _passive;

    public void SetPassive(PassiveData passive)
    {
        _passive = passive;
        if (iconImage != null)
        {
            iconImage.sprite         = passive?.icon;
            iconImage.enabled        = passive?.icon != null;
            iconImage.preserveAspect = true;
        }
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_passive == null || tooltipPanel == null) return;
        if (tooltipName != null) tooltipName.text = _passive.passiveName;
        if (tooltipDesc  != null) tooltipDesc.text  = _passive.description;
        tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
}
