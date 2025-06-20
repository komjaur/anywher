/* =========================================================================
 *  RecipeDefinition — one craftable recipe (ScriptableObject)
 * ========================================================================= */
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Recipe", menuName = "Game/Crafting/new Recipe")]
public sealed class RecipeDefinition : ScriptableObject
{
    [System.Serializable]
    public struct Ingredient
    {
        public ItemData item;           // What?
        [Min(1)] public int amount;     // How many?
    }

    /* ─────────── Inputs & outputs ─────────── */

    [Header("Input")]
    public List<Ingredient> ingredients = new();

    [Header("Output")]
    public ItemData resultItem;
    [Min(1)] public int resultAmount = 1;

    /* ─────────── XP reward ─────────── */

    [Header("Reward")]
    [Min(0)] public int   xpReward = 0;        // flat XP granted per craft  
    public SkillId        rewardSkill;         // which skill receives the XP
    // -- or, if you prefer a direct reference:  public SkillDefinition skill;
}
