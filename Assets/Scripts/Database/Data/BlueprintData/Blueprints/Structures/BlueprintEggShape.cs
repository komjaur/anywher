using UnityEngine;
#if false
[CreateAssetMenu(fileName = "Blueprint_EggShape", menuName = "Game/World/Blueprint/Structure/Egg Shape Blueprint")]
public class BlueprintEggShape : BlueprintStructure
{
    [Header("Egg Shape Dimensions")]
    public int eggWidth = 10;   // Horizontal diameter
    public int eggHeight = 14;  // Vertical diameter
    public int wallThickness = 2;  // Thickness of the border wall

    [Header("Tiles")]
    public TileData wallTile;
    public TileData heartTile;

    [Header("Fill Tiles (Optional)")]
    [Tooltip("Tile to fill the front layer. If null, no front tile will be placed.")]
    public TileData fillTileFront;

    [Tooltip("Tile to fill the background layer. If null, no background tile will be placed.")]
    public TileData fillTileBackground;


    [Header("Heart Shape Settings")]
    [Tooltip("Scale factor for the heart shape. Adjust to control the size of the heart in the center.")]
    public float heartScale = 3.0f;

    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        // Verify required references
        if (wallTile == null || heartTile == null)
        {
            Debug.LogWarning("BlueprintEggShape missing required TileData references (wallTile or heartTile)!");
            return;
        }
        // fillTileFront & fillTileBackground are optional; we check for null before we use them.

        // Calculate half-width & half-height (radii)
        float rx = eggWidth / 2f;
        float ry = eggHeight / 2f;

        // Depending on anchor setting, determine the bounding box for iteration
        int minX, maxX, minY, maxY;
        if (anchorIsCenter)
        {
            minX = Mathf.FloorToInt(anchorX - rx);
            maxX = Mathf.FloorToInt(anchorX + rx);
            minY = Mathf.FloorToInt(anchorY - ry);
            maxY = Mathf.FloorToInt(anchorY + ry);
        }
        else
        {
            // When the anchor is at the bottom
            minX = Mathf.FloorToInt(anchorX - rx);
            maxX = Mathf.FloorToInt(anchorX + rx);
            minY = anchorY;
            maxY = anchorY + eggHeight;
        }

        // Determine the ellipse center based on anchor type
        float centerX = anchorIsCenter ? anchorX : (anchorX + rx);
        float centerY = anchorIsCenter ? anchorY : anchorY; // for non-centered, anchor is at bottom on Y

        // Loop over the bounding box of the ellipse
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                // Compute relative coordinates
                float dx = x - centerX;
                float dy = y - centerY;

                // Check if (x, y) is inside the egg’s ellipse
                if (IsInsideEllipse(dx, dy, rx, ry))
                {
                    // Check if we're within the thick wall region
                    if (IsThickBorder(x, y, rx, ry, anchorX, anchorY, wallThickness))
                    {
                        // Place the wall tile in the front layer
                        world.SetTileID( x, y, wallTile.tileID, false);
                    }
                    // Otherwise, if inside the heart shape, place the heart tile in the front
                    else if (IsInsideHeartShape(dx, dy, heartScale))
                    {
                        world.SetTileID( x, y, heartTile.tileID, false);
                    }
                    else
                    {
                        // For the interior region that is not the thick border and not the heart:
                        // 1) (Optional) place a front tile if fillTileFront is not null
                        // 2) (Optional) place a background tile if fillTileBackground is not null

                        if (fillTileFront != null)
                        {
                            world.SetTileID( x, y, fillTileFront.tileID, false);
                        }

                        if (fillTileBackground != null)
                        {
                            // If you want to remove any existing tile in the background first, you could do:
                            //   WorldMapPostGenerator.SetTileID(world, x, y, world.tileDatabase.UndergroundAirTile.tileID, true);
                            // or skip that if you only want to overwrite.

                            world.SetTileID( x, y, fillTileBackground.tileID, true);
                        }
                    }
                }
            }
        }
    }

    // Checks if a point (dx, dy) is inside the ellipse defined by radii (rx, ry)
    bool IsInsideEllipse(float dx, float dy, float rx, float ry)
    {
        if (rx <= 0f || ry <= 0f) return false;
        float ellipseVal = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);
        return ellipseVal <= 1f;
    }

    // Determines if a cell is in the thick border region
    bool IsThickBorder(int cellX, int cellY, float rx, float ry, int anchorX, int anchorY, int thickness)
    {
        float centerX = anchorIsCenter ? anchorX : (anchorX + rx);
        float centerY = anchorIsCenter ? anchorY : anchorY;

        // Check the neighborhood within “thickness” range
        for (int offsetX = -thickness + 1; offsetX < thickness; offsetX++)
        {
            for (int offsetY = -thickness + 1; offsetY < thickness; offsetY++)
            {
                int nx = cellX + offsetX;
                int ny = cellY + offsetY;
                float ndx = nx - centerX;
                float ndy = ny - centerY;

                // If any neighbor is outside the ellipse, we're on the border
                if (!IsInsideEllipse(ndx, ndy, rx, ry))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Checks if a point is in the heart shape
    bool IsInsideHeartShape(float dx, float dy, float heartScale)
    {
        // Scale the relative coordinates
        float xNorm = dx / heartScale;
        float yNorm = dy / heartScale;

        // Classic implicit heart shape equation:
        //   (x^2 + y^2 - 1)^3 - x^2 * y^3 < 0
        return Mathf.Pow(xNorm * xNorm + yNorm * yNorm - 1, 3)
               - (xNorm * xNorm * Mathf.Pow(yNorm, 3)) < 0;
    }
}
#endif