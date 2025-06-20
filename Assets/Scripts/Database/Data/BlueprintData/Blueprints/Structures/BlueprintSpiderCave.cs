using UnityEngine;
using System.Collections.Generic;
#if false
[CreateAssetMenu(
        fileName = "Blueprint_SpiderCave",
        menuName  = "Game/World/Blueprint/Structure/Spider Cave")]
public class BlueprintSpiderCave : BlueprintStructure
{
    /* ───────────── tunables ───────────── */

    [Header("Tiles")]
    public TileData webTile;   // required
    public TileData wallTile;  // optional

    [Header("Web Fill")]
    [Range(0f,1f)] public float webFillChance = 1f;
    public int   maxFloodSize = 10_000;

    [Header("Wall Rim")]
    [Min(1)] public int wallThickness = 1;

    /* ───────────── main ───────────── */

    public override void PlaceStructure(World w,int ax,int ay)
    {
        if (!webTile) return;
        if (!w.IsUndergroundAir(w.GetTileID( ax, ay))) return;

        /* 1 ─ flood-fill underground air cavity (4-way) */
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

        /* 2 ─ weave webs */
        foreach (var pos in cavity)
            if (Random.value <= webFillChance)
                w.SetTileID( pos.x,pos.y, webTile.tileID);

        /* 3 ─ optional stone rim */
        if (!wallTile) return;

        var rim = new HashSet<Vector2Int>();
        foreach (var core in cavity)
        {
            for (int dx=-wallThickness; dx<=wallThickness; ++dx)
            for (int dy=-wallThickness; dy<=wallThickness; ++dy)
            {
                if (dx==0 && dy==0) continue;
                Vector2Int r = core + new Vector2Int(dx,dy);
                if (cavity.Contains(r) || rim.Contains(r)) continue;
                if (!w.IsLiquid( w.GetTileID(r.x,r.y)))
                    rim.Add(r);
            }
        }
        foreach (var p in rim)
            w.SetTileID(p.x,p.y, wallTile.tileID);
    }
}
#endif