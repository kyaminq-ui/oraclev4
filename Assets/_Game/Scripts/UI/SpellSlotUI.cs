using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Carte de sort dans l'éventail.
/// Hover/clic pilotés depuis DeckUI (RectTransformUtility).
/// Sélection = contour doré animé (Outline) sur l'illustration, sans dupliquer le visuel.
/// </summary>
public class SpellSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // =========================================================
    // RÉFÉRENCES
    // =========================================================
    [Header("Icône")]
    public Image iconImage;
    [Tooltip("Cadre UI_CARTE_SORT (optionnel)")]
    public Image cardFrameImage;
    [Tooltip("Référence héritée (DimOverlay). Non utilisé : le voile noir « non lançable » a été retiré.")]
    public Image dimOverlay;

    [Tooltip("Hauteur de carte de référence (px) pour l’échelle du contour sélection. Doit matcher fanCardH par défaut.")]
    public float hudReferenceCardHeight = 240f;

    [Header("Labels")]
    public TextMeshProUGUI paCostText;
    public TextMeshProUGUI hotkeyText;

    // selectionBorder conservé pour la compatibilité Inspector mais inutilisé
    [Header("Sélection")]
    public Image selectionBorder;

    [Header("Hover")]
    [Tooltip("Zoom discret — l'éventail reste lisible.")]
    public float hoverScale   = 1.09f;
    public float animDuration = 0.14f;
    [Tooltip("Éclaircit légèrement l'illustration si le sort est utilisable.")]
    public float hoverBrightFactor = 1.12f;

    [Header("Contour sélection")]
    public Color glowColor      = new Color(1f, 0.78f, 0.22f, 1f);
    public float glowPulseSpeed = 2.2f;
    public float glowMinAlpha   = 0.55f;
    public float glowMaxAlpha   = 1f;
    [Tooltip("Épaisseur du contour à hudReferenceCardHeight (Outline Unity UI).")]
    public float selectionOutlineMin = 1f;
    public float selectionOutlineMax = 2f;

    [Header("Couleurs")]
    public Color availableColor   = Color.white;
    public Color unavailableColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Carte pleine (GIF CARTE_SORT)")]
    [Tooltip("Si activé : illustration pleine carte, cadre UI_CARTE_SORT masqué, zone image élargie pour garder le texte en bas lisible.")]
    public bool useFullCardArt = true;

    // =========================================================
    // ÉTAT
    // =========================================================
    private SpellData         spell;
    private TacticalCharacter owner;
    private DeckUI            deckUI;
    private int               slotIndex;
    private bool              isSelected;
    private bool              isHovered;
    private Coroutine         _animCoroutine;
    private Coroutine         _selectionPulseCoroutine;
    private Outline           _iconOutline;
    private Color             _lastIconBase = Color.white;
    private bool              _lastCanCast;
    private float             _hudScale = 1f;

    // Fan rest-pose
    Vector2 _restPos;
    float   _restAngle;
    Vector2 _restSize;
    float   _raiseDelta = 64f;

    public SpellData Spell    => spell;
    public bool      HasSpell => spell != null;
    public int       SlotIndex => slotIndex;

    // =========================================================
    // REST POSE
    // =========================================================
    public void SetRestPose(Vector2 pos, float angleDeg, Vector2 size, float raiseDelta)
    {
        _restPos    = pos;
        _restAngle  = angleDeg;
        _restSize   = size;
        _raiseDelta = raiseDelta;
        var rt = GetComponent<RectTransform>();
        rt.anchoredPosition = _restPos;
        rt.localRotation    = Quaternion.Euler(0f, 0f, _restAngle);
        rt.sizeDelta        = _restSize;
        rt.localScale       = Vector3.one;

        UpdateHudScaleFromRestSize();
        ApplyScaledHudPresentation();
    }

    void UpdateHudScaleFromRestSize()
    {
        float h = _restSize.y;
        if (h < 2f)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
                h = rt.sizeDelta.y > 2f ? rt.sizeDelta.y : rt.rect.height;
        }
        if (h < 2f)
            h = hudReferenceCardHeight;
        float denom = Mathf.Max(1f, hudReferenceCardHeight);
        _hudScale = Mathf.Clamp(h / denom, 0.42f, 1.4f);
    }

    /// <summary>Contour sélection : appliquer après calcul de <see cref="_hudScale"/>.</summary>
    void ApplyScaledHudPresentation()
    {
        RefreshIconOutlineBaseDistance();
    }

    void RefreshIconOutlineBaseDistance()
    {
        if (_iconOutline == null) return;
        if (_selectionPulseCoroutine != null) return;
        float t = selectionOutlineMin * _hudScale;
        _iconOutline.effectDistance = new Vector2(t, -t);
    }

    void Awake()
    {
        if (iconImage      != null) iconImage.preserveAspect      = true;
        if (cardFrameImage != null) cardFrameImage.preserveAspect = true;

        // L'ancien selectionBorder (remplissage plein) est complètement désactivé
        if (selectionBorder != null) selectionBorder.enabled = false;

        DestroyLegacySlotOverlayChildren();
        DestroyLegacyCooldownChildren();
    }

    void OnEnable()
    {
        DestroyLegacyCooldownChildren();
    }

    static void DestroyObjectSafe(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    /// <summary>Supprime HoverSheen et DimOverlay (anciens calques plein écran sur la carte).</summary>
    void DestroyLegacySlotOverlayChildren()
    {
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            string n = all[i].name;
            if (n == "HoverSheen" || n == "DimOverlay")
                DestroyObjectSafe(all[i].gameObject);
        }
    }

    /// <summary>Supprime tout calque / texte de recharge sur la carte (ancien HUD ou noms custom).</summary>
    void DestroyLegacyCooldownChildren()
    {
        var all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == transform) continue;
            string n = all[i].name;
            if (n.IndexOf("cooldown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                DestroyObjectSafe(all[i].gameObject);
        }
    }

    void Start()
    {
        if (_restSize == Vector2.zero)
        {
            var rt  = GetComponent<RectTransform>();
            _restPos   = rt.anchoredPosition;
            _restAngle = rt.localEulerAngles.z;
            _restSize  = rt.sizeDelta != Vector2.zero ? rt.sizeDelta : new Vector2(156f, 240f);
        }
        UpdateHudScaleFromRestSize();
        ApplyScaledHudPresentation();
    }

    // =========================================================
    // INITIALISATION
    // =========================================================
    public void Setup(SpellData spellData, TacticalCharacter character, DeckUI deck, int index)
    {
        spell     = spellData;
        owner     = character;
        deckUI    = deck;
        slotIndex = index;

        if (hotkeyText  != null) hotkeyText.enabled  = false;
        if (paCostText  != null) paCostText.enabled  = false; // coût PA masqué sur les cartes

        if (spell != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite  = spell.icon;
                iconImage.enabled = spell.icon != null;
            }
        }
        else
        {
            if (iconImage != null)    iconImage.enabled = false;
            if (paCostText != null)   paCostText.text   = "";
        }

        ApplyFullCardArtLayout(spell != null && spell.icon != null);

        SetSelected(false);
        Refresh();
    }

    void ApplyFullCardArtLayout(bool hasSpellIcon)
    {
        if (!useFullCardArt)
            return;

        var iconArea = transform.Find("IconArea") as RectTransform;
        if (iconArea != null)
        {
            iconArea.anchorMin = new Vector2(0.02f, 0.02f);
            iconArea.anchorMax = new Vector2(0.98f, 0.98f);
            iconArea.offsetMin = Vector2.zero;
            iconArea.offsetMax = Vector2.zero;
        }

        if (cardFrameImage != null)
            cardFrameImage.enabled = false;

        if (iconImage == null) return;

        var arf = iconImage.GetComponent<AspectRatioFitter>();
        if (!hasSpellIcon)
        {
            if (arf != null)
            {
                if (Application.isPlaying) Destroy(arf);
                else DestroyImmediate(arf);
            }
            return;
        }

        if (iconImage.sprite == null) return;

        if (arf == null) arf = iconImage.gameObject.AddComponent<AspectRatioFitter>();
        arf.enabled      = true;
        arf.aspectMode     = AspectRatioFitter.AspectMode.FitInParent;
        arf.aspectRatio    = Mathf.Max(0.01f, iconImage.sprite.rect.width / Mathf.Max(1f, iconImage.sprite.rect.height));
    }

    void EnsureIconOutline()
    {
        if (_iconOutline != null || iconImage == null) return;
        _iconOutline = iconImage.GetComponent<Outline>();
        if (_iconOutline == null)
            _iconOutline = iconImage.gameObject.AddComponent<Outline>();
        float t = selectionOutlineMin * _hudScale;
        _iconOutline.effectDistance = new Vector2(t, -t);
        _iconOutline.useGraphicAlpha = true;
        _iconOutline.enabled = false;
    }

    // =========================================================
    // REFRESH
    // =========================================================
    public void Refresh()
    {
        if (spell == null || owner == null) return;

        bool canCast = owner.CanCastSpell(spell);

        _lastCanCast  = canCast;
        _lastIconBase = canCast ? availableColor : unavailableColor;
        ApplyIconColorFromState();
    }

    void ApplyIconColorFromState()
    {
        if (iconImage == null) return;
        Color c = _lastIconBase;
        if (isHovered && _lastCanCast)
            c *= hoverBrightFactor;
        iconImage.color = c;
    }

    // =========================================================
    // SÉLECTION — contour doré sur l'icône
    // =========================================================
    public void SetSelected(bool value)
    {
        isSelected = value;

        if (selectionBorder != null) selectionBorder.enabled = false;

        if (value)
        {
            EnsureIconOutline();
            if (_iconOutline != null)
            {
                _iconOutline.enabled = true;
                if (_selectionPulseCoroutine != null) StopCoroutine(_selectionPulseCoroutine);
                _selectionPulseCoroutine = StartCoroutine(SelectionOutlinePulse());
            }

            if (cardFrameImage != null && cardFrameImage.enabled)
                cardFrameImage.color = glowColor;

            if (isHovered)
            {
                var rt = GetComponent<RectTransform>();
                AnimateTo(rt, _restPos, Quaternion.Euler(0f, 0f, _restAngle), Vector3.one);
            }
        }
        else
        {
            if (_selectionPulseCoroutine != null)
            {
                StopCoroutine(_selectionPulseCoroutine);
                _selectionPulseCoroutine = null;
            }
            if (_iconOutline != null)
            {
                _iconOutline.enabled = false;
            }

            if (cardFrameImage != null && cardFrameImage.enabled)
                cardFrameImage.color = Color.white;
        }
    }

    IEnumerator SelectionOutlinePulse()
    {
        while (_iconOutline != null && _iconOutline.enabled)
        {
            float t  = (Mathf.Sin(Time.unscaledTime * glowPulseSpeed) + 1f) * 0.5f;
            float w  = Mathf.Lerp(selectionOutlineMin * _hudScale, selectionOutlineMax * _hudScale, t);
            float a  = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, t);
            _iconOutline.effectDistance = new Vector2(w, -w);
            _iconOutline.effectColor    = new Color(glowColor.r, glowColor.g, glowColor.b, a);
            yield return null;
        }
    }

    // =========================================================
    // HOVER — piloté par DeckUI (ForceEnter/ForceExit)
    // =========================================================
    public void ForceEnter()
    {
        if (isHovered) return;
        if (_restSize == Vector2.zero) return;

        isHovered = true;
        transform.SetAsLastSibling();

        if (!isSelected)
        {
            var rt = GetComponent<RectTransform>();
            AnimateTo(rt,
                _restPos + Vector2.up * _raiseDelta,
                Quaternion.identity,
                Vector3.one * hoverScale);
        }

        ApplyIconColorFromState();

        if (spell != null && deckUI != null)
            deckUI.ShowTooltip(spell, transform.position);
    }

    public void ForceExit()
    {
        if (!isHovered) return;

        isHovered = false;

        var rt = GetComponent<RectTransform>();
        AnimateTo(rt,
            _restPos,
            Quaternion.Euler(0f, 0f, _restAngle),
            Vector3.one);

        ApplyIconColorFromState();

        deckUI?.RestoreSiblingOrder();
        deckUI?.HideTooltip();
    }

    // Hover via EventSystem (secours si RectTransformUtility est imprécis)
    public void OnPointerEnter(PointerEventData eventData) => ForceEnter();
    public void OnPointerExit(PointerEventData eventData)  => ForceExit();
    // OnPointerClick intentionnellement absent : DeckUI.HandleMouseClick est la seule source
    // (double-fire = sélection puis désélection immédiate sur le même clic)

    // =========================================================
    // ANIMATION SMOOTH
    // =========================================================
    void AnimateTo(RectTransform rt, Vector2 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimCoroutine(rt, targetPos, targetRot, targetScale));
    }

    IEnumerator AnimCoroutine(RectTransform rt, Vector2 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        float elapsed = 0f;
        Vector2    startPos   = rt.anchoredPosition;
        Quaternion startRot   = rt.localRotation;
        Vector3    startScale = rt.localScale;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / animDuration));
            rt.anchoredPosition = Vector2.Lerp(startPos,   targetPos,   t);
            rt.localRotation    = Quaternion.Slerp(startRot, targetRot, t);
            rt.localScale       = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
        rt.localRotation    = targetRot;
        rt.localScale       = targetScale;
        _animCoroutine      = null;
    }
}
