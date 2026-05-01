// ================================================================
// IsoGridFixer.cs — Script de patch automatique
// Placer dans : Assets/_Game/Scripts/Editor/IsoGridFixer.cs
// Lancer via : Menu Unity → Oracle → Fix Iso Grid Alignment
// ================================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class IsoGridFixer
{
    const string PATH_GRID_CONFIG    = "Assets/_Game/Scripts/Core/GridConfig.cs";
    const string PATH_GRID_MANAGER   = "Assets/_Game/Scripts/Core/GridManager.cs";
    const string PATH_CELL_HIGHLIGHT = "Assets/_Game/Scripts/Core/CellHighlight.cs";
    const string PATH_TACTICAL_CHAR  = "Assets/_Game/Scripts/Combat/TacticalCharacter.cs";

    [MenuItem("Oracle/Fix Iso Grid Alignment")]
    public static void Run()
    {
        int fixedCount = 0;
        bool allOk = true;

        allOk &= PatchGridConfig(ref fixedCount);
        allOk &= PatchGridManager(ref fixedCount);
        allOk &= PatchCellHighlight(ref fixedCount);
        allOk &= PatchTacticalCharacter(ref fixedCount);

        AssetDatabase.Refresh();

        if (allOk)
        {
            string msg = fixedCount > 0
                ? $"{fixedCount} fichier(s) patché(s) avec succès."
                : "Tous les fichiers étaient déjà patchés — rien à faire.";

            Debug.Log($"[IsoGridFixer] ✅ {msg}\n" +
                      "👉 Régler 'Cell Highlight Y Offset' dans GridConfig_Combat.\n" +
                      "   Monter par pas de 0.1 jusqu'à aligner la grille sur la face des blocs.");

            EditorUtility.DisplayDialog("IsoGridFixer — Succès", msg +
                "\n\nRégler « Cell Highlight Y Offset » dans GridConfig_Combat\n" +
                "(monter par pas de 0.1 pour aligner la grille sur la face des blocs).", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("IsoGridFixer — Erreurs",
                "Certains fichiers n'ont pas pu être patchés.\nVoir la Console pour le détail.", "OK");
        }
    }

    // ================================================================
    // PATCH 1 — GridConfig.cs : ajoute cellHighlightYOffset
    // ================================================================
    static bool PatchGridConfig(ref int count)
    {
        string src = ReadFile(PATH_GRID_CONFIG);
        if (src == null) return false;

        if (src.Contains("cellHighlightYOffset"))
        {
            Debug.Log($"[IsoGridFixer] ⏭ {PATH_GRID_CONFIG} — déjà patché.");
            return true;
        }

        // Cherche le premier [Header( après les champs de base pour insérer avant
        // On cible le header Arène qui est stable
        string anchor = "[Header(\"=== ARÈNE";
        if (!src.Contains(anchor))
            anchor = "[Header(\"=== AR"; // fallback partiel

        if (!src.Contains(anchor))
        {
            Debug.LogError($"[IsoGridFixer] {PATH_GRID_CONFIG} : ancre header introuvable.");
            return false;
        }

        string insert =
            "    [Header(\"=== HIGHLIGHT — Décalage visuel de la grille ===\")]\n" +
            "    [Tooltip(\"Décalage Y appliqué aux highlights pour les aligner sur la face supérieure des blocs iso.\\n\" +\n" +
            "             \"Valeur typique = hauteur_tranche_px / pixels_par_unité (ex: 16px / 32PPU = 0.5).\\n\" +\n" +
            "             \"Augmenter si la grille est dans la tranche, diminuer si elle flotte.\")]\n" +
            "    public float cellHighlightYOffset = 0f;\n\n";

        src = src.Replace(anchor, insert + anchor);
        return WriteFile(PATH_GRID_CONFIG, src, ref count);
    }

    // ================================================================
    // PATCH 2 — GridManager.cs
    // Ajoute GridToWorldFace() via regex, corrige PlaceObject/MoveObject
    // ================================================================
    static bool PatchGridManager(ref int count)
    {
        string src = ReadFile(PATH_GRID_MANAGER);
        if (src == null) return false;

        bool changed = false;

        // ── A) Ajouter GridToWorldFace si absent ──────────────────────────
        if (!src.Contains("GridToWorldFace"))
        {
            // Regex : trouve la méthode GridToWorld complète et insère GridToWorldFace juste après
            // Pattern robuste : s'arrête à la } fermante de GridToWorld (première } après le return)
            var match = Regex.Match(src,
                @"public Vector3 GridToWorld\(int gridX, int gridY\)\s*\{[^}]+\}",
                RegexOptions.Singleline);

            if (!match.Success)
            {
                Debug.LogError("[IsoGridFixer] GridManager.cs : méthode GridToWorld introuvable par regex.");
                return false;
            }

            string newMethod =
                "\n\n    /// <summary>\n" +
                "    /// Comme GridToWorld mais décalé de cellHighlightYOffset en Y.\n" +
                "    /// Positionne sur la face supérieure du bloc iso (corrige le décalage tranche/pivot).\n" +
                "    /// </summary>\n" +
                "    public Vector3 GridToWorldFace(int gridX, int gridY)\n" +
                "    {\n" +
                "        Vector3 pos = GridToWorld(gridX, gridY);\n" +
                "        pos.y += config.cellHighlightYOffset;\n" +
                "        return pos;\n" +
                "    }";

            int insertAt = match.Index + match.Length;
            src = src.Insert(insertAt, newMethod);
            changed = true;
            Debug.Log("[IsoGridFixer] GridManager.cs : GridToWorldFace inséré.");
        }

        // ── B) PlaceObject : cell.WorldPosition → GridToWorldFace ────────
        if (src.Contains("obj.transform.position = cell.WorldPosition;"))
        {
            src = src.Replace(
                "obj.transform.position = cell.WorldPosition;",
                "obj.transform.position = GridToWorldFace(x, y);");
            changed = true;
        }

        // ── C) MoveObject : toCell.WorldPosition → GridToWorldFace ───────
        if (src.Contains("obj.transform.position = toCell.WorldPosition;"))
        {
            src = src.Replace(
                "obj.transform.position = toCell.WorldPosition;",
                "obj.transform.position = GridToWorldFace(toX, toY);");
            changed = true;
        }

        if (!changed)
        {
            Debug.Log($"[IsoGridFixer] ⏭ {PATH_GRID_MANAGER} — déjà patché.");
            return true;
        }

        return WriteFile(PATH_GRID_MANAGER, src, ref count);
    }

    // ================================================================
    // PATCH 3 — CellHighlight.cs : applique l'offset dans Initialize
    // ================================================================
    static bool PatchCellHighlight(ref int count)
    {
        string src = ReadFile(PATH_CELL_HIGHLIGHT);
        if (src == null) return false;

        if (src.Contains("cellHighlightYOffset"))
        {
            Debug.Log($"[IsoGridFixer] ⏭ {PATH_CELL_HIGHLIGHT} — déjà patché.");
            return true;
        }

        // Ancre : dernière ligne de Initialize, le nom du gameObject
        string anchor = "gameObject.name = $\"Cell_{cell.GridX}_{cell.GridY}\";";
        if (!src.Contains(anchor))
        {
            // Fallback : cherche la fin de la méthode Initialize par regex
            var m = Regex.Match(src, @"gameObject\.name\s*=\s*\$""Cell_[^;]+"";");
            if (!m.Success)
            {
                Debug.LogError($"[IsoGridFixer] {PATH_CELL_HIGHLIGHT} : ancre Initialize introuvable.");
                return false;
            }
            anchor = m.Value;
        }

        string insert =
            "\n\n        // ── CORRECTION ISO ────────────────────────────────────────────\n" +
            "        // Décale le visuel vers le haut pour aligner la grille sur la\n" +
            "        // face supérieure des blocs (et non dans leur tranche latérale).\n" +
            "        // Configurer cellHighlightYOffset dans GridConfig.\n" +
            "        // ─────────────────────────────────────────────────────────────\n" +
            "        if (config.cellHighlightYOffset != 0f)\n" +
            "        {\n" +
            "            Vector3 p = transform.position;\n" +
            "            p.y += config.cellHighlightYOffset;\n" +
            "            transform.position = p;\n" +
            "        }";

        src = src.Replace(anchor, anchor + insert);
        return WriteFile(PATH_CELL_HIGHLIGHT, src, ref count);
    }

    // ================================================================
    // PATCH 4 — TacticalCharacter.cs : WorldPosition → GridToWorldFace
    // ================================================================
    static bool PatchTacticalCharacter(ref int count)
    {
        string src = ReadFile(PATH_TACTICAL_CHAR);
        if (src == null) return false;

        if (src.Contains("GridToWorldFace"))
        {
            Debug.Log($"[IsoGridFixer] ⏭ {PATH_TACTICAL_CHAR} — déjà patché.");
            return true;
        }

        bool changed = false;

        // Initialize — position initiale
        if (src.Contains("transform.position = startCell.WorldPosition;"))
        {
            src = src.Replace(
                "transform.position = startCell.WorldPosition;",
                "transform.position = GridManager.Instance != null\n" +
                "            ? GridManager.Instance.GridToWorldFace(startCell.GridX, startCell.GridY)\n" +
                "            : startCell.WorldPosition;");
            changed = true;
        }

        // Mouvement animé — plusieurs variantes possibles
        if (src.Contains("Vector3 start = transform.position, end = next.WorldPosition;"))
        {
            src = src.Replace(
                "Vector3 start = transform.position, end = next.WorldPosition;",
                "Vector3 start = transform.position;\n" +
                "            Vector3 end = GridManager.Instance != null\n" +
                "                ? GridManager.Instance.GridToWorldFace(next.GridX, next.GridY)\n" +
                "                : next.WorldPosition;");
            changed = true;
        }
        else if (src.Contains("end = next.WorldPosition;"))
        {
            src = src.Replace(
                "end = next.WorldPosition;",
                "end = GridManager.Instance != null\n" +
                "                ? GridManager.Instance.GridToWorldFace(next.GridX, next.GridY)\n" +
                "                : next.WorldPosition;");
            changed = true;
        }

        if (!changed)
        {
            Debug.LogWarning($"[IsoGridFixer] ⚠️ {PATH_TACTICAL_CHAR} — ancres non trouvées. Vérifier manuellement.");
            return true; // pas bloquant
        }

        return WriteFile(PATH_TACTICAL_CHAR, src, ref count);
    }

    // ================================================================
    // HELPERS
    // ================================================================

    static string ReadFile(string assetPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[IsoGridFixer] Fichier introuvable : {fullPath}");
            return null;
        }
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    static bool WriteFile(string assetPath, string content, ref int count)
    {
        string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        count++;
        Debug.Log($"[IsoGridFixer] ✅ {assetPath} patché.");
        return true;
    }
}
#endif