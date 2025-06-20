/* BiomeMapGenerator.cs
 * ---------------------------------------------------------------------------
 * Generates area & biome maps, then writes them into World chunks.
 * • ALL three zones (Sky, Overworld, Underworld) now pick their biome by
 *   *nearest preferred elevation*, no special-case logic remains.
 * ------------------------------------------------------------------------- */
using UnityEngine;
using System.Collections;
using System.Diagnostics;          // Stopwatch
using System;
using System.Collections.Generic;

public static class BiomeMapGenerator
{
    /* ────────────────────  ENTRY  ──────────────────── */
    public static IEnumerator GenerateFullMapAsync(
        World world, WorldData data, Action<World> onComplete)
    {
        var sw = new Stopwatch(); sw.Start();

        /* 1) Voronoi area map (low-res) */
        byte[,] areaLow = GenerateAreaMap(
            data, data.areaGenerationSize.x, data.areaGenerationSize.y);

        /* 2) Stretch to elevation resolution */
        byte[,] areaHi = StretchMap(
            areaLow,
            data.areaGenerationSize.x, data.areaGenerationSize.y,
            data.elevationGenerationSize.x, data.elevationGenerationSize.y);

        /* 3) Elevation map */
        float[,] elev = GenerateElevationMap(
            data, data.elevationGenerationSize.x, data.elevationGenerationSize.y);

        /* 4) Area + elevation ⇒ biome map */
        byte[,] biomeHi = GenerateBiomeMap(data, areaHi, elev);

        /* 5) Stretch / warp to world size & fill chunks */
        int finalW = data.widthInChunks * data.chunkSize;
        int finalH = data.heightInChunks * data.chunkSize;

        StretchAreaAndBiomeWithEdgeNoiseAndFillChunks(
            world, data,
            areaHi, biomeHi,
            data.elevationGenerationSize.x, data.elevationGenerationSize.y,
            finalW, finalH);

        sw.Stop();
        UnityEngine.Debug.Log($"Full biome map generated in {sw.ElapsedMilliseconds} ms");
        yield return null;
        onComplete?.Invoke(world);
    }

