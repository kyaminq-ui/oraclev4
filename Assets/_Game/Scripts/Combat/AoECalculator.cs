using System.Collections.Generic;
using UnityEngine;

public static class AoECalculator
{
    /// <summary>Demi-angle du cône en degrés (ouverture totale ≈ 2× cette valeur).</summary>
    public const float DefaultConeHalfAngleDegrees = 52f;

    public static List<Cell> GetAffectedCells(ZoneType zone, Cell origin, Cell target, int radius)
    {
        switch (zone)
        {
            case ZoneType.Self:          return new List<Cell> { origin };
            case ZoneType.SingleTarget:  return new List<Cell> { target };
            case ZoneType.FreeCell:      return new List<Cell> { target };
            case ZoneType.Cross:         return GetCross(target);
            case ZoneType.Circle:        return GetCircleWithObstacles(target, radius);
            case ZoneType.Line:          return GetLine(origin, target);
            case ZoneType.Bounce:        return GetBounce(origin, target);
            case ZoneType.Cone:          return GetCone(origin, target, radius, DefaultConeHalfAngleDegrees);
            case ZoneType.Boost:         return GetCircleWithObstacles(origin, radius);
            default:                     return new List<Cell> { target };
        }
    }

    // =========================================================
    // CROIX — centre + 4 adjacentes
    // =========================================================
    private static List<Cell> GetCross(Cell center)
    {
        var cells = new List<Cell>();
        if (center == null) return cells;
        cells.Add(center);
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            Cell c = GridManager.Instance.GetCell(center.GridX + dx[i], center.GridY + dy[i]);
            if (c != null) cells.Add(c);
        }
        return cells;
    }

    // =========================================================
    // CERCLE — distance Manhattan <= radius, LOS depuis le centre (obstacles)
    // =========================================================
    private static List<Cell> GetCircleWithObstacles(Cell center, int radius)
    {
        var cells = new List<Cell>();
        if (center == null || GridManager.Instance == null) return cells;
        for (int x = center.GridX - radius; x <= center.GridX + radius; x++)
            for (int y = center.GridY - radius; y <= center.GridY + radius; y++)
            {
                int dist = Mathf.Abs(x - center.GridX) + Mathf.Abs(y - center.GridY);
                if (dist > radius) continue;
                Cell c = GridManager.Instance.GetCell(x, y);
                if (c == null) continue;
                if (!HasLineOfSight(center, c)) continue;
                cells.Add(c);
            }
        return cells;
    }

    // =========================================================
    // CÔNE — ouverture angulaire depuis l'origine vers la cible, profondeur Chebyshev
    // =========================================================
    private static List<Cell> GetCone(Cell origin, Cell focal, int range, float halfAngleDeg)
    {
        var cells = new List<Cell>();
        if (origin == null || focal == null || GridManager.Instance == null) return cells;
        int ox = origin.GridX, oy = origin.GridY;
        int fx = focal.GridX - ox, fy = focal.GridY - oy;
        if (fx == 0 && fy == 0) return cells;

        float bearing = Mathf.Atan2(fy, fx) * Mathf.Rad2Deg;
        int gw = GridManager.Instance.GridWidth;
        int gh = GridManager.Instance.GridHeight;

        for (int x = 0; x < gw; x++)
        {
            for (int y = 0; y < gh; y++)
            {
                int dx = x - ox, dy = y - oy;
                if (dx == 0 && dy == 0) continue;
                int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                if (cheb > range) continue;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(bearing, ang)) > halfAngleDeg) continue;
                Cell c = GridManager.Instance.GetCell(x, y);
                if (c == null) continue;
                if (!HasLineOfSight(origin, c)) continue;
                cells.Add(c);
            }
        }
        return cells;
    }

    // =========================================================
    // LIGNE — dans la direction origine→cible, s'arrête aux obstacles
    // =========================================================
    private static List<Cell> GetLine(Cell origin, Cell target)
    {
        var cells = new List<Cell>();
        if (origin == null || target == null) return cells;

        int dx = target.GridX - origin.GridX;
        int dy = target.GridY - origin.GridY;
        int stepX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        int stepY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
        int length = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        int x = origin.GridX + stepX;
        int y = origin.GridY + stepY;
        for (int i = 0; i < length; i++)
        {
            Cell c = GridManager.Instance.GetCell(x, y);
            if (c == null) break;
            cells.Add(c);
            if (!c.IsWalkable) break;
            x += stepX;
            y += stepY;
        }
        return cells;
    }

    // =========================================================
    // REBOND — cible principale + cases adjacentes à la cible
    // =========================================================
    private static List<Cell> GetBounce(Cell origin, Cell primaryTarget)
    {
        var cells = new List<Cell> { primaryTarget };
        if (primaryTarget == null) return cells;
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int i = 0; i < 4; i++)
        {
            Cell c = GridManager.Instance.GetCell(primaryTarget.GridX + dx[i], primaryTarget.GridY + dy[i]);
            if (c != null && c != origin) cells.Add(c);
        }
        return cells;
    }

    // =========================================================
    // LIGNE DE VUE — Bresenham entre deux cases
    // =========================================================
    public static bool HasLineOfSight(Cell from, Cell to)
    {
        if (from == null || to == null) return false;
        int x = from.GridX, y = from.GridY;
        int dx = Mathf.Abs(to.GridX - from.GridX);
        int dy = Mathf.Abs(to.GridY - from.GridY);
        int sx = from.GridX < to.GridX ? 1 : -1;
        int sy = from.GridY < to.GridY ? 1 : -1;
        int err = dx - dy;

        while (x != to.GridX || y != to.GridY)
        {
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 <  dx) { err += dx; y += sy; }
            if (x == to.GridX && y == to.GridY) break;
            Cell c = GridManager.Instance.GetCell(x, y);
            if (c != null && !c.IsWalkable) return false;
        }
        return true;
    }
}
