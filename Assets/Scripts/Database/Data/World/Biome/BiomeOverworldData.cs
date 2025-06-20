using UnityEngine;
[CreateAssetMenu(fileName = "Biome_OW_", menuName = "Game/World/Biome/Overworld Biome")]
public class BiomeOverworldData : BiomeData
{
    [Header("Overworld Biome Settings")]
    public OverworldLayer[] overworldLayers;


        public float OverworldnoiseScale = 1f;
    public float OverworldnoiseIntensity = 1f;
}

[System.Serializable]
public class OverworldLayer
{
    public float minDepth = 0f;
    public float maxDepth = 3f;

    public BiomeSubTile[] FrontLayerTiles;
    public BiomeSubTile[] BackgroundLayerTiles;
}

