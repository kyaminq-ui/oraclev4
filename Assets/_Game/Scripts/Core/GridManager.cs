using UnityEngine;
using System.Collections.Generic;
public class GridManager : MonoBehaviour
{
    // =========================================================
    // SINGLETON
    // =========================================================
    public static GridManager Instance { get; private set; }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeGrid();
    }
    // =========================================================
    // CONFIGURATION
    // =========================================================
    [Header("=== CONFIGURATION ===")]
    public GridConfig config;

    public int GridWidth  => config != null ? config.width  : 0;
    public int GridHeight => config != null ? config.height : 0;
    [Header("=== PREFABS ===")]
    public GameObject cellPrefab;
    // =========================================================
    // DONNÉES PRIVÉES
    // =========================================================
    private Cell[,] grid;
    private Transform gridContainer;
    /// <summary>Racine des objets de cellule (« === GRID === »). Utiliser pour rattacher les visuels d'arène aux mêmes transformations que la grille.</summary>
    public Transform GridVisualRoot => gridContainer;

    private List<Cell> highlightedCells = new List<Cell>();
    private Cell selectedCell = null;
    private Cell hoveredCell = null;
    // =========================================================
    // INITIALISATION
    // =========================================================
    void InitializeGrid()
    {
        if (config == null)
        {
            Debug.LogError("GridManager : Aucun GridConfig assigné !");
            return;
        }
        gridContainer = new GameObject("=== GRID ===").transform;
        gridContainer.SetParent(transform);
        grid = new Cell[config.width, config.height];
        for (int x = 0; x < config.width; x++)
            for (int y = 0; y < config.height; y++)
                CreateCell(x, y);
        Debug.Log($"Grille {config.width}x{config.height} créée.");
    }
    void CreateCell(int x, int y)
    {
        Vector3 worldPos = GridToWorld(x, y);
        Cell cell = new Cell(x, y, worldPos);
        grid[x, y] = cell;
        CreateCellVisual(cell, worldPos);
    }
    void CreateCellVisual(Cell cell, Vector3 worldPos)
    {
        GameObject cellObject;
        if (cellPrefab != null)
            cellObject = Instantiate(cellPrefab, worldPos, Quaternion.identity, gridContainer);
        else
        {
            cellObject = new GameObject($"Cell_{cell.GridX}_{cell.GridY}");
            cellObject.transform.position = worldPos;
            cellObject.transform.SetParent(gridContainer);
        }
        CellHighlight highlight = cellObject.GetComponent<CellHighlight>();
        if (highlight == null)
            highlight = cellObject.AddComponent<CellHighlight>();
        highlight.Initialize(cell, config);
        cell.VisualObject = cellObject;
        highlight.SetVisible(config.showGridOnStart);
    }
    // =========================================================
    // ACCÈS AUX CELLULES
    // =========================================================
    public Cell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y)) return null;
        return grid[x, y];
    }
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < config.width && y >= 0 && y < config.height;
    }
    // Surcharge qui accepte directement une Cell
    public List<Cell> GetNeighbors(Cell cell, bool includeDiagonals = false)
    {
        return GetNeighbors(cell.GridX, cell.GridY, includeDiagonals);
    }
    public List<Cell> GetNeighbors(int x, int y, bool includeDiagonals = false)
    {
        List<Cell> neighbors = new List<Cell>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        int[] dxDiag = { 1, 1, -1, -1 };
        int[] dyDiag = { 1, -1, 1, -1 };
        for (int i = 0; i < 4; i++)
        {
            Cell neighbor = GetCell(x + dx[i], y + dy[i]);
            if (neighbor != null) neighbors.Add(neighbor);
        }
        if (includeDiagonals)
        {
            for (int i = 0; i < 4; i++)
            {
                Cell neighbor = GetCell(x + dxDiag[i], y + dyDiag[i]);
                if (neighbor != null) neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }
    // =========================================================
    // CONVERSIONS — Grille logique (col,row) ↔ Monde X/Y
    // Iso 2:1 classique (identique à Tilemap Isometric pointy en espace "diamant") :
    //   worldX = origin.x + (col - row) * (tileWidth  / 2)
    //   worldY = origin.y + (col + row) * (tileHeight / 2)
    // Remarque : il n'y a pas de composant Unity Grid/Tilemap dans ce projet — seulement ces formules et des SpriteRenderer.
    // =========================================================
    public Vector3 GridToWorld(int gridX, int gridY)
    {
        float worldX = (gridX - gridY) * (config.tileWidth / 2f);
        float worldY = (gridX + gridY) * (config.tileHeight / 2f);
        return new Vector3(
        config.gridOrigin.x + worldX,
        config.gridOrigin.y + worldY,
        config.gridOrigin.z
        );
    }
    public Vector3 GridToWorldFace(int gridX, int gridY)
    {
        Vector3 base_ = GridToWorld(gridX, gridY);
        return base_ + new Vector3(config.characterWorldOffset.x, config.characterWorldOffset.y, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float localX = worldPosition.x - config.gridOrigin.x;
        float localY = worldPosition.y - config.gridOrigin.y;
        float gridXf = (localX / config.tileWidth + localY / config.tileHeight);
        float gridYf = (localY / config.tileHeight - localX / config.tileWidth);
        return new Vector2Int(Mathf.RoundToInt(gridXf), Mathf.RoundToInt(gridYf));
    }
    public Cell GetCellFromWorldPosition(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        return GetCell(gridPos.x, gridPos.y);
    }

    // Projects a screen point onto the world z=0 plane via ray-casting.
    // Works for both orthographic and perspective cameras — avoids ScreenToWorldPoint z issues.
    bool ScreenToWorldOnPlane(Camera cam, Vector2 screenPos, out Vector3 worldPos)
    {
        Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        Plane plane = new Plane(Vector3.forward, Vector3.zero); // world z = 0
        if (plane.Raycast(ray, out float dist))
        {
            worldPos = ray.GetPoint(dist);
            return true;
        }
        worldPos = Vector3.zero;
        return false;
    }

    // Diamond hit-test picking: checks the mouse against the actual losange shape of each
    // candidate cell (approx cell + 3×3 neighbours) using the visual centre (with cellHighlightYOffset).
    // This is the correct picking method for isometric grids.
    public Cell GetCellAtScreenPosition(Camera cam, Vector2 screenPos)
    {
        if (!ScreenToWorldOnPlane(cam, screenPos, out Vector3 worldPos))
            return null;

        Vector2Int approx = WorldToGrid(worldPos);
        float halfW  = config.tileWidth  / 2f;
        float halfH  = config.tileHeight / 2f;
        float visualY = config.cellHighlightYOffset;

        Cell  best     = null;
        float bestDist = float.MaxValue;

        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            Cell cell = GetCell(approx.x + dx, approx.y + dy);
            if (cell == null) continue;

            Vector3 center = GridToWorld(cell.GridX, cell.GridY);
            center.y += visualY;

            float dist = Mathf.Abs(worldPos.x - center.x) / halfW
                       + Mathf.Abs(worldPos.y - center.y) / halfH;

            if (dist < bestDist) { bestDist = dist; best = cell; }
        }

        return bestDist <= 1f ? best : null;
    }
    // =========================================================
    // HIGHLIGHTS
    // =========================================================
    // Highlight une case avec une couleur directe (pour dégradés distance).
    public void HighlightCell(Cell cell, Color color, bool pulse = true)
    {
        if (cell == null) return;
        cell.SetHighlight(HighlightType.Move);
        if (!highlightedCells.Contains(cell))
            highlightedCells.Add(cell);
        if (cell.VisualObject != null)
        {
            CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
            h?.ApplyColor(color, pulse);
        }
    }

    public void HighlightCells(List<Cell> cells, HighlightType type)
    {
        foreach (Cell cell in cells)
        {
            if (cell == null) continue;
            cell.SetHighlight(type);
            highlightedCells.Add(cell);
            if (cell.VisualObject != null)
            {
                CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
                h?.ApplyHighlight(type);
            }
        }
    }
    public void HighlightCell(int x, int y, HighlightType type)
    {
        Cell cell = GetCell(x, y);
        if (cell == null) return;
        cell.SetHighlight(type);
        if (!highlightedCells.Contains(cell))
            highlightedCells.Add(cell);
        if (cell.VisualObject != null)
        {
            CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
            h?.ApplyHighlight(type);
        }
    }
    public void ClearAllHighlights()
    {
        foreach (Cell cell in highlightedCells)
        {
            if (cell == null) continue;
            cell.ClearHighlight();
            if (cell.VisualObject != null)
            {
                CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
                h?.ResetColor();
            }
        }
        highlightedCells.Clear();
    }
    public void HighlightRange(int centerX, int centerY, int range, HighlightType type)
    {
        List<Cell> cells = new List<Cell>();
        for (int x = centerX - range; x <= centerX + range; x++)
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                if (x == centerX && y == centerY) continue;
                Cell cell = GetCell(x, y);
                if (cell != null && cell.IsWalkable && !cell.IsOccupied) cells.Add(cell);
            }
        HighlightCells(cells, type);
    }
    public void HighlightDiamond(int centerX, int centerY, int range, HighlightType type)
    {
        List<Cell> cells = new List<Cell>();
        for (int x = centerX - range; x <= centerX + range; x++)
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                int dist = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
                if (dist <= range && !(x == centerX && y == centerY))
                {
                    Cell cell = GetCell(x, y);
                    if (cell != null) cells.Add(cell);
                }
            }
        HighlightCells(cells, type);
    }
    // =========================================================
    // SÉLECTION
    // =========================================================
    public void SelectCell(int x, int y)
    {
        if (selectedCell != null)
        {
            selectedCell.IsSelected = false;
            if (selectedCell.VisualObject != null)
            {
                CellHighlight h = selectedCell.VisualObject.GetComponent<CellHighlight>();
                h?.ApplyHighlight(selectedCell.CurrentHighlight);
            }
        }
        Cell cell = GetCell(x, y);
        if (cell == null) return;
        selectedCell = cell;
        cell.IsSelected = true;
        if (cell.VisualObject != null)
        {
            CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
            h?.ApplyHighlight(HighlightType.Selected);
        }
    }
    public void SetHoveredCell(int x, int y)
    {
        if (hoveredCell != null && !hoveredCell.IsSelected)
        {
            hoveredCell.IsHovered = false;
            if (hoveredCell.VisualObject != null)
            {
                CellHighlight h = hoveredCell.VisualObject.GetComponent<CellHighlight>();
                h?.ApplyHighlight(hoveredCell.CurrentHighlight);
            }
        }
        Cell cell = GetCell(x, y);
        if (cell == null) { hoveredCell = null; return; }
        hoveredCell = cell;
        cell.IsHovered = true;
        if (!cell.IsSelected && cell.VisualObject != null)
        {
            CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
            h?.ApplyHighlight(HighlightType.Hover);
        }
    }
    // =========================================================
    // GESTION DES OCCUPANTS
    // =========================================================
    public bool PlaceObject(GameObject obj, int x, int y)
    {
        Cell cell = GetCell(x, y);
        if (cell == null) return false;
        if (cell.IsOccupied) return false;
        if (!cell.IsWalkable) return false;
        cell.SetOccupant(obj);
        obj.transform.position = GridToWorldFace(x, y);
        return true;
    }
    public void RemoveObject(int x, int y)
    {
        Cell cell = GetCell(x, y);
        cell?.ClearOccupant();
    }
    public bool MoveObject(int fromX, int fromY, int toX, int toY)
    {
        Cell fromCell = GetCell(fromX, fromY);
        Cell toCell = GetCell(toX, toY);
        if (fromCell == null || toCell == null) return false;
        if (!fromCell.IsOccupied || toCell.IsOccupied || !toCell.IsWalkable) return false;
        GameObject obj = fromCell.Occupant;
        fromCell.ClearOccupant();
        toCell.SetOccupant(obj);
        obj.transform.position = GridToWorldFace(toX, toY);
        return true;
    }
    // =========================================================
    // RÉGÉNÉRATION
    // =========================================================

    /// <summary>
    /// Supprime et reconstruit entièrement la grille.
    /// Utile après avoir modifié GridConfig.width / GridConfig.height au runtime.
    /// </summary>
    public void RegenerateGrid()
    {
        if (gridContainer != null)
        {
            if (Application.isPlaying)
                Destroy(gridContainer.gameObject);
            else
                DestroyImmediate(gridContainer.gameObject);
        }

        highlightedCells.Clear();
        selectedCell = null;
        hoveredCell = null;

        InitializeGrid();
    }

    // =========================================================
    // VISIBILITÉ
    // =========================================================
    public void SetGridVisible(bool visible)
    {
        for (int x = 0; x < config.width; x++)
            for (int y = 0; y < config.height; y++)
            {
                Cell cell = grid[x, y];
                if (cell?.VisualObject != null)
                {
                    CellHighlight h = cell.VisualObject.GetComponent<CellHighlight>();
                    h?.SetVisible(visible);
                }
            }
    }
    // =========================================================
    // GIZMOS
    // =========================================================
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (config == null) return;
        for (int x = 0; x < config.width; x++)
        {
            for (int y = 0; y < config.height; y++)
            {
                Vector3 pos = GridToWorld(x, y);
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.3f);
                float hw = config.tileWidth / 2f;
                float hh = config.tileHeight / 2f;
                Vector3 top = pos + new Vector3(0, hh, 0);
                Vector3 bottom = pos + new Vector3(0, -hh, 0);
                Vector3 left = pos + new Vector3(-hw, 0, 0);
                Vector3 right = pos + new Vector3(hw, 0, 0);
                Gizmos.DrawLine(top, right);
                Gizmos.DrawLine(right, bottom);
                Gizmos.DrawLine(bottom, left);
                Gizmos.DrawLine(left, top);
                if (config.width <= 10 && config.height <= 10)
                    UnityEditor.Handles.Label(pos, $"{x},{y}");
            }
        }
    }
#endif
}