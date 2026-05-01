using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimerUI : MonoBehaviour
{
    // =========================================================
    // RÉFÉRENCES
    // =========================================================
    [Header("Références")]
    [Tooltip("Silhouette du chronomètre (ex. timer_icon_hud) ; remplissage et texte par-dessus.")]
    public Image timerIconImage;
    [Tooltip("Jauge radiale du temps restant (par-dessus l’icône).")]
    public Image fillImage;
    [Tooltip("Secondes restantes, centré sur l’icône.")]
    public TextMeshProUGUI timeText;

    [Header("Feedback sonore (5 dernières secondes)")]
    public AudioSource audioSource;
    public AudioClip tickClip;

    [Header("Visibilité")]
    [Tooltip("Masqué hors combat ; affiché dès que le combat est actif (TurnManager).")]
    public bool hideOutsideCombat = true;

    public enum TimerHostDock { TopRight, TopCenter }

    [Header("Position HUD")]
    [Tooltip("TimerHost : centre entre les barres HP (recommandé) ou coin haut-droite.")]
    public TimerHostDock timerHostDock = TimerHostDock.TopCenter;
    [Tooltip("Décalage du TimerHost lorsque le dock est au centre (x=0 = milieu écran).")]
    public Vector2 topCenterAnchoredPosition = new Vector2(0f, -10f);
    [Tooltip("Décalage du TimerHost en mode haut-droite.")]
    public Vector2 topRightAnchoredPosition = new Vector2(-16f, -16f);
    public Vector2 timerSlotSizeDelta = new Vector2(56f, 56f);

    [Header("Anneau de temps")]
    [Tooltip("Si faux : pas de voile doré semi-transparent (seulement l’icône sablier + chiffre).")]
    public bool showRadialTimeFill = false;

    static readonly string kTimerHostName = "TimerHost";

    // =========================================================
    // COULEURS (tour ~15 s : vert >8 s, orange >3 s, rouge sinon)
    // =========================================================
    [Header("Couleurs")]
    public Color colorGreen  = new Color(0.20f, 0.80f, 0.20f);
    public Color colorOrange = new Color(1.00f, 0.50f, 0.00f);
    public Color colorRed    = new Color(0.80f, 0.10f, 0.10f);

    // =========================================================
    // SEUILS (secondes restantes)
    // =========================================================
    [Header("Seuils (secondes restantes)")]
    public float thresholdGreenAbove = 8f;
    public float thresholdOrangeAbove = 3f;

    [Header("Urgence")]
    [Tooltip("Pulsation + ticks — spec : < 5 s")]
    public float pulseUnderSeconds = 5f;

    private float maxDuration;
    private int lastTickSecond = -1;
    CanvasGroup _visibility;

    // =========================================================
    // INITIALISATION
    // =========================================================
    void Awake()
    {
        ApplyHostRectLayout();

        if (timerIconImage != null)
            timerIconImage.preserveAspect = true;

        _visibility = GetComponent<CanvasGroup>();
        if (_visibility == null)
            _visibility = gameObject.AddComponent<CanvasGroup>();
        _visibility.blocksRaycasts = false;
        _visibility.interactable = false;
        if (hideOutsideCombat)
            _visibility.alpha = 0f;

        if (fillImage != null)
        {
            fillImage.type            = Image.Type.Filled;
            fillImage.fillMethod     = Image.FillMethod.Radial360;
            fillImage.fillOrigin     = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            if (!showRadialTimeFill)
            {
                fillImage.enabled = false;
                fillImage.color   = Color.clear;
            }
        }

        EnsureTimeCopyStyle();
    }

    /// <summary>Rappelé par <see cref="CombatHUD"/> après mise en page du HUD pour réaligner l’hôte.</summary>
    public void ApplyHostRectLayout()
    {
        var selfRt = GetComponent<RectTransform>();
        var host = transform.parent as RectTransform;

        if (host != null && host.name == kTimerHostName)
        {
            if (timerHostDock == TimerHostDock.TopCenter)
            {
                host.anchorMin = host.anchorMax = new Vector2(0.5f, 1f);
                host.pivot = new Vector2(0.5f, 1f);
                host.anchoredPosition = topCenterAnchoredPosition;
            }
            else
            {
                host.anchorMin = host.anchorMax = new Vector2(1f, 1f);
                host.pivot = new Vector2(1f, 1f);
                host.anchoredPosition = topRightAnchoredPosition;
            }
            host.sizeDelta = timerSlotSizeDelta;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = selfRt.offsetMax = Vector2.zero;
            selfRt.localScale = Vector3.one;
            return;
        }

        // Hors TimerHost (ex. TimerCorner sur l’écran passif) : rester en haut-droite du parent.
        selfRt.anchorMin = selfRt.anchorMax = new Vector2(1f, 1f);
        selfRt.pivot = new Vector2(1f, 1f);
        selfRt.anchoredPosition = topRightAnchoredPosition;
        selfRt.sizeDelta = timerSlotSizeDelta;
        selfRt.localScale = Vector3.one;
    }

    void EnsureTimeCopyStyle()
    {
        if (timeText == null) return;
        timeText.enableWordWrapping = false;
    }

    void OnEnable()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnStart += OnNewTurn;
            maxDuration = Mathf.Max(0.01f, TurnManager.Instance.turnDuration);
            lastTickSecond = -1;
        }
    }

    void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart -= OnNewTurn;
    }

    void Start()
    {
        OracleUIImportantFont.Apply(timeText);
    }

    void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnStart -= OnNewTurn;
    }

    void OnNewTurn(TacticalCharacter _)
    {
        if (TurnManager.Instance == null) return;
        maxDuration = TurnManager.Instance.turnDuration;
        lastTickSecond = -1;
        transform.localScale = Vector3.one;
    }

    // =========================================================
    // MISE À JOUR
    // =========================================================
    void Update()
    {
        if (TurnManager.Instance == null)
        {
            if (hideOutsideCombat && _visibility != null)
                _visibility.alpha = 0f;
            return;
        }

        bool show = !hideOutsideCombat || TurnManager.Instance.IsCombatActive;
        if (_visibility != null)
            _visibility.alpha = show ? 1f : 0f;

        if (!show || !TurnManager.Instance.IsCombatActive)
        {
            transform.localScale = Vector3.one;
            lastTickSecond = -1;
            return;
        }

        float remaining = TurnManager.Instance.TimeRemaining;
        float ratio = (maxDuration > 0f) ? remaining / maxDuration : 0f;

        if (timeText != null)
        {
            timeText.text      = Mathf.CeilToInt(remaining).ToString();
            timeText.alignment = TextAlignmentOptions.Center;
        }

        Color targetColor;
        if (remaining > thresholdGreenAbove)       targetColor = colorGreen;
        else if (remaining > thresholdOrangeAbove) targetColor = colorOrange;
        else                                       targetColor = colorRed;

        if (showRadialTimeFill && fillImage != null && fillImage.enabled)
        {
            fillImage.fillAmount = ratio;
            var cFill = new Color(0.788f, 0.659f, 0.298f,
                0.28f + 0.22f * Mathf.Clamp01(ratio));
            fillImage.color = cFill;
        }
        if (timeText != null)  timeText.color  = targetColor;
        if (timerIconImage != null)
            timerIconImage.color = Color.white;

        if (remaining <= pulseUnderSeconds && remaining > 0f)
        {
            float pulse = 1f + 0.10f * Mathf.Sin(Time.time * 12f);
            transform.localScale = Vector3.one * pulse;

            int sec = Mathf.CeilToInt(remaining);
            if (sec != lastTickSecond && sec > 0)
            {
                lastTickSecond = sec;
                PlayTick();
            }
        }
        else
        {
            transform.localScale = Vector3.one;
            lastTickSecond = -1;
        }
    }

    void PlayTick()
    {
        if (tickClip == null) return;
        if (audioSource != null)
            audioSource.PlayOneShot(tickClip);
        else
            AudioSource.PlayClipAtPoint(tickClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }
}
