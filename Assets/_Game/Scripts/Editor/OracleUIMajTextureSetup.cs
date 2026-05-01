#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Importe les textures du dossier UI_Maj (GIF / PNG) en sprites UI pixel-art.
/// À lancer une fois après copie des fichiers depuis UI_MAJ/ à la racine du projet.
/// </summary>
public static class OracleUIMajTextureSetup
{
    public const string UiMajFolder = "Assets/_Game/Sprites/UI_Maj";
    /// <summary>Ressources dupliquées pour <see cref="Resources.Load"/> (HUD runtime).</summary>
    public const string HudResourcesFolder = "Assets/_Game/Resources/OracleHUD";

    static readonly string[] SupportedExts = { ".gif", ".png", ".jpg", ".jpeg" };

    [MenuItem("Oracle/Configure UI_Maj Sprites (barres, passif, cartes)")]
    public static void ConfigureAllInFolder()
    {
        int n = ConfigureSpritesInFolder(UiMajFolder, required: true);
        if (n < 0) return;
        n += ConfigureSpritesInFolder(HudResourcesFolder, required: false);

        AssetDatabase.Refresh();
        Debug.Log($"[OracleUIMajTextureSetup] {n} texture(s) configurée(s) en Sprite (Point filter, readable).");
        EditorUtility.DisplayDialog("Oracle — UI_Maj",
            $"{n} texture(s) configurée(s) (UI_Maj + Resources/OracleHUD si présent).\nTu peux relancer Build Combat HUD.", "OK");
    }

    /// <returns>Nombre de textures configurées, ou -1 si <paramref name="required"/> et dossier absent.</returns>
    static int ConfigureSpritesInFolder(string assetFolder, bool required)
    {
        if (!AssetDatabase.IsValidFolder(assetFolder))
        {
            if (required)
            {
                EditorUtility.DisplayDialog("Oracle — UI_Maj",
                    $"Dossier introuvable : {assetFolder}\nCopie les GIF depuis UI_MAJ/ vers ce dossier.", "OK");
                return -1;
            }
            return 0;
        }

        string absFolder = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
        if (!Directory.Exists(absFolder))
        {
            if (required)
            {
                EditorUtility.DisplayDialog("Oracle — UI_Maj",
                    $"Dossier physique introuvable : {absFolder}", "OK");
                return -1;
            }
            return 0;
        }

        int n = 0;
        foreach (string ext in SupportedExts)
        {
            foreach (string absPath in Directory.GetFiles(absFolder, "*" + ext))
            {
                string assetPath = "Assets" + absPath
                    .Replace(Application.dataPath, "")
                    .Replace("\\", "/");

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti == null) continue;

                ti.textureType         = TextureImporterType.Sprite;
                ti.spriteImportMode    = SpriteImportMode.Single;
                ti.filterMode          = FilterMode.Point;
                ti.mipmapEnabled       = false;
                ti.alphaIsTransparency = true;
                ti.isReadable          = true;
                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
                n++;
            }
        }

        return n;
    }

    public static Sprite TryLoadSprite(string fileNameWithoutExtension)
    {
        string baseNoExt = $"{UiMajFolder}/{fileNameWithoutExtension}";
        string[] exts = { ".gif", ".png", ".jpg", ".jpeg" };

        foreach (var ext in exts)
        {
            string path = baseNoExt + ext;
            if (!System.IO.File.Exists(
                    System.IO.Path.Combine(Application.dataPath,
                        path.Substring("Assets/".Length)))) continue;

            // S'assurer que l'asset est importé et configuré en Sprite readable
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (ti.textureType      != TextureImporterType.Sprite)       { ti.textureType      = TextureImporterType.Sprite;  dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single)          { ti.spriteImportMode = SpriteImportMode.Single;      dirty = true; }
                if (!ti.isReadable)                                           { ti.isReadable       = true;                         dirty = true; }
                if (ti.mipmapEnabled)                                         { ti.mipmapEnabled    = false;                        dirty = true; }
                if (!ti.alphaIsTransparency)                                  { ti.alphaIsTransparency = true;                     dirty = true; }
                if (dirty) { ti.SaveAndReimport(); AssetDatabase.Refresh(); }
            }

            // Essai 1 : sprite direct
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) { Debug.Log($"[OracleUIMajTextureSetup] Sprite chargé (direct) : {path}"); return sp; }

            // Essai 2 : sous-assets
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) { Debug.Log($"[OracleUIMajTextureSetup] Sprite chargé (sub-asset) : {path}"); return s; }

            // Essai 3 : Texture2D → Sprite.Create
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null && tex.width > 0)
            {
                Debug.Log($"[OracleUIMajTextureSetup] Sprite créé depuis Texture2D ({tex.width}×{tex.height}) : {path}");
                return Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect);
            }
            Debug.LogWarning($"[OracleUIMajTextureSetup] Texture introuvable ou invalide : {path}  (tex={tex})");
        }

        Debug.LogWarning($"[OracleUIMajTextureSetup] Aucun fichier image trouvé pour : {baseNoExt}");
        return null;
    }
}
#endif
