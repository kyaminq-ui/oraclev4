using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterStats", menuName = "Oracle/Character Stats")]
public class CharacterStats : ScriptableObject
{
    [Header("Points de vie")]
    public int maxHP = 50;

    [Header("Points d'Action / Mouvement")]
    public int maxPA = 8;
    public int maxPM = 3;

    [Header("Initiative combat (tactical)")]
    [Tooltip("Plus la valeur est haute, plus le personnage agit tôt dans l'ordre des tours.")]
    public int initiative = 10;

}
