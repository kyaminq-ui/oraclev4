using UnityEngine;
using TMPro;

/// <summary>
/// Charge la police Aseprite pour les textes UI importants (Resources).
/// Priorité : <c>Fonts/Aseprite SDF</c> (TMP pré-généré) puis génération dynamique depuis <c>Fonts/Aseprite</c> (.otf).
/// </summary>
public static class OracleUIImportantFont
{
    const string ResourcesTmpPath    = "Fonts/Aseprite SDF";
    const string ResourcesLegacyPath = "Fonts/Aseprite";

    static TMP_FontAsset _font;

    public static TMP_FontAsset GetFont()
    {
        if (_font != null) return _font;

        _font = Resources.Load<TMP_FontAsset>(ResourcesTmpPath);
        if (_font != null) return _font;

        var legacy = Resources.Load<Font>(ResourcesLegacyPath);
        if (legacy == null)
        {
            Debug.LogWarning(
                "[OracleUIImportantFont] Police introuvable. Attendu : Resources/Fonts/Aseprite.otf " +
                "et/C ou menu Oracle → Fonts → Generate Aseprite SDF (TextMeshPro).");
            return null;
        }

        _font = TMP_FontAsset.CreateFontAsset(legacy);
        return _font;
    }

    public static void Apply(TextMeshProUGUI text)
    {
        if (text == null) return;
        var f = GetFont();
        if (f == null) return;
        text.font = f;
        text.havePropertiesChanged = true;
    }
}
