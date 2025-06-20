using UnityEngine;
using System.Collections.Generic;
#if false
[CreateAssetMenu(
        fileName = "Blueprint_BeeHive",
        menuName  = "Game/World/Blueprint/Structure/Bee Honeycomb Hive")]
public class BlueprintBeeHive : BlueprintStructure
{
    /* ─────────────────────────  DIMENSIONS  ───────────────────────── */
    [Header("Hive Footprint (rough)")]
    [Min(12)] public int sizeX = 34;
    [Min(10)] public int sizeY = 26;

    [Tooltip("0 = perfect hex / teardrop, 1 = noisy / irregular")]
    [Range(0f,1f)] public float contourNoise = 0.4f;

    /* ─────────────────────────  TILES  ───────────────────────────── */
    [Header("Tiles")]
    public TileData hiveBlockTile;     // solid shell (front)
    public TileData honeycombWallTile; // background wall
    public TileData honeyLiquidTile;   // honey (front)
    public TileData airTileOverride;   // optional – else UndergroundAir

    /* ───────────────────  HONEY-POOL SETTINGS  ───────────────────── */
    [Header("Honey Pools")]
    [Range(0f,1f)] public float poolFillPercent = 0.30f;

    /* ---------------------------------------------------------------- */
    public override void PlaceStructure(World world,int ax,int ay)
    {
        /* safety ----------------------------------------------------- */
        if (!hiveBlockTile || !honeycombWallTile || !honeyLiquidTile)
        {  Debug.LogWarning("[BeeHive] Assign all mandatory TileData!"); return; }

        TileData airTile = airTileOverride ?? world.tiles.UndergroundAirTile;
        if (!airTile) { Debug.LogWarning("[BeeHive] UndergroundAirTile missing!"); return; }

        /* pre-compute ------------------------------------------------ */
        int halfW = sizeX/2, halfH = sizeY/2;
        int minX = ax-halfW, maxX = ax+halfW;
        int minY = ay-halfH, maxY = ay+halfH;

        float rx = halfW, ry = halfH;                     // base radii
        float noiseSeed = Random.Range(0f,10_000f);

        var cavityCells = new HashSet<Vector2Int>();

        /* 1 ── build outer shell + wall ----------------------------- */
        for(int y=minY; y<=maxY; ++y)
        {
            for(int x=minX; x<=maxX; ++x)
            {
                if (!InsideHiveShape(x-ax, y-ay, rx, ry, noiseSeed, contourNoise))
                    continue;

                world.SetTileID(x,y, hiveBlockTile.tileID, false);
                world.SetTileID(x,y, honeycombWallTile.tileID, true);
            }
        }

        /* 2 ── carve cavity & fill bottom with honey ---------------- */
        int cavRx = Mathf.RoundToInt(rx*0.55f);
        int cavRy = Mathf.RoundToInt(ry*0.55f);
        int poolTop = -cavRy + Mathf.RoundToInt(cavRy*poolFillPercent);

        for(int dy=-cavRy; dy<=cavRy; ++dy)
        {
            float yy = dy/(float)cavRy;
            for(int dx=-cavRx; dx<=cavRx; ++dx)
            {
                float xx = dx/(float)cavRx;
                if (xx*xx+yy*yy>1f) continue;             // outside cavity

                int wx=ax+dx, wy=ay+dy;
                world.SetTileID(wx,wy, airTile.tileID,false);   // hollow centre

                if (dy<=poolTop)                              // honey pool
                    world.SetTileID(wx,wy,honeyLiquidTile.tileID,false);

                cavityCells.Add(new Vector2Int(wx,wy));
            }
        }
    }

    /* ---------------------------------------------------------------- */
    bool InsideHiveShape(int dx,int dy,float rx,float ry,
                         float seed,float noiseAmt)
    {
        /* base teardrop (ellipse that narrows towards top) */
        float nx = dx/rx;
        float ny = dy/ry;
        float baseShape = nx*nx + ny*ny*(1f+0.8f*mathfAbs(ny)); // fattens bottom

        if (baseShape>1f) return false;

        /* six-lobed “honey-comb” scallop (polar) */
        float angle = Mathf.Atan2(ny, nx);                    // –π … π
        float scallop = 0.12f * Mathf.Cos(angle*6f);          // 6 bumps
        float radiusAdj = 1f + scallop;

        /* noise for irregularity */
        float n = Mathf.PerlinNoise(seed+dx*0.15f, seed+dy*0.15f)-0.5f;

        /* point is inside if radius within adjusted boundary */
        float dist = Mathf.Sqrt(nx*nx+ny*ny);
        return dist <= radiusAdj - n*noiseAmt;
    }

    /* ---------------------------------------------------------------- */
    static float mathfAbs(float v)=> v<0?-v:v;

    public override BoundsInt GetStructureBounds(int ax,int ay)
    {
        int radX = Mathf.CeilToInt(sizeX*0.5f);
        int radY = Mathf.CeilToInt(sizeY*0.5f);
        return new BoundsInt(ax-radX, ay-radY,0, radX*2, radY*2,1);
    }
}
#endif