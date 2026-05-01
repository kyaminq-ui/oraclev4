using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Liste de <see cref="SpellData"/> dont on pioche sans remise un sous-ensemble pour chaque combat.
/// Remplissage rapide : menu Oracle « Spell Deck Pool — créer ou remplir ».
/// </summary>
[CreateAssetMenu(fileName = "SpellDeckPool", menuName = "Oracle/Spell Deck Pool")]
public class SpellDeckPool : ScriptableObject
{
    public List<SpellData> candidates = new List<SpellData>();

    public int CandidateCount => candidates != null ? candidates.Count : 0;

    const int BalancedPerCategory = 2;

    /// <summary>
    /// Mélange + pioche <paramref name="count"/> sorts. Si <paramref name="count"/> vaut 6 et le pool
    /// permet 2 attaques + 2 survie + 2 tactiques, ce tirage est garanti ; sinon repli sur mélange pur.
    /// </summary>
    public List<SpellData> DrawRandomUnique(System.Random rng, int count = DeckData.MaxSpells)
    {
        count = Mathf.Max(1, count);
        if (candidates == null || candidates.Count == 0) return new List<SpellData>();

        if (count == DeckData.MaxSpells)
        {
            var balanced = TryDrawBalancedSix(rng);
            if (balanced != null) return balanced;
            Debug.LogWarning("[SpellDeckPool] Tirage 2+2+2 impossible (catégories manquantes ou mal renseignées). Repli sur tirage aléatoire simple. Ré-exécutez Oracle → Spell Deck Pool — créer ou remplir.");
        }

        var src = new List<SpellData>();
        foreach (var s in candidates)
            if (s != null) src.Add(s);

        if (src.Count == 0) return new List<SpellData>();

        ShuffleInPlace(rng, src);
        int n = Mathf.Min(count, src.Count);
        return src.GetRange(0, n);
    }

    /// <summary>2 sorts par <see cref="SpellDeckCategory"/>, puis mélange — seulement si assez de candidats par catégorie.</summary>
    List<SpellData> TryDrawBalancedSix(System.Random rng)
    {
        var atk = new List<SpellData>();
        var sur = new List<SpellData>();
        var tac = new List<SpellData>();
        foreach (var s in candidates)
        {
            if (s == null) continue;
            switch (s.deckCategory)
            {
                case SpellDeckCategory.Attack:   atk.Add(s); break;
                case SpellDeckCategory.Survival: sur.Add(s); break;
                case SpellDeckCategory.Tactic:   tac.Add(s); break;
            }
        }

        if (atk.Count < BalancedPerCategory || sur.Count < BalancedPerCategory || tac.Count < BalancedPerCategory)
            return null;

        ShuffleInPlace(rng, atk);
        ShuffleInPlace(rng, sur);
        ShuffleInPlace(rng, tac);

        var result = new List<SpellData>(DeckData.MaxSpells);
        result.AddRange(atk.GetRange(0, BalancedPerCategory));
        result.AddRange(sur.GetRange(0, BalancedPerCategory));
        result.AddRange(tac.GetRange(0, BalancedPerCategory));
        ShuffleInPlace(rng, result);
        return result;
    }

    static void ShuffleInPlace(System.Random rng, IList<SpellData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            SpellData t = list[i];
            list[i] = list[j];
            list[j] = t;
        }
    }
}
