/* =========================================================================
 *  RecipeDatabase — drops all recipe assets in one place
 * ========================================================================= */
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Game/Crafting/new Database")]
public class RecipeDatabase : ScriptableObject
{
    public RecipeDefinition[] all;
}
