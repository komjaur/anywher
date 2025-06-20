using UnityEngine;
#if false
[CreateAssetMenu(fileName = "Blueprint_SimpleBox", menuName = "Game/World/Blueprint/Structure/Simple Box Blueprint")]
public class BlueprintSimpleBox : BlueprintStructure
{
    [Header("Box Dimensions")]
    public int boxWidth = 8;
    public int boxHeight = 6;

    [Header("Tiles")]
    public TileData wallTile;
    public TileData backgroundTile;
    public TileData chestTile;

    // ------------------------------------------------------------
    // OVERRIDE: We’ll scan downward before placing the box.
    // ------------------------------------------------------------
    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        // 1) Verify we have needed tile references
        if (wallTile == null || backgroundTile == null)
        {
            Debug.LogWarning("BlueprintSimpleBox missing required wallTile or backgroundTile!");
            return;
        }

        // 2) Try to find solid ground by scanning downward
        int groundY = FindGroundY(world, anchorX, anchorY);
        if (groundY < 0)
        {
            // Could not find ground, so skip placement
            Debug.LogWarning("No solid ground found. Skipping structure placement.");
            return;
        }

        // Place this box so that its bottom is just above the ground
        int finalAnchorY = groundY + 1; // The “floor” of the box sits on top of ground

        // 3) Compute bounding box for the house
        int minX = anchorX;
        int maxX = anchorX + boxWidth - 1;

        int minY = finalAnchorY;
        int maxY = finalAnchorY + boxHeight - 1;

        // 4) Fill rectangle with walls on the border, background in the interior
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                bool onBorder = (x == minX || x == maxX || y == minY || y == maxY);
                if (onBorder)
                {
                    // Place wall in front layer
                    world.SetTileID(x, y, wallTile.tileID, isBackground: false);
                }
                else
                {
                    // Interior => place background tile
                    // (First overwrite front with air if you want an empty interior)
                    if (world.Data.tileDatabase.UndergroundAirTile != null)
                    {
                        world.SetTileID(x, y, world.Data.tileDatabase.UndergroundAirTile.tileID, isBackground: false);
                    }
                    // Then place the background tile
                    world.SetTileID(x, y, backgroundTile.tileID, isBackground: true);
                }
            }
        }

        // 5) Optionally place a chest in the center
        if (chestTile != null)
        {
            int chestX = (minX + maxX) / 2;
            int chestY = (minY + maxY) / 2;
            world.SetTileID(chestX, chestY, chestTile.tileID, isBackground: false);
        }
    }

    // ------------------------------------------------------------
    // Scans downward from the starting Y to find a solid tile.
    // Returns the Y coordinate of that solid tile, or -1 if not found.
    // ------------------------------------------------------------
    private int FindGroundY(World world, int startX, int startY)
    {
        // 1) Don’t search below world bounds
        int minValidY = 0; // If your world can go negative, adjust accordingly

        // 2) Move downward from startY
        for (int y = startY; y >= minValidY; y--)
        {
            // Retrieve the tile ID in the front layer (or “solid” layer)
            int tileID = world.GetTileID(startX, y);
            if (tileID > 0)
            {
                // Look up the TileData to see if it’s solid
                TileData tileData = world.tiles.GetTileDataByIndex(tileID);
                if (tileData != null && tileData.behavior == BlockBehavior.Solid)
                {
                    return y; // Found ground
                }
            }
        }

        // If we never found a solid tile, return -1
        return -1;
    }
}
#endif