using System;
using UnityEngine;

/// <summary>Journal de combat texte pour HUD / consoles.</summary>
public static class CombatLog
{
    public static event Action<string> OnMessage;

    public static void Append(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Debug.Log("[CombatLog] " + message);
        OnMessage?.Invoke(message);
    }

    public static void AppendDamage(TacticalCharacter target, int amount)
    {
        if (target == null) return;
        Append($"{target.name} subit <b>{amount}</b> dégât(s).");
    }

    public static void AppendHeal(TacticalCharacter target, int amount)
    {
        if (target == null) return;
        Append($"{target.name} récupère <b>{amount}</b> PV.");
    }

    public static void AppendSpell(TacticalCharacter caster, SpellData spell)
    {
        if (caster == null || spell == null) return;
        Append($"{caster.name} lance <b>{spell.spellName}</b>.");
    }
}
