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
    static BiomeData[]  _biomes;
    static AreaData[]   _areas;
    static OreSetting[] _ores;

    /* ──────────  fast per-tile meta (built once at startup)  ─────────── */
    static bool[]     _isSolid, _isLiquid;           // tileID → bool
    static TileData[] _tileCache;                    // tileID → TileData

    /* chunk-column noise caches reused between chunks (to avoid GC) */
    static float[] s_cosCache;
    static float[] s_fbmCache;
    static float[] s_ridgeCache;

    /* =================================================================== */
    /*  one-time meta build                                                */
    /* =================================================================== */
    static void BuildTileMeta(TileDatabase db)
    {
        int max = db.tiles.Length;                   // IDs == indices
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

    /* =================================================================== */
    /*  public entry                                                       */
    /* =================================================================== */
    public static void GenerateWorldTiles(World world, WorldData data)
    {
        if (_isSolid == null)                 // build meta tables once
            BuildTileMeta(data.tileDatabase);

        _biomes = data.biomeDatabase?.biomelist;
        _areas  = data.areasDatabase?.areas;
        _ores   = data.oresDatabase != null ? data.oresDatabase.ores : null;

        int seed = data.seed;

        for (int cy = 0; cy < data.heightInChunks; ++cy)
        for (int cx = 0; cx < data.widthInChunks;  ++cx)
        {
            Vector2Int c = new(cx, cy);
            Chunk ck = world.GetChunk(c) ?? world.AddChunk(c);
            FillChunkTiles(ck, data, seed);
        }
    }

    /* =================================================================== */
    /*  helpers                                                            */
    /* =================================================================== */

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

    /* ------------------------------------------------------------------ */
    /*  small helper container                                             */
    /* ------------------------------------------------------------------ */
    struct ColumnNoise
    {
        public float[] Cos, Fbm, Ridge;
    }

    /* ------------------------------------------------------------------ */
    /*  per-chunk column-noise cache                                       */
    /* ------------------------------------------------------------------ */
    static ColumnNoise BuildNoiseCache(int wx0, int cs, int seed, WorldData d)
    {
        if (s_cosCache == null || s_cosCache.Length != cs)
        {
            s_cosCache   = new float[cs];
            s_fbmCache   = new float[cs];
            s_ridgeCache = new float[cs];
        }

        ColumnNoise n;
        n.Cos   = s_cosCache;
        n.Fbm   = s_fbmCache;
        n.Ridge = s_ridgeCache;

        float lowFreq   = d.skyLowFreq;
        float ridgeFreq = d.skyRidgeFreq;

        for (int lx = 0; lx < cs; ++lx)
        {
            int wx = wx0 + lx;

            n.Cos[lx] = Mathf.Cos((wx + seed) * d.skyLineWaveScale) *
                        0.5f * d.skyLineWaveAmplitude;

            n.Fbm[lx]   = FBM((wx + seed) * lowFreq);
            n.Ridge[lx] = Ridge((wx + seed) * ridgeFreq);
        }

        return n;
    }

    /* ------------------------------------------------------------------ */
    /*  area-blend lookup                                                  */
    /* ------------------------------------------------------------------ */
    static (AreaOverworldData main, AreaOverworldData blend, float blendT)
        GetAreaBlend(Chunk chunk, int lx, int ly, int smoothW)
    {
        byte areaIx = chunk.areaIDs[lx, ly];
        AreaOverworldData aMain = (areaIx < _areas?.Length)
                                  ? _areas[areaIx] as AreaOverworldData
                                  : null;

        float blendT = 0f;
        AreaOverworldData aBlend = null;

        /* ── look left ── */
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

        /* ── look right ── */
        if (aBlend == null && lx < chunk.size - 1)
        {
            byte rightIx = chunk.areaIDs[lx + 1, ly];
            if (rightIx != areaIx)
            {
                int dist = 0;
                for (int dx = 1; dx <= smoothW && lx + dx < chunk.size; ++dx)
                    if (chunk.areaIDs[lx + dx, ly] == rightIx) dist++; else break;

                aBlend = (rightIx < _areas?.Length)
                         ? _areas[rightIx] as AreaOverworldData : null;
                blendT = (float)dist / (smoothW + 1);
            }
        }

        return (aMain, aBlend, blendT);
    }

    /* ------------------------------------------------------------------ */
    /*  skyline height                                                     */
    /* ------------------------------------------------------------------ */
    static float ComputeSkyline(
        WorldData d,
        AreaOverworldData main,
        AreaOverworldData blend,
        float blendT,
        float cosBase, float fbmBase, float ridgeBase,
        int wx, int wy, int seed,
        float baseSkyNorm)
    {
        float lowFreq   = d.skyLowFreq,   lowAmp   = d.skyLowAmp;
        float ridgeFreq = d.skyRidgeFreq, ridgeAmp = d.skyRidgeAmp;

        float cosMul = main != null ? main.skyCosAmpMul : 1f;
        float cosOff = main != null ? main.skyCosOffset : 0f;

        float rugMain  = (main != null && main.skylineRuggedness >= 0f)
                         ? main.skylineRuggedness : 0.5f;
        float rugBlend = blend != null && blend.skylineRuggedness >= 0f
                         ? blend.skylineRuggedness : 0.5f;

        if (blend != null && blendT > 0f)
        {
            cosMul  = Mathf.Lerp(cosMul,  blend.skyCosAmpMul, blendT);
            cosOff  = Mathf.Lerp(cosOff,  blend.skyCosOffset, blendT);
            rugMain = Mathf.Lerp(rugMain, rugBlend,           blendT);
        }

        float ampMul = Mathf.Lerp(0.3f, 1.7f, rugMain);
        lowAmp   *= ampMul;
        ridgeAmp *= ampMul;
        float mountainAmpLocal = d.skyMountainAmp * ampMul;

        float cosWave = cosBase * cosMul + cosOff;
        float fbm     = fbmBase   * lowAmp;
        float ridges  = ridgeBase * ridgeAmp;

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

        return baseSkyNorm + cosWave + fbm + ridges + mountain;
    }

    /* ------------------------------------------------------------------ */
    /*  front-mask + area-noise sampling                                   */
    /* ------------------------------------------------------------------ */
    static (bool frontMasked, float areaNoise, float blendNoise)
        ComputeMaskAndAreaNoise(
            int wx, int wy, int seed, BiomeData biome, AreaData area,
            bool useWorldMask, WorldData d)
    {
        bool frontMasked = false;
        if (useWorldMask && biome != null && biome.worldNoiseThreshold > 0f)
        {
            float m = Mathf.PerlinNoise((wx + seed) * d.worldNoiseFrequency,
                                        (wy + seed) * d.worldNoiseFrequency);
            if (m < biome.worldNoiseThreshold) frontMasked = true;
        }

        float blendNoise = biome?.areaNoiseBlend ?? 0f;
        float areaNoise  = 0f;
        if (blendNoise > 0f)
        {
            float areaFreq = area != null ? area.areaNoiseFrequency : 0.003f;
            areaNoise = Mathf.PerlinNoise((wx + seed) * areaFreq,
                                          (wy + seed) * areaFreq);
        }

        return (frontMasked, areaNoise, blendNoise);
    }

    /* ------------------------------------------------------------------ */
    /*  tile pickers & writers                                             */
    /* ------------------------------------------------------------------ */

    static void PickTiles(
        BiomeData biome, AreaData area,
        float nY, float skyNorm, bool frontMasked,
        float areaNoise, float blendNoise,
        int wx, int wy, int seed,
        ref bool skyFound, ref bool ugAirFound, ref bool solidFound,
        out int front, out int back, WorldData d)
    {
        front = back = -1;

        bool isAboveSky = nY > skyNorm;
        bool biomeIsSky = area != null && area.zone == ZoneType.Sky;

        if (isAboveSky && !biomeIsSky)
        {
            /* clear sky */
            front = SafeID(d.tileDatabase.SkyAirTile);
            back  = 0;
            skyFound = true;
        }
        else
        {
            /* sample overworld-layer or generic blocks */
            float depthPx = Mathf.Max(0f, skyNorm * (d.heightInChunks * d.chunkSize) - wy);

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

            /* safe fallbacks */
            if (front < 0)
                front = SafeID(isAboveSky
                               ? d.tileDatabase.SkyAirTile
                               : d.tileDatabase.UndergroundAirTile);

            if (back < 0)
                back = 0;

            /* ore injection on solid tiles */
            if (!frontMasked && IsSolidFast(front))
            {
                if (biome?.Ores is { Length: > 0 })
                    TryInjectLocalOre(ref front, biome.Ores, nY,
                                      wx, wy, seed, area, d);

                if (_ores is { Length: > 0 })
                    TryInjectGlobalOre(ref front, _ores, nY,
                                       wx, wy, seed, area, d);
            }

            if (IsUGAir(front, d) || IsUGAir(back, d))  ugAirFound  = true;
            if (IsSolidFast(front) || IsSolidFast(back)) solidFound = true;
        }

        /* never leave air in the background layer */
        if (back == d.tileDatabase.SkyAirTile?.tileID ||
            back == d.tileDatabase.UndergroundAirTile?.tileID)
            back = 0;

        /* global world-mask overrides */
        if (frontMasked)
            front = SafeID(d.tileDatabase.UndergroundAirTile);
    }

    static void WriteLayers(Chunk chunk, int lx, int ly, int front, int back,
                            ref bool liquidFound, WorldData d)
    {
        bool fLiquid = IsLiquidFast(front);

        if (fLiquid)
        {
            chunk.liquidLayerTileIndexes[lx, ly] = front;
            chunk.frontLayerTileIndexes [lx, ly] = 0;
        }
        else
        {
            chunk.frontLayerTileIndexes [lx, ly] = front;
            chunk.liquidLayerTileIndexes[lx, ly] = 0;
        }

        chunk.backgroundLayerTileIndexes[lx, ly] = back;

        if (fLiquid) liquidFound = true;
    }

    static void SetChunkFlags(Chunk chunk, WorldData d,
                              bool skyFound, bool ugAirFound,
                              bool solidFound, bool liquidFound)
    {
        ChunkFlags cf = ChunkFlags.None;

        if (chunk.IsCompletelySky(d.tileDatabase.SkyAirTile))
            cf |= ChunkFlags.ClearSky;
        else if (chunk.IsCompletelyUndergroundAir(d.tileDatabase.UndergroundAirTile))
            cf |= ChunkFlags.CaveAir;

        if (skyFound && solidFound && !cf.HasFlag(ChunkFlags.ClearSky))
            cf |= ChunkFlags.Surface;

        if (ugAirFound && solidFound && !cf.HasFlag(ChunkFlags.CaveAir))
            cf |= ChunkFlags.Cave;

        if (liquidFound) cf |= ChunkFlags.Liquids;

        chunk.SetFlags(cf);
    }

    /* ------------------------------------------------------------------ */
    /*  main chunk-fill routine                                            */
    /* ------------------------------------------------------------------ */
    public static void FillChunkTiles(Chunk chunk, WorldData d, int seed)
    {
        chunk.SetFlags(ChunkFlags.None);

        int  cs   = chunk.size;
        int  wx0  = chunk.position.x * cs;
        int  wy0  = chunk.position.y * cs;
        int  mapH = d.heightInChunks * d.chunkSize;

        float baseSkyNorm = d.overworldStarts - d.overworldDepth * 0.5f;
        bool  useWorldMask = d.worldNoiseFrequency > 0f;
        int   smoothW      = Mathf.Clamp(d.borderSmoothWidth, 0, cs - 1);

        ColumnNoise n = BuildNoiseCache(wx0, cs, seed, d);

        bool skyFound = false, ugAirFound = false,
             solidFound = false, liquidFound = false;

        for (int ly = 0; ly < cs; ++ly)
        {
            int   wy = wy0 + ly;
            float nY = (float)wy / mapH;

            for (int lx = 0; lx < cs; ++lx)
            {
                int wx = wx0 + lx;

                var (aMain, aBlend, blendT) = GetAreaBlend(chunk, lx, ly, smoothW);

                float skyNorm = ComputeSkyline(d, aMain, aBlend, blendT,
                                               n.Cos[lx], n.Fbm[lx], n.Ridge[lx],
                                               wx, wy, seed, baseSkyNorm);

                byte biomeIx = chunk.biomeIDs[lx, ly];
                BiomeData biome = (biomeIx < _biomes?.Length) ? _biomes[biomeIx] : null;

                AreaData area = aMain ??
                                ((chunk.areaIDs[lx, ly] < _areas?.Length)
                                 ? _areas[chunk.areaIDs[lx, ly]] : null);

                var maskArea = ComputeMaskAndAreaNoise(wx, wy, seed,
                                                       biome, area,
                                                       useWorldMask, d);

                int front, back;
                PickTiles(biome, area, nY, skyNorm, maskArea.frontMasked,
                          maskArea.areaNoise, maskArea.blendNoise,
                          wx, wy, seed,
                          ref skyFound, ref ugAirFound, ref solidFound,
                          out front, out back, d);

                WriteLayers(chunk, lx, ly, front, back, ref liquidFound, d);
            }
        }

        SetChunkFlags(chunk, d, skyFound, ugAirFound, solidFound, liquidFound);
    }

    /* ------------------------------------------------------------------ */
    /*  overworld depth-layer helper                                       */
    /* ------------------------------------------------------------------ */
    static bool TryPickOverworldLayer(
        BiomeOverworldData ow, float depthPx,
        int wx, int wy, int seed,
        float areaNoise, float blend,
        out int front, out int back)
    {
        foreach (var L in ow.overworldLayers)
            if (depthPx >= L.minDepth && depthPx <= L.maxDepth)
            {
                front = PickFromSubTiles(L.FrontLayerTiles,
                                         ow.OverworldnoiseScale,
                                         ow.OverworldnoiseIntensity,
                                         wx, wy, seed,
                                         areaNoise, blend);

                back  = PickFromSubTiles(L.BackgroundLayerTiles,
                                         ow.OverworldnoiseScale,
                                         ow.OverworldnoiseIntensity,
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
    static int PickFromBlock(BiomeBlock blk, BiomeData biome,
                             int wx, int wy, int seed,
                             float areaNoise, float blend)
    {
        if (blk == null || blk.subTiles is not { Length: > 0 }) return -1;

        float n = SampleBiomeNoise(biome, wx, wy, seed, blk.NoiseOffset,
                                   areaNoise, blend);
        return NearestThreshold(blk.subTiles, n);
    }

    static int PickFromSubTiles(BiomeSubTile[] subs, float scale, float intens,
                                int wx, int wy, int seed,
                                float areaNoise, float blend)
    {
        if (subs == null || subs.Length == 0) return -1;

        float nBiome = Mathf.PerlinNoise(wx * scale, (seed + wy) * scale) * intens;
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
        if (!IsSolidFast(front) || ores is not { Length: > 0 }) return;

        TileData host = _tileCache[front];

        foreach (var o in ores)
        {
            if (!o.oreTile) continue;
            if (depthNorm < o.minDepthNorm || depthNorm > o.maxDepthNorm) continue;
            if (o.validHostTiles is { Count: > 0 } && !o.validHostTiles.Contains(host))
                continue;

            float n = Mathf.PerlinNoise((wx + seed) * o.noiseFrequency,
                                        (wy + seed) * o.noiseFrequency);

            if (n > o.threshold && Random.value < o.chance)
            {
                front = o.oreTile.tileID;
                return;
            }
        }
    }

    /* ------------------------------------------------------------------ */
    /*  global-ore injection                                              */
    /* ------------------------------------------------------------------ */
    static void TryInjectGlobalOre(
        ref int front,
        OreSetting[] ores,
        float depthNorm,
        int wx, int wy, int seed,
        AreaData area,
        WorldData d)
    {
        if (!IsSolidFast(front) || ores is not { Length: > 0 }) return;

        TileData host = _tileCache[front];

        foreach (var g in ores)
        {
            if (!g.oreTile) continue;
            if (depthNorm < g.minDepthNorm || depthNorm > g.maxDepthNorm) continue;

            if (g.allowedAreas is { Count: > 0 } &&
                (area == null || !g.allowedAreas.Contains(area)))
                continue;

            if (g.validHostTiles is { Count: > 0 } && !g.validHostTiles.Contains(host))
                continue;

            float n = Mathf.PerlinNoise(
                        (wx + seed + g.noiseOffset.x) * g.noiseFrequency,
                        (wy + seed + g.noiseOffset.y) * g.noiseFrequency);

            if (n > g.threshold && Random.value < g.chance)
            {
                front = g.oreTile.tileID;
                return;
            }
        }
    }

    /* ------------------------------------------------------------------ */
    /*  closest-threshold (binary search)                                 */
    /* ------------------------------------------------------------------ */
    static int NearestThreshold(BiomeSubTile[] subs, float n)
    {
        int lo = 0, hi = subs.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (subs[mid].threshold < n) lo = mid + 1;
            else                         hi = mid - 1;
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
    static float SampleBiomeNoise(
        BiomeData b, int wx, int wy, int seed, Vector2 off,
        float areaNoise, float blend)
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
    static int SafeID(TileData td) => td ? td.tileID : 0;

    static bool IsUGAir(int id, WorldData d) =>
        id > 0 &&
        d.tileDatabase.UndergroundAirTile &&
        id == d.tileDatabase.UndergroundAirTile.tileID;
}
