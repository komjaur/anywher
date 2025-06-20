// ────────────────────────────────────────────────────────────────────────
//  WorldData.cs
// ────────────────────────────────────────────────────────────────────────
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "new World", menuName = "Game/World/new World")]
public class WorldData : ScriptableObject
{
    /* ─── Databases ─────────────────────────────────────────────── */
    [Header("Databases")]
    public TileDatabase      tileDatabase;
    public AreasDatabase     areasDatabase;
    public BiomeDatabase     biomeDatabase;
    public StructureDatabase structureDatabase;
    public OresDatabase      oresDatabase;
    public UnitDatabase      unitDatabase;
    /* ─── Generator seed ────────────────────────────────────────── */
    [Header("Generation Seed")]
    public int seed = 123;

    /* ─── Dimensions (chunks) ───────────────────────────────────── */
    [Header("World Dimensions (in Chunks)")]
    public int   chunkSize       = 16;
    public int   widthInChunks   = 64;
    public int   heightInChunks  = 32;
    public float overworldStarts = 0.5f;
    public float overworldDepth  = 0.2f;

    /* ─── World-scale biome mask ────────────────────────────────── */
    [Header("World-scale Biome Mask")]
    [Tooltip("Perlin frequency used to mask entire biomes (≤0 disables the mask)")]
    public float worldNoiseFrequency = 0f;   // 0 → feature disabled

    /* ─── Area (Voronoi) generation ────────────────────────────── */
    [Header("Area Generation (Voronoi)")]
    public Vector2Int areaGenerationSize = new(256, 256);
    public int        areaPointsCount    = 25;
    public int        areaOverworldStripes = 10;

    /* ─── Elevation noise ───────────────────────────────────────── */
    [Header("Elevation Noise")]
    public Vector2Int elevationGenerationSize = new(512, 512);
    public float      elevationNoiseScale     = 0.01f;
    public float      elevationNoiseIntensity = 1.0f;

    /* ─── Edge warp noise ───────────────────────────────────────── */
    [Header("Edge-warp Noise")]
    public float edgeNoiseScale     = 0.01f;
    public float edgeNoiseIntensity = 1.0f;

    /* ─── Skyline wave (cosine) ─────────────────────────────────── */
    [Header("Skyline Wave (cos)")]
    [Tooltip("Horizontal scale of the cosine skyline (smaller = more hills)")]
    public float skyLineWaveScale = 0.01f;
    [Tooltip("Vertical amplitude (0…1) of the skyline wave")]
    public float skyLineWaveAmplitude = 0.1f;

    /* ─── Skyline noise layers ──────────────────────────────────── */
    [Header("Skyline Noise (extra layers)")]
    [Tooltip("Adds gently undulating hills")]
    public float skyLowFreq  = 0.002f;
    public float skyLowAmp   = 0.20f;

    [Tooltip("Adds sharp ridges/mesas; keep small")]
    public float skyRidgeFreq = 0.01f;
    public float skyRidgeAmp  = 0.08f;
[Header("Skyline Mountains")]
[Tooltip("Lower = rarer, larger features (0.0003 … 0.002)")]
public float skyMountainFreq = 0.0008f;

[Tooltip("Vertical reach of tall peaks (0 … 0.6)")]
public float skyMountainAmp  = 0.35f;

[Tooltip("How deep valleys can carve below the cosine baseline (0 … 1)")]
public float skyValleyFactor = 0.5f;   // 0 = no valleys, 1 = valleys as tall as mountains

    /* ─── Skyline border blending ───────────────────────────────── */
    [Header("Skyline Blending")]
    [Tooltip("Columns over which skyline parameters blend between neighbouring areas")]
    [Range(0,16)] public int borderSmoothWidth = 4;


}
