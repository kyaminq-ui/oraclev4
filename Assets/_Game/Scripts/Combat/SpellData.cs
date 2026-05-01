using UnityEngine;
using System.Collections.Generic;

public enum ZoneType
{
    SingleTarget,
    Cross,       // Centre + 4 adjacentes (Explosion Solaire)
    Line,        // Ligne dans une direction
    Circle,      // Cercle rayon R (Pluie de Flèches, Surcharge)
    Bounce,      // Rebond sur ennemis adjacents à la cible (Ricochet)
    Self,        // Soi-même uniquement
    FreeCell,    // Case libre dans la portée (Saut de l'Ange, Pilier de Pierre)
    Cone,        // Cône devant le lanceur (axe vers la case visée)
    Boost,       // Zone autour du lanceur (rayon = aoeRadius)
}

public enum SpellEffectType
{
    Damage,
    Heal,
    SelfDamage,          // Coût en PV (Adrénaline)
    Push,                // Repousser de N cases (Surcharge, Vent de Panique)
    Pull,                // Attirer le lanceur vers la cible (Liane de Fer)
    Swap,                // Échanger positions (Voltige)
    Teleport,            // Se téléporter sur case libre (Saut de l'Ange)
    Shield,              // Bouclier de N PB (Rempart)
    DamageReduction,     // Réduire dégâts reçus de N par hit (Peau d'Écorce)
    Thorns,              // Renvoyer N dégâts à l'attaquant (Épine)
    Invisible,           // Devenir invisible
    Silence,             // Empêcher l'utilisation de sorts
    BonusPA,             // Gagner N PA ce tour
    BonusPM,             // Gagner N PM ce tour
    RemovePM,            // Retirer N PM à la cible (Patate de forain)
    StealPM,             // Voler N PM à la cible (Siphon)
    BonusPANextTurn,     // +N PA au début du prochain tour (Méditation)
    BonusRange,          // +N portée sur tous les sorts ce tour (Esprit Clair)
    Bleed,               // N dégâts par tour pendant D tours (Hémorragie)
    Cleanse,             // Retirer tous les malus (Purge)
    CreateWall,          // Créer obstacle temporaire (Pilier de Pierre, Balise Statique)
    LastBreath,          // Survivre à 1 PV (Second Souffle)
    ConvertPMtoPA,       // Consomme 1 PM → gagne 1 PA (Sacrifice)
    GravityDebuff,       // Interdit téléportation/dash (Gravité)
    ReduceFirstAttack,   // Premier sort réduit de 20% (Vent de Panique)
}

public enum SpellCondition
{
    Always,
    TargetHPBelow,   // Exécution : bonus si cible < seuil
    FromBehind,      // Couteau dans le dos : bonus si dans le dos
    SelfHPBelow,
}

/// <summary>Catégorie pour la constitution du deck (tirage 2+2+2 depuis le pool de 30 sorts).</summary>
public enum SpellDeckCategory
{
    Attack,
    Survival,
    Tactic
}

[System.Serializable]
public class SpellEffect
{
    public SpellEffectType type = SpellEffectType.Damage;
    public int value = 0;
    public int duration = 0;
    public SpellCondition condition = SpellCondition.Always;
    public int conditionThreshold = 0;
    public float conditionMultiplier = 1f;
}

[CreateAssetMenu(fileName = "NewSpell", menuName = "Oracle/Spell Data")]
public class SpellData : ScriptableObject
{
    [Header("Identité")]
    public string spellName = "Nouveau Sort";
    [Tooltip("Attaques / Survie / Tactiques — rempli auto par le menu Oracle « Spell Deck Pool ».")]
    public SpellDeckCategory deckCategory = SpellDeckCategory.Attack;
    public Sprite icon;
    [TextArea(2, 4)] public string description;
    [TextArea(2, 4)] public string synergyDescription;

    [Header("Coût & Cooldown")]
    public int paCost = 2;
    public int cooldown = 0;

    [Header("Portée")]
    public bool isMeleeOnly = false;
    public int rangeMin = 1;
    public int rangeMax = 3;
    public bool requiresLineOfSight = false;
    public bool ignoresLineOfSight = false;

    [Header("Zone d'effet")]
    public ZoneType zoneType = ZoneType.SingleTarget;
    public int aoeRadius = 1;

    [Header("Effets")]
    public List<SpellEffect> effects = new List<SpellEffect>();
}
