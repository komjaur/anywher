using UnityEngine;

public abstract class BlueprintGrowable : Blueprint
{
    [Header("Space requirements")]
    public int FreeHeightRequired = 3;   // tiles upward from anchorY
    public int FreeWidthRequired  = 1;   // tiles centred on anchorX
    public TileData[] SuitableGroundTiles;   
/// <summary>
/// Returns true only when BOTH conditions hold:
///   • The tile directly below anchorY is in SuitableGroundTiles
///     (or the list is empty, meaning “any solid block”).
///   • Every tile inside the clearance box is empty / air / tree.
/// </summary>
public bool HasFreeSpace(World world, int anchorX, int anchorY)
{
    /* ── 1 ▸ Ground-tile test ─────────────────────────────────────── */
    int groundY = anchorY - 1;
    if (groundY < 0) return false;                       // beneath world

    int groundID = world.GetTileID(anchorX, groundY);
    if (groundID == 1) return false;                     // can’t grow in mid-air

    // If a whitelist exists, groundID must match one of its entries.
    if (SuitableGroundTiles != null && SuitableGroundTiles.Length > 0)
    {
        bool ok = false;
        foreach (var td in SuitableGroundTiles)
            if (td != null && td.tileID == groundID) { ok = true; break; }
        if (!ok) return false;
    }

    /* ── 2 ▸ Clearance-box test ───────────────────────────────────── */
    int halfW = Mathf.FloorToInt(FreeWidthRequired * 0.5f);

    for (int x = anchorX - halfW; x <= anchorX + halfW; x++)
    for (int y = anchorY;        y <  anchorY + FreeHeightRequired; y++)
    {
        int id = world.GetTileID(x, y);
        if (id == 0) continue;                           // empty

        TileData td = world.tiles.GetTileDataByID(id);
        if (td == null)        return false;             // unknown tile → block
        if (td.behavior == BlockBehavior.Air) continue;  // sky/underground air
         if (td.tag      == BlockTag.Tree)      return false; // ← NEW: block


        return false;                                    // blocked by solid/liquid/etc.
    }

    return true;
}


    public abstract override void PlaceStructure(World world, int anchorX, int anchorY);
}
