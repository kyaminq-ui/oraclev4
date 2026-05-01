#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Importe les GIF/PNG de CARTE_SORT comme sprites UI et assigne le champ <see cref="SpellData.icon"/>
/// pour les sorts de combat (Attaques / Survie / Tactiques). Le cadre du Deck reste <c>UI_CARTE_SORT</c> ;
/// ces images remplissent la zone illustration de chaque carte dans le CombatHUD / DeckUI.
/// Menu : Oracle/Import & Assign CARTE_SORT (illustrations sorts)
/// </summary>
public static class OracleSpellCarteSortSetup
{
    public const string SpellCardsFolder = "Assets/_Game/Sprites/CARTE_SORT";

    static readonly string[] TextureExtensions = { ".gif", ".png", ".jpg", ".jpeg" };

    static readonly IReadOnlyDictionary<string, string> SpellNameToArtStem =
        new Dictionary<string, string>
        {
            { "Couteau dans le dos", "attack_couteau_dans_le_dos" },
            { "Dague de Verre", "attack_dague_de_verre" },
            { "Éclat Arcanique", "attack_eclat_arcanique" },
            { "Exécution", "attack_execution" },
            { "Explosion Solaire", "attack_explosion_solaire" },
            { "Hémorragie", "attack_hemorragie" },
            { "Patate de forain", "attack_patate_de_forain" },
            { "Pluie de Flèches", "attack_pluie_de_fleches" },
            { "Ricochet", "attack_ricochet" },
            { "Silence", "attack_silence" },
            { "Adrénaline", "survival_adrenaline" },
            { "Balise Statique", "survival_balise_statique" },
            { "Esprit Clair", "survival_esprit_clair" },
            { "Invisibilité", "survival_invisibilite" },
            { "Méditation", "survival_meditation" },
            { "Pansement", "survival_pansement" },
            { "Peau d'Écorce", "survival_peau_decorce" },
            { "Purge", "survival_purge" },
            { "Rempart", "survival_rempart" },
            { "Second Souffle", "survival_second_souffle" },
            { "Épine", "tactic_epine" },
            { "Gravité", "tactic_gravite" },
            { "Liane de Fer", "tactic_liane_de_fer" },
            { "Pilier de Pierre", "tactic_pilier_de_pierre" },
            { "Sacrifice", "tactic_sacrifice" },
            { "Saut de l'Ange", "tactic_saut_de_l_ange" },
            { "Siphon", "tactic_siphon" },
            { "Surcharge", "tactic_surcharge" },
            { "Vent de Panique", "tactic_vent_de_panique" },
            { "Voltige", "tactic_voltige" },
        };

    static readonly string[] CombatSpellRoots =
    {
        "Assets/_Game/ScriptableObjects/Spells/Attaques",
        "Assets/_Game/ScriptableObjects/Spells/Survie",
        "Assets/_Game/ScriptableObjects/Spells/Tactiques",
    };

    const string Menu = "Oracle/Import & Assign CARTE_SORT (illustrations sorts)";

    [MenuItem(Menu)]
    public static void ImportAndAssign()
    {
        if (!AssetDatabase.IsValidFolder(SpellCardsFolder))
        {
            EditorUtility.DisplayDialog("Oracle — CARTE_SORT",
                $"Dossier introuvable : {SpellCardsFolder}\n" +
                "Ajoute les GIF/PNG des cartes dans ce dossier Assets (ils peuvent provenir d’une archive externe).", "OK");
            return;
        }

        int tex = ConfigureTexturesInSpellCardsFolder();
        int assigned = AssignIconsToSpellData();
        AssetDatabase.SaveAssets();
        Debug.Log($"[OracleSpellCarteSortSetup] Textures configurées : {tex} | Icônes assignées : {assigned}");
        EditorUtility.DisplayDialog("Oracle — CARTE_SORT",
            $"{tex} fichier(s) image configuré(s) en sprite.\n" +
            $"{assigned} SpellData mis à jour avec les illustrations du dossier CARTE_SORT.\n\n" +
            "Lance la scène de combat pour vérifier le Deck.", "OK");
    }

    static int ConfigureTexturesInSpellCardsFolder()
    {
        string abs = Path.Combine(Application.dataPath, SpellCardsFolder.Substring("Assets/".Length));
        if (!Directory.Exists(abs)) return 0;

        int n = 0;
        foreach (string ext in TextureExtensions)
        {
            foreach (string absPath in Directory.GetFiles(abs, "*" + ext))
            {
                string assetPath = "Assets" + absPath
                    .Replace(Application.dataPath, "")
                    .Replace("\\", "/");

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti == null) continue;

                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; dirty = true; }
                if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; dirty = true; }
                if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (!ti.isReadable) { ti.isReadable = true; dirty = true; }

                if (dirty)
                {
                    EditorUtility.SetDirty(ti);
                    ti.SaveAndReimport();
                }
                n++;
            }
        }

        AssetDatabase.Refresh();
        return n;
    }

    static int AssignIconsToSpellData()
    {
        int count = 0;

        foreach (string folder in CombatSpellRoots)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;

            foreach (string guid in AssetDatabase.FindAssets("t:SpellData", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var spell = AssetDatabase.LoadAssetAtPath<SpellData>(path);
                if (spell == null) continue;

                string key = !string.IsNullOrEmpty(spell.spellName) ? spell.spellName : spell.name;
                if (!SpellNameToArtStem.TryGetValue(key, out string stem))
                {
                    Debug.LogWarning($"[OracleSpellCarteSortSetup] Pas de fichier CARTE_SORT mappé pour le sort « {key} » ({path})");
                    continue;
                }

                Sprite sprite = TryFirstExistingSprite(stem);
                if (sprite == null)
                {
                    Debug.LogError($"[OracleSpellCarteSortSetup] Sprite introuvable pour « {key} » (stem={stem})");
                    continue;
                }

                using (var so = new SerializedObject(spell))
                {
                    var iconProp = so.FindProperty("icon");
                    if (iconProp == null)
                    {
                        Debug.LogError($"[OracleSpellCarteSortSetup] Champ 'icon' introuvable sur {path}");
                        continue;
                    }

                    iconProp.objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorUtility.SetDirty(spell);
                count++;
            }
        }

        return count;
    }

    static Sprite TryFirstExistingSprite(string fileStem)
    {
        foreach (string ext in TextureExtensions)
        {
            string path = $"{SpellCardsFolder}/{fileStem}{ext}";
            string full = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
            if (!File.Exists(full)) continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if (obj is Sprite s) return s;

        }

        return null;
    }
}
#endif
