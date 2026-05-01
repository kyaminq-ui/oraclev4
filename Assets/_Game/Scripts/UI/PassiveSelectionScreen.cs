using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Écran de sélection de passif — Oracle v2 redesign minimaliste.
/// Logique identique à l'ancienne version, cosmétique allégé.
/// </summary>
public class PassiveSelectionScreen : MonoBehaviour
{
    // =========================================================
    // ÉVÉNEMENT
    // =========================================================
    public event System.Action<PassiveData> OnPassiveSelected;

    // =========================================================
    // RÉFÉRENCES
    // =========================================================
    [Header("Données")]
    public PassivePool passivePool;

    [Header("Cartes")]
    public List<PassiveCardUI> cards = new();

    [Header("Timer (sélection ~30 s)")]
    [Tooltip("Fond / silhouette du chrono (ex. timer_icon_hud).")]
    public Image             timerIcon;
    public Image             timerFill;
    public TextMeshProUGUI   timerText;
    public float             selectionDuration = 30f;

    [Tooltip("Nombre de passifs proposés (max = nb de PassiveCardUI). Spec : 5.")]
    [Range(1, 18)]
    public int offeredPassiveCount = 5;

    [Header("Bouton confirmer")]
    public Button            confirmButton;

    [Header("Récap")]
    public GameObject        recapPanel;
    public TextMeshProUGUI   recapText;

    // =========================================================
    // ÉTAT
    // =========================================================
    private List<PassiveData> displayedPassives = new();
    private PassiveCardUI     selectedCard;
    private float             timeRemaining;
    private bool              selectionDone;

