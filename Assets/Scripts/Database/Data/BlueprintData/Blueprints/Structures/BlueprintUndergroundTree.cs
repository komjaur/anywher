using UnityEngine;
#if false
/// <summary>
/// Massive underground tree with configurable taper, roots, branches and
/// multi-layer canopy.  No extra tiles are required – still uses the same
/// trunk / leaf / root TileData references.
/// </summary>
[CreateAssetMenu(fileName = "Blueprint_UndergroundTree",
                 menuName = "Game/World/Blueprint/Structure/Massive Underground Tree")]
public class BlueprintUndergroundTree : BlueprintStructure
{
    /* ───────────────────────── Inspector ───────────────────────── */

    [Header("Basic Dimensions")]
    [Min(3)] public int trunkHeight = 24;
    [Min(1)] public int trunkRadius = 2;
    [Min(2)] public int rootDepth   = 10;

    [Header("Tiles")]
    public TileData trunkTile;
    public TileData leafTile;
    public TileData rootTile;

    [Header("Taper & Branching")]
    [Range(0f, 0.8f)] public float taperPercent = 0.3f;
    [Min(2)] public int branchStep = 6;
    public Vector2Int branchLenMinMax = new(3, 5);

    [Header("Roots")]
    [Range(0f, 1f)] public float sideRootChance = 0.4f;

    [Header("Canopy")]
    [Min(3)] public int canopyRadius = 7;
    [Range(2, 5)] public int canopyLayers = 3;
    [Min(1)] public int canopyFalloff = 2;
    [Range(0f, 1f)] public float tendrilChance = 0.15f;

    [Header("Misc")]
    public bool isHollow = false;

    /* ───────────────────────── Entry ──────────────────────────── */

    public override void PlaceStructure(World w, int ax, int ay)
    {
        if (!ValidateTiles()) return;

        PlaceRoots(w, ax, ay);
        PlaceTaperedTrunk(w, ax, ay);
        PlaceCanopy(w, ax, ay + trunkHeight);
    }

    bool ValidateTiles()
    {
        if (trunkTile && rootTile && leafTile) return true;
        Debug.LogWarning("UndergroundTree blueprint missing tile references.");
        return false;
    }

    /* ───────────────────────── Roots ──────────────────────────── */

    void PlaceRoots(World w, int cx, int cy)
    {
        // main tap-root
        for (int y = 1; y <= rootDepth; y++)
            w.SetTileID(cx, cy - y, rootTile.tileID);

        // ring of diagonal side roots
        for (int depth = 1; depth <= rootDepth; depth++)
        {
            if (Random.value > sideRootChance) continue;

            int dirX = Random.value < 0.5f ? -1 : 1;
            int dirY = Random.value < 0.3f ? -1 : 0; // mostly horizontal
            int len  = Random.Range(2, trunkRadius * 3);

            int sx = cx; int sy = cy - depth;
            for (int i = 1; i <= len; i++)
            {
                sx += dirX; sy += dirY;
                w.SetTileID(sx, sy, rootTile.tileID);
            }
        }
    }

    /* ───────────────────────── Trunk ──────────────────────────── */

    void PlaceTaperedTrunk(World w, int cx, int cy)
    {
        for (int y = 1; y <= trunkHeight; y++)
        {
            float t = 1f - (y / (float)trunkHeight) * taperPercent;
            int r = Mathf.Max(1, Mathf.RoundToInt(trunkRadius * t));

            for (int dx = -r; dx <= r; dx++)
            {
                bool shell = Mathf.Abs(dx) == r;
                if (isHollow && !shell) continue;
                w.SetTileID(cx + dx, cy + y, trunkTile.tileID);
            }

            // branches every branchStep blocks
            if (y % branchStep == 0 && y < trunkHeight - 2)
                SpawnBranchRing(w, cx, cy + y, r);
        }
    }

    void SpawnBranchRing(World w, int bx, int by, int r)
    {
        int lenMin = branchLenMinMax.x;
        int lenMax = branchLenMinMax.y;

        SpawnBranch(w, bx + r, by, Vector2Int.right,  lenMin, lenMax);
        SpawnBranch(w, bx - r, by, Vector2Int.left,   lenMin, lenMax);
        SpawnBranch(w, bx,     by, Vector2Int.right + Vector2Int.up, lenMin, lenMax);
        SpawnBranch(w, bx,     by, Vector2Int.left  + Vector2Int.up, lenMin, lenMax);
    }

    void SpawnBranch(World w, int sx, int sy, Vector2Int dir, int lenMin, int lenMax)
    {
        int len = Random.Range(lenMin, lenMax + 1);
        int x = sx, y = sy;

        for (int i = 1; i <= len; i++)
        {
            x += dir.x;
            y += (i % 2 == 0) ? dir.y : 0; // slight upward curve
            w.SetTileID(x, y, trunkTile.tileID);

            // leaf tuft at the tip
            if (i == len)
                w.SetTileID(x, y + 1, leafTile.tileID);
        }
    }

    /* ───────────────────────── Canopy ─────────────────────────── */

    void PlaceCanopy(World w, int cx, int startY)
    {
        for (int layer = 0; layer < canopyLayers; layer++)
        {
            int r = canopyRadius - layer * canopyFalloff;
            int cy = startY + layer; // slight upward stack

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    // rough sphere + jitter
                    float noise = Random.Range(-0.4f, 0.4f);
                    if (dx * dx + dy * dy <= (r + noise) * (r + noise))
                    {
                        int wx = cx + dx;
                        int wy = cy + dy;
                        w.SetTileID(wx, wy, leafTile.tileID);

                        // hanging tendril
                        if (Random.value < tendrilChance && dy < 0)
                            SpawnTendril(w, wx, wy - 1);
                    }
                }
            }
        }
    }

    void SpawnTendril(World w, int sx, int sy)
    {
        int len = Random.Range(2, 5);
        for (int i = 0; i < len; i++)
        {
            int y = sy - i;
            if (w.GetTileID(sx, y) != 0) break;
            w.SetTileID(sx, y, leafTile.tileID);
        }
    }

    /* ───────────────────────── Bounds ─────────────────────────── */

    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int r = Mathf.Max(trunkRadius, canopyRadius);
        int halfW = r + 2;
        int h = trunkHeight + canopyRadius + 2;
        int d = rootDepth + 2;
        return new BoundsInt(ax - halfW, ay - d, 0,
                             halfW * 2 + 1, h + d, 1);
    }
}
#endif