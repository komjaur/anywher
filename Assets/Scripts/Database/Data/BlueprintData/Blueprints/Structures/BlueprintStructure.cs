using UnityEngine;

public abstract class BlueprintStructure : Blueprint
{
    [Header("Placement Constraints")]
    [Tooltip("If true, this structure requires all its cells be within the world bounds. " +
             "If false, partial out-of-bounds placement is allowed.")]
    public bool requiresFullInBounds = true;
    public bool canBePlacedInAir = false;
    [Tooltip("How wide (in tiles) the structure is. For certain shapes, may be approximate.")]
    public int structureWidth = 1;

    [Tooltip("How tall (in tiles) the structure is. For certain shapes, may be approximate.")]
    public int structureHeight = 1;

    [Tooltip("If true, treat (anchorX, anchorY) as the center of the structure. " +
             "If false, treat it as the bottom of the structure (or top, or corner—your choice).")]
    public bool anchorIsCenter = true;
    
    // ------------------------------------------------------------------------
    // The base bounding box is a simple rectangle:
    // If anchorIsCenter => bounding box is centered at (anchorX, anchorY).
    // Else => bounding box extends from anchor to anchor + (width, height).
    // Child classes can override for custom shapes (egg, circle, irregular, etc.).
    // ------------------------------------------------------------------------
    public virtual BoundsInt GetStructureBounds(int anchorX, int anchorY)
    {
        // For 2D, the Z dimension is unused: set sizeZ=1 and position.z=0
        if (structureWidth < 1 || structureHeight < 1)
        {
            // Degenerate size => just a single tile at anchor
            return new BoundsInt(anchorX, anchorY, 0, 1, 1, 1);
        }

        if (anchorIsCenter)
        {
            // If anchored at center => bounding box is centered around (anchorX, anchorY).
            // For an even dimension, you can round or floor. Here we floor to get an integer box.
            int halfW = structureWidth / 2;
            int halfH = structureHeight / 2;

            int minX = anchorX - halfW;
            int minY = anchorY - halfH;
            return new BoundsInt(
                minX,          // x
                minY,          // y
                0,             // z
                structureWidth, 
                structureHeight, 
                1
            );
        }
        else
        {
            // If not anchored at center => interpret anchor as the "bottom" of the structure.
            // That means the bounding box goes from (anchorX, anchorY)
            // up to (anchorX+width-1, anchorY+height-1).
            return new BoundsInt(
                anchorX,
                anchorY,
                0,
                structureWidth,
                structureHeight,
                1
            );
        }
    }

    // ------------------------------------------------------------------------
    // Checks if the structure can be placed in bounds.
    // If requiresFullInBounds = false => always OK.
    // Else we do a bounding box check to ensure entire structure is in the world.
    // ------------------------------------------------------------------------
    public virtual bool CanPlaceStructure(World world, int anchorX, int anchorY)
    {
        // If we do not require full in-bounds => no check needed
        if (!requiresFullInBounds)
            return true;

        // Otherwise, we do a bounding box check
        BoundsInt bounds = GetStructureBounds(anchorX, anchorY);

        // Convert the bounding box corners to max/min (since BoundsInt is (position + size))
        int minX = bounds.xMin;
        int minY = bounds.yMin;
        int maxX = bounds.xMax - 1;
        int maxY = bounds.yMax - 1;

        // Grab the world’s max valid indexes
        int worldMaxX = world.widthInChunks * world.chunkSize - 1;
        int worldMaxY = world.heightInChunks * world.chunkSize - 1;

        // If the bounding box extends out of the valid range => can't place
        if (minX < 0 || minY < 0 || maxX > worldMaxX || maxY > worldMaxY)
        {
            return false;
        }

        return true;
    }

    // Each child blueprint must implement the actual tile placement
    public override abstract void PlaceStructure(World world, int anchorX, int anchorY);
}