    // =========================================================
    // OUVERTURE
    // =========================================================
    public void Show()
    {
        gameObject.SetActive(true);
        FixStretchLayoutIfBroken();
        EnsureConfirmButtonHorizontallyCentered();
        transform.SetAsLastSibling();
        EnsureFrontCanvas();
        RemovePassiveCardHpBarDecorClones();
        OracleHudRuntimeSprites.ApplyPassiveTimerIfMissing(timerIcon);

        OracleUIImportantFont.Apply(timerText);
        ApplyPassiveScreenAsepriteFonts();

        selectionDone = false;
        selectedCard  = null;
        timeRemaining = selectionDuration;

        if (passivePool == null)
        {
            Debug.LogError("[PassiveSelectionScreen] PassivePool non assigné !");
            return;
        }

        int offer = Mathf.Min(cards.Count, Mathf.Max(1, offeredPassiveCount));
        displayedPassives = passivePool.GetRandom(offer);

        if (displayedPassives.Count == 0)
        {
            Debug.LogError("[PassiveSelectionScreen] PassivePool vide.");
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            bool hasData = i < displayedPassives.Count;
            cards[i].gameObject.SetActive(hasData);
            if (!hasData) continue;

            cards[i].Setup(displayedPassives[i]);
            int idx = i;
            var btn = cards[i].GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectCard(idx));
            }
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = false;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }

        if (recapPanel != null) recapPanel.SetActive(false);

        BringPassiveTimerOnTopAndStyleText();
        if (timerFill != null)
        {
            var c = timerFill.color;
            timerFill.color = new Color(0.788f, 0.659f, 0.298f,
                c.a > 0.01f ? c.a : 0.45f);
        }
    }

    /// <summary>Titre (« Choisis … »), recap, bouton confirmer : police Aseprite (<see cref="OracleUIImportantFont"/>).</summary>
    void ApplyPassiveScreenAsepriteFonts()
    {
        var titleTf = transform.Find("Title");
        if (titleTf != null)
            OracleUIImportantFont.Apply(titleTf.GetComponent<TextMeshProUGUI>());

        foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp == null) continue;
            string s = tmp.text;
            if (!string.IsNullOrEmpty(s) && s.TrimStart().IndexOf("Choisis", StringComparison.OrdinalIgnoreCase) >= 0)
                OracleUIImportantFont.Apply(tmp);
        }

        OracleUIImportantFont.Apply(recapText);

        if (confirmButton != null)
            OracleUIImportantFont.Apply(confirmButton.GetComponentInChildren<TextMeshProUGUI>(true));
    }

    /// <summary>
    /// Ancien builder minimalist « Separator » (ligne horizontale) pouvait ressembler à une piste de barre HP.
    /// </summary>
    void RemovePassiveCardHpBarDecorClones()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null) continue;
            var sep = cards[i].transform.Find("Separator");
            if (sep != null)
                Destroy(sep.gameObject);
        }
    }

    /// <summary>
    /// Le groupe timer passe au-dessus du titre / cartes ; le décompte est centré sur l’icône.
    /// </summary>
    void BringPassiveTimerOnTopAndStyleText()
    {
        Transform corner = timerIcon != null
            ? timerIcon.transform.parent
            : (transform.Find("TimerCorner") ?? transform.Find("TimerContainer"));
        if (corner != null)
        {
            corner.gameObject.SetActive(true);
            corner.SetAsLastSibling();
        }

        if (timerText != null)
        {
            timerText.enableWordWrapping = false;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontStyle = FontStyles.Bold;
            timerText.outlineWidth = 0.18f;
            timerText.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        }
        if (timerIcon != null) timerIcon.enabled = true;
        if (timerFill != null) timerFill.enabled = true;
    }

    // =========================================================
    // UPDATE — TIMER
    // =========================================================
    void Update()
    {
        if (!gameObject.activeSelf || selectionDone) return;

        timeRemaining -= Time.unscaledDeltaTime;

        float denom = Mathf.Max(0.01f, selectionDuration);
        float ratio = Mathf.Clamp01(timeRemaining / denom);
        if (timerFill != null) timerFill.fillAmount = ratio;
        int secLeft = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining));
        if (timerText != null) timerText.text = secLeft.ToString();

        if (timeRemaining <= 0f) AutoSelect();
    }

    // =========================================================
    // SÉLECTION
    // =========================================================
    private void SelectCard(int index)
    {
        if (selectionDone || index >= cards.Count) return;

        if (selectedCard != null) selectedCard.SetSelected(false);
        selectedCard = cards[index];
        selectedCard.SetSelected(true);

        if (confirmButton != null) confirmButton.interactable = true;
    }

    private void Confirm()
    {
        if (selectedCard == null) { AutoSelect(); return; }
        FinalizeSelection(selectedCard.Data);
    }

    private void AutoSelect()
    {
        if (selectionDone) return;
        FinalizeSelection(displayedPassives[UnityEngine.Random.Range(0, displayedPassives.Count)]);
    }

    /// <summary>
    /// Sélectionne immédiatement un passif aléatoire (pour les tests solo / IA).
    /// </summary>
    public void AutoSelectInstant()
    {
        if (selectionDone) return;
        if (displayedPassives == null || displayedPassives.Count == 0)
        {
            // L'écran n'a pas encore été affiché — on force l'événement sans données visuelles
            OnPassiveSelected?.Invoke(null);
            selectionDone = true;
            return;
        }
        FinalizeSelection(displayedPassives[UnityEngine.Random.Range(0, displayedPassives.Count)]);
    }

    private void FinalizeSelection(PassiveData passive)
    {
        selectionDone = true;
        ShowRecap(passive);
        OnPassiveSelected?.Invoke(passive);
    }

    // =========================================================
    // RÉCAP
    // =========================================================
    private void ShowRecap(PassiveData passive)
    {
        if (recapPanel  == null) return;
        recapPanel.SetActive(true);
        if (recapText != null)
            recapText.text = $"Passif choisi : {passive.passiveName}";
    }

    public void Hide() => gameObject.SetActive(false);

    // =========================================================
    // HELPERS INTERNES
    // =========================================================
    /// <summary>
    /// Corrige l’alignement du bouton Confirmer pour les scènes construites avec un décalage X
    /// (ex. ancien builder) ou un enfant étiré dans un <c>ConfirmWrap</c> pleine largeur.
    /// </summary>
    void EnsureConfirmButtonHorizontallyCentered()
    {
        if (confirmButton == null) return;
        var btnRT = confirmButton.transform as RectTransform;
        if (btnRT == null) return;

        var parent = confirmButton.transform.parent;

        // Ancrage bas-centre avec offset X ≠ 0 (ex. OraclePassiveScreenBuilder -100)
        if (Mathf.Approximately(btnRT.anchorMin.x, 0.5f) && Mathf.Approximately(btnRT.anchorMax.x, 0.5f) &&
            Mathf.Approximately(btnRT.anchorMin.y, 0f) && Mathf.Approximately(btnRT.anchorMax.y, 0f))
        {
            if (!Mathf.Approximately(btnRT.anchoredPosition.x, 0f))
                btnRT.anchoredPosition = new Vector2(0f, btnRT.anchoredPosition.y);
            return;
        }

        // v1 « ConfirmWrap » : bouton étiré → le recentrer en 200px avec HLG sur le parent
        if (parent != null && parent.name == "ConfirmWrap")
        {
            if (parent.GetComponent<HorizontalLayoutGroup>() == null)
            {
                var hlg = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.padding                = new RectOffset(0, 0, 0, 0);
                hlg.spacing                = 0f;
                hlg.childAlignment         = TextAnchor.MiddleCenter;
                hlg.childControlWidth     = false;
                hlg.childControlHeight    = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
            }

            btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot     = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = Vector2.zero;
            btnRT.sizeDelta   = new Vector2(200f, 52f);
            btnRT.localScale  = Vector3.one;

            var le = confirmButton.GetComponent<LayoutElement>();
            if (le == null) le = confirmButton.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth  = 200f;
            le.preferredHeight = 52f;
        }
    }

    void FixStretchLayoutIfBroken()
    {
        var rt = GetComponent<RectTransform>();
        if (rt.anchorMax.x <= 0.01f && rt.anchorMax.y <= 0.01f &&
            rt.sizeDelta.x  <= 4f   && rt.sizeDelta.y  <= 4f)
        {
            rt.anchorMin = rt.offsetMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMax = rt.anchoredPosition = Vector2.zero;
        }

        var bgTr = transform.Find("Background") as RectTransform;
        if (bgTr != null &&
            bgTr.anchorMax.x <= 0.01f && bgTr.anchorMax.y <= 0.01f &&
            bgTr.sizeDelta.x  <= 4f   && bgTr.sizeDelta.y  <= 4f)
            StretchRect(bgTr);
    }

    static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = rt.offsetMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMax = rt.anchoredPosition = Vector2.zero;
    }

    void EnsureFrontCanvas()
    {
        var c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 5000;

        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }
}
