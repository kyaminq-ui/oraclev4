public class TriggerContext
{
    public PassiveTrigger type;
    public TacticalCharacter owner;   // Porteur du passif
    public TacticalCharacter other;   // Attaquant, cible ou personnage concerné
    public SpellData spell;
    public int value;                 // Valeur modifiable (dégâts, soins…)
    public int distance;              // Distance owner → other en cases
    public bool isMeleeSpell;
    public bool isRangedAttack;

    public TriggerContext(PassiveTrigger type, TacticalCharacter owner,
        TacticalCharacter other = null, SpellData spell = null,
        int value = 0, int distance = 0, bool isMelee = false, bool isRanged = false)
    {
        this.type         = type;
        this.owner        = owner;
        this.other        = other;
        this.spell        = spell;
        this.value        = value;
        this.distance     = distance;
        this.isMeleeSpell = isMelee;
        this.isRangedAttack = isRanged;
    }
}
