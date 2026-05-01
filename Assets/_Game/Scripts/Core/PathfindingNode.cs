/// <summary>
/// Noeud utilisé par l'algorithme A* pendant la recherche de chemin
/// Enveloppe une Cell avec les coûts nécessaires au calcul
/// </summary>
public class PathfindingNode
{
    // =========================================================
    // RÉFÉRENCE À LA CELLULE
    // =========================================================

    /// <summary>La cellule de la grille que représente ce noeud</summary>
    public Cell cell;

    // =========================================================
    // COÛTS A*
    // =========================================================

    /// <summary>
    /// Coût depuis le départ jusqu'à ce noeud
    /// (nombre de cases parcourues)
    /// </summary>
    public float gCost;

    /// <summary>
    /// Estimation du coût depuis ce noeud jusqu'à l'arrivée
    /// (heuristique — distance de Manhattan)
    /// </summary>
    public float hCost;

    /// <summary>
    /// Coût total = gCost + hCost
    /// A* choisit toujours le noeud avec le fCost le plus bas
    /// </summary>
    public float fCost => gCost + hCost;

    // =========================================================
    // PARENT
    // =========================================================

    /// <summary>
    /// Noeud précédent dans le chemin
    /// Permet de reconstruire le chemin complet à la fin
    /// </summary>
    public PathfindingNode parent;

    // =========================================================
    // CONSTRUCTEUR
    // =========================================================

    /// <summary>
    /// Créer un noeud A* à partir d'une cellule
    /// </summary>
    public PathfindingNode(Cell cell)
    {
        this.cell = cell;
        this.gCost = 0;
        this.hCost = 0;
        this.parent = null;
    }
}
