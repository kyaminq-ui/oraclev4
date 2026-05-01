#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crée ou met à jour le pool utilisé au runtime (Resources ou assignation manuelle).
/// </summary>
public static class OracleSpellDeckPoolMenu
{
    const string ResourcesPath = "Assets/_Game/Resources/OracleSpellPools/AllCombatSpellsPool.asset";

    [MenuItem("Oracle/Spell Deck Pool — créer ou remplir (Attaques + Survie + Tactiques)")]
    public static void CreateOrRefresh()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
            AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources/OracleSpellPools"))
            AssetDatabase.CreateFolder("Assets/_Game/Resources", "OracleSpellPools");

        var guids = AssetDatabase.FindAssets("t:SpellData",
            new[]
            {
                "Assets/_Game/ScriptableObjects/Spells/Attaques",
                "Assets/_Game/ScriptableObjects/Spells/Survie",
                "Assets/_Game/ScriptableObjects/Spells/Tactiques"
            });

        var list = new List<SpellData>();
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var s = AssetDatabase.LoadAssetAtPath<SpellData>(path);
            if (s == null) continue;

            if (path.IndexOf("/Attaques/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                s.deckCategory = SpellDeckCategory.Attack;
            else if (path.IndexOf("/Survie/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                s.deckCategory = SpellDeckCategory.Survival;
            else if (path.IndexOf("/Tactiques/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                s.deckCategory = SpellDeckCategory.Tactic;

            EditorUtility.SetDirty(s);
            list.Add(s);
        }

        var pool = AssetDatabase.LoadAssetAtPath<SpellDeckPool>(ResourcesPath);
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<SpellDeckPool>();
            AssetDatabase.CreateAsset(pool, ResourcesPath);
        }

        pool.candidates = list;
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = pool;

        EditorUtility.DisplayDialog("Oracle — Spell Deck Pool",
            $"Pool mis à jour : {list.Count} sorts issus Attaques / Survie / Tactiques.\n\n" +
            "Asset : " + ResourcesPath + "\n" +
            "CombatInitializer charge automatiquement « OracleSpellPools/AllCombatSpellsPool » si le champ Spell Deck Pool est vide.",
            "OK");
    }
}
#endif
