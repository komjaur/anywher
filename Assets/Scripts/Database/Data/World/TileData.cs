using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public enum BlockBehavior { Solid, Liquid, Air, Powder, Soil, Platform }
public enum BlockTag       { Basic, Tree, Vine, Foliage }

public enum RenderGroup
{
    None            = -1,            // sentinel – use Solid if unspecified

    Wall            = 0,
    UnderwaterDecor = 1,
    Liquid          = 2,
    Solid           = 3,
    Platform        = 4,
    Vegetation      = 5,
    Decoration      = 6,
    Wiring          = 7,
    Overlay         = 8,             // on-top cracks / transient FX

    Count           = 9              // number of Tilemaps to allocate
}

[CreateAssetMenu(fileName = "Tile", menuName = "Game/World/Tile")]
public sealed class TileData : ScriptableObject
{
    [Header("Identification")]
    public int      tileID = 0;                 // assigned by the database
    public string   tileName;
    public Color    color  = Color.white;
    public TileBase tileBase;

    [Header("Physical properties")]
    public BlockBehavior behavior   = BlockBehavior.Solid;
    public BlockTag      tag        = BlockTag.Basic;
    public RenderGroup   renderLayer= RenderGroup.Solid;

    [Tooltip("If true the tile cannot be broken in normal play (e.g. dungeon bricks).")]
    public bool unbreakable = false;

    [Tooltip("Pickaxe/tool power required to break this tile (≤0 means any tool).")]
    public int  requiredToolPower = 1;

    [Tooltip("Health / mining time multiplier (100 = default).")]
    public int  health = 100;

    [Header("XP reward")]
    [Tooltip("XP granted when this block is broken (0 = use tag-based default).")]
    public int xpOnMine = 0;

    [Header("Lighting")]
    public float lightStrength = 0f;
    public Color lightColor    = Color.white;
    [Range(0f, 1f)] public float lightFalloff = 0.10f;

    [Header("Growth & special tiles")]
    public TileData grassTile;
    public TileData vineTile;
    public int      vineMinLength = 1;
    public int      vineMaxLength = 6;

    public List<TileData> canPlaceOnTiles = new();
    public BlueprintGrowable BlueprintGrowableMatured;

    [Header("Drops & loot")]
    [Tooltip("Item spawned when this block is mined (null = no drop).")]
    public ItemData dropItem;

    /* ───────── convenience ───────── */

    public bool EmitsLight => lightStrength > 0f;

    public bool CanBeBrokenBy(int toolPower) =>
        !unbreakable && (requiredToolPower <= 0 || toolPower >= requiredToolPower);
}
