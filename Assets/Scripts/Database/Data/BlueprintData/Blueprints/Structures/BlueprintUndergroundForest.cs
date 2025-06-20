using UnityEngine;
using System.Collections.Generic;
#if false
[CreateAssetMenu(
        fileName = "Blueprint_HorizontalForestCave",
        menuName  = "Game/World/Blueprint/Structure/Horizontal Forest Cave")]
public class BlueprintHorizontalForestCave : BlueprintStructure
{
    /* ─────────────────────────  SHAPE  ─────────────────────────────── */
    [Header("Cave Shape")]
    [Min(4)] public int caveLength  = 60;
    [Min(1)] public int caveRadius  = 6;

    [Min(0)] public int verticalAmplitude = 4;
    public float noiseScale = 0.05f;

    /* ─────────────────────────  SURFACE  ──────────────────────────── */
    [Header("Surface Tiles")]
    public TileData grassTile;              // optional – falls back to Area.defaultGrass
    public TileData dirtTile;               // optional – falls back to Area.defaultDirt
    [Min(1)] public int dirtDepth = 2;

    /* ─────────────────────────  INTERIOR  ─────────────────────────── */
    [Header("Interior Decoration")]
    public TileData foliageTile;
    [Range(0f,1f)] public float foliageFillChance = 0.4f;

    [Header("Trees (weighted)")]
    public WeightedTree[] treeBlueprints;        // optional – falls back to Area.Trees
    [Range(0f,1f)] public float treeChance = 0.25f;

    /* ─────────────────────────  RIM “WALLS”  ───────────────────────── */
    [Header("Rim Walls (now use dirtTile)")]
    [Min(1)] public int wallThickness = 1;

    /* ─────────────────────────  HELPERS  ──────────────────────────── */
    DropTable<BlueprintTree> BuildTreeTable(WeightedTree[] list)
    {
        if (list == null || list.Length == 0) return null;
        var tbl = new DropTable<BlueprintTree>();
        foreach (var w in list)
            if (w != null && w.prefab != null && w.weight > 0f)
                tbl.Add(w.prefab, w.weight);
        tbl.Build();
        return tbl;
    }

    /* ─────────────────────────  MAIN  ─────────────────────────────── */
    public override void PlaceStructure(World world, int ax, int ay)
    {
        AreaData area = world.GetArea(ax, ay);

        /* 1 → Area defaults win, blueprint values fallback */
        TileData grass = area?.defaultGrass ?? grassTile;
        TileData dirt  = area?.defaultDirt  ?? dirtTile;

        /* 2 → weighted tree table:  Area wins, else blueprint list */
        DropTable<BlueprintTree> treeTable =
              (area?.Trees != null && area.Trees.Length > 0)
                ? BuildTreeTable(area.Trees)
                : BuildTreeTable(treeBlueprints);

        if (grass == null)
        {
            Debug.LogWarning("HorizontalForestCave: no grass tile assigned.");
            return;
        }

        TileData uAir = world.tiles.UndergroundAirTile;
        if (uAir == null)
        {
            Debug.LogWarning("world.tileDatabase.UndergroundAirTile is missing!");
            return;
        }

        /* ───────── cave carving ───────── */
        int halfLen = caveLength / 2;
        var air = new HashSet<Vector2Int>();
        var rng = new System.Random(ax ^ ay);
        float seedX = rng.Next(1, 10_000);

        for (int dx = -halfLen; dx <= halfLen; dx++)
        {
            float t  = (dx + halfLen) / (float)caveLength;
            float nC = Mathf.PerlinNoise(seedX + t * caveLength * noiseScale, 0f);
            float nR = Mathf.PerlinNoise(seedX + 1000 + t * caveLength * noiseScale, 0f);

            int cy  = ay + Mathf.RoundToInt((nC - .5f) * 2f * verticalAmplitude);
            int rad = Mathf.Max(1, Mathf.RoundToInt(caveRadius * Mathf.Lerp(0.6f, 1.3f, nR)));
            int wx  = ax + dx;

            for (int dy = -rad; dy <= rad; dy++)
            {
                int wy = cy + dy;
                if (dx*dx / (float)(halfLen*halfLen) + dy*dy / (float)(rad*rad) <= 1f)
                {
                    world.SetTileID(wx, wy, uAir.tileID, false);
                    air.Add(new Vector2Int(wx, wy));
                }
            }

            /* side pockets */
            if (rng.NextDouble() < 0.05)
            {
                int pR = Mathf.Max(1, rad / 2);
                int oy = cy + rng.Next(-rad, rad + 1);
                int ox = wx + rng.Next(-rad, rad + 1);

                for (int py = -pR; py <= pR; py++)
                for (int px = -pR; px <= pR; px++)
                    if (px*px + py*py <= pR*pR)
                    {
                        int gx = ox + px, gy = oy + py;
                        world.SetTileID(gx, gy, uAir.tileID, false);
                        air.Add(new Vector2Int(gx, gy));
                    }
            }
        }

        /* identify floor */
        var floor = new List<Vector2Int>();
        var minY = new Dictionary<int, int>();
        foreach (var p in air)
            if (!minY.ContainsKey(p.x) || p.y < minY[p.x])
                minY[p.x] = p.y;
        foreach (var kv in minY)
            floor.Add(new Vector2Int(kv.Key, kv.Value));

        /* grass on floor */
        foreach (var pos in floor)
            world.SetTileID(pos.x, pos.y, grass.tileID, false);

        /* dirt under grass */
        if (dirt != null && dirtDepth > 0)
            foreach (var pos in floor)
                for (int d = 1; d <= dirtDepth; d++)
                {
                    var b = new Vector2Int(pos.x, pos.y - d);
                    if (air.Contains(b)) break;
                    world.SetTileID(b.x, b.y, dirt.tileID, false);
                }

        /* foliage */
        if (foliageTile && foliageFillChance > 0f)
            foreach (var pos in floor)
                if (air.Contains(pos + Vector2Int.up) && Random.value <= foliageFillChance)
                    world.SetTileID(pos.x, pos.y + 1, foliageTile.tileID, false);

        /* trees (weighted) */
        if (treeTable != null && treeChance > 0f)
            foreach (var pos in floor)
                if (air.Contains(pos + Vector2Int.up) && Random.value <= treeChance)
                    treeTable.Roll()?.PlaceStructure(world, pos.x, pos.y + 1);

        /* rim “walls” */
        if (dirt != null && wallThickness > 0)
        {
            int downReach = wallThickness + dirtDepth;
            var rim = new HashSet<Vector2Int>();

            foreach (var p in air)
                for (int dx = -wallThickness; dx <= wallThickness; dx++)
                for (int dy = -downReach;     dy <= wallThickness; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = new Vector2Int(p.x + dx, p.y + dy);
                    if (air.Contains(n) || rim.Contains(n)) continue;
                    if (world.GetTileID(n.x, n.y) == dirt.tileID) continue;
                    rim.Add(n);
                }

            foreach (var p in rim)
                world.SetTileID(p.x, p.y, dirt.tileID, false);
        }
    }

    /* —— bounds —— */
    public override BoundsInt GetStructureBounds(int ax, int ay) =>
        new BoundsInt(
            ax - caveLength/2 - wallThickness,
            ay - caveRadius - verticalAmplitude - wallThickness - dirtDepth,
            0,
            caveLength + wallThickness*2,
            caveRadius*2 + verticalAmplitude*2 + wallThickness*2 + dirtDepth,
            1);
}
#endif