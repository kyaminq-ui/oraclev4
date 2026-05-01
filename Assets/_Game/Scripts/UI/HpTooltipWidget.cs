using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Infobulle HP qui apparaît quand la souris survole une case occupée.
/// Positionne le panneau près du curseur et affiche "Nom\nHP: cur / max".
/// </summary>
public class HpTooltipWidget : MonoBehaviour
{
    [Header("Références")]
    public Camera       cam;
    public RectTransform panel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI hpText;

    [Header("Offset souris (px)")]
    public Vector2 offset = new Vector2(14f, 14f);

    private Canvas _canvas;

    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (cam == null || GridManager.Instance == null)
        {
            Hide(); return;
        }

        if (CombatInitializer.Instance != null &&
            CombatInitializer.Instance.CurrentPhase != CombatInitializer.CombatPhase.Combat)
        {
            Hide();
            return;
        }

        Cell cell = GridManager.Instance.GetCellAtScreenPosition(cam, Input.mousePosition);
        if (cell == null || !cell.IsOccupied)
        {
            Hide(); return;
        }

        var tc = cell.Occupant != null
            ? cell.Occupant.GetComponent<TacticalCharacter>()
            : null;

        if (tc == null || tc.stats == null)
        {
            Hide(); return;
        }

        Show(tc);
    }

    void Show(TacticalCharacter tc)
    {
        if (panel == null) return;

        if (nameText != null) nameText.text = tc.name;
        if (hpText   != null) hpText.text   = $"HP : {tc.CurrentHP} / {tc.stats.maxHP}";

        // Convertir la position souris en position local au canvas
        Vector2 localPos;
        if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            localPos = (Vector2)Input.mousePosition + offset;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas != null ? _canvas.GetComponent<RectTransform>() : panel.parent as RectTransform,
                Input.mousePosition, cam, out localPos);
            localPos += offset;
        }

        panel.anchoredPosition = localPos;
        panel.gameObject.SetActive(true);
    }

    void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
