using System;

public enum StatusEffectType
{
    Bleed,               // Dégâts par tour (Hémorragie)
    Silence,             // Pas de sorts (Silence)
    DamageReduction,     // Réduit dégâts reçus par hit (Peau d'Écorce)
    Thorns,              // Renvoie dégâts à l'attaquant (Épine)
    Invisible,           // Pas ciblable, se brise au 1er sort (Invisibilité)
    Shield,              // Absorbe X dégâts (Rempart, Balise)
    BonusPANextTurn,     // +X PA au début du prochain tour (Méditation)
    GravityDebuff,       // Pas de téléportation/dash (Gravité)
    LastBreath,          // Survit à 1 PV une fois (Second Souffle)
    ReducedAttack,       // Premier sort du tour réduit de 20% (Vent de Panique)
    ReducedPM,           // PM réduit (Patate de forain)
}

[Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public int value;
    public int turnsRemaining;
    public bool isDebuff;

    public StatusEffect(StatusEffectType type, int value, int duration, bool isDebuff = false)
    {
        this.type = type;
        this.value = value;
        turnsRemaining = duration;
        this.isDebuff = isDebuff;
    }

    public bool IsExpired => turnsRemaining <= 0;

    public void Tick() => turnsRemaining = Math.Max(0, turnsRemaining - 1);
}
