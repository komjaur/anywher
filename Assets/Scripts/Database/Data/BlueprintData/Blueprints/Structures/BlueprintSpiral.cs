using UnityEngine;
using System.Collections.Generic;
#if false
[CreateAssetMenu(
    fileName = "Blueprint_Spiral",
    menuName  = "Game/World/Blueprint/Structure/Spiral Cave")]
public class BlueprintSpiral : BlueprintStructure
{
    /* ───────── Spiral Shape ───────── */
    [Header("Spiral Shape")]
    [Min(2)] public int   revolutions   = 3;
    [Min(4)] public int   spiralRadius  = 20;
    [Min(1)] public int   pathThickness = 3;
    public float noiseScale     = 0.1f;
    public float verticalJitter = 4f;

    /* ───────── Entrance ───────── */
    [Header("Entrance Settings")]
    [Range(0f, 1f)] public float entranceAirFraction = 0.12f;
    public TileData airTile;        // under-ground air
    public TileData chestTile;      // chest at entrance

    /* ───────── Tier Blocks & Ore ───────── */
    [System.Serializable] public class TierData
    {
        public TileData block;
        public TileData ore;
        [Range(0f,1f)] public float oreChance = 0.1f;
    }
    [Header("Tier 1 (outer third)")]  public TierData tier1;
    [Header("Tier 2 (middle third)")] public TierData tier2;
    [Header("Tier 3 (inner third)")]  public TierData tier3;

    /* ───────── Water Pockets ───────── */
    [Header("Water Pockets")]
    public TileData waterTile;
    [Range(0f,1f)] public float waterChance = 0.03f;  // roll per tunnel cell
    [Min(1)] public int waterPocketRadius = 2;        // circular pocket size

    /* ───────── Walls / Background ───────── */
    [Header("Walls (optional)")]
    public TileData wallTile;
    [Min(0)] public int wallThickness = 1;

    /* ───────── End Opening ───────── */
    [Header("End Opening")]  [Min(2)] public int endOpenRadius = 6;

    /* ───────────────── MAIN ───────────────── */
    public override void PlaceStructure(World world, int ax, int ay)
    {
        if (!airTile || !tier1.block || !tier2.block || !tier3.block)
        {
            Debug.LogWarning("BlueprintSpiral: missing essential tiles.");
            return;
        }

        var rng    = new System.Random(ax ^ ay);
        float seed = rng.Next(1_000);

        var carved = new HashSet<Vector2Int>();  // all tunnel cells

        int steps = revolutions * 360;
        int entranceX = ax, entranceY = ay;
        int endX = ax, endY = ay;
        bool firstPt = true;

        /* 1 ── carve spiral and sprinkle ores + water ─────────────── */
        for (int angle = 0; angle < steps; angle++)
        {
            float t      = angle / (float)steps;         // 0‥1
            float theta  = Mathf.Deg2Rad * angle;
            float radius = Mathf.Lerp(3f, spiralRadius, t);

            int cx = ax + Mathf.RoundToInt(Mathf.Cos(theta) * radius);
            int cy = ay + Mathf.RoundToInt(Mathf.Sin(theta) * radius);

            cy += Mathf.RoundToInt(
                    (Mathf.PerlinNoise(seed + cx * noiseScale, seed + cy * noiseScale) - .5f)
                    * 2f * verticalJitter);

            if (firstPt) { entranceX = cx; entranceY = cy; firstPt = false; }
            endX = cx; endY = cy;

            bool isEntranceAir = t < entranceAirFraction;
            TierData tier      = t < .333f ? tier1 : t < .666f ? tier2 : tier3;

            for (int dx = -pathThickness; dx <= pathThickness; dx++)
            for (int dy = -pathThickness; dy <= pathThickness; dy++)
            {
                if (dx*dx + dy*dy > pathThickness*pathThickness) continue;

                int gx = cx + dx, gy = cy + dy;
                var pos = new Vector2Int(gx, gy);
                carved.Add(pos);

                TileData tile = isEntranceAir ? airTile : tier.block;
                if (!isEntranceAir && tier.ore && Random.value < tier.oreChance)
                    tile = tier.ore;

                world.SetTileID(gx, gy, tile.tileID, false);

                /* ── maybe spawn a water pocket here ── */
                if (!isEntranceAir && waterTile && Random.value < waterChance)
                    CarveWaterPocket(world, gx, gy, carved);
            }
        }

        /* 2 ── chest at entrance ─────────────────────────────────── */
        if (chestTile)
            world.SetTileID(entranceX, entranceY, chestTile.tileID, false);

        /* 3 ── hollow opening at end ─────────────────────────────── */
        var endAir = new HashSet<Vector2Int>();
        for (int dx = -endOpenRadius; dx <= endOpenRadius; dx++)
        for (int dy = -endOpenRadius; dy <= endOpenRadius; dy++)
        {
            if (dx*dx + dy*dy > endOpenRadius*endOpenRadius) continue;
            int gx = endX + dx, gy = endY + dy;
            world.SetTileID(gx, gy, airTile.tileID, false);
            endAir.Add(new Vector2Int(gx, gy));
        }
        carved.ExceptWith(endAir);

        /* 4 ── background rim walls ──────────────────────────────── */
        if (wallTile && wallThickness > 0)
        {
            var rims = new HashSet<Vector2Int>();
            foreach (var p in carved)
                for (int dx = -wallThickness; dx <= wallThickness; dx++)
                for (int dy = -wallThickness; dy <= wallThickness; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var q = new Vector2Int(p.x + dx, p.y + dy);
                    if (carved.Contains(q) || endAir.Contains(q) || rims.Contains(q)) continue;
                    rims.Add(q);
                }
            foreach (var p in rims)
                world.SetTileID(p.x, p.y, wallTile.tileID, false);
        }
    }

    /* carve circular water pocket centred on (cx,cy) */
    void CarveWaterPocket(World world, int cx, int cy, HashSet<Vector2Int> carved)
    {
        int r2 = waterPocketRadius * waterPocketRadius;
        for (int dx = -waterPocketRadius; dx <= waterPocketRadius; dx++)
        for (int dy = -waterPocketRadius; dy <= waterPocketRadius; dy++)
        {
            if (dx*dx + dy*dy > r2) continue;
            int gx = cx + dx, gy = cy + dy;
            world.SetTileID(gx, gy, waterTile.tileID, false);
            carved.Add(new Vector2Int(gx, gy));
        }
    }

    /* ───────── bounds ───────── */
    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int size = spiralRadius + pathThickness + wallThickness +
                   Mathf.Max(endOpenRadius, waterPocketRadius, Mathf.CeilToInt(verticalJitter));
        return new BoundsInt(ax - size, ay - size, 0, size*2+1, size*2+1, 1);
    }
}
#endif