using System.Collections.Generic;
using UnityEngine;

public static class SpellResolver
{
    public static void Resolve(SpellData spell, TacticalCharacter caster, List<Cell> affectedCells)
    {
        foreach (SpellEffect effect in spell.effects)
            ApplyEffect(effect, spell, caster, affectedCells);

        caster.NotifySpellCast(spell);
    }

    // =========================================================
    // APPLICATION D'UN EFFET
    // =========================================================
    private static void ApplyEffect(SpellEffect effect, SpellData spell, TacticalCharacter caster, List<Cell> cells)
    {
        foreach (Cell cell in cells)
        {
            TacticalCharacter target = GetCharacterAt(cell);

            switch (effect.type)
            {
                // ---- Dégâts ----
                case SpellEffectType.Damage:
                    if (target == null) break;
                    int dmg = effect.value;
                    if (effect.condition == SpellCondition.TargetHPBelow && target.CurrentHP < effect.conditionThreshold)
                        dmg = Mathf.RoundToInt(dmg * effect.conditionMultiplier);
                    else if (effect.condition == SpellCondition.FromBehind && IsFromBehind(caster, target))
                        dmg = Mathf.RoundToInt(dmg * effect.conditionMultiplier);

                    // Passifs offensifs (Berserker, Maître d'Arme, Sniper, Masse Critique)
                    var pm = caster.GetComponent<PassiveManager>();
                    if (pm != null)
                    {
                        int dist = Mathf.Abs(caster.CurrentCell.GridX - cell.GridX)
                                 + Mathf.Abs(caster.CurrentCell.GridY - cell.GridY);
                        dmg = pm.ModifyOutgoingDamage(dmg, spell, target, dist);
                    }

                    target.TakeDamage(dmg, caster);
                    CombatLog.Append($"{caster?.name ?? "?"} inflige <b>{dmg}</b> PV à {target.name} ({spell.spellName}).");
                    break;

                case SpellEffectType.SelfDamage:
                    caster.TakeDamage(effect.value, null);
                    break;

                // ---- Soin ----
                case SpellEffectType.Heal:
                    (target ?? caster).Heal(effect.value);
                    CombatLog.Append($"{(target ?? caster).name} récupère <b>{effect.value}</b> PV.");
                    break;

                // ---- Saignement ----
                case SpellEffectType.Bleed:
                    target?.AddStatusEffect(new StatusEffect(StatusEffectType.Bleed, effect.value, effect.duration, true));
                    break;

                // ---- Statuts offensifs ----
                case SpellEffectType.Silence:
                    target?.AddStatusEffect(new StatusEffect(StatusEffectType.Silence, 0, effect.duration, true));
                    break;

                case SpellEffectType.GravityDebuff:
                    target?.AddStatusEffect(new StatusEffect(StatusEffectType.GravityDebuff, 0, effect.duration, true));
                    break;

                case SpellEffectType.ReduceFirstAttack:
                    target?.AddStatusEffect(new StatusEffect(StatusEffectType.ReducedAttack, effect.value, effect.duration, true));
                    break;

                case SpellEffectType.RemovePM:
                    target?.RemovePM(effect.value);
                    break;

                case SpellEffectType.StealPM:
                    if (target != null)
                    {
                        int stolen = target.RemovePM(effect.value);
                        caster.AddBonusPM(stolen);
                    }
                    break;

                // ---- Statuts défensifs (sur le caster) ----
                case SpellEffectType.Shield:
                    caster.AddStatusEffect(new StatusEffect(StatusEffectType.Shield, effect.value, effect.duration));
                    break;

                case SpellEffectType.DamageReduction:
                    caster.AddStatusEffect(new StatusEffect(StatusEffectType.DamageReduction, effect.value, effect.duration));
                    break;

                case SpellEffectType.Thorns:
                    caster.AddStatusEffect(new StatusEffect(StatusEffectType.Thorns, effect.value, effect.duration));
                    break;

                case SpellEffectType.Invisible:
                    caster.AddStatusEffect(new StatusEffect(StatusEffectType.Invisible, 0, effect.duration));
                    break;

                case SpellEffectType.LastBreath:
                    caster.AddStatusEffect(new StatusEffect(StatusEffectType.LastBreath, 1, effect.duration));
                    break;

                // ---- Ressources ----
                case SpellEffectType.BonusPA:
                    caster.AddBonusPA(effect.value);
                    break;

                case SpellEffectType.BonusPM:
                    caster.AddBonusPM(effect.value);
                    break;

                case SpellEffectType.BonusPANextTurn:
                    caster.AddNextTurnBonusPA(effect.value);
                    break;

                case SpellEffectType.BonusRange:
                    caster.AddBonusRange(effect.value, effect.duration);
                    break;

                case SpellEffectType.ConvertPMtoPA:
                    if (caster.RemovePM(1) > 0) caster.AddBonusPA(1);
                    break;

                // ---- Nettoyage ----
                case SpellEffectType.Cleanse:
                    (target ?? caster).ClearAllDebuffs();
                    break;

                // ---- Déplacements ----
                case SpellEffectType.Push:
                    if (target != null) Push(target, caster.CurrentCell, effect.value);
                    break;

                case SpellEffectType.Pull:
                    PullToward(caster, cell, effect.value);
                    break;

                case SpellEffectType.Swap:
                    if (target != null) Swap(caster, target);
                    break;

                case SpellEffectType.Teleport:
                    if (!cell.IsOccupied && cell.IsWalkable) MoveInstant(caster, cell);
                    break;

                // ---- Mur temporaire ----
                case SpellEffectType.CreateWall:
                    if (cell != null && !cell.IsOccupied)
                        cell.IsWalkable = false;
                    // TODO : WallManager pour retirer le mur après effect.duration tours
                    break;
            }
        }
    }

