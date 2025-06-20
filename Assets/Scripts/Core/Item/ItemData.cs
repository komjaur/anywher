// ItemData.cs
// -----------------------------------------------------------------------------
//  Definition of a single item that can appear in the player’s inventory,
//  drop from blocks, be crafted, consumed, equipped, etc.
// -----------------------------------------------------------------------------

using UnityEngine;

public enum ItemType
{
    Material,      // raw resources (stone, wood, sand, ore)
    Consumable,    // food, potions, ammo
    Tool,          // pickaxe, axe, hammer…
    Weapon,        // sword, bow, staff…
    Armor,         // helmet, chest, boots
    Accessory,     // rings, trinkets
    Placeable      // turns back into a Tile when clicked in the world
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Quest
}

[CreateAssetMenu(fileName = "Item", menuName = "Game/Inventory/Item")]
public sealed class ItemData : ScriptableObject
{
    /* ───────── Identification ───────── */
    [Header("Identification")]
    public int itemID = 0;               // unique, set by your database tool
    public string itemName = "New Item";

    [Tooltip("16×16 or 32×32 sprite used in the inventory / hot-bar.")]
    public Sprite icon;

    [Multiline(3)]
    public string description;

    /* ───────── Classification ───────── */
    [Header("Type & rarity")]
    public ItemType type = ItemType.Material;
    public ItemRarity rarity = ItemRarity.Common;

    /* ───────── Stack & value ───────── */
    [Header("Stacking & value")]
    [Tooltip("Maximum items per inventory slot (1 = non-stackable).")]
    public int maxStack = 999;

    [Tooltip("NPC sell price in copper / basic currency.")]
    public int valueCopper = 0;

    /* ───────── Combat stats (optional) ───────── */
    [Header("Combat (only if weapon/tool)")]
    public int damage = 0;
    public float knockback = 0f;
    public float attackSpeed = 1f;    // swings per second

    /* ───────── Tool stats (optional) ───────── */
    [Header("Tool power")]
    public int pickaxePower = 0;
    public int axePower = 0;
    public int hammerPower = 0;

    /* ───────── Placeable tiles ───────── */
    [Header("World placement")]
    [Tooltip("If non-null, placing this item creates that Tile in the world.")]
    public TileData placeableTile;

    /* ───────── Convenience ───────── */
    public bool IsStackable => maxStack > 1;
    public bool IsPlaceable => placeableTile != null;
    public bool IsWeapon => damage > 0 && type == ItemType.Weapon;
    public bool IsTool => pickaxePower > 0 || axePower > 0 || hammerPower > 0;
        /* ── helper properties ── */

    public bool IsConsumable => type == ItemType.Consumable;
}
