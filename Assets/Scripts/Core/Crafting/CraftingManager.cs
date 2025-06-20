using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public sealed class CraftingManager : MonoBehaviour
{
    /* ──────── Dependencies ──────── */
    [Header("Data source")]
    [SerializeField] private RecipeDatabase recipeDatabase;

    [Header("Runtime")]
    [SerializeField] private SkillManager skillManager;   // ← swap in

    public IEnumerable<RecipeDefinition> AllRecipes => recipes;
    private readonly List<RecipeDefinition> recipes = new();

    /* ──────── Lifecycle ──────── */
    private void Awake()
    {
        if (!recipeDatabase && GameManager.Instance)
            recipeDatabase = GameManager.Instance.GameData.recipeDatabase;

        if (!recipeDatabase)
        {
            Debug.LogError("CraftingManager: RecipeDatabase missing.");
            enabled = false;
            return;
        }

        recipes.AddRange(recipeDatabase.all.Where(r => r));

        // If not wired in the inspector, fetch from GameManager (created earlier).
        if (!skillManager && GameManager.Instance)
            skillManager = GameManager.Instance.SkillManager;
    }

    /* ──────── Public API ──────── */
    public IEnumerable<RecipeDefinition> GetCraftable(Inventory inv) =>
        recipes.Where(r => CanCraft(inv, r));

    public bool CanCraft(Inventory inv, RecipeDefinition recipe)
    {
        foreach (var ing in recipe.ingredients)
            if (inv.CountItem(ing.item) < ing.amount)
                return false;
        return true;
    }

    /// Consume ingredients, add outputs, and award XP. Returns true if successful.
    public bool Craft(Inventory inv, RecipeDefinition recipe)
    {
        if (!CanCraft(inv, recipe))
            return false;

        /* remove inputs */
        foreach (var ing in recipe.ingredients)
            inv.TryRemoveItem(ing.item, ing.amount);

        /* add outputs */
        inv.TryAddItem(recipe.resultItem, recipe.resultAmount);

        /* award XP */
        if (skillManager && recipe.xpReward > 0)
            skillManager.AddXp(recipe.rewardSkill, recipe.xpReward);

        return true;
    }
}
