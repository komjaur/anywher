using UnityEngine;
using System.Collections.Generic;
#if false
/* ────────────────────────────────────────────────────────────────
   BlueprintRandomVariant – 3-variant edition
   • Only ever writes the Underground-Air tile (front layer)
   • Variants: BlobFill, Column, SnakeTunnel
   ─────────────────────────────────────────────────────────────── */

public enum RandomVariant
{
    BlobFill,
    Column,
    SnakeTunnel        // ← new
}

[CreateAssetMenu(fileName = "Blueprint_RandomVariant",
                 menuName  = "Game/World/Blueprint/Structure/Random Variant")]
public class BlueprintRandomVariant : BlueprintStructure
{
    /* ── toggles ── */
    [Header("Variants Enabled")]
    public bool useBlobFill   = true;
    public bool useColumn     = true;
    public bool useSnakeTunnel= true;   // ← new toggle

    /* ── size & noise ── */
    [Header("Common Size Range")] public int minSize = 4, maxSize = 9;
    [Header("Blob Raggedness")]   [Range(0,1)] public float blobNoiseCutoff = .55f;
    public float blobNoiseFreq = .25f;

    /* ── ONLY tile we ever write ── */
    [Header("Underground-Air Tile")]
    public TileData undergroundAir;

    /* ============================================================ */
    public override void PlaceStructure(World w, int ax, int ay)
    {
        switch (Pick())
        {
            case RandomVariant.BlobFill:   DrawBlob(w, ax, ay, ragged:false); break;
            case RandomVariant.Column:     DrawColumn(w, ax, ay);            break;
            case RandomVariant.SnakeTunnel:DrawSnake(w, ax, ay);             break;
        }
    }

    /* ── picker ── */
    RandomVariant Pick()
    {
        var bag = new List<RandomVariant>();
        if (useBlobFill)    bag.Add(RandomVariant.BlobFill);
        if (useColumn)      bag.Add(RandomVariant.Column);
        if (useSnakeTunnel) bag.Add(RandomVariant.SnakeTunnel);
        if (bag.Count == 0) bag.Add(RandomVariant.BlobFill);
        return bag[Random.Range(0, bag.Count)];
    }

    /* ── helper ── */
    void Carve(World w, int x, int y)
        => w.SetTileID(x, y, undergroundAir.tileID, false);

    /* ── variant implementations ── */

    // 1. BlobFill (optionally ragged)
    void DrawBlob(World w, int cx, int cy, bool ragged)
    {
        int r   = Random.Range(minSize, maxSize + 1);
        int rSq = r * r;
        int seed = w.seed ^ (cx << 3) ^ cy;

        for (int y = -r; y <= r; ++y)
        for (int x = -r; x <= r; ++x)
        {
            if (x * x + y * y > rSq) continue;
            if (ragged)
            {
                float n = Mathf.PerlinNoise((x + seed) * blobNoiseFreq,
                                            (y + seed) * blobNoiseFreq);
                if (n > blobNoiseCutoff) continue;
            }
            Carve(w, cx + x, cy + y);
        }
    }

    // 2. Column (straight vertical shaft downwards)
    void DrawColumn(World w, int x, int y)
    {
        int depth = maxSize * 4;
        for (int i = 0; i < depth; ++i) Carve(w, x, y + i);
    }

    // 3. SnakeTunnel – random-walk meander
    void DrawSnake(World w, int x, int y)
    {
        int length = maxSize * 5;
        for (int i = 0; i < length; ++i)
        {
            Carve(w, x, y);
            x += Random.Range(-1, 2);   // step −1, 0, or +1
            y += Random.Range(-1, 2);
        }
    }

    /* ── bounds (coarse estimate) ── */
    public override BoundsInt GetStructureBounds(int ax, int ay)
    {
        int r = maxSize * 4;
        return new BoundsInt(ax - r, ay - r, 0,
                             r * 2 + 1, r * 2 + 1, 1);
    }
}
#endif