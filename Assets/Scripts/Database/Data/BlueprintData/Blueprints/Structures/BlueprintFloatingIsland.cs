using UnityEngine;
#if false
/// <summary>
/// Generates an organically-shaped floating island and writes tiles via
/// <c>World.SetTileID(x, y, tileID, isBackground)</c>.
///
/// • <b>FrontLayerTiles</b>  → interior solid blocks (foreground)  
/// • <b>BackgroundLayerTiles</b> → interior wall tiles (background)
///
/// Extras  
/// ──────
/// • Grass cap (<see cref="grassTile"/>) on every exposed roof tile.  
/// • <b>dirtDepth</b> rows of <see cref="dirtTile"/> directly beneath that grass – they
///   overwrite existing tiles until air is reached.  
/// • Random trees on the grass (<see cref="treeChance"/>, <see cref="treeBlueprints"/>).  
/// • A buried-treasure chest (<see cref="chestTile"/>) one tile below the dirt layer,
///   centred on the island.
/// </summary>
[CreateAssetMenu(
    fileName = "Blueprint_FloatingIsland",
    menuName = "Game/World/Blueprint/Structure/Floating Island Blueprint")]
public class BlueprintFloatingIsland : BlueprintStructure
{
    #region Inspector
    [Header("Island Dimensions")]
    [Min(1)] public int radiusX = 10;
    [Min(1)] public int radiusY = 5;

    [Header("Surface Tiles")]
    public TileData grassTile;          // roof cap
    public TileData dirtTile;           // filler under grass
    [Min(1)] public int dirtDepth = 2;  // depth in tiles

    [Header("Biome Blocks (Interior Variants)")]
    public BiomeBlock FrontLayerTiles;      // solid blocks (e.g. dirt / stone)
    public BiomeBlock BackgroundLayerTiles; // walls

    [Header("Noise / Shape")]
    [Range(0f, 1f)] public float edgeNoiseStrength = 0.25f;
    public float edgeNoiseScale = 0.3f;
    public int   noiseSeed      = 0;

    [Header("Tree Settings")]
    [Range(0f, 1f)] public float treeChance = 0.3f;
    public BlueprintTree[] treeBlueprints;

    [Header("Buried Treasure")]
    public TileData chestTile;          // drag a chest block here
    public int chestYOffset = 1;        // how many tiles below the dirt layer
    #endregion
    //------------------------------------------------------------------

    public override void PlaceStructure(World world, int cx, int cy)
    {
        if (!ValidateBlocks()) return;

        WriteIsland(world, cx, cy);
        LayDirtUnderGrass(world, cx, cy);
        PlaceChest(world, cx, cy);
        MaybePlantTrees(world, cx, cy);
    }

    bool ValidateBlocks()
    {
        bool ok = true;
        if (FrontLayerTiles == null || FrontLayerTiles.subTiles is not { Length: >0 })
        { Debug.LogWarning("FrontLayerTiles must contain at least one subtile."); ok = false; }

        if (BackgroundLayerTiles == null || BackgroundLayerTiles.subTiles is not { Length: >0 })
        { Debug.LogWarning("BackgroundLayerTiles must contain at least one subtile."); ok = false; }

        if (grassTile == null || grassTile.tileID < 0)
            Debug.LogWarning("GrassTile is missing – roof will stay as interior block.");

        if (dirtTile == null || dirtTile.tileID < 0)
            Debug.LogWarning("DirtTile is missing – no dirt layer will be applied.");

        if (chestTile == null || chestTile.tileID < 0)
            Debug.LogWarning("ChestTile is missing – no treasure chest will spawn.");

        return ok;
    }

    //------------------------------------------------------------------
    #region Island Body
    void WriteIsland(World w, int cx, int cy)
    {
        int minX = cx - radiusX - 1, maxX = cx + radiusX + 1;
        int minY = cy - radiusY - 1, maxY = cy + radiusY + 1;
        float invRX2 = 1f / (radiusX * radiusX);
        float invRY2 = 1f / (radiusY * radiusY);

        // Track highest solid block in each X column
        int width = maxX - minX + 1;
        _topY = new int[width];
        for (int i = 0; i < width; i++) _topY[i] = int.MinValue;

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float dx = x - cx, dy = y - cy;
            float ellipseVal = (dx * dx) * invRX2 + (dy * dy) * invRY2;
            float edge = Mathf.PerlinNoise((x + noiseSeed) * edgeNoiseScale,
                                           (y + noiseSeed) * edgeNoiseScale);
            if (ellipseVal > 1f + (edge - 0.5f) * 2f * edgeNoiseStrength) continue;

            float n = Mathf.PerlinNoise(
                (x + noiseSeed + FrontLayerTiles.NoiseOffset.x) * edgeNoiseScale,
                (y + noiseSeed + FrontLayerTiles.NoiseOffset.y) * edgeNoiseScale);

            int frontID = NearestThreshold(FrontLayerTiles.subTiles, n);
            int backID  = NearestThreshold(BackgroundLayerTiles.subTiles, n);

            if (frontID >= 0)
            {
                w.SetTileID(x, y, frontID, false);          // solid block
                int idx = x - minX;
                if (y > _topY[idx]) _topY[idx] = y;         // remember roof height
            }
            if (backID >= 0)
                w.SetTileID(x, y, backID, true);            // wall
        }

