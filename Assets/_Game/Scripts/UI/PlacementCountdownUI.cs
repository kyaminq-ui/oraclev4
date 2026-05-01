using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Chrono pendant la phase de placement — silhouette <c>timer_icon_hud</c>, jauge radiale dorée discrète, secondes au centre.
/// </summary>
public class PlacementCountdownUI : MonoBehaviour
{
    [Header("Références (optionnelles — créées par EnsureBuilt)")]
    public Image timerIconImage;
    public Image timerFillImage;
    public TextMeshProUGUI timeText;

    [Header("Style")]
    public Color fillTint = new Color(0.788f, 0.659f, 0.298f, 0.42f);
    public Vector2 anchorTopRightOffset = new Vector2(-16f, -16f);
    public Vector2 slotSize = new Vector2(56f, 56f);

    float _endUnscaled;
    float _duration;
    bool _running;
    bool _built;

    public bool IsRunning => _running;

    public void Show(float durationSeconds)
    {
        EnsureBuilt();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        _duration = Mathf.Max(0.01f, durationSeconds);
        _endUnscaled = Time.unscaledTime + _duration;
        _running = true;

        if (timerIconImage != null)
        {
            timerIconImage.preserveAspect = true;
            var s = OracleHudRuntimeSprites.LoadTimerIcon();
            if (s != null) timerIconImage.sprite = s;
            timerIconImage.color = Color.white;
            timerIconImage.enabled = true;
        }
        if (timerFillImage != null)
        {
            timerFillImage.color = fillTint;
            timerFillImage.fillAmount = 1f;
        }
        OracleUIImportantFont.Apply(timeText);
        ApplyVisuals(_duration);
    }

    public void Hide()
    {
        _running = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_running) return;
        float remaining = _endUnscaled - Time.unscaledTime;
        if (remaining <= 0f)
        {
            ApplyVisuals(0f);
            _running = false;
            return;
        }
        ApplyVisuals(remaining);
    }

    void ApplyVisuals(float remaining)
    {
        float ratio = _duration > 0f ? Mathf.Clamp01(remaining / _duration) : 0f;
        if (timerFillImage != null)
            timerFillImage.fillAmount = ratio;
        if (timeText != null)
            timeText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
    }

    public void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = anchorTopRightOffset;
        rt.sizeDelta = slotSize;

        if (timerIconImage == null)
        {
            var iconGO = new GameObject("TimerIcon", typeof(RectTransform));
            iconGO.transform.SetParent(transform, false);
            var irt = iconGO.GetComponent<RectTransform>();
            Stretch(irt);
            timerIconImage = iconGO.AddComponent<Image>();
            timerIconImage.preserveAspect = true;
            timerIconImage.raycastTarget = false;
        }

        if (timerFillImage == null)
        {
            var fillGO = new GameObject("TimerFill", typeof(RectTransform));
            fillGO.transform.SetParent(transform, false);
            var frt = fillGO.GetComponent<RectTransform>();
            Stretch(frt);
            timerFillImage = fillGO.AddComponent<Image>();
            timerFillImage.type = Image.Type.Filled;
            timerFillImage.fillMethod = Image.FillMethod.Radial360;
            timerFillImage.fillOrigin = (int)Image.Origin360.Top;
            timerFillImage.fillClockwise = false;
            timerFillImage.raycastTarget = false;
        }

        if (timeText == null)
        {
            var txtGO = new GameObject("TimerText", typeof(RectTransform));
            txtGO.transform.SetParent(transform, false);
            Stretch(txtGO.GetComponent<RectTransform>());
            timeText = txtGO.AddComponent<TextMeshProUGUI>();
            timeText.alignment = TextAlignmentOptions.Center;
            timeText.fontSize = 20f;
            timeText.fontStyle = FontStyles.Bold;
            timeText.color = new Color(0.788f, 0.659f, 0.298f, 1f);
            timeText.raycastTarget = false;
        }

        var cg = gameObject.GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 4100;
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        gameObject.SetActive(false);
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        r.anchoredPosition = Vector2.zero;
        r.localScale = Vector3.one;
    }
}
