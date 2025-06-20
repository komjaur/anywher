/* =========================================================================
   WorldMapGenerator.cs  (fast-path version, 24-Apr-25)
   – world-scale mask, area-blend, local & global ores
   – NO ScriptableObject look-ups in hot loops
   – skyline & mask noise still per-tile (see step-2 guide to cache rows)
   =========================================================================*/
using UnityEngine;
using System.Runtime.CompilerServices;               // MethodImpl

public static class WorldMapGenerator
{
    /* ──────────────────────────  cached refs  ────────────────────────── */
    static BiomeData[] _biomes;
    static AreaData[]  _areas;
    static OreSetting[] _ores;

    /* ──────────  fast per-tile meta (built once at startup)  ─────────── */
    static bool[]    _isSolid, _isLiquid;            // tileID → bool
    static TileData[] _tileCache;                    // tileID → TileData

    static void BuildTileMeta(TileDatabase db)
    {
        int max = db.tiles.Length;                   // IDs == indices (after your sorter)
        _isSolid   = new bool[max];
        _isLiquid  = new bool[max];
        _tileCache = db.tiles;                       // cheap alias

        foreach (var t in db.tiles)
        {
            _isSolid [t.tileID] = t.behavior == BlockBehavior.Solid;
            _isLiquid[t.tileID] = t.behavior == BlockBehavior.Liquid;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsSolidFast(int id)  => id > 0 && _isSolid[id];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsLiquidFast(int id) => id > 0 && _isLiquid[id];

    /* ────────────────────────  public entry  ─────────────────────────── */
    public static void GenerateWorldTiles(World world, WorldData data)
    {
        /* build meta tables once */
        if (_isSolid == null) BuildTileMeta(data.tileDatabase);

        _biomes = data.biomeDatabase?.biomelist;
        _areas  = data.areasDatabase?.areas;
        _ores   = data.oresDatabase ? data.oresDatabase.ores : null;

        int seed = data.seed;

        for (int cy = 0; cy < data.heightInChunks; ++cy)
        for (int cx = 0; cx < data.widthInChunks;  ++cx)
        {
            Vector2Int c = new(cx, cy);
            Chunk ck = world.GetChunk(c) ?? world.AddChunk(c);
            FillChunkTiles(ck, data, seed);
        }
    }



    /* ────────────────────────── Helpers ───────────────────────────── */

    // 1-D fractal Brownian motion (3 octaves, lacunarity 2, gain 0.5)
    static float FBM(float x)
    {
        float sum = 0f, amp = 1f, freq = 1f;
        for (int i = 0; i < 3; ++i)
        {
            sum += amp * (Mathf.PerlinNoise(x * freq, 0f) * 2f - 1f);
            freq *= 2f;
            amp  *= 0.5f;
        }
        return sum;
    }

    // Cheap ridge noise: inverted-V Perlin squared (sharp peaks only)
    static float Ridge(float x)
    {
        float n = Mathf.PerlinNoise(x, 0f);
        n = 2f * (0.5f - Mathf.Abs(0.5f - n));
        return n * n;
    }

public static void FillChunkTiles(Chunk chunk, WorldData d, int seed)
{
    /* ─── reset & boiler-plate ───────────────────────────────────── */
    chunk.SetFlags(ChunkFlags.None);

    int  cs   = chunk.size;
    int  wx0  = chunk.position.x * cs;
    int  wy0  = chunk.position.y * cs;
    int  mapH = d.heightInChunks * d.chunkSize;

    float baseSkyNorm = d.overworldStarts - d.overworldDepth * 0.5f;
    bool  useWorldMask = d.worldNoiseFrequency > 0f;
    int   smoothW      = Mathf.Clamp(d.borderSmoothWidth, 0, cs - 1);

    /* ─── bookkeeping for flag logic ─────────────────────────────── */
    bool skyFound    = false;   // any tile above skyline
    bool ugAirFound  = false;   // any underground-air tile
    bool solidFound  = false;   // any solid block
    bool liquidFound = false;   // any liquid tile

    /* ─── tile loop ──────────────────────────────────────────────── */
    for (int ly = 0; ly < cs; ++ly)
    {
        int   wy   = wy0 + ly;
        float nY   = (float)wy / mapH;           // depth norm (0 top, 1 bottom)

        for (int lx = 0; lx < cs; ++lx)
        {
            int wx = wx0 + lx;

            /* ── area & blend look-ups (unchanged) ───────────────── */
            byte areaIx   = chunk.areaIDs[lx, ly];
            AreaOverworldData aMain = (areaIx < _areas?.Length)
                                      ? _areas[areaIx] as AreaOverworldData
                                      : null;

            float blendT = 0f;
            AreaOverworldData aBlend = null;

            // left border scan
            if (lx > 0)
            {
                byte leftIx = chunk.areaIDs[lx - 1, ly];
                if (leftIx != areaIx)
                {
                    int dist = 0;
                    for (int dx = 1; dx <= smoothW && lx - dx >= 0; ++dx)
                        if (chunk.areaIDs[lx - dx, ly] == leftIx) dist++; else break;

                    aBlend = (leftIx < _areas?.Length)
                             ? _areas[leftIx] as AreaOverworldData : null;
                    blendT = 1f - (float)dist / (smoothW + 1);
                }
            }

            // right border scan
            if (aBlend == null && lx < cs - 1)
            {
                byte rightIx = chunk.areaIDs[lx + 1, ly];
                if (rightIx != areaIx)
                {
                    int dist = 0;
                    for (int dx = 1; dx <= smoothW && lx + dx < cs; ++dx)
                        if (chunk.areaIDs[lx + dx, ly] == rightIx) dist++; else break;

                    aBlend = (rightIx < _areas?.Length)
                             ? _areas[rightIx] as AreaOverworldData : null;
                    blendT = (float)dist / (smoothW + 1);
                }
            }

            /* ── skyline parameters & noise (unchanged) ──────────── */
            float lowFreq   = d.skyLowFreq,   lowAmp   = d.skyLowAmp;
            float ridgeFreq = d.skyRidgeFreq, ridgeAmp = d.skyRidgeAmp;
            float cosMul = aMain ? aMain.skyCosAmpMul : 1f;
            float cosOff = aMain ? aMain.skyCosOffset : 0f;

            float rugMain  = (aMain && aMain.skylineRuggedness >= 0f)
                             ? aMain.skylineRuggedness : 0.5f;
            float rugBlend = 0.5f;
            if (aBlend) rugBlend = (aBlend.skylineRuggedness >= 0f)
                                   ? aBlend.skylineRuggedness : 0.5f;

            if (aBlend && blendT > 0f)
            {
                cosMul  = Mathf.Lerp(cosMul,  aBlend.skyCosAmpMul, blendT);
                cosOff  = Mathf.Lerp(cosOff,  aBlend.skyCosOffset, blendT);
                rugMain = Mathf.Lerp(rugMain, rugBlend,           blendT);
            }

            float ampMul = Mathf.Lerp(0.3f, 1.7f, rugMain);
            lowAmp   *= ampMul;
            ridgeAmp *= ampMul;
            float mountainAmpLocal = d.skyMountainAmp * ampMul;

            float cosWave = Mathf.Cos((wx + seed) * d.skyLineWaveScale)
                          * 0.5f * d.skyLineWaveAmplitude;
            cosWave = cosWave * cosMul + cosOff;
            float fbm    = FBM((wx + seed) * lowFreq)     * lowAmp;
            float ridges = Ridge((wx + seed) * ridgeFreq) * ridgeAmp;

            float mNoise = Mathf.PerlinNoise((wx + seed) * d.skyMountainFreq,
                                             (wy + seed) * d.skyMountainFreq);

            float mountain = 0f;
            if (mNoise > 0.6f)
            {
                float t = (mNoise - 0.6f) / 0.4f;
                mountain = t * t * mountainAmpLocal;
            }
            else if (mNoise < 0.3f)
            {
                float t = (0.3f - mNoise) / 0.3f;
                mountain = -t * t * mountainAmpLocal * d.skyValleyFactor;
            }

            float skyNorm = baseSkyNorm + cosWave + fbm + ridges + mountain;

            /* ── biome / area look-ups ─────────────────────────── */
            byte biomeIx = chunk.biomeIDs[lx, ly];
            BiomeData biome = (biomeIx < _biomes?.Length) ? _biomes[biomeIx] : null;
            AreaData  area  = aMain ?? ((areaIx < _areas?.Length) ? _areas[areaIx] : null);

            /* ── world mask ───────────────────────────────────── */
            bool frontMasked = false;
            if (useWorldMask && biome != null && biome.worldNoiseThreshold > 0f)
            {
                float m = Mathf.PerlinNoise((wx + seed) * d.worldNoiseFrequency,
                                            (wy + seed) * d.worldNoiseFrequency);
                if (m < biome.worldNoiseThreshold) frontMasked = true;
            }

            /* ── area blend noise ─────────────────────────────── */
            float blendNoise = biome?.areaNoiseBlend ?? 0f;
            float areaNoise  = 0f;
            if (blendNoise > 0f)
            {
                float areaFreq = area ? area.areaNoiseFrequency : 0.003f;
                areaNoise = Mathf.PerlinNoise((wx + seed) * areaFreq,
                                              (wy + seed) * areaFreq);
            }

        /* ── decide tiles ─────────────────────────────────── */
int front = -1, back = -1;
bool isAboveSky = nY > skyNorm;
bool biomeIsSky = area != null && area.zone == ZoneType.Sky;

if (isAboveSky && !biomeIsSky)
{
    // Above the skyline: put Sky-air on the FRONT only.
    front = SafeID(d.tileDatabase.SkyAirTile);
    back  = 0;
    skyFound = true;
}
else
{
    /* ---------- pick front / back from biome rules ---------- */
    float depthPx = Mathf.Max(0f, skyNorm * mapH - wy);

    bool usedLayer = biome is BiomeOverworldData ow &&
                     ow.overworldLayers is { Length: > 0 } &&
                     TryPickOverworldLayer(
                         ow, depthPx, wx, wy, seed,
                         areaNoise, blendNoise,
                         out front, out back);

    if (!usedLayer)
    {
        front = PickFromBlock(biome?.FrontLayerTiles, biome,
                              wx, wy, seed, areaNoise, blendNoise);

        back  = PickFromBlock(biome?.BackgroundLayerTiles, biome,
                              wx, wy, seed, areaNoise, blendNoise);
    }

    /* ---------- defaults if picker returned “none” ---------- */
    if (front < 0)
        front = SafeID(isAboveSky
                       ? d.tileDatabase.SkyAirTile
                       : d.tileDatabase.UndergroundAirTile);

    if (back < 0)
        back = 0;

    /* ---------- inject local ores only into solid front tiles ---------- */
    if (!frontMasked && IsSolidFast(front) &&
        biome?.Ores is { Length: > 0 })
    {
        TryInjectLocalOre(ref front, biome.Ores, nY,
                          wx, wy, seed, area, d);
    }

    if (IsUGAir(front, d) || IsUGAir(back, d))  ugAirFound  = true;
    if (IsSolidFast(front) || IsSolidFast(back)) solidFound = true;
}

/* ---------- universal safeguard: background must never be air ---------- */
if (back == d.tileDatabase.SkyAirTile?.tileID ||
    back == d.tileDatabase.UndergroundAirTile?.tileID)
{
    back = 0;
}

if (frontMasked)
    front = SafeID(d.tileDatabase.UndergroundAirTile);

         /* ── write to layers ─────────────────────────────── */
        bool fLiquid = IsLiquidFast(front);   // only the FRONT tile can ever be liquid

        if (fLiquid)
        {
            // put the liquid in the dedicated liquid layer
            chunk.liquidLayerTileIndexes[lx, ly] = front;
            chunk.frontLayerTileIndexes [lx, ly] = 0;      // nothing in the front layer
        }
        else
        {
            // solid / air in the front
            chunk.frontLayerTileIndexes [lx, ly] = front;
            chunk.liquidLayerTileIndexes[lx, ly] = 0;      // clear residuals
        }

        /* the background layer is never liquid – it is either a wall block or NULL */
        chunk.backgroundLayerTileIndexes[lx, ly] = back;

        if (fLiquid) liquidFound = true;

        }
    }

    /* ─── flags ──────────────────────────────────────────────────── */
    ChunkFlags cf = ChunkFlags.None;

    // whole-chunk conditions
    if (chunk.IsCompletelySky(d.tileDatabase.SkyAirTile))
        cf |= ChunkFlags.ClearSky;
    else if (chunk.IsCompletelyUndergroundAir(d.tileDatabase.UndergroundAirTile))
        cf |= ChunkFlags.CaveAir;

    // surface: must have sky-air AND solid, but not pure sky
    if (skyFound && solidFound && !cf.HasFlag(ChunkFlags.ClearSky))   // ← MODIFIED
        cf |= ChunkFlags.Surface;

    // cave (UG-air + solid, but not pure UG-air)
    if (ugAirFound && solidFound && !cf.HasFlag(ChunkFlags.CaveAir))
        cf |= ChunkFlags.Cave;

    if (liquidFound) cf |= ChunkFlags.Liquids;

    chunk.SetFlags(cf);
}










    /* ------------------------------------------------------------------ */
    /*  overworld depth-layer helper                                      */
    /* ------------------------------------------------------------------ */
    static bool TryPickOverworldLayer(
        BiomeOverworldData ow, float depthPx,
        int wx,int wy,int seed,
        float areaNoise,float blend,
        out int front,out int back)
    {
        foreach (var L in ow.overworldLayers)
            if (depthPx >= L.minDepth && depthPx <= L.maxDepth)
            {
                front = PickFromSubTiles(L.FrontLayerTiles,
                                         ow.OverworldnoiseScale, ow.OverworldnoiseIntensity,
                                         wx, wy, seed,
                                         areaNoise, blend);

                back  = PickFromSubTiles(L.BackgroundLayerTiles,
                                         ow.OverworldnoiseScale, ow.OverworldnoiseIntensity,
                                         wx, wy, seed,
                                         areaNoise, blend);
                return true;
            }
        front = back = -1;
        return false;
    }

    /* ------------------------------------------------------------------ */
    /*  block & subtile pickers                                           */
    /* ------------------------------------------------------------------ */
    static int PickFromBlock(BiomeBlock blk,BiomeData biome,
                             int wx,int wy,int seed,
                             float areaNoise,float blend)
    {
        if (blk == null || blk.subTiles is not { Length: >0 }) return -1;

        float n = SampleBiomeNoise(biome, wx, wy, seed, blk.NoiseOffset,
                                   areaNoise, blend);
        return NearestThreshold(blk.subTiles, n);
    }

    static int PickFromSubTiles(BiomeSubTile[] subs,float scale,float intens,
                                int wx,int wy,int seed,
                                float areaNoise,float blend)
    {
        if (subs == null || subs.Length == 0) return -1;

        float nBiome = Mathf.PerlinNoise(wx*scale,(seed+wy)*scale)*intens;
        float n = blend > 0f ? Mathf.Lerp(nBiome, areaNoise, blend) : nBiome;
        return NearestThreshold(subs, n);
    }

    /* ------------------------------------------------------------------ */
    /*  local-ore injection                                               */
    /* ------------------------------------------------------------------ */
    static void TryInjectLocalOre(
        ref int front,
        OreSetting[] ores,
        float depthNorm,
        int wx, int wy, int seed,
        AreaData area,
        WorldData d)
    {
        if (!IsSolidFast(front) || ores == null || ores.Length == 0) return;

        TileData host = _tileCache[front];

        foreach (var o in ores)
        {
            if (!o.oreTile) continue;
            if (depthNorm < o.minDepthNorm || depthNorm > o.maxDepthNorm) continue;
            if (o.validHostTiles?.Count > 0 && !o.validHostTiles.Contains(host)) continue;

            float n = Mathf.PerlinNoise(
                        (wx + seed) * o.noiseFrequency,
                        (wy + seed) * o.noiseFrequency);

            if (n > o.threshold && Random.value < o.chance)
            {
                front = o.oreTile.tileID;
                return;
            }
        }
    }

    /* ------------------------------------------------------------------ */
    /*  closest-threshold (binary search)                                 */
    /* ------------------------------------------------------------------ */
    static int NearestThreshold(BiomeSubTile[] subs,float n)
    {
        int lo = 0, hi = subs.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (subs[mid].threshold < n) lo = mid + 1; else hi = mid - 1;
        }
        int low = Mathf.Clamp(hi, 0, subs.Length - 1);
        int up  = Mathf.Clamp(lo, 0, subs.Length - 1);
        int pick = Mathf.Abs(n - subs[low].threshold) <= Mathf.Abs(n - subs[up].threshold)
                   ? low : up;
        return subs[pick].tileData ? subs[pick].tileData.tileID : -1;
    }

    /* ------------------------------------------------------------------ */
    /*  FBm per-biome noise (plus optional blend)                         */
    /* ------------------------------------------------------------------ */
    static float SampleBiomeNoise(BiomeData b,int wx,int wy,int seed,Vector2 off,
                                  float areaNoise,float blend)
    {
        if (b == null) return areaNoise;

        float freq = b.frequency, amp = 1f, sum = 0f;
        for (int o = 0; o < b.octaves; ++o)
        {
            float nx = (wx + off.x + seed + b.offset.x) * freq * b.stretch.x;
            float ny = (wy + off.y + seed + b.offset.y) * freq * b.stretch.y;

            float n = Mathf.PerlinNoise(nx, ny);
            sum  += n * amp;
            freq *= b.lacunarity;
            amp  *= b.persistence;
        }
        float biomeN = Mathf.Clamp01(sum * b.strength);
        return blend > 0f ? Mathf.Lerp(biomeN, areaNoise, blend) : biomeN;
    }

    /* ------------------------------------------------------------------ */
    /*  tiny helpers                                                      */
    /* ------------------------------------------------------------------ */
    static int  SafeID(TileData td) => td ? td.tileID : 0;
    static bool IsUGAir(int id,WorldData d)=>
        id>0 && d.tileDatabase.UndergroundAirTile &&
        id==d.tileDatabase.UndergroundAirTile.tileID;
}
