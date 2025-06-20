using UnityEngine;

/* ───────── helper payloads ───────── */
[System.Serializable] public class WeightedTile  { public TileData tile;  [Min(0)] public float weight = 1; }
[System.Serializable] public class WeightedTree  { public BlueprintTree prefab; [Min(0)] public float weight = 1; }

public enum ZoneType { Sky, Overworld, Underworld }

[CreateAssetMenu(fileName = "Area_", menuName = "Game/World/new Area Data")]
public class AreaData : ScriptableObject
{
    /* ───────── identification ───────── */
    public string   areaName;
    public Color    color = Color.white;
    public ZoneType zone;

    /* ───────── biomes & noise ───────── */
    public BiomeData[] biomes;
    [Tooltip("Per-area noise scale deciding which biome appears where.")]
    public float areaNoiseFrequency = 0.003f;

    /* ───────── weighted spawns ───────── */
    public WeightedTile[] Foliage;
    public WeightedTile[] Decor;
    public WeightedTree[] Trees;
    public OreSetting[]   Ores;
    public WeightedUnit[] Units;
    public FluidMidi.StreamingAsset[] MusicPlaylist;
    [Range(0,1)] public float foliageSpawnChance = 0.40f;
    [Range(0,1)] public float decorSpawnChance   = 0.15f;
    [Range(0,1)] public float vineSpawnChance    = 0.30f;
    [Range(0,1)] public float treeSpawnChance    = 0.30f;

    /* ───────── default ground tiles ───────── */
    public TileData defaultDirt;
    public TileData defaultStone;
    public TileData defaultGrass;

    /* ───────── PARALLAX BACKGROUND ───────── */
    [Header("Parallax Background")]
    [Tooltip("Leave null if this area should have no distant background.")]
    public ParallaxData parallax;
}