    /* ────────────────── AREA MAP (Voronoi) ────────────────── */
    static byte[,] GenerateAreaMap(WorldData data, int w, int h)
    {
        if (data.areasDatabase?.areas == null || data.areasDatabase.areas.Length == 0)
            return new byte[w, h];

        var rng = new System.Random(data.seed);

        int ptsSU    = data.areaPointsCount;
        int owStrips = data.areaOverworldStripes;

        Vector2[] centres = new Vector2[ptsSU + owStrips];
        byte[]    areaIDs = new byte[ptsSU + owStrips];
        int ci = 0;

        float owTopNorm    = data.overworldStarts;
        float owBottomNorm = data.overworldStarts - data.overworldDepth;

        /* A) random points for SKY + UNDERWORLD */
        for (int i = 0; i < ptsSU; i++)
        {
            float nY  = (float)rng.NextDouble();
            float xPx = (float)rng.NextDouble() * w;
            float yPx = nY * h;

            ZoneType zone = nY > owTopNorm
                            ? ZoneType.Sky
                            : (nY < owBottomNorm ? ZoneType.Underworld
                                                 : ZoneType.Overworld);

            if (zone == ZoneType.Overworld) continue;   // OW handled below

            float heatTarget = 1f - 2f * (xPx / w);     // +1 hot-left → −1 cold-right
            int idx = PickDepthHeatArea(data.areasDatabase, zone, nY, heatTarget, rng);

            centres[ci] = new Vector2(xPx, yPx);
            areaIDs[ci] = (byte)idx;
            ci++;
        }

        /* B) Overworld stripe centres chosen by heat (area choice only) */
        float midY = (owTopNorm + owBottomNorm) * 0.5f * h;

        for (int s = 0; s < owStrips; s++)
        {
            float fracX = owStrips == 1 ? 0.5f : (float)s / (owStrips - 1);
            float xPx   = fracX * w;
            float heat  = 1f - 2f * fracX;

            int idx = PickAreaByHeat(data.areasDatabase, heat, rng);

            centres[ci] = new Vector2(xPx, midY);
            areaIDs[ci] = (byte)idx;
            ci++;
        }

        /* C) Voronoi raster fill */
        byte[,] map = new byte[w, h];
        int total   = ci;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float best = float.MaxValue; int bestI = 0;
            for (int i = 0; i < total; i++)
            {
                float dx = x - centres[i].x;
                float dy = y - centres[i].y;
                float d2 = dx * dx + dy * dy;
                if (d2 < best) { best = d2; bestI = i; }
            }
            map[x, y] = areaIDs[bestI];
        }
        return map;
    }

    /* ────────────────── ELEVATION MAP ────────────────── */
    static float[,] GenerateElevationMap(WorldData data, int w, int h)
    {
        float[,] m = new float[w, h];
        float s = data.elevationNoiseScale, k = data.elevationNoiseIntensity;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            m[x, y] = Mathf.PerlinNoise(x * s, y * s) * k;
        return m;
    }

    /* ────────────────── BIOME MAP (elev-res) ────────────────── */
    static byte[,] GenerateBiomeMap(
        WorldData data, byte[,] areaMap, float[,] elevMap)
    {
        int w = areaMap.GetLength(0), h = areaMap.GetLength(1);

        AreaData[] areas = data.areasDatabase.areas;
        byte[,] result   = new byte[w, h];
        var globalDB     = data.biomeDatabase;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            AreaData  area  = areas[areaMap[x, y]];
            BiomeData biome = PickBiomeByElevation(area, elevMap[x, y]);  // ← unified

            int idx = (biome != null && globalDB?.biomelist != null)
                      ? Array.IndexOf(globalDB.biomelist, biome)
                      : 0;
            result[x, y] = (byte)Mathf.Clamp(idx, 0, 255);
        }
        return result;
    }

    /* ────────────────── SKY / UW / OW BIOME PICKER (nearest elevation) ───── */
    static BiomeData PickBiomeByElevation(AreaData area, float e)
    {
        if (area.biomes == null || area.biomes.Length == 0) return null;
        BiomeData best = area.biomes[0];
        float     dMin = Mathf.Abs(e - best.elevation);

        foreach (var b in area.biomes)
        {
            float d = Mathf.Abs(e - b.elevation);
            if (d < dMin) { dMin = d; best = b; }
        }
        return best;
    }

    /* ────────────────── STRETCH + EDGE-NOISE + CHUNK FILL ────────────────── */
    static void StretchAreaAndBiomeWithEdgeNoiseAndFillChunks(
        World world, WorldData data,
        byte[,] areaSrc, byte[,] biomeSrc,
        int oldW, int oldH, int newW, int newH)
    {
        float sx = (float)oldW / newW, sy = (float)oldH / newH;
        float nS = data.edgeNoiseScale, nI = data.edgeNoiseIntensity;

        for (int y = 0; y < newH; y++)
        for (int x = 0; x < newW; x++)
        {
            float noise = Mathf.PerlinNoise(x * nS, y * nS);
            float shift = (noise - 0.5f) * nI;

            int sxIdx = Mathf.Clamp(Mathf.FloorToInt((x + shift) * sx), 0, oldW - 1);
            int syIdx = Mathf.Clamp(Mathf.FloorToInt((y + shift) * sy), 0, oldH - 1);

            byte aVal = areaSrc [sxIdx, syIdx];
            byte bVal = biomeSrc[sxIdx, syIdx];

            int cX = x / world.Data.chunkSize, cY = y / world.Data.chunkSize;
            Vector2Int coord = new(cX, cY);
            var chunk = world.GetChunk(coord) ?? world.AddChunk(coord);

            int lx = x % world.Data.chunkSize, ly = y % world.Data.chunkSize;
            chunk.areaIDs [lx, ly] = aVal;
            chunk.biomeIDs[lx, ly] = bVal;
            chunk.AddAreaID (aVal);
            chunk.AddBiomeID(bVal);
        }
    }

    /* ────────────────── SIMPLE NEAREST-NEIGHBOUR STRETCH ────────────────── */
    static byte[,] StretchMap(byte[,] src, int oldW, int oldH, int newW, int newH)
    {
        byte[,] r = new byte[newW, newH];
        float sx = (float)oldW / newW, sy = (float)oldH / newH;

        for (int y = 0; y < newH; y++)
        for (int x = 0; x < newW; x++)
            r[x, y] = src[
                Mathf.Clamp(Mathf.FloorToInt(x * sx), 0, oldW - 1),
                Mathf.Clamp(Mathf.FloorToInt(y * sy), 0, oldH - 1)];
        return r;
    }

    /* ────────────────── DEPTH + HEAT AREA PICKER ────────────────── */
    // Chooses an area whose depth band contains normY **AND**
    // whose heat band is closest to targetHeat (OW = single heat value).
    static int PickDepthHeatArea(
        AreasDatabase db, ZoneType zone,
        float normY, float targetHeat, System.Random rng)
    {
        float best = float.MaxValue;
        List<int> ties = new();

        for (int i = 0; i < db.areas.Length; i++)
        {
            AreaData a = db.areas[i];
            if (a.zone != zone) continue;

            float diff;

            switch (zone)
            {
                case ZoneType.Overworld:
                    diff = Mathf.Abs(((AreaOverworldData)a).heat - targetHeat);
                    break;

                case ZoneType.Sky:
                {
                    var sw = (AreaSkyworldData)a;
                    if (normY < sw.minDepth || normY > sw.maxDepth) continue;

                    diff = targetHeat < sw.minHeat ? sw.minHeat - targetHeat
                         : targetHeat > sw.maxHeat ? targetHeat - sw.maxHeat
                         : 0f;
                    break;
                }

                case ZoneType.Underworld:
                {
                    var uw = (AreaUnderworldData)a;
                    if (normY < uw.minDepth || normY > uw.maxDepth) continue;

                    diff = targetHeat < uw.minHeat ? uw.minHeat - targetHeat
                         : targetHeat > uw.maxHeat ? targetHeat - uw.maxHeat
                         : 0f;
                    break;
                }

                default: continue;
            }

            if (diff < best - 1e-4f) { best = diff; ties.Clear(); ties.Add(i); }
            else if (Mathf.Abs(diff - best) < 1e-4f) ties.Add(i);
        }
        return ties.Count == 0 ? 0 : ties[rng.Next(ties.Count)];
    }

    /* ────────────────── HEAT-ONLY PICK (Overworld areas) ────────────────── */
    static int PickAreaByHeat(AreasDatabase db, float target, System.Random rng)
    {
        float best = float.MaxValue; List<int> ties = new();

        for (int i = 0; i < db.areas.Length; i++)
            if (db.areas[i] is AreaOverworldData ow)
            {
                float diff = Mathf.Abs(ow.heat - target);
                if (diff < best - 1e-4f) { best = diff; ties.Clear(); ties.Add(i); }
                else if (Mathf.Abs(diff - best) < 1e-4f) ties.Add(i);
            }
        return ties.Count == 0 ? 0 : ties[rng.Next(ties.Count)];
    }
}
