using UnityEngine;
#if false
[CreateAssetMenu(fileName = "Blueprint_Pyramid", menuName = "Game/World/Blueprint/Structure/Pyramid Blueprint")]
public class BlueprintPyramid : BlueprintStructure
{
    [Header("Pyramid Dimensions")]
    public int pyramidHeight = 10;

    [Header("Offsets")]
    [Tooltip("Offset from the anchor on X and Y axes. Positive moves the pyramid up/right, negative down/left.")]
    public int xOffset = 0;
    public int yOffset = 0;

    [Header("Tiles")]
    public TileData pyramidTile;         // The main tile used for the pyramid
    public TileData backgroundWallTile;  // Optional background tile
    public TileData airTile;             // The "air" tile used to carve tunnels/chambers
    public TileData chestTile;           // Represents your treasure chest

    [Tooltip("If true, the pyramid’s interior is left empty (only an outer ‘shell’).")]
    public bool hollowInside = false;

    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        // Make sure we have the necessary tiles
        if (pyramidTile == null)
        {
            Debug.LogWarning("BlueprintPyramid is missing 'pyramidTile'!");
            return;
        }
        if (airTile == null)
        {
            Debug.LogWarning("BlueprintPyramid is missing 'airTile'!");
            return;
        }
        // chestTile is optional, so we don’t return even if it’s missing

        // Apply offsets to anchor
        anchorX += xOffset;
        anchorY += yOffset;

        // 1) Optional: place background walls for each row
        if (backgroundWallTile != null)
        {
            PlaceBackgroundWalls(world, anchorX, anchorY);
        }

        // 2) Place the main pyramid shape in the front layer
        PlacePyramidShape(world, anchorX, anchorY);

        // 3) If not hollow, carve out an internal passage
        if (!hollowInside)
        {
            CarveInternalTunnelAndRooms(world, anchorX, anchorY);
        }
    }

    // ------------------------------------------------------------------------
    // Helper to find the bottom Y of the pyramid if anchor is truly the center.
    // Then each “level” is rowY = bottomY + level.
    // ------------------------------------------------------------------------
    private int GetBottomY(int centerY)
    {
        // If we want the anchor to be the vertical center, half the pyramid is below anchorY.
        int halfHeight = pyramidHeight / 2;
        return centerY - halfHeight;
    }

    // ------------------------------------------------------------------------
    // Places background tiles row by row, matching the pyramid’s shape.
    // ------------------------------------------------------------------------
    void PlaceBackgroundWalls(World world, int centerX, int centerY)
    {
        int bottomY = GetBottomY(centerY);

        for (int level = 0; level < pyramidHeight; level++)
        {
            int rowY = bottomY + level;
            // The widest row is at level=0 => width = 2 * pyramidHeight
            int rowWidth = 2 * (pyramidHeight - level);
            // Center each row horizontally about centerX
            int rowLeft = centerX - (rowWidth / 2);

            // Fill the entire row in the background
            for (int x = 0; x < rowWidth; x++)
            {
                world.SetTileID(
                    rowLeft + x,
                    rowY,
                    backgroundWallTile.tileID,
                    isBackground: true
                );
            }
        }
    }

    // ------------------------------------------------------------------------
    // Places the main pyramid shape in the front layer.
    // If hollowInside == true, only places the “outline” per row.
    // ------------------------------------------------------------------------
    void PlacePyramidShape(World world, int centerX, int centerY)
    {
        int bottomY = GetBottomY(centerY);

        for (int level = 0; level < pyramidHeight; level++)
        {
            int rowY = bottomY + level;
            int rowWidth = 2 * (pyramidHeight - level);
            int rowLeft = centerX - (rowWidth / 2);

            if (hollowInside)
            {
                // Outline each row: place leftmost & rightmost
                if (rowWidth <= 2)
                {
                    // If the row is very narrow, fill it
                    for (int x = 0; x < rowWidth; x++)
                    {
                        world.SetTileID(rowLeft + x, rowY, pyramidTile.tileID);
                    }
                }
                else
                {
                    // Just place the edges
                    world.SetTileID(rowLeft, rowY, pyramidTile.tileID);
                    world.SetTileID(rowLeft + rowWidth - 1, rowY, pyramidTile.tileID);
                }
            }
            else
            {
                // Solid fill of pyramidTile
                for (int x = 0; x < rowWidth; x++)
                {
                    world.SetTileID(rowLeft + x, rowY, pyramidTile.tileID);
                }
            }
        }
    }

    // ------------------------------------------------------------------------
    // If the pyramid is not hollow, carve a tunnel & main chamber inside.
    // Overwrites some pyramid tiles with airTile.
    // ------------------------------------------------------------------------
    void CarveInternalTunnelAndRooms(World world, int centerX, int centerY)
    {
        int bottomY = GetBottomY(centerY);
        int topY = bottomY + pyramidHeight - 1;

        // A) Entrance is at the base’s horizontal center
        int entranceX = centerX;
        int entranceY = bottomY;

        // B) Corridor goes from bottom ~75% up
        int corridorTopY = bottomY + (int)(0.75f * pyramidHeight);

        // Carve a 1-wide vertical corridor
        for (int y = entranceY; y <= corridorTopY; y++)
        {
            world.SetTileID(entranceX, y, airTile.tileID);
        }

        // C) Main chamber near the top
        int chamberWidth = 5;
        int chamberHeight = 4;
        int chamberLeft = entranceX - (chamberWidth / 2);
        int chamberBottom = corridorTopY;

        CarveRect(world, chamberLeft, chamberBottom, chamberWidth, chamberHeight, airTile);

        // D) Place a chest in the chamber center
        if (chestTile != null)
        {
            int chestX = chamberLeft + (chamberWidth / 2);
            int chestY = chamberBottom + (chamberHeight / 2);
            world.SetTileID(chestX, chestY, chestTile.tileID);
        }

        // E) Optional: hidden side rooms
        CarveHiddenRoom(world, chamberLeft - 1, chamberBottom + 1, 3, 3, airTile);          
        CarveHiddenRoom(world, chamberLeft + chamberWidth - 2, chamberBottom + 1, 3, 3, airTile); 
    }

    // ------------------------------------------------------------------------
    // Overwrites a rectangular region in the front layer with carveTile (often air).
    // ------------------------------------------------------------------------
    void CarveRect(World world, int xStart, int yStart, int width, int height, TileData carveTile)
    {
        for (int y = yStart; y < yStart + height; y++)
        {
            for (int x = xStart; x < xStart + width; x++)
            {
                world.SetTileID(x, y, carveTile.tileID);
            }
        }
    }

    // ------------------------------------------------------------------------
    // A “hidden room” is just a carved-out rectangular area. 
    // If not connected by a corridor, it’s effectively secret.
    // ------------------------------------------------------------------------
    void CarveHiddenRoom(World world, int xStart, int yStart, int width, int height, TileData carveTile)
    {
        for (int y = yStart; y < yStart + height; y++)
        {
            for (int x = xStart; x < xStart + width; x++)
            {
                world.SetTileID(x, y, carveTile.tileID);
            }
        }
    }
}
#endif