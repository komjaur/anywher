using UnityEngine;

/* ──────────────────────────────────────────────────────────────
 *  GameData – top–level ScriptableObject that the GameManager
 *  keeps in its inspector.  All “global” authoring-time data
 *  hangs off this asset.
 * ─────────────────────────────────────────────────────────────*/
[CreateAssetMenu(fileName = "Game", menuName = "Game/System/new Game")]
public sealed class GameData : ScriptableObject
{
    /* ---------- World generation ----------------------------------- */
    [Header("World-generation")]
    public WorldData world;                     // seed, size, DB refs …

    /* ---------- General settings ----------------------------------- */
    [Header("Game settings")]
    public GameSettings settings;

    /* ---------- Audio ---------------------------------------------- */
    [Header("Audio clips & Sound-font")]
    public AudioClip levelUpClip;   // ping that plays on XP level-up
    public FluidMidi.StreamingAsset soundFont;  // background music bank

    /* ---------- Databases / gameplay assets ------------------------ */
    [Header("Databases")]
    public SkillDatabase skillDatabase;
    public RecipeDatabase recipeDatabase;      // crafting recipes

    [Header("Unit templates")]
    public UnitTemplate playerUnit;

    /* ---------- UI -------------------------------------------------- */
    [Header("UI / HUD prefabs")]
    public GameObject uiToast;                  // XP / info toast prefab

    /* ---------- Optional: mining crack overlay --------------------- */
    [Header("Mining - Crack overlay frames (empty = no overlay)")]
    [Tooltip("Ordered low→high damage. 1-4 sprites is typical. " +
             "TerrainEditService passes a progress value (0–1).")]
    public Sprite[] crackStages;
    /*  The mining-HP system never *requires* this.  If you leave the
        array empty no overlay will be drawn (your code can simply
        return / skip when crackStages.Length == 0).                     */
    
}
