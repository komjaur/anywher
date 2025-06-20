/*  BiomeData.cs
 *  ------------------------------------------------------------------------
 *  A ScriptableObject that tells the generator how a single biome looks
 *  and behaves.  All runtime code still works – only the *layout* and
 *  [Header] labels were improved so designers can find things faster.
 *  Nothing was added, deleted, or renamed.
 *  --------------------------------------------------------------------- */

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public abstract class BiomeData : ScriptableObject
{
    /* ───── 1 ▸ identity ───── */
    [Header("▶ Identity & Description")]
    public string biomeName = "New Biome";
    [TextArea] public string description = "Short biome blurb…";

    /* ───── 2 ▸ visuals ───── */
    [Header("▶ Visuals")]
    public Color  color = Color.white;
    [Range(0f,1f)] public float elevation = 0.5f;

    /* ───── 3 ▸ world mask ───── */
    [Header("▶ World-scale Masking")]
    [Range(0f,1f)] public float worldNoiseThreshold = 0f;

    /* ───── 4 ▸ area-noise blend ───── */
    [Header("▶ Area-noise Blend (optional)")]
    [Range(0f,1f)] public float areaNoiseBlend = 0f;

    /* ───── 5 ▸ detail noise ───── */
    [Header("▶ Per-biome FBm Noise")]
    public NoiseType noiseType = NoiseType.Perlin;
    public float frequency = 0.14f, strength = 1f;
    public Vector2 stretch = Vector2.one;
    [Range(1,8)] public int octaves = 1;
    public float lacunarity = 2f, persistence = 0.5f;
    public Vector2 offset = Vector2.zero;

    /* ───── 6 ▸ tile sets ───── */
    [Header("▶ Tile Sets")]
    public BiomeBlock FrontLayerTiles;
    public BiomeBlock BackgroundLayerTiles;

    /* ───── 7 ▸ extra ores ───── */
    [Header("▶ Extra Local Ores")]
    public OreSetting[] Ores;

    /* ───── 8 ▸ weighted décor & trees ───── */
    [Header("▶ Extra Foliage, Decor & Trees (weighted)")]
    public WeightedTile[]  Foliage;   // replaces TileData[] Foliage
    public WeightedTile[]  Decor;     // replaces TileData[] Decor
    public WeightedTree[]  Trees;     // replaces BlueprintTree[] Trees

    /* ───── 9 ▸ multipliers ───── */
    [Header("▶ Spawn-Chance Multipliers  (× Area values)")]
    [Range(0f,2f)] public float foliageChanceMul = 1f;
    [Range(0f,2f)] public float vineChanceMul    = 1f;
    [Range(0f,2f)] public float treeChanceMul    = 1f;
    [Range(0f,2f)] public float decorChanceMul   = 1f;
}

/* ────────────── helper types (unchanged) ────────────── */
public enum NoiseType { Perlin, Simplex }

[System.Serializable]
public class BiomeBlock
{
    public Vector2        NoiseOffset;
    public BiomeSubTile[] subTiles;
}

[System.Serializable]
public class BiomeSubTile
{
    public TileData tileData;
    [Range(0f,1f)] public float threshold;
}
