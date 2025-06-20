using UnityEngine;
#if false
[CreateAssetMenu(
        fileName = "Blueprint_LavaBall",
        menuName  = "Game/World/Blueprint/Structure/Lava-Filled Ball")]
public class BlueprintLavaBall : BlueprintStructure
{
    /* ──────────────  SHAPE  ────────────── */
    [Header("Ball Dimensions")]
    [Min(4)] public int diameterX = 20;
    [Min(4)] public int diameterY = 20;

    [Header("Inner Room")]
    [Min(1)] public int innerRadius = 4;        // radius of *air* pocket

    /* ──────────────  TILES  ────────────── */
    [Header("Tiles")]
    public TileData lavaTile;                   // mandatory
    public TileData wallTile;                   // wall ring around the room
    public TileData chestTile;                  // single chest placed at centre
    public TileData airTileOverride;            // optional – else world.UndergroundAirTile

    /* ──────────────  MAIN  ────────────── */
    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        /* safety checks ------------------------------------------------ */
        if (lavaTile == null || wallTile == null || chestTile == null)
        {
            Debug.LogWarning("[LavaBall] lavaTile, wallTile or chestTile missing!");
            return;
        }

        TileData airTile = airTileOverride ?? world.tiles.UndergroundAirTile;
        if (airTile == null)
        {
            Debug.LogWarning("[LavaBall] world.tiles.UndergroundAirTile is not set!");
            return;
        }

        /* ellipse radii & bounds -------------------------------------- */
        float rx = diameterX * 0.5f;
        float ry = diameterY * 0.5f;

        int minX = Mathf.FloorToInt(anchorX - rx);
        int maxX = Mathf.FloorToInt(anchorX + rx);
        int minY = Mathf.FloorToInt(anchorY - ry);
        int maxY = Mathf.FloorToInt(anchorY + ry);

        int innerR2        = innerRadius        * innerRadius;          // air
        int innerWallR2Min = (innerRadius + 0) * (innerRadius + 0);     // start of wall
        int innerWallR2Max = (innerRadius + 1) * (innerRadius + 1);     // 1-tile thick

        /* carve -------------------------------------------------------- */
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - anchorX;
                float dy = y - anchorY;

                if (!InsideEllipse(dx, dy, rx, ry)) continue;  // outside ball

                float d2 = dx * dx + dy * dy;                  // distance² from centre

                // 1) inner AIR pocket
                if (d2 <= innerR2)
                {
                    world.SetTileID(x, y, airTile.tileID, false);
                    world.SetTileID(x, y, airTile.tileID, true);
                }
                // 2) 1-tile WALL ring
                else if (d2 > innerWallR2Min && d2 <= innerWallR2Max)
                {
                    world.SetTileID(x, y, wallTile.tileID, false);
                    world.SetTileID(x, y, wallTile.tileID, true);
                }
                // 3) remaining volume filled with LAVA (front only)
                else
                {
                    world.SetTileID(x, y, lavaTile.tileID, false);
                }
            }
        }

        /* centre chest ------------------------------------------------- */
        world.SetTileID(anchorX, anchorY, chestTile.tileID, false);
        world.SetTileID(anchorX, anchorY, chestTile.tileID, true); // optional: show in BG too
    }

    /* helpers ---------------------------------------------------------- */
    static bool InsideEllipse(float dx, float dy, float rx, float ry) =>
        (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1f;

    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int radX = Mathf.CeilToInt(diameterX * 0.5f);
        int radY = Mathf.CeilToInt(diameterY * 0.5f);
        return new BoundsInt(ax - radX, ay - radY, 0,
                             radX * 2,  radY * 2, 1);
    }
}
#endif