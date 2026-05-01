using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(TacticalCharacter))]
public class PassiveManager : MonoBehaviour
{
    // =========================================================
    // ÉTAT
    // =========================================================
    private TacticalCharacter character;
    public PassiveData activePassive;

    private bool castSpellThisTurn  = false;
    private bool masseCritiqueBonus = false;

    // =========================================================
    // INIT
    // =========================================================
    void Awake()
    {
        character = GetComponent<TacticalCharacter>();
    }

    void Start()
    {
        character.OnSpellCast += HandleSpellCast;
        character.OnTurnStart_Passive += HandleTurnStart;
        character.OnTurnEnd_Passive   += HandleTurnEnd;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnEnd += HandleAnyTurnEnd;
    }

    void OnDestroy()
    {
        character.OnSpellCast         -= HandleSpellCast;
        character.OnTurnStart_Passive -= HandleTurnStart;
        character.OnTurnEnd_Passive   -= HandleTurnEnd;

        if (TurnManager.Instance != null)
            TurnManager.Instance.OnTurnEnd -= HandleAnyTurnEnd;
    }

    public void SetPassive(PassiveData passive) => activePassive = passive;

    // =========================================================
    // MODIFICATION DÉGÂTS SORTANTS (appelé par SpellResolver)
    // =========================================================
    public int ModifyOutgoingDamage(int damage, SpellData spell, TacticalCharacter target, int distance)
    {
        if (activePassive == null) return damage;

        switch (activePassive.passiveType)
        {
            // +20% si HP < seuil
            case PassiveType.Berserker:
                if (character.CurrentHP <= activePassive.conditionThreshold)
                    damage = Mathf.RoundToInt(damage * (1f + activePassive.effectValue));
                break;

            // +2 sur sorts CàC
            case PassiveType.MaitreArme:
                if (spell != null && spell.isMeleeOnly)
                    damage += (int)activePassive.effectValue;
                break;

            // +2 si distance > seuil (5 cases)
            case PassiveType.Sniper:
                if (spell != null && !spell.isMeleeOnly && distance > activePassive.conditionThreshold)
                    damage += (int)activePassive.effectValue;
                break;

            // +15% si le flag a été levé (Masse Critique)
            case PassiveType.MasseCritique:
                if (masseCritiqueBonus)
                {
                    damage = Mathf.RoundToInt(damage * (1f + activePassive.effectValue));
                    masseCritiqueBonus = false;
                }
                break;
        }
        return damage;
    }

    // =========================================================
    // MODIFICATION DÉGÂTS ENTRANTS (appelé par TacticalCharacter)
    // =========================================================
    public int ModifyIncomingDamage(int damage, TacticalCharacter attacker)
    {
        if (activePassive == null) return damage;

        switch (activePassive.passiveType)
        {
            // 10% esquive totale (attaque à distance uniquement)
            case PassiveType.Evasif:
                bool isRanged = attacker != null && !IsAdjacent(attacker);
                if (isRanged && Random.value < activePassive.procChance)
                    damage = 0;
                break;

            // 20% chance de réduire les dégâts de 20%
            case PassiveType.BouclierHasardeux:
                if (Random.value < activePassive.procChance)
                    damage = Mathf.RoundToInt(damage * (1f - activePassive.effectValue));
                break;
        }
        return damage;
    }

    // =========================================================
    // HANDLERS D'ÉVÉNEMENTS
    // =========================================================
    private void HandleSpellCast(SpellData spell)
    {
        castSpellThisTurn = true;

        // Masse Critique : 20% chance de lever le flag de crit
        if (activePassive?.passiveType == PassiveType.MasseCritique)
            if (Random.value < activePassive.procChance)
                masseCritiqueBonus = true;
    }

    private void HandleTurnStart()
    {
        castSpellThisTurn = false;

        // Dernier Rempart : +5 PB si HP < seuil
        if (activePassive?.passiveType == PassiveType.DernierRempart)
            if (character.CurrentHP <= activePassive.conditionThreshold)
                character.AddStatusEffect(new StatusEffect(StatusEffectType.Shield, (int)activePassive.effectValue, 1));
    }

    private void HandleTurnEnd()
    {
        // Camouflage : invisible + décalage d'1 case si aucun sort lancé
        if (activePassive?.passiveType == PassiveType.Camouflage && !castSpellThisTurn)
        {
            character.AddStatusEffect(new StatusEffect(StatusEffectType.Invisible, 0, 1));
            ShiftOneRandomCell();
        }
    }

    private void HandleAnyTurnEnd(TacticalCharacter whoJustPlayed)
    {
        // Toxicité : 2 dégâts si l'ennemi termine son tour adjacent
        if (activePassive?.passiveType != PassiveType.Toxicite) return;
        if (whoJustPlayed == character) return;
        if (IsAdjacent(whoJustPlayed))
            whoJustPlayed.TakeDamage((int)activePassive.effectValue, character);
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================
    private bool IsAdjacent(TacticalCharacter other)
    {
        if (character.CurrentCell == null || other.CurrentCell == null) return false;
        int dx = Mathf.Abs(character.CurrentCell.GridX - other.CurrentCell.GridX);
        int dy = Mathf.Abs(character.CurrentCell.GridY - other.CurrentCell.GridY);
        return dx + dy == 1;
    }

    private void ShiftOneRandomCell()
    {
        if (character.CurrentCell == null) return;
        var neighbors = GridManager.Instance.GetNeighbors(character.CurrentCell);
        var free = neighbors.FindAll(c => c.IsWalkable && !c.IsOccupied);
        if (free.Count == 0) return;

        Cell dest = free[Random.Range(0, free.Count)];
        character.CurrentCell.ClearOccupant();
        dest.SetOccupant(character.gameObject);
        character.transform.position = dest.WorldPosition;
        character.ForceSetCell(dest);
    }
}
