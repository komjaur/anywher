using UnityEngine;
using System.Collections.Generic;
#if false
[CreateAssetMenu(
    fileName = "Blueprint_AntColony",
    menuName  = "Game/World/Blueprint/Structure/Ant Colony (Branch-2)")]
public class BlueprintAntColony : BlueprintStructure
{
    /* ───────── Easy Controls ───────── */
    [Header("Layout")]
    public int  colonyRadius  = 80;     // colony footprint
    public int  roomCount     = 15;     // hard cap (incl. queen)
    [Min(3)] public int  roomRadius    = 9;
    [Min(2)] public int  tunnelRadius  = 4;
    [Range(0f,1f)] public float curviness = 0.4f;
    public int  clearMargin   = 2;

    [Header("Branching")]
    [Range(0f,1f)] public float childSpawnChance = 0.7f; // chance per child slot

    [Header("Walls / Tiles")]
    [Min(0)] public int  wallThickness = 1;
    public TileData airTile;
    public TileData wallTile;

    /* ───────── MAIN ───────── */
    public override void PlaceStructure(World world, int ax, int ay)
    {
        if (!airTile) { Debug.LogWarning("AntColony: airTile missing."); return; }

        var rng    = new System.Random(ax ^ ay);
        var carved = new HashSet<Vector2Int>();
        var rooms  = new List<Vector2Int>();          // all centres
        var queue  = new Queue<Vector2Int>();         // rooms to expand

        /* 1 ─ Queen chamber */
        var queen = new Vector2Int(ax, ay);
        CarveCircle(world, queen, roomRadius + 3, carved);
        rooms.Add(queen);
        queue.Enqueue(queen);

        /* 2 ─ Grow recursively */
        while (queue.Count > 0 && rooms.Count < roomCount)
        {
            var parent = queue.Dequeue();

            for (int slot = 0; slot < 2 && rooms.Count < roomCount; slot++)
            {
                if (rng.NextDouble() > childSpawnChance) continue;   // skip this child slot

                // pick direction & distance
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                int   dist= rng.Next(colonyRadius / 4, colonyRadius);

                var cand = parent + new Vector2Int(
                               Mathf.RoundToInt(Mathf.Cos(ang) * dist),
                               Mathf.RoundToInt(Mathf.Sin(ang) * dist));

                // clearance check
                if (!AreaIsClear(cand, roomRadius + clearMargin, carved)) { slot--; continue; }

                // tunnel & carve room
                CarveTunnel(world, parent, cand, carved, rng);
                CarveCircle(world, cand, roomRadius, carved);

                rooms.Add(cand);
                queue.Enqueue(cand);
            }
        }

        /* 3 ─ Rim walls */
        if (wallTile && wallThickness > 0) PaintRim(world, carved);
    }

    /* ───────── carving helpers ───────── */
    bool AreaIsClear(Vector2Int c, int radius, HashSet<Vector2Int> carved)
    {
        int r2 = radius * radius;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
            if (dx*dx + dy*dy <= r2 &&
                carved.Contains(new Vector2Int(c.x + dx, c.y + dy)))
                return false;
        return true;
    }

    void CarveCircle(World w, Vector2Int c, int r, HashSet<Vector2Int> carved)
    {
        int r2 = r * r;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (dx*dx + dy*dy > r2) continue;
            int gx = c.x + dx, gy = c.y + dy;
            w.SetTileID(gx, gy, airTile.tileID, false);
            carved.Add(new Vector2Int(gx, gy));
        }
    }

    void CarveTunnel(World w, Vector2Int from, Vector2Int to,
                     HashSet<Vector2Int> carved, System.Random rng)
    {
        var pos = from; int guard = 0;
        while (pos != to && guard++ < 10_000)
        {
            CarveCircle(w, pos, tunnelRadius, carved);

            Vector2Int dir = new Vector2Int(
                 Mathf.Clamp(to.x - pos.x, -1, 1),
                 Mathf.Clamp(to.y - pos.y, -1, 1));

            if (rng.NextDouble() < curviness)
                dir = rng.NextDouble() < 0.5 ? new Vector2Int(-dir.y, dir.x)
                                             : new Vector2Int(dir.y, -dir.x);

            Vector2Int next = pos + dir;
            if (!StepIsClear(next, carved))
            {
                Vector2Int alt1 = new Vector2Int(-dir.y,  dir.x);
                Vector2Int alt2 = new Vector2Int( dir.y, -dir.x);
                if      (StepIsClear(pos + alt1, carved)) dir = alt1;
                else if (StepIsClear(pos + alt2, carved)) dir = alt2;
            }
            pos += dir;
        }
    }

    bool StepIsClear(Vector2Int p, HashSet<Vector2Int> carved)
    {
        int checkR = tunnelRadius + clearMargin;
        for (int dx = -checkR; dx <= checkR; dx++)
        for (int dy = -checkR; dy <= checkR; dy++)
            if (carved.Contains(new Vector2Int(p.x + dx, p.y + dy)))
                return false;
        return true;
    }

    void PaintRim(World w, HashSet<Vector2Int> carved)
    {
        var rim = new HashSet<Vector2Int>();
        foreach (var p in carved)
            for (int dx = -wallThickness; dx <= wallThickness; dx++)
            for (int dy = -wallThickness; dy <= wallThickness; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var q = new Vector2Int(p.x + dx, p.y + dy);
                if (carved.Contains(q) || rim.Contains(q)) continue;
                rim.Add(q);
            }
        foreach (var v in rim)
            w.SetTileID(v.x, v.y, wallTile.tileID, false);
    }

    /* ───────── bounds ───────── */
    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int size = colonyRadius + wallThickness + 4;
        return new BoundsInt(ax - size, ay - size, 0, size*2 + 1, size*2 + 1, 1);
    }
}
#endif