using UnityEngine;

/// <summary>
/// Configuration de la grille - modifiable dans l'Inspector sans toucher au code
/// Créer via : Clic droit → Create → Grid → Grid Configuration
/// </summary>
[CreateAssetMenu(fileName = "GridConfig", menuName = "Grid/Grid Configuration")]
public class GridConfig : ScriptableObject
{
    [Header("=== DIMENSIONS DE LA GRILLE ===")]
    [Tooltip("Nombre de colonnes (axe X)")]
    [Range(2, 30)]
    public int width = 10;

    [Tooltip("Nombre de lignes (axe Y)")]
    [Range(2, 30)]
    public int height = 10;

    [Header("=== TAILLE DES TUILES ===")]
    [Tooltip("Largeur d'une tuile en unités Unity")]
    public float tileWidth = 1f;

    [Tooltip("Hauteur d'une tuile en unités Unity (généralement tileWidth/2)")]
    public float tileHeight = 0.5f;

    [Header("=== ORIGINE DE LA GRILLE ===")]
    [Tooltip("Position monde du point (0,0) logique pour GridToWorld (coin isométrique de référence).")]
    public Vector3 gridOrigin = Vector3.zero;

        [Header("=== PERSONNAGES — Décalage sprite sur la case ===")]
    [Tooltip("Décalage X/Y appliqué à la position monde d'un personnage pour le centrer visuellement sur la face isométrique.\n" +
             "Ajuster X pour centrer horizontalement, Y pour monter/descendre sur la case.")]
    public Vector2 characterWorldOffset = Vector2.zero;

    [Tooltip("Biais ajouté au sortingOrder du personnage pour qu'il s'affiche toujours au-dessus des highlights de cases.\n" +
             "Valeur par défaut : 1000. Augmenter si des sprites décoratifs passent encore par-dessus.")]
    public int characterSortingBias = 1000;

    [Header("=== SOURIS — Correction picking isométrique ===")]
    [Tooltip("Décalage X/Y soustrait à la position monde de la souris avant de chercher la case.\n" +
             "Régler Y si la sélection de case est trop haute ou trop basse par rapport au curseur.\n" +
             "Doit correspondre au décalage visuel total appliqué aux sprites de cases (cellHighlightYOffset + pivot du sprite).")]
    public Vector2 mousePickOffset = Vector2.zero;

    [Header("=== HIGHLIGHT — Décalage visuel de la grille ===")]
    [Tooltip("Décalage Y appliqué aux sprites de highlight/grille pour les aligner sur le dessus des blocs iso.\n" +
             "Valeur typique = hauteur_tranche_pixels / pixels_par_unité.\n" +
             "Ex : tranche 16px sur sprite à 32 PPU → 0.5  |  tranche 8px → 0.25\n" +
             "Augmenter si la grille apparaît dans la tranche, diminuer si elle flotte au-dessus.")]
    public float cellHighlightYOffset = 0f;

[Header("=== ARÈNE — Sprites ArenaGenerator ===")]
    [Tooltip("Ajoutée à Cell.WorldPosition pour chaque tuile décorative : corrige pivots différents / micro-décalage Z pour le « 2.5D ».")]
    public Vector3 arenaTileSpriteWorldOffset = Vector3.zero;

    [Tooltip("Si non vide, tous les SpriteRenderer des tuiles d'arène utilisent ce Sorting Layer (créer dans Edit → Project Settings → Tags and Layers).")]
    public string arenaTileSortingLayerName = "";

    [Tooltip("Valeur ajoutée au tri calculé (ordre relatif sol / obstacle). Réglage commun : 0.")]
    public int arenaTileSortingOrderBias = 0;

    [Header("=== VISUELS ===")]
    [Tooltip("Sprite utilisé pour afficher une cellule")]
    public Sprite cellSprite;

    [Tooltip("Couleur de base des cellules")]
    public Color defaultCellColor = new Color(1f, 1f, 1f, 0.1f);

    [Header("=== COULEURS DE HIGHLIGHT ===")]
    [Tooltip("Déplacement possible")]
    public Color moveColor = new Color(0.2f, 0.5f, 1f, 0.6f);      // Bleu

    [Tooltip("Zone d'attaque")]
    public Color attackColor = new Color(1f, 0.2f, 0.2f, 0.6f);    // Rouge

    [Tooltip("Zone AoE (Area of Effect)")]
    public Color aoeColor = new Color(1f, 0.6f, 0.1f, 0.6f);       // Orange

    [Tooltip("Cellule sélectionnée")]
    public Color selectedColor = new Color(1f, 1f, 0.2f, 0.8f);    // Jaune

    [Tooltip("Cellule survolée (hover)")]
    public Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 0.4f);   // Gris clair

    [Header("=== COMPORTEMENT ===")]
    [Tooltip("Afficher la grille au démarrage")]
    public bool showGridOnStart = true;

    [Tooltip("Activer le debug (logs dans la console)")]
    public bool debugMode = false;
}
