using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
    // =========================================================
    // MÉTHODE PRINCIPALE — Trouver un chemin entre deux cases
    // =========================================================

    /// <summary>
    /// Trouve le chemin le plus court entre startCell et endCell
    /// Retourne null si aucun chemin n'existe
    /// </summary>
    public List<Cell> FindPath(Cell startCell, Cell endCell)
    {
        // Vérifications de base
        if (startCell == null || endCell == null)
        {
            Debug.LogWarning("Pathfinding : startCell ou endCell est null !");
            return null;
        }

        if (!endCell.IsWalkable)
        {
            Debug.LogWarning("Pathfinding : la cellule d'arrivée n'est pas accessible !");
            return null;
        }

        // ---- Initialisation A* ----
        List<PathfindingNode> openList = new List<PathfindingNode>();    // Cases à explorer
        HashSet<Cell> closedSet = new HashSet<Cell>();                   // Cases déjà explorées

        // Créer le noeud de départ
        PathfindingNode startNode = new PathfindingNode(startCell);
        startNode.gCost = 0;
        startNode.hCost = GetHeuristic(startCell, endCell);
        openList.Add(startNode);

        // ---- Boucle principale A* ----
        while (openList.Count > 0)
        {
            // 1. Prendre le noeud avec le fCost le plus bas
            PathfindingNode currentNode = GetLowestFCost(openList);

            // 2. On a trouvé l'arrivée !
            if (currentNode.cell == endCell)
            {
                return ReconstructPath(currentNode);
            }

            // 3. Marquer comme exploré
            openList.Remove(currentNode);
            closedSet.Add(currentNode.cell);

            // 4. Explorer les voisins
            List<Cell> neighbors = GridManager.Instance.GetNeighbors(currentNode.cell);

            foreach (Cell neighborCell in neighbors)
            {
                // Ignorer si déjà exploré, non accessible, ou occupé par un autre personnage
                if (closedSet.Contains(neighborCell)) continue;
                if (!neighborCell.IsWalkable) continue;
                if (neighborCell.IsOccupied && neighborCell != endCell) continue;

                // Calculer le coût pour atteindre ce voisin
                float newGCost = currentNode.gCost + 1f; // Coût uniforme = 1 par case

                // Est-ce que ce voisin est déjà dans l'open list ?
                PathfindingNode existingNode = FindNodeInList(openList, neighborCell);

                if (existingNode == null)
                {
                    // Nouveau noeud → l'ajouter
                    PathfindingNode newNode = new PathfindingNode(neighborCell);
                    newNode.gCost = newGCost;
                    newNode.hCost = GetHeuristic(neighborCell, endCell);
                    newNode.parent = currentNode;
                    openList.Add(newNode);
                }
                else if (newGCost < existingNode.gCost)
                {
                    // Meilleur chemin trouvé → mettre à jour
                    existingNode.gCost = newGCost;
                    existingNode.parent = currentNode;
                }
            }
        }

        // Aucun chemin trouvé
        Debug.LogWarning($"Pathfinding : aucun chemin entre " +
                         $"({startCell.GridX},{startCell.GridY}) " +   // ← GridX majuscule
                         $"et ({endCell.GridX},{endCell.GridY})");      // ← GridX majuscule
        return null;
    }

    // =========================================================
    // MÉTHODE — Cases accessibles depuis un point
    // =========================================================

    /// <summary>
    /// Retourne toutes les cases accessibles depuis startCell
    /// en maximum maxDistance pas
    /// </summary>
    public List<Cell> GetReachableCells(Cell startCell, int maxDistance)
    {
        List<Cell> reachableCells = new List<Cell>();

        if (startCell == null) return reachableCells;

        // Dictionnaire : Cell → coût minimal pour l'atteindre
        Dictionary<Cell, float> costMap = new Dictionary<Cell, float>();
        Queue<Cell> toExplore = new Queue<Cell>();

        costMap[startCell] = 0;
        toExplore.Enqueue(startCell);

        while (toExplore.Count > 0)
        {
            Cell current = toExplore.Dequeue();
            float currentCost = costMap[current];

            // Explorer les voisins
            List<Cell> neighbors = GridManager.Instance.GetNeighbors(current);

            foreach (Cell neighbor in neighbors)
            {
                if (!neighbor.IsWalkable) continue;
                if (neighbor.IsOccupied) continue;  // cases occupées = impassables pour les déplacements

                float newCost = currentCost + 1f;

                // Dans la portée et pas encore visité (ou chemin moins cher trouvé)
                if (newCost <= maxDistance && !costMap.ContainsKey(neighbor))
                {
                    costMap[neighbor] = newCost;
                    reachableCells.Add(neighbor);
                    toExplore.Enqueue(neighbor);
                }
            }
        }

        return reachableCells;
    }

    // Même que GetReachableCells mais retourne le coût (distance) par case.
    public Dictionary<Cell, int> GetReachableCellsWithDistance(Cell startCell, int maxDistance)
    {
        var result = new Dictionary<Cell, int>();
        if (startCell == null) return result;

        var costMap = new Dictionary<Cell, int>();
        var queue   = new Queue<Cell>();
        costMap[startCell] = 0;
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            Cell current = queue.Dequeue();
            int cost     = costMap[current];
            foreach (Cell neighbor in GridManager.Instance.GetNeighbors(current))
            {
                if (!neighbor.IsWalkable) continue;
                if (neighbor.IsOccupied) continue;  // cases occupées = impassables
                int newCost = cost + 1;
                if (newCost <= maxDistance && !costMap.ContainsKey(neighbor))
                {
                    costMap[neighbor]  = newCost;
                    result[neighbor]   = newCost;
                    queue.Enqueue(neighbor);
                }
            }
        }
        return result;
    }

    // =========================================================
    // MÉTHODES PRIVÉES — Utilitaires A*
    // =========================================================

    /// <summary>
    /// Heuristique : distance de Manhattan entre deux cases
    /// </summary>
    private float GetHeuristic(Cell a, Cell b)
    {
        // ↓ GridX et GridY en majuscule (PascalCase)
        return Mathf.Abs(a.GridX - b.GridX) + Mathf.Abs(a.GridY - b.GridY);
    }

    /// <summary>
    /// Trouve le noeud avec le fCost le plus bas dans la liste
    /// </summary>
    private PathfindingNode GetLowestFCost(List<PathfindingNode> list)
    {
        PathfindingNode lowest = list[0];

        foreach (PathfindingNode node in list)
        {
            if (node.fCost < lowest.fCost)
                lowest = node;
        }

        return lowest;
    }

    /// <summary>
    /// Cherche un noeud dans la liste par sa cellule
    /// </summary>
    private PathfindingNode FindNodeInList(List<PathfindingNode> list, Cell cell)
    {
        foreach (PathfindingNode node in list)
        {
            if (node.cell == cell)
                return node;
        }
        return null;
    }

    /// <summary>
    /// Remonte le chemin depuis l'arrivée jusqu'au départ
    /// </summary>
    private List<Cell> ReconstructPath(PathfindingNode endNode)
    {
        List<Cell> path = new List<Cell>();
        PathfindingNode current = endNode;

        // Remonter les parents jusqu'au départ
        while (current != null)
        {
            path.Add(current.cell);
            current = current.parent;
        }

        // Le chemin est à l'envers (arrivée → départ), on l'inverse
        path.Reverse();

        return path;
    }
}
