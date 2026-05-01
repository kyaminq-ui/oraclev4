using UnityEngine;

public class CellHighlight : MonoBehaviour
{
    // =========================================================
    // R�F�RENCES PRIV�ES
    // =========================================================

    private SpriteRenderer spriteRenderer;
    private GridConfig config;
    private Cell linkedCell;

    // Animation de pulsation
    private bool isPulsing = false;
    private float pulseTimer = 0f;
    private Color baseColor;

    [Header("Animation")]
    [Range(0f, 5f)]
    public float pulseSpeed = 2f;

    [Range(0f, 1f)]
    public float pulseIntensity = 0.3f;

    // =========================================================
    // INITIALISATION � Appel�e par GridManager
    // =========================================================

    public void Initialize(Cell cell, GridConfig gridConfig)
    {
        linkedCell = cell;
        config = gridConfig;

        // R�cup�rer ou cr�er le SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Appliquer le sprite de config si disponible
        if (config.cellSprite != null)
            spriteRenderer.sprite = config.cellSprite;

        // Couleur par d�faut
        spriteRenderer.color = config.defaultCellColor;
        baseColor = config.defaultCellColor;

        // Nommer le GameObject pour la Hierarchy
        gameObject.name = $"Cell_{cell.GridX}_{cell.GridY}";

        // ─────────────────────────────────────────────────────────────
        // CORRECTION ISO : décaler le visuel vers le haut pour aligner
        // la grille sur la face supérieure des blocs (pas dans la tranche).
        // ─────────────────────────────────────────────────────────────
        if (config.cellHighlightYOffset != 0f)
        {
            Vector3 p = transform.position;
            p.y += config.cellHighlightYOffset;
            transform.position = p;
        }
    }

    // =========================================================
    // UPDATE � Animation pulsation
    // =========================================================

    void Update()
    {
        if (!isPulsing) return;

        pulseTimer += Time.deltaTime * pulseSpeed;

        // Sin oscille entre -1 et 1, on ram�ne en 0-1
        float sinValue = (Mathf.Sin(pulseTimer) + 1f) / 2f;

        // Lerp entre baseColor et blanc
        Color pulseColor = Color.Lerp(baseColor, Color.white, sinValue * pulseIntensity);
        spriteRenderer.color = pulseColor;
    }

    // =========================================================
    // M�THODES PUBLIQUES
    // =========================================================

    /// <summary>Applique un highlight selon le type</summary>
    public void ApplyHighlight(HighlightType type)
    {
        isPulsing = false;
        pulseTimer = 0f;

        switch (type)
        {
            case HighlightType.None:
                SetColor(config.defaultCellColor, false);
                break;

            case HighlightType.Move:
                SetColor(config.moveColor, true);
                break;

            case HighlightType.Attack:
                SetColor(config.attackColor, true);
                break;

            case HighlightType.AoE:
                SetColor(config.aoeColor, true);
                break;

            case HighlightType.Selected:
                SetColor(config.selectedColor, false);
                break;

            case HighlightType.Hover:
                SetColor(config.hoverColor, false);
                break;

            default:
                SetColor(config.defaultCellColor, false);
                break;
        }
    }

    /// <summary>Applique une couleur arbitraire (utilisé pour les dégradés PM)</summary>
    public void ApplyColor(Color color, bool pulse = true)
    {
        SetColor(color, pulse);
    }

    /// <summary>Remet la couleur par d�faut et stoppe la pulsation</summary>
    public void ResetColor()
    {
        isPulsing = false;
        pulseTimer = 0f;
        baseColor = config.defaultCellColor;
        spriteRenderer.color = config.defaultCellColor;
    }

    /// <summary>Affiche ou cache ce visuel</summary>
    public void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }

    // =========================================================
    // M�THODE PRIV�E
    // =========================================================

    void SetColor(Color color, bool pulse)
    {
        baseColor = color;
        spriteRenderer.color = color;
        isPulsing = pulse;
    }
}
