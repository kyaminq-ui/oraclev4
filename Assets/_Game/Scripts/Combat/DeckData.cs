using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewDeck", menuName = "Oracle/Deck Data")]
public class DeckData : ScriptableObject
{
    public const int MaxSpells = 6;

    [Header("Sorts (6 max)")]
    [SerializeField] private List<SpellData> spells = new List<SpellData>();

    [Header("Passif (1 max)")]
    [SerializeField] private PassiveData passive;

    public IReadOnlyList<SpellData> Spells => spells;
    public PassiveData Passive => passive;

    public bool CanAddSpell() => spells.Count < MaxSpells;
    public bool HasPassive() => passive != null;

    public bool AddSpell(SpellData spell)
    {
        if (spell == null || !CanAddSpell() || spells.Contains(spell)) return false;
        spells.Add(spell);
        return true;
    }

    public bool RemoveSpell(SpellData spell) => spells.Remove(spell);

    public void SetPassive(PassiveData p) => passive = p;
    public void ClearPassive() => passive = null;
}
