#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Oracle — Fix rapide des deux problèmes post-build :
///   1. PassivePool non assigné sur PassiveSelectionScreen
///   2. CombatHUD RectTransform avec offsets parasites (Left=-25, Bottom=-25)
///
/// Menu : Oracle/Fix — Assign PassivePool + Fix HUD Rect
/// </summary>
public static class OracleQuickFix
{
    const string POOL_PATH_1 = "Assets/_Game/ScriptableObjects/AllPassivesPool.asset";
    const string POOL_PATH_2 = "Assets/_Game/ScriptableObjects/PassivePool.asset";
    const string PASSIVE_DIR = "Assets/_Game/ScriptableObjects/Spells/Passifs";

    [MenuItem("Oracle/Fix — Assign PassivePool + Fix HUD Rect")]
    static void RunFix()
    {
        int fixes = 0;

        // ──────────────────────────────────────────────────────
        //  1. PASSIVE POOL
        // ──────────────────────────────────────────────────────
        var pss = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        if (pss == null)
        {
            Debug.LogWarning("[OracleQuickFix] Aucun PassiveSelectionScreen dans la scène.");
        }
        else if (pss.passivePool != null)
        {
            Debug.Log($"[OracleQuickFix] PassivePool déjà assigné ({pss.passivePool.allPassives.Count} passifs). OK.");
        }
        else
        {
            // Charger le pool existant (essayer les deux paths connus)
            PassivePool pool = AssetDatabase.LoadAssetAtPath<PassivePool>(POOL_PATH_1)
                            ?? AssetDatabase.LoadAssetAtPath<PassivePool>(POOL_PATH_2);

            if (pool == null)
            {
                // Dernier recours : chercher n'importe quel PassivePool dans le projet
                var guids = AssetDatabase.FindAssets("t:PassivePool");
                foreach (var guid in guids)
                {
                    pool = AssetDatabase.LoadAssetAtPath<PassivePool>(AssetDatabase.GUIDToAssetPath(guid));
                    if (pool != null) break;
                }
            }

            if (pool == null)
            {
                // Créer un pool vide si vraiment rien n'existe
                pool = ScriptableObject.CreateInstance<PassivePool>();
                AssetDatabase.CreateAsset(pool, POOL_PATH_1);
                Debug.LogWarning("[OracleQuickFix] Aucun PassivePool trouvé — pool vide créé. Ajoute les PassiveData manuellement.");
            }

            // Remplir automatiquement si vide
            if (pool.allPassives.Count == 0 && AssetDatabase.IsValidFolder(PASSIVE_DIR))
            {
                var pGuids = AssetDatabase.FindAssets("t:PassiveData", new[] { PASSIVE_DIR });
                foreach (var g in pGuids)
                {
                    var p = AssetDatabase.LoadAssetAtPath<PassiveData>(AssetDatabase.GUIDToAssetPath(g));
                    if (p != null && !pool.allPassives.Contains(p))
                        pool.allPassives.Add(p);
                }
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
            }

            Undo.RecordObject(pss, "Assign PassivePool");
            pss.passivePool = pool;
            EditorUtility.SetDirty(pss);
            fixes++;
            Debug.Log($"[OracleQuickFix] PassivePool assigné ({pool.allPassives.Count} passifs) sur {pss.name}.");
        }

        // ──────────────────────────────────────────────────────
        //  2. COMBAT HUD — RectTransform propre (stretch 0/0/0/0)
        // ──────────────────────────────────────────────────────
        var hud = Object.FindObjectOfType<CombatHUD>(true);
        if (hud == null)
        {
            Debug.LogWarning("[OracleQuickFix] Aucun CombatHUD dans la scène.");
        }
        else
        {
            var rt = hud.GetComponent<RectTransform>();
            if (rt != null)
            {
                bool dirty = false;

                // Doit être full-stretch (anchorMin=0,0 anchorMax=1,1 offsets=0)
                if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one ||
                    rt.offsetMin != Vector2.zero  || rt.offsetMax != Vector2.zero)
                {
                    Undo.RecordObject(rt, "Fix HUD RectTransform");
                    rt.anchorMin        = Vector2.zero;
                    rt.anchorMax        = Vector2.one;
                    rt.offsetMin        = Vector2.zero;
                    rt.offsetMax        = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale       = Vector3.one;
                    dirty = true;
                }

                // Vérifier aussi le pivot (doit être 0.5, 0.5 pour un stretch)
                if (rt.pivot != new Vector2(0.5f, 0.5f))
                {
                    Undo.RecordObject(rt, "Fix HUD Pivot");
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    dirty = true;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(hud.gameObject);
                    fixes++;
                    Debug.Log("[OracleQuickFix] CombatHUD RectTransform corrigé (offsets remis à 0).");
                }
                else
                {
                    Debug.Log("[OracleQuickFix] CombatHUD RectTransform déjà propre. OK.");
                }
            }
        }

        // ──────────────────────────────────────────────────────
        //  3. CANVAS — vérifier CanvasScaler et référence résolution
        // ──────────────────────────────────────────────────────
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                Undo.AddComponent<CanvasScaler>(canvas.gameObject);
                fixes++;
                Debug.Log("[OracleQuickFix] CanvasScaler ajouté au Canvas.");
            }
        }

        // ──────────────────────────────────────────────────────
        //  Résumé
        // ──────────────────────────────────────────────────────
        AssetDatabase.SaveAssets();

        string msg = fixes > 0
            ? $"{fixes} correction(s) appliquée(s).\n\nVérifications effectuées :\n• PassivePool → assigné sur PassiveSelectionScreen\n• CombatHUD RectTransform → offsets remis à zéro\n• CanvasScaler → présent"
            : "Tout était déjà correct. Aucune modification.";

        EditorUtility.DisplayDialog("Oracle — Quick Fix", msg, "OK");
    }

    // ──────────────────────────────────────────────────────────
    //  Bonus : assignation manuelle du pool depuis le menu
    // ──────────────────────────────────────────────────────────
    [MenuItem("Oracle/Fix — Force Assign PassivePool (manuel)")]
    static void ForceAssignPool()
    {
        var pss = Object.FindObjectOfType<PassiveSelectionScreen>(true);
        if (pss == null) { EditorUtility.DisplayDialog("Oracle", "Aucun PassiveSelectionScreen trouvé.", "OK"); return; }

        // Ouvrir un sélecteur de fichier pour choisir le pool manuellement
        string path = EditorUtility.OpenFilePanel("Choisir un PassivePool", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;

        // Convertir en chemin relatif Assets/...
        if (path.StartsWith(Application.dataPath))
            path = "Assets" + path.Substring(Application.dataPath.Length);

        var pool = AssetDatabase.LoadAssetAtPath<PassivePool>(path);
        if (pool == null) { EditorUtility.DisplayDialog("Oracle", "Le fichier sélectionné n'est pas un PassivePool.", "OK"); return; }

        Undo.RecordObject(pss, "Force Assign PassivePool");
        pss.passivePool = pool;
        EditorUtility.SetDirty(pss);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Oracle", $"PassivePool assigné : {pool.allPassives.Count} passifs.", "OK");
    }
}
#endif
