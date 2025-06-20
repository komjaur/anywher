using UnityEngine;
using System.Collections.Generic;
#if false
/// Compact snake-style labyrinth using a DFS “perfect maze”.
[CreateAssetMenu(
    fileName = "Blueprint_SnakeLabyrinth_Compact",
    menuName  = "Game/World/Blueprint/Structure/Snake Labyrinth Compact")]
public class BlueprintSnakeLabyrinth : BlueprintStructure
{
    /* ── Minimal Settings ── */
    [Header("Shape")]
    public int mazeRadius     = 80; // overall footprint (tiles)
    public int maxCells       = 400;
    public int cellStep       = 4;  // grid spacing
    public int tunnelRadius   = 3;  // corridor thickness

    [Header("Walls / Tiles")]
    public int wallThickness  = 2;
    public TileData floorTile;
    public TileData wallTile;
    public TileData chestTile;

    /* ── Internals ── */
    static readonly Vector2Int[] DIR4 =
    {
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int( 0, 1),
        new Vector2Int( 0,-1)
    };

    // Carve filled disc
    void CarveCircle(World w, Vector2 pos, int r, HashSet<Vector2Int> floor)
    {
        int r2 = r * r;
        int minX = Mathf.FloorToInt(pos.x - r),
            maxX = Mathf.FloorToInt(pos.x + r);
        int minY = Mathf.FloorToInt(pos.y - r),
            maxY = Mathf.FloorToInt(pos.y + r);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float dx = x - pos.x, dy = y - pos.y;
            if (dx*dx + dy*dy > r2) continue;
            w.SetTileID(x, y, floorTile.tileID, false);
            floor.Add(new Vector2Int(x, y));
        }
    }

    // Corridor via lerped circles
    void CarveCorridor(World w, Vector2Int a, Vector2Int b,
                       HashSet<Vector2Int> floor)
    {
        int steps = Mathf.CeilToInt(Vector2Int.Distance(a, b));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            p += Random.insideUnitCircle * 0.3f;      // organic jitter
            CarveCircle(w, p, tunnelRadius, floor);
        }
    }

    void BuildWalls(World w, HashSet<Vector2Int> floor)
    {
        var rim = new HashSet<Vector2Int>();
        foreach (var p in floor)
            for (int dy = -wallThickness; dy <= wallThickness; dy++)
            for (int dx = -wallThickness; dx <= wallThickness; dx++)
            {
                int dsq = dx*dx + dy*dy;
                if (dsq > wallThickness*wallThickness) continue;
                var q = new Vector2Int(p.x+dx, p.y+dy);
                if (floor.Contains(q) || rim.Contains(q)) continue;
                rim.Add(q);
            }
        foreach (var v in rim)
            w.SetTileID(v.x, v.y, wallTile.tileID, false);
    }

    Vector2Int Furthest(HashSet<Vector2Int> floor, Vector2Int start)
    {
        var q = new Queue<Vector2Int>();
        var dist = new Dictionary<Vector2Int,int>{{start,0}};
        q.Enqueue(start); Vector2Int best = start;

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in DIR4)
            {
                var n = p + d;
                if (!floor.Contains(n) || dist.ContainsKey(n)) continue;
                dist[n] = dist[p] + 1;
                best = dist[n] > dist[best] ? n : best;
                q.Enqueue(n);
            }
        }
        return best;
    }

    /* ── Entry Point ── */
    public override void PlaceStructure(World w, int ax, int ay)
    {
        if (!floorTile || !wallTile || !chestTile) { Debug.LogWarning("Tiles missing."); return; }

        var mazeFloor = new HashSet<Vector2Int>();
        var visited   = new HashSet<Vector2Int>();
        var stack     = new Stack<Vector2Int>();

        Vector2Int startCell = new Vector2Int(ax, ay);
        stack.Push(startCell); visited.Add(startCell);

        // DFS perfect maze
        while (stack.Count > 0 && visited.Count < maxCells)
        {
            var cur = stack.Peek();
            var opts = new List<Vector2Int>();
            foreach (var d in DIR4)
            {
                var n = cur + d * cellStep;
                if (!visited.Contains(n) && Vector2Int.Distance(n, startCell) <= mazeRadius)
                    opts.Add(n);
            }
            if (opts.Count == 0) { stack.Pop(); continue; }

            var next = opts[Random.Range(0, opts.Count)];
            CarveCorridor(w, cur, next, mazeFloor);
            stack.Push(next); visited.Add(next);
        }

        BuildWalls(w, mazeFloor);

        // Chest at farthest dead-end
        Vector2Int chestPos = Furthest(mazeFloor, startCell);
        w.SetTileID(chestPos.x, chestPos.y, chestTile.tileID, false);
    }

    /* ── Bounds (square) ── */
    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int size = mazeRadius + wallThickness + 2;
        return new BoundsInt(ax-size, ay-size, 0, size*2+1, size*2+1, 1);
    }
}
#endif