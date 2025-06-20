/* ──────────────────────────────────────────────────────────────────────────
   BlueprintTree – trunk + side leaves + distinct crown tile
   ---------------------------------------------------------------------- */
using UnityEngine;

[CreateAssetMenu(
        fileName = "Blueprint_Tree_",
        menuName  = "Game/World/Blueprint/Growable/Tree Blueprint")]
public class BlueprintTree : BlueprintGrowable
{
    /* ────────────────  Inspector fields  ──────────────── */
    [Header("Tiles")]
    [Tooltip("Bark segment (usually a RuleTile).")]
    public TileData trunkTile;        // bark

    [Tooltip("Single-leaf tile used for left / right branches.")]
    public TileData leafTile;         // branch leaves

    [Tooltip("Tile placed at the very top of the tree (crown).")]
    public TileData crownTile;        // crown sprite

    [Header("Height range (inclusive)")]
    [Min(4)] public int trunkHeightMin = 4;
    [Min(6)] public int trunkHeightMax = 6;

    [Header("Branch probability")]
    [Range(0f, 1f)] public float branchChance = 0.35f;

    /* ─────────────────────────  MAIN  ───────────────────────── */
    public override void PlaceStructure(World world, int x, int y)
    {
        if (!HasFreeSpace(world, x, y) ||
            trunkTile == null || leafTile == null || crownTile == null)
            return;

        int height = Random.Range(trunkHeightMin, trunkHeightMax + 1);

        /* 1 ▸ trunk & optional side leaves */
        for (int i = 0; i < height; i++)
        {
            int ty = y + i;
            world.SetTileID(x, ty, trunkTile.tileID);

            bool canBranch = i > 1 && i < height - 1;          // skip the base & near-top
            if (canBranch && Random.value < branchChance)
            {
                world.SetTileID(x - 1, ty, leafTile.tileID);   // left leaf
                world.SetTileID(x + 1, ty, leafTile.tileID);   // right leaf
            }
        }

        /* 2 ▸ crown */
        int crownY = y + height;
        world.SetTileID(x, crownY, crownTile.tileID);
    }
}
