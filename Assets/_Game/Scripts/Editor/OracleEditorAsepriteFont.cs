#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Génère l’asset TMP SDF depuis Aseprite.otf et permet d’assigner la police dans les builders Editor.
/// </summary>
public static class OracleEditorAsepriteFont
{
    public const string TmpAssetPath    = "Assets/_Game/Resources/Fonts/Aseprite SDF.asset";
    public const string LegacyFontPath  = "Assets/_Game/Resources/Fonts/Aseprite.otf";

    public static TMP_FontAsset TryLoadTmpAsset()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpAssetPath);
    }

    public static void AssignIfAvailable(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        var f = TryLoadTmpAsset();
        if (f != null)
        {
            tmp.font = f;
            tmp.havePropertiesChanged = true;
        }
    }

    [MenuItem("Oracle/Fonts/Generate Aseprite SDF (TextMeshPro)")]
    public static void GenerateSdfFontAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources/Fonts"))
        {
            EditorUtility.DisplayDialog(
                "Oracle — Police",
                "Dossier introuvable : Assets/_Game/Resources/Fonts\n" +
                "Place Aseprite.otf sous ce chemin.", "OK");
            return;
        }

        var legacy = AssetDatabase.LoadAssetAtPath<Font>(LegacyFontPath);
        if (legacy == null)
        {
            EditorUtility.DisplayDialog(
                "Oracle — Police",
                $"Font introuvable : {LegacyFontPath}", "OK");
            return;
        }

        var existing = TryLoadTmpAsset();
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Oracle — Police",
                    "Aseprite SDF.asset existe déjà. Remplacer ?",
                    "Remplacer", "Annuler"))
                return;
            AssetDatabase.DeleteAsset(TmpAssetPath);
        }

        var fa = TMP_FontAsset.CreateFontAsset(legacy);
        AssetDatabase.CreateAsset(fa, TmpAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Oracle — Police",
            $"Asset créé : {TmpAssetPath}\n\n" +
            "Les textes importants utiliseront cette police (ou l’OTF en secours au runtime).",
            "OK");
        Selection.activeObject = fa;
    }
}
#endif
