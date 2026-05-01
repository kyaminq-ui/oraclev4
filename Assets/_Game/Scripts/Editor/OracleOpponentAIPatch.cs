#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Ajoute OpponentAI sur le TacticalCharacter adversaire.
/// Menu : Oracle/PATCH — Ajouter IA adversaire
/// </summary>
public static class OracleOpponentAIPatch
{
    [MenuItem("Oracle/PATCH — Ajouter IA adversaire")]
    public static void Run()
    {
        var ci = Object.FindObjectOfType<CombatInitializer>(true);
        if (ci == null)
        {
            EditorUtility.DisplayDialog("Oracle — IA", "Aucun CombatInitializer dans la scène.", "OK");
            return;
        }

        TacticalCharacter opp = ci.opponent;
        if (opp == null)
        {
            // Fallback : deuxième TacticalCharacter dans la scène
            var all = Object.FindObjectsOfType<TacticalCharacter>(true);
            foreach (var tc in all)
                if (tc != ci.player) { opp = tc; break; }
        }

        if (opp == null)
        {
            EditorUtility.DisplayDialog("Oracle — IA",
                "Impossible de trouver le TacticalCharacter adversaire.\n" +
                "Assigne-le dans le champ 'opponent' du CombatInitializer.", "OK");
            return;
        }

        // Vérifier si OpponentAI est déjà présent
        if (opp.GetComponent<OpponentAI>() != null)
        {
            EditorUtility.DisplayDialog("Oracle — IA",
                $"OpponentAI déjà présent sur {opp.name}.", "OK");
            return;
        }

        // Mode test : placement auto + skip passif instantané
        Undo.RecordObject(ci, "Enable AI test options");
        ci.autoPlaceOpponent     = true;
        ci.skipPassiveSelection  = true;

        // Ajouter OpponentAI
        Undo.AddComponent<OpponentAI>(opp.gameObject);

        EditorUtility.SetDirty(ci);
        EditorUtility.SetDirty(opp.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Oracle — IA OK",
            $"OpponentAI ajouté sur '{opp.name}'.\n\n" +
            "• autoPlaceOpponent    = true  (placement instantané)\n" +
            "• skipPassiveSelection = true  (passif aléatoire immédiat)\n" +
            "• En solo : l'IA joue ses tours automatiquement\n" +
            "• En réseau (Photon 2+ joueurs) : l'IA + skip restent inactifs\n\n" +
            "→ Ctrl+S puis Play.", "OK");
    }
}
#endif