    // =========================================================
    // UTILITAIRES
    // =========================================================
    private static TacticalCharacter GetCharacterAt(Cell cell)
    {
        if (cell == null || !cell.IsOccupied) return null;
        return cell.Occupant?.GetComponent<TacticalCharacter>();
    }

    private static bool IsFromBehind(TacticalCharacter attacker, TacticalCharacter target)
    {
        Cell a = attacker.CurrentCell;
        Cell t = target.CurrentCell;
        int dx = a.GridX - t.GridX;
        int dy = a.GridY - t.GridY;
        switch (target.Facing)
        {
            case FacingDirection.SouthEast: return dx < 0 && dy > 0;
            case FacingDirection.SouthWest: return dx > 0 && dy > 0;
            case FacingDirection.NorthEast: return dx < 0 && dy < 0;
            case FacingDirection.NorthWest: return dx > 0 && dy < 0;
            default: return false;
        }
    }

    private static void Push(TacticalCharacter target, Cell pushSource, int distance)
    {
        Cell tc = target.CurrentCell;
        int stepX = tc.GridX - pushSource.GridX;
        int stepY = tc.GridY - pushSource.GridY;
        if (stepX != 0) stepX = stepX > 0 ? 1 : -1;
        if (stepY != 0) stepY = stepY > 0 ? 1 : -1;

        Cell dest = tc;
        for (int i = 0; i < distance; i++)
        {
            Cell next = GridManager.Instance.GetCell(dest.GridX + stepX, dest.GridY + stepY);
            if (next == null || !next.IsWalkable || next.IsOccupied) break;
            dest = next;
        }
        if (dest != tc) MoveInstant(target, dest);
    }

    private static void PullToward(TacticalCharacter caster, Cell targetCell, int distance)
    {
        Cell origin = caster.CurrentCell;
        int stepX = targetCell.GridX - origin.GridX;
        int stepY = targetCell.GridY - origin.GridY;
        if (stepX != 0) stepX = stepX > 0 ? 1 : -1;
        if (stepY != 0) stepY = stepY > 0 ? 1 : -1;

        // Case adjacente à la cible (juste avant)
        Cell dest = GridManager.Instance.GetCell(targetCell.GridX - stepX, targetCell.GridY - stepY);
        if (dest == null || !dest.IsWalkable || dest.IsOccupied) return;
        MoveInstant(caster, dest);
    }

    private static void Swap(TacticalCharacter a, TacticalCharacter b)
    {
        Cell ca = a.CurrentCell;
        Cell cb = b.CurrentCell;
        ca.ClearOccupant();
        cb.ClearOccupant();
        ca.SetOccupant(b.gameObject);
        cb.SetOccupant(a.gameObject);
        if (GridManager.Instance != null)
        {
            b.transform.position = GridManager.Instance.GridToWorldFace(ca.GridX, ca.GridY);
            a.transform.position = GridManager.Instance.GridToWorldFace(cb.GridX, cb.GridY);
        }
        else
        {
            b.transform.position = ca.WorldPosition;
            a.transform.position = cb.WorldPosition;
        }
        b.ForceSetCell(ca);
        a.ForceSetCell(cb);
    }

    private static void MoveInstant(TacticalCharacter character, Cell destination)
    {
        character.CurrentCell?.ClearOccupant();
        destination.SetOccupant(character.gameObject);
        character.transform.position = GridManager.Instance != null
            ? GridManager.Instance.GridToWorldFace(destination.GridX, destination.GridY)
            : destination.WorldPosition;
        character.ForceSetCell(destination);
    }
}
