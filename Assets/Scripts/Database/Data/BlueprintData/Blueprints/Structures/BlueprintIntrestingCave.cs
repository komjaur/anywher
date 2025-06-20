using UnityEngine;
using System.Collections.Generic;
using System.Linq;   // ← needed for ElementAt()
#if false
[CreateAssetMenu(
        fileName = "Blueprint_IntrestingCave",
        menuName  = "Game/World/Blueprint/Structure/Intresting Cave")]
public class BlueprintIntrestingCave : BlueprintStructure
{
    /* ───────────── tunables ───────────── */

    [Header("Tiles")]
    public TileData grassTile;      // required – floor grass
    public TileData vineTile;       // optional – hanging vines
    public TileData wallTile;       // optional – mossy / stone rim

    [Header("Grass & Vines")]
    [Range(0f,1f)] public float grassGrowChance = .8f;
    [Range(0f,1f)] public float vineHangChance  = .25f;
    [Min(1)]       public int   vineMaxLength   = 8;

    [Header("Flood-fill")]
    public int maxFloodSize = 10_000;

    [Header("Wall Rim")]
    [Min(1)] public int wallThickness = 1;

    /* NEW ─ extra cavern carving */
    [Header("Extra Cavern Carving")]
    [Min(0)] public int  branchCount      = 3;   // how many branches to dig
    [Min(1)] public int  branchLength     = 35;  // steps per branch
    [Min(1)] public int  branchRadius     = 2;   // half-width of each dig “blob”
    [Range(0f,1f)] public float turnChance = .30f; // chance to change direction each step

    /* ───────────── main ───────────── */

    public override void PlaceStructure(World w,int ax,int ay)
    {
        if (!grassTile) return;
        if (!w.IsUndergroundAir(w.GetTileID(ax,ay))) return;

        /* ---------- 1. flood-fill initial cavity ---------- */
        var cavity   = new HashSet<Vector2Int>();
        var frontier = new Queue<Vector2Int>();

        Vector2Int start = new(ax, ay);
        cavity.Add(start);
        frontier.Enqueue(start);

        Vector2Int[] dirs =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (frontier.Count>0 && cavity.Count<maxFloodSize)
        {
            Vector2Int p = frontier.Dequeue();
            foreach (var d in dirs)
            {
                Vector2Int n = p + d;
                if (cavity.Contains(n)) continue;
                if (w.IsUndergroundAir(w.GetTileID(n.x,n.y)))
                {
                    cavity.Add(n);
                    frontier.Enqueue(n);
                    if (cavity.Count >= maxFloodSize) break;
                }
            }
        }

        /* ---------- 2. carve extra branches ---------- */
        if (branchCount > 0 && cavity.Count > 0)
        {
            int airID = w.GetTileID(ax, ay);   // whatever ID the engine uses for air

            for (int b = 0; b < branchCount; ++b)
            {
                Vector2Int cur = cavity.ElementAt(Random.Range(0, cavity.Count));
                Vector2Int dir = dirs[Random.Range(0, dirs.Length)];

                for (int step = 0; step < branchLength; ++step)
                {
                    /* optional random turn */
                    if (Random.value < turnChance)
                        dir = dirs[Random.Range(0, dirs.Length)];

                    cur += dir;

                    /* dig a little blob around the current point */
                    for (int dx = -branchRadius; dx <= branchRadius; ++dx)
                    for (int dy = -branchRadius; dy <= branchRadius; ++dy)
                    {
                        Vector2Int p = cur + new Vector2Int(dx, dy);
                        if (w.IsLiquid(w.GetTileID(p.x, p.y))) continue; // don’t open into water
                        w.SetTileID(p.x, p.y, airID);
                        cavity.Add(p);
                    }
                }
            }
        }

        /* helper: treat anything that is neither air nor liquid as solid */
        bool IsSolid(int id) => !w.IsUndergroundAir(id) && !w.IsLiquid(id);

        /* ---------- 3. grow grass on floor ---------- */
        foreach (var pos in cavity)
        {
            int belowID = w.GetTileID(pos.x, pos.y - 1);
            if (IsSolid(belowID) && Random.value <= grassGrowChance)
                w.SetTileID(pos.x, pos.y - 1, grassTile.tileID);
        }

        /* ---------- 4. hang vines from ceiling ---------- */
        if (vineTile)
        {
            foreach (var pos in cavity)
            {
                int aboveID = w.GetTileID(pos.x, pos.y + 1);
                if (IsSolid(aboveID) && Random.value <= vineHangChance)
                {
                    for (int len = 1; len <= vineMaxLength; ++len)
                    {
                        int ty = pos.y - len;   // vines grow downward
                        if (!w.IsUndergroundAir(w.GetTileID(pos.x, ty))) break;
                        w.SetTileID(pos.x, ty, vineTile.tileID);
                    }
                }
            }
        }

        /* ---------- 5. optional thick stone / moss rim ---------- */
        if (wallTile)
        {
            var rim = new HashSet<Vector2Int>();
            foreach (var core in cavity)
            {
                for (int dx = -wallThickness; dx <= wallThickness; ++dx)
                for (int dy = -wallThickness; dy <= wallThickness; ++dy)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int r = core + new Vector2Int(dx, dy);
                    if (cavity.Contains(r) || rim.Contains(r)) continue;
                    if (!w.IsLiquid(w.GetTileID(r.x, r.y)))
                        rim.Add(r);
                }
            }
            foreach (var p in rim)
                w.SetTileID(p.x, p.y, wallTile.tileID);
        }
    }
}
#endif