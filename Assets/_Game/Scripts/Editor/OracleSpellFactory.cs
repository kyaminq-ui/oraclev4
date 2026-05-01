#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Oracle Spell Factory — Génère tous les SpellData et PassiveData du jeu.
/// Menu : Oracle > Generate Spells & Passives
/// </summary>
public static class OracleSpellFactory
{
    private const string SpellPath   = "Assets/_Game/ScriptableObjects/Spells";
    private const string AttackPath  = "Assets/_Game/ScriptableObjects/Spells/Attaques";
    private const string TactPath    = "Assets/_Game/ScriptableObjects/Spells/Tactiques";
    private const string SurvivePath = "Assets/_Game/ScriptableObjects/Spells/Survie";
    private const string PassivePath = "Assets/_Game/ScriptableObjects/Spells/Passifs";

    [MenuItem("Oracle/Generate Spells & Passives")]
    public static void GenerateAll()
    {
        EnsureFolders();

        int created = 0;
        created += CreateAttaques();
        created += CreateTactiques();
        created += CreateSurvie();
        created += CreatePassives();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Oracle — Factory",
            $"{created} assets créés avec succès !\n\n" +
            "Tu peux maintenant les assigner dans ton DeckData.",
            "Super !");
        Debug.Log($"[OracleSpellFactory] {created} assets générés.");
    }

    // =========================================================
    // DOSSIERS
    // =========================================================
    static void EnsureFolders()
    {
        CreateFolder("Assets/_Game/ScriptableObjects", "Spells");
        CreateFolder(SpellPath, "Attaques");
        CreateFolder(SpellPath, "Tactiques");
        CreateFolder(SpellPath, "Survie");
        CreateFolder(SpellPath, "Passifs");
    }

    static void CreateFolder(string parent, string name)
    {
        string full = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, name);
    }

    // =========================================================
    // ATTAQUES (10)
    // =========================================================
    static int CreateAttaques()
    {
        int n = 0;

        // 1 — Couteau dans le dos
        var s = Spell("Couteau dans le dos", 4, 0,
            melee: true, rangeMin: 1, rangeMax: 1,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "5 dégâts. +50% si lancé depuis le dos de la cible.",
            synergy: "Combo avec Voltige pour attaquer dans le dos. Fort avec Maître d'Arme.");
        s.effects.Add(E(SpellEffectType.Damage, 5, cond: SpellCondition.Always));
        s.effects.Add(E(SpellEffectType.Damage, 2, cond: SpellCondition.FromBehind, mult: 0.5f));
        n += Save(s, AttackPath);

        // 2 — Exécution
        s = Spell("Exécution", 4, 0,
            melee: false, rangeMin: 1, rangeMax: 4,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "6 dégâts. +4 dégâts si la cible a moins de 15 PV.",
            synergy: "Dévastateur après Hémorragie pour finir une cible affaiblie.");
        s.effects.Add(E(SpellEffectType.Damage, 6));
        s.effects.Add(E(SpellEffectType.Damage, 4, cond: SpellCondition.TargetHPBelow, threshold: 15));
        n += Save(s, AttackPath);

        // 3 — Hémorragie
        s = Spell("Hémorragie", 3, 0,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "3 dégâts immédiats + 1 dégât/tour pendant 2 tours. Cumulable x2.",
            synergy: "Combo avec Exécution quand la cible passe sous 15 PV.");
        s.effects.Add(E(SpellEffectType.Damage, 3));
        s.effects.Add(E(SpellEffectType.Bleed, 1, duration: 2));
        n += Save(s, AttackPath);

        // 4 — Ricochet
        s = Spell("Ricochet", 4, 0,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.Bounce, aoe: 0,
            desc: "5 dégâts sur la cible. Rebondit sur les ennemis adjacents pour 2 dégâts chacun.",
            synergy: "Rentable si la cible est collée à un allié ennemi. Combo avec Surcharge.");
        s.effects.Add(E(SpellEffectType.Damage, 5));
        n += Save(s, AttackPath);

        // 5 — Explosion Solaire
        s = Spell("Explosion Solaire", 5, 2,
            melee: false, rangeMin: 1, rangeMax: 4,
            zone: ZoneType.Cross, aoe: 1,
            desc: "10 dégâts sur toutes les cases de la croix. Ligne de vue requise.",
            synergy: "Combo avec Vent de Panique pour concentrer les ennemis d'abord.");
        s.requiresLineOfSight = true;
        s.effects.Add(E(SpellEffectType.Damage, 10));
        n += Save(s, AttackPath);

        // 6 — Patate de forain
        s = Spell("Patate de forain", 4, 0,
            melee: true, rangeMin: 1, rangeMax: 1,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "7 dégâts. Retire 1 PM à la cible pour ce tour.",
            synergy: "Empêche la fuite. Combo avec Épine pour forcer l'ennemi au CàC.");
        s.effects.Add(E(SpellEffectType.Damage, 7));
        s.effects.Add(E(SpellEffectType.RemovePM, 1));
        n += Save(s, AttackPath);

        // 7 — Dague de Verre
        s = Spell("Dague de Verre", 2, 0,
            melee: true, rangeMin: 1, rangeMax: 1,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "4 dégâts. Sort léger, idéal pour finir un tour avec les PA restants.",
            synergy: "Spam possible. Combo avec Maître d'Arme pour 6 dégâts en 2 PA.");
        s.effects.Add(E(SpellEffectType.Damage, 4));
        n += Save(s, AttackPath);

        // 8 — Pluie de Flèches
        s = Spell("Pluie de Flèches", 4, 0,
            melee: false, rangeMin: 2, rangeMax: 5,
            zone: ZoneType.Circle, aoe: 2,
            desc: "2 dégâts sur toutes les cases du cercle rayon 2. Ignore la ligne de vue.",
            synergy: "Idéal après Pilier de Pierre pour forcer les ennemis dans une zone.");
        s.ignoresLineOfSight = true;
        s.effects.Add(E(SpellEffectType.Damage, 2));
        n += Save(s, AttackPath);

        // 9 — Éclat Arcanique
        s = Spell("Éclat Arcanique", 3, 0,
            melee: false, rangeMin: 4, rangeMax: 6,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "6 dégâts. Uniquement à longue distance (min 4 cases).",
            synergy: "Parfait avec Esprit Clair (+2 portée). Combo avec Sniper (+2 dégâts à 5+ cases).");
        s.effects.Add(E(SpellEffectType.Damage, 6));
        n += Save(s, AttackPath);

        // 10 — Silence
        s = Spell("Silence", 5, 3,
            melee: false, rangeMin: 1, rangeMax: 4,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "8 dégâts. La cible ne peut pas utiliser de sorts au prochain tour.",
            synergy: "Silencer un soigneur change le cours du duel. Combo avec Hémorragie.");
        s.effects.Add(E(SpellEffectType.Damage, 8));
        s.effects.Add(E(SpellEffectType.Silence, 1, duration: 1));
        n += Save(s, AttackPath);

        return n;
    }

    // =========================================================
    // TACTIQUES (10)
    // =========================================================
    static int CreateTactiques()
    {
        int n = 0;

        // 1 — Voltige
        var s = Spell("Voltige", 3, 2,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "Échange de position avec la cible (allié ou ennemi).",
            synergy: "Permet d'attaquer dans le dos immédiatement après.");
        s.effects.Add(E(SpellEffectType.Swap, 0));
        n += Save(s, TactPath);

        // 2 — Surcharge
        s = Spell("Surcharge", 3, 0,
            melee: true, rangeMin: 0, rangeMax: 1,
            zone: ZoneType.Circle, aoe: 1,
            desc: "Repousse tous les ennemis adjacents d'1 case.",
            synergy: "Crée de l'espace pour un sort à distance. Combo avec Pilier de Pierre.");
        s.effects.Add(E(SpellEffectType.Push, 1));
        n += Save(s, TactPath);

        // 3 — Gravité
        s = Spell("Gravité", 4, 2,
            melee: false, rangeMin: 1, rangeMax: 4,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "La cible ne peut pas utiliser téléportation ou dash au prochain tour.",
            synergy: "Contre parfait contre Saut de l'Ange ou Liane de Fer.");
        s.effects.Add(E(SpellEffectType.GravityDebuff, 1, duration: 1));
        n += Save(s, TactPath);

        // 4 — Saut de l'Ange
        s = Spell("Saut de l'Ange", 3, 2,
            melee: false, rangeMin: 1, rangeMax: 4,
            zone: ZoneType.FreeCell, aoe: 0,
            desc: "Se téléporte sur une case libre dans un rayon de 4 cases. Ignore les obstacles.",
            synergy: "Repositionnement express. Combo avec Couteau dans le dos.");
        s.ignoresLineOfSight = true;
        s.effects.Add(E(SpellEffectType.Teleport, 0));
        n += Save(s, TactPath);

        // 5 — Liane de Fer
        s = Spell("Liane de Fer", 3, 2,
            melee: false, rangeMin: 2, rangeMax: 6,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "L'utilisateur est attiré vers la case adjacente à la cible.",
            synergy: "Rapprochement pour les builds CàC. Combo avec Patate de forain.");
        s.effects.Add(E(SpellEffectType.Pull, 0));
        n += Save(s, TactPath);

        // 6 — Vent de Panique
        s = Spell("Vent de Panique", 4, 2,
            melee: true, rangeMin: 0, rangeMax: 1,
            zone: ZoneType.Circle, aoe: 1,
            desc: "Repousse d'1 case tous les ennemis adjacents. Leur première attaque est réduite de 20%.",
            synergy: "Ouvre de la distance. Combo avec Explosion Solaire sur ennemis regroupés.");
        s.effects.Add(E(SpellEffectType.Push, 1));
        s.effects.Add(E(SpellEffectType.ReduceFirstAttack, 0, duration: 1));
        n += Save(s, TactPath);

        // 7 — Pilier de Pierre
        s = Spell("Pilier de Pierre", 4, 0,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.FreeCell, aoe: 0,
            desc: "Crée un mur indestructible d'1 case pendant 2 tours. Bloque déplacements et LdV.",
            synergy: "Force l'ennemi à contourner. Combo avec Pluie de Flèches pour un couloir.");
        s.effects.Add(E(SpellEffectType.CreateWall, 2));
        n += Save(s, TactPath);

        // 8 — Épine
        s = Spell("Épine", 3, 0,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Renvoie 2 dégâts à chaque attaquant pendant 1 tour complet.",
            synergy: "Fort avec Toxicité. Dissuade les attaques CàC. Combo avec Peau d'Écorce.");
        s.effects.Add(E(SpellEffectType.Thorns, 2, duration: 1));
        n += Save(s, TactPath);

        // 9 — Sacrifice
        s = Spell("Sacrifice", 0, 2,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Consomme 1 PM pour gagner 1 PA ce tour.",
            synergy: "Combo avec Méditation pour des enchaînements hors-norme.");
        s.effects.Add(E(SpellEffectType.ConvertPMtoPA, 1));
        n += Save(s, TactPath);

        // 10 — Siphon
        s = Spell("Siphon", 3, 1,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "Vole 1 PM à la cible. Le PM volé est ajouté au total du lanceur.",
            synergy: "Combo avec Patate de forain pour priver totalement l'ennemi de mobilité.");
        s.effects.Add(E(SpellEffectType.StealPM, 1));
        n += Save(s, TactPath);

        return n;
    }

    // =========================================================
    // SURVIE (10)
    // =========================================================
    static int CreateSurvie()
    {
        int n = 0;

        // 1 — Adrénaline
        var s = Spell("Adrénaline", 2, 0,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Gagne 2 PM ce tour. Perd 3 PV (non annulable).",
            synergy: "Combo avec Sacrifice pour convertir les PM en PA. Fort avec Berserker.");
        s.effects.Add(E(SpellEffectType.BonusPM, 2));
        s.effects.Add(E(SpellEffectType.SelfDamage, 3));
        n += Save(s, SurvivePath);

        // 2 — Méditation
        s = Spell("Méditation", 2, 2,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Donne 2 PA supplémentaires au début du prochain tour.",
            synergy: "Prépare un tour d'hyper-activité. Combo avec Sacrifice ou Adrénaline.");
        s.effects.Add(E(SpellEffectType.BonusPANextTurn, 2));
        n += Save(s, SurvivePath);

        // 3 — Rempart
        s = Spell("Rempart", 4, 0,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Crée un bouclier de 8 PB jusqu'à la fin du tour.",
            synergy: "Combo avec Épine : encaisser et renvoyer simultanément. Fort avec Dernier Rempart.");
        s.effects.Add(E(SpellEffectType.Shield, 8, duration: 1));
        n += Save(s, SurvivePath);

        // 4 — Invisibilité
        s = Spell("Invisibilité", 5, 3,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Devient invisible pendant 1 tour ou jusqu'à la prochaine attaque lancée.",
            synergy: "Repositionnement libre pendant l'invisibilité. Combo avec Camouflage.");
        s.effects.Add(E(SpellEffectType.Invisible, 1, duration: 1));
        n += Save(s, SurvivePath);

        // 5 — Esprit Clair
        s = Spell("Esprit Clair", 2, 2,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Augmente la portée de tous les sorts de 2 cases pour ce tour.",
            synergy: "Combo avec Éclat Arcanique (portée 4→8) ou Pluie de Flèches.");
        s.effects.Add(E(SpellEffectType.BonusRange, 2, duration: 1));
        n += Save(s, SurvivePath);

        // 6 — Balise Statique
        s = Spell("Balise Statique", 3, 0,
            melee: false, rangeMin: 1, rangeMax: 3,
            zone: ZoneType.FreeCell, aoe: 0,
            desc: "Invoque un bloqueur de 5 PV sur une case libre pendant 3 tours.",
            synergy: "Crée un couloir ou bloque une fuite. Moins solide que Pilier mais dure plus longtemps.");
        s.effects.Add(E(SpellEffectType.CreateWall, 3));
        n += Save(s, SurvivePath);

        // 7 — Pansement
        s = Spell("Pansement", 3, 2,
            melee: false, rangeMin: 1, rangeMax: 2,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "Soigne 5 PV. Ne peut pas dépasser le maximum de PV.",
            synergy: "Combo avec Second Souffle pour une survie d'urgence. Contre Hémorragie avec Purge.");
        s.effects.Add(E(SpellEffectType.Heal, 5));
        n += Save(s, SurvivePath);

        // 8 — Peau d'Écorce
        s = Spell("Peau d'Écorce", 3, 0,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Réduit chaque attaque reçue de 3 dégâts pendant 1 tour.",
            synergy: "Combo avec Épine pour encaisser et renvoyer. Fort avec Dernier Rempart.");
        s.effects.Add(E(SpellEffectType.DamageReduction, 3, duration: 1));
        n += Save(s, SurvivePath);

        // 9 — Purge
        s = Spell("Purge", 2, 0,
            melee: false, rangeMin: 1, rangeMax: 2,
            zone: ZoneType.SingleTarget, aoe: 0,
            desc: "Retire tous les malus actifs de la cible (saignement, silence, PM réduits...).",
            synergy: "Contre direct à Hémorragie et Silence. Indispensable dans les duels longs.");
        s.effects.Add(E(SpellEffectType.Cleanse, 0));
        n += Save(s, SurvivePath);

        // 10 — Second Souffle
        s = Spell("Second Souffle", 8, 5,
            melee: false, rangeMin: 0, rangeMax: 0,
            zone: ZoneType.Self, aoe: 0,
            desc: "Si le lanceur descend à 0 PV ce tour, il reste à 1 PV. À lancer avant les dégâts fatals.",
            synergy: "Ultime de dernier recours. Combo avec Berserker : survivre à 1 PV = +20% dégâts.");
        s.effects.Add(E(SpellEffectType.LastBreath, 0));
        n += Save(s, SurvivePath);

        return n;
    }

    // =========================================================
    // PASSIFS (10)
    // =========================================================
    static int CreatePassives()
    {
        int n = 0;

        n += SavePassive(Passive("Berserker",
            PassiveType.Berserker, PassiveTrigger.Permanent,
            effectValue: 0.20f, procChance: 1f, condThreshold: 15,
            desc: "+20% dégâts sur tous les sorts tant que les PV sont sous 15."));

        n += SavePassive(Passive("Évasif",
            PassiveType.Evasif, PassiveTrigger.OnDamageReceived,
            effectValue: 0f, procChance: 0.10f, condThreshold: 0,
            desc: "10% de chances d'esquiver totalement une attaque à distance."));

        n += SavePassive(Passive("Dernier Rempart",
            PassiveType.DernierRempart, PassiveTrigger.OnTurnStart,
            effectValue: 5f, procChance: 1f, condThreshold: 25,
            desc: "Gagne 5 PB au début de chaque tour si les PV sont sous 25."));

        n += SavePassive(Passive("Vigilance",
            PassiveType.Vigilance, PassiveTrigger.Permanent,
            effectValue: 0f, procChance: 1f, condThreshold: 0,
            desc: "Ne peut pas être taclé. Déplacement libre en quittant le corps-à-corps."));

        n += SavePassive(Passive("Masse Critique",
            PassiveType.MasseCritique, PassiveTrigger.OnSpellCast,
            effectValue: 0.15f, procChance: 0.20f, condThreshold: 0,
            desc: "20% de chances qu'un sort inflige +15% de dégâts."));

        n += SavePassive(Passive("Maître d'Arme",
            PassiveType.MaitreArme, PassiveTrigger.Permanent,
            effectValue: 2f, procChance: 1f, condThreshold: 0,
            desc: "+2 dégâts sur tous les sorts de corps-à-corps."));

        n += SavePassive(Passive("Camouflage",
            PassiveType.Camouflage, PassiveTrigger.OnTurnEnd,
            effectValue: 0f, procChance: 1f, condThreshold: 0,
            desc: "Si aucun sort lancé ce tour : devient invisible et se décale d'1 case."));

        n += SavePassive(Passive("Sniper",
            PassiveType.Sniper, PassiveTrigger.Permanent,
            effectValue: 2f, procChance: 1f, condThreshold: 5,
            desc: "+2 dégâts si le sort est lancé à plus de 5 cases de distance."));

        n += SavePassive(Passive("Bouclier Hasardeux",
            PassiveType.BouclierHasardeux, PassiveTrigger.OnDamageReceived,
            effectValue: 0.20f, procChance: 0.20f, condThreshold: 0,
            desc: "20% de chances de réduire une attaque reçue de 20%."));

        n += SavePassive(Passive("Toxicité",
            PassiveType.Toxicite, PassiveTrigger.OnEnemyTurnEnd,
            effectValue: 2f, procChance: 1f, condThreshold: 0,
            desc: "Tout ennemi qui finit son tour au CàC perd 2 PV."));

        return n;
    }

    // =========================================================
    // CONSTRUCTEURS
    // =========================================================
    static SpellData Spell(string name, int pa, int cd,
        bool melee, int rangeMin, int rangeMax,
        ZoneType zone, int aoe,
        string desc, string synergy)
    {
        var s = ScriptableObject.CreateInstance<SpellData>();
        s.spellName          = name;
        s.paCost             = pa;
        s.cooldown           = cd;
        s.isMeleeOnly        = melee;
        s.rangeMin           = rangeMin;
        s.rangeMax           = rangeMax;
        s.zoneType           = zone;
        s.aoeRadius          = aoe;
        s.description        = desc;
        s.synergyDescription = synergy;
        return s;
    }

    static SpellEffect E(SpellEffectType type, int value,
        int duration = 0,
        SpellCondition cond = SpellCondition.Always,
        int threshold = 0,
        float mult = 1f)
    {
        return new SpellEffect
        {
            type                = type,
            value               = value,
            duration            = duration,
            condition           = cond,
            conditionThreshold  = threshold,
            conditionMultiplier = mult,
        };
    }

    static PassiveData Passive(string name,
        PassiveType type, PassiveTrigger trigger,
        float effectValue, float procChance, int condThreshold,
        string desc)
    {
        var p = ScriptableObject.CreateInstance<PassiveData>();
        p.passiveName        = name;
        p.passiveType        = type;
        p.trigger            = trigger;
        p.effectValue        = effectValue;
        p.procChance         = procChance;
        p.conditionThreshold = condThreshold;
        p.description        = desc;
        return p;
    }

    // =========================================================
    // SAVE
    // =========================================================
    static int Save(SpellData s, string folder)
    {
        string path = $"{folder}/{s.spellName}.asset";
        if (AssetDatabase.LoadAssetAtPath<SpellData>(path) != null)
        {
            Debug.Log($"[OracleSpellFactory] Ignoré (déjà existant) : {s.spellName}");
            return 0;
        }
        AssetDatabase.CreateAsset(s, path);
        return 1;
    }

    static int SavePassive(PassiveData p)
    {
        string path = $"{PassivePath}/{p.passiveName}.asset";
        if (AssetDatabase.LoadAssetAtPath<PassiveData>(path) != null)
        {
            Debug.Log($"[OracleSpellFactory] Ignoré (déjà existant) : {p.passiveName}");
            return 0;
        }
        AssetDatabase.CreateAsset(p, path);
        return 1;
    }
}
#endif