        // Grass pass
        if (grassTile != null && grassTile.tileID >= 0)
        {
            for (int col = 0; col < width; col++)
            {
                int y = _topY[col];
                if (y != int.MinValue)
                {
                    int x = minX + col;
                    w.SetTileID(x, y, grassTile.tileID, false);
                }
            }
        }

        _minX = minX;
        _minY = minY;
    }

    int[] _topY; int _minX; int _minY;
    #endregion

    //------------------------------------------------------------------
    #region Dirt Layer
    void LayDirtUnderGrass(World w, int cx, int cy)
    {
        if (dirtTile == null || dirtTile.tileID < 0 || dirtDepth <= 0 || _topY == null)
            return;

        int width = _topY.Length;
        for (int col = 0; col < width; col++)
        {
            int yGrass = _topY[col];
            if (yGrass == int.MinValue) continue;

            for (int d = 1; d <= dirtDepth; d++)
            {
                int ty = yGrass - d; if (ty < _minY) break;
                int x  = _minX + col;

                int current = w.GetTileID(x, ty);
                if (current == 0) break; // reached air

                w.SetTileID(x, ty, dirtTile.tileID, false);
            }
        }
    }
    #endregion

    //------------------------------------------------------------------
    #region Buried Treasure
    void PlaceChest(World w, int cx, int cy)
    {
        if (chestTile == null || chestTile.tileID < 0 || _topY == null) return;

        int centreCol = cx - _minX;
        if (centreCol < 0 || centreCol >= _topY.Length) return;

        int yGrass = _topY[centreCol];
        if (yGrass == int.MinValue) return;

        int chestY = yGrass - dirtDepth - chestYOffset;
        if (chestY < _minY) chestY = _minY;

        // Skip if chest would end up in air
        if (w.GetTileID(cx, chestY) == 0) return;

        w.SetTileID(cx, chestY, chestTile.tileID, false);
    }
    #endregion

    //------------------------------------------------------------------
    #region Trees
    void MaybePlantTrees(World w, int cx, int cy)
    {
        if (treeBlueprints == null || treeBlueprints.Length == 0 || treeChance <= 0f) return;

        for (int x = cx - radiusX; x <= cx + radiusX; x++)
        {
            for (int y = cy + radiusY + 1; y >= cy - radiusY; y--)
            {
                if (!IsGrassTile(w.GetTileID(x, y))) continue;
                if (w.GetTileID(x, y + 1) > 0) break; // blocked

                if (Random.value < treeChance)
                {
                    BlueprintTree bp = treeBlueprints[Random.Range(0, treeBlueprints.Length)];
                    bp.PlaceStructure(w, x, y + 1); // trunk above grass
                }
                break; // next column
            }
        }
    }
    #endregion

    //------------------------------------------------------------------
    #region Helpers
    public override BoundsInt GetStructureBounds(int ax, int ay) =>
        new BoundsInt(ax - radiusX - 1, ay - radiusY - 1, 0,
                      (radiusX + 1) * 2 + 1, (radiusY + 1) * 2 + 1, 1);

    bool IsGrassTile(int id) =>
        ContainsTile(FrontLayerTiles, id) ||
        (grassTile != null && grassTile.tileID == id);

    static bool ContainsTile(BiomeBlock blk, int id)
    {
        if (blk == null || blk.subTiles == null) return false;
        foreach (var s in blk.subTiles)
            if (s.tileData != null && s.tileData.tileID == id) return true;
        return false;
    }

    static int NearestThreshold(BiomeSubTile[] subs, float n)
    {
        // assumes array sorted by ascending threshold
        int lo = 0, hi = subs.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (subs[mid].threshold < n) lo = mid + 1; else hi = mid - 1;
        }
        int low  = Mathf.Clamp(hi, 0, subs.Length - 1);
        int up   = Mathf.Clamp(lo, 0, subs.Length - 1);
        int pick = Mathf.Abs(n - subs[low].threshold) <= Mathf.Abs(n - subs[up].threshold)
                   ? low : up;
        return subs[pick].tileData ? subs[pick].tileData.tileID : -1;
    }
    #endregion
}
#endif