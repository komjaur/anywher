using UnityEngine;
using System;
using System.Collections.Generic;

/* ─────────────────────────── orchestrator ────────────────────────── */
public static class WorldPostProcessor
{
    private static readonly IPostPass[] _passes =
    {
        new PowderPass(),
        new LiquidPass(),
        new GrassPass(),
        new TreePass(),
        new VinePass(),
        new DecorPass(),
        new FoliagePass()
        
    };

    /// <summary>
    /// Runs every post-generation pass on every chunk in <paramref name="world"/>.
    /// Call right after WorldManager.GenerateWorldPost().
    /// </summary>
    public static void PostProcessWorld(World world, WorldData data)
    {
        TableCache.Clear();   // fresh drop-tables each run

        for (int cy = 0; cy < world.Data.heightInChunks; cy++)
        for (int cx = 0; cx < world.Data.widthInChunks;  cx++)
        {
            Vector2Int coord = new(cx, cy);
            Chunk chunk = world.GetChunk(coord);
            if (chunk == null) continue;

            foreach (var pass in _passes)
                pass.Execute(world, chunk, data);
        }
    }
}

/* ──────────────────────── common interface ───────────────────────── */
interface IPostPass
{
    void Execute(World world, Chunk chunk, WorldData data);
}

/* ─────────────────────────── Powder ─────────────────────────── */
/* ─────────────────────────── Powder ─────────────────────────── */
sealed class PowderPass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        int cs = chunk.size;
        for (int y = 0; y < cs; y++)
        for (int x = 0; x < cs; x++)
            CheckAndSettlePowder(world, chunk, data, x, y);
    }

    /* ------------------------------------------------------------------ */
    private static void CheckAndSettlePowder(World world, Chunk chunk,
                                             WorldData data, int lx, int ly)
    {
        int wx = chunk.position.x * chunk.size + lx;
        int wy = chunk.position.y * chunk.size + ly;

        int id = world.GetTileID(wx, wy);                 // returns liquid first
        if (id <= 0) return;

        TileData td = data.tileDatabase.GetTileDataByID(id);
        if (td == null || td.behavior != BlockBehavior.Powder) return;

        SettlePowderColumn(world, data, wx, wy);
    }

    /* ------------------------------------------------------------------ */
    private static void SettlePowderColumn(World world, WorldData data,
                                           int startX, int startY)
    {
        int startID = world.GetTileID(startX, startY);
        if (startID < 0) return;

        TileData td = data.tileDatabase.GetTileDataByID(startID);
        if (td == null || td.behavior != BlockBehavior.Powder) return;

        /* 1) gather the whole contiguous column of powder */
        int topY = startY;
        while (IsPowder(world, data, startX, topY + 1)) topY++;

        /* 2) count free (air + liquid) space below */
        int free = 0;
        while (IsAirOrLiquid(world, data, startX, startY - free - 1))
            free++;
        if (free == 0) return;

        /* 3) settle each powder tile downward, clearing liquid if needed */
        for (int y = startY; y <= topY; y++)
        {
            int oldID = world.GetTileID(startX, y);
            if (oldID <= 0) continue;

            int destY = y - free;

            /* NEW – if liquid occupies the destination, remove it first */
            if (world.GetLiquidID(startX, destY) > 0)
                world.SetLiquidID(startX, destY, 0);        // clear water/lava

            world.SetTileID(startX, destY, oldID);          // drop the powder
            world.SetTileID(startX, y, data.tileDatabase.UndergroundAirTile.tileID);
        }
    }

    /* ------------------------------------------------------------------ */
    private static bool IsPowder(World world, WorldData data, int x, int y)
    {
        int id = world.GetTileID(x, y);
        if (id <= 0) return false;
        TileData td = data.tileDatabase.GetTileDataByID(id);
        return td != null && td.behavior == BlockBehavior.Powder;
    }

    private static bool IsAirOrLiquid(World world, WorldData data, int x, int y)
    {
        int id = world.GetTileID(x, y);
        if (id < 0) return false;
        TileData td = data.tileDatabase.GetTileDataByID(id);
        if (td == null) return false;
        return td.behavior == BlockBehavior.Air || td.behavior == BlockBehavior.Liquid;
    }
}


/* ─────────────────────────── Liquid ─────────────────────────── */
/* ─────────────────────────── Liquid ─────────────────────────── */
/* ─────────────────────────── Liquid ─────────────────────────── */
sealed class LiquidPass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        int cs = chunk.size;
        for (int y = 0; y < cs; y++)
        for (int x = 0; x < cs; x++)
            CheckAndSpreadLiquid(world, chunk, data, x, y);   // ← pass data too
    }

    /* ----------------------------------------------------------- */
    private static void CheckAndSpreadLiquid(World world, Chunk chunk,
                                             WorldData data, int lx, int ly)
    {
        int wx = chunk.position.x * chunk.size + lx;
        int wy = chunk.position.y * chunk.size + ly;

        int liquidID = world.GetLiquidID(wx, wy);
        if (liquidID <= 0) return;

        SpreadLiquidFloodFill(world, data, wx, wy, liquidID); // ← pass data
    }

    /* bfs flood-fill + vine cleanup ----------------------------- */
    private static void SpreadLiquidFloodFill(World world, WorldData data,
                                              int startX, int startY,
                                              int liquidID)
    {
        int worldW = world.widthInChunks  * world.chunkSize;
        int worldH = world.heightInChunks * world.chunkSize;

        var q       = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        Vector2Int[] nbr = { new(0,-1), new(-1,0), new(1,0) };

        void Enqueue(Vector2Int p) { if (visited.Add(p)) q.Enqueue(p); }
        Enqueue(new Vector2Int(startX, startY));

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            foreach (var d in nbr)
            {
                int nx = cur.x + d.x;
                int ny = cur.y + d.y;
                if (nx < 0 || ny < 0 || nx >= worldW || ny >= worldH) continue;

                var np = new Vector2Int(nx, ny);
                if (visited.Contains(np)) continue;

                if (world.GetLiquidID(nx, ny) > 0) { visited.Add(np); continue; }
                if (!world.IsPassableForLiquid(nx, ny)) { visited.Add(np); continue; }

                /* put liquid */
                world.SetLiquidID(nx, ny, liquidID);

                /* NEW: remove any vines touched by this liquid */
                RemoveAdjacentVines(world, data, nx, ny);

                /* mark chunk */
                var cPos = new Vector2Int(nx / world.chunkSize, ny / world.chunkSize);
                world.GetChunk(cPos)?.AddFlags(ChunkFlags.Liquids);

                Enqueue(np);
            }
        }
    }

    /* -------- helper that nukes vines in and around (x,y) ------------ */
    private static void RemoveAdjacentVines(World world, WorldData data,
                                            int x, int y)
    {
        static IEnumerable<Vector2Int> Around(int cx, int cy)
        {
            yield return new(cx, cy);          // same cell
            yield return new(cx, cy + 1);
            yield return new(cx, cy - 1);
            yield return new(cx + 1, cy);
            yield return new(cx - 1, cy);
        }

        int airID = data.tileDatabase.UndergroundAirTile.tileID;

        foreach (var p in Around(x, y))
        {
            int id = world.GetTileID(p.x, p.y);
            if (id <= 0) continue;

            TileData td = data.tileDatabase.GetTileDataByID(id);
            if (td != null && td.tag == BlockTag.Vine)
                world.SetTileID(p.x, p.y, airID);
        }
    }
}



/* ─────────────────────────── Grass ─────────────────────────── */
sealed class GrassPass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        int cs = chunk.size;
        for (int y = 0; y < cs; y++)
        for (int x = 0; x < cs; x++)
            CheckAndConvertToGrass(world, chunk, data, x, y);
    }

    private static void CheckAndConvertToGrass(World world, Chunk chunk,
                                               WorldData data, int lx, int ly)
    {
        int id = chunk.frontLayerTileIndexes[lx, ly];
        if (id <= 0) return;

        TileData td = data.tileDatabase.GetTileDataByID(id);
        if (td == null || td.grassTile == null) return;

        int wx = chunk.position.x * chunk.size + lx;
        int wy = chunk.position.y * chunk.size + ly;

        AreaData area = world.GetArea(wx, wy);
        if (area == null) return;
        if (area.zone != ZoneType.Sky && area.zone != ZoneType.Overworld) return;

        int sky  = data.tileDatabase.SkyAirTile?.tileID          ?? -1;
        int cave = data.tileDatabase.UndergroundAirTile?.tileID ?? -1;

        int[] dx = { 0,-1, 1, 0 };
        int[] dy = { 1, 0, 0,-1 };

        for (int i = 0; i < 4; i++)
        {
            int nx = wx + dx[i];
            int ny = wy + dy[i];
            int nb = world.GetTileID(nx, ny);
            if (nb == sky || nb == cave)
            {
                chunk.frontLayerTileIndexes[lx, ly] = td.grassTile.tileID;
                break;
            }
        }
    }
}

/* ─────────────────────────── Vine ─────────────────────────── */
/* ────────────────────── Vine ────────────────────── */
sealed class VinePass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        int cs = chunk.size;
        for (int y = 0; y < cs; y++)
        for (int x = 0; x < cs; x++)
            CheckAndGrowVines(world, chunk, data, x, y);
    }

    private static void CheckAndGrowVines(World world, Chunk chunk,
                                          WorldData data, int lx, int ly)
    {
        int groundID = chunk.frontLayerTileIndexes[lx, ly];
        if (groundID == 0) return;

        int wx = chunk.position.x * chunk.size + lx;
        int wy = chunk.position.y * chunk.size + ly;

        /* NEW #1 – don’t start vines that are already under liquid */
        if (world.GetLiquidID(wx, wy) > 0) return;

        byte bID = chunk.biomeIDs[lx, ly];
        if (bID >= data.biomeDatabase.biomelist.Length) return;
        BiomeData biome = data.biomeDatabase.biomelist[bID];
        if (biome == null) return;

        AreaData area = world.GetArea(wx, wy);
        if (area == null) return;

        float chance = area.vineSpawnChance * biome.vineChanceMul;
        if (chance <= 0f || UnityEngine.Random.value > chance) return;

        TileData baseTD = data.tileDatabase.GetTileDataByID(groundID);
        if (baseTD == null || baseTD.vineTile == null) return;

        int len = UnityEngine.Random.Range(baseTD.vineMinLength,
                                           baseTD.vineMaxLength + 1);

        /* grow downward */
        int y = wy - 1;
        for (int i = 0; i < len && y >= 0; i++, y--)
        {
            /* NEW #2 – stop when we hit liquid */
            if (world.GetLiquidID(wx, y) > 0) break;

            int belowID  = world.GetTileID(wx, y);
            TileData td  = data.tileDatabase.GetTileDataByID(belowID);
            bool isAir   = td == null || td.behavior == BlockBehavior.Air;
            if (!isAir) break;

            world.SetTileID(wx, y, baseTD.vineTile.tileID);
        }
    }
}


/* ─────────────────────────── Decor ─────────────────────────── */
sealed class DecorPass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        CheckAndScatterDecor(world, chunk, data);
    }

    private static void CheckAndScatterDecor(World world, Chunk chunk, WorldData data)
    {
        if (!chunk.HasFlags(ChunkFlags.Cave)) return;  // only inside caves

        int cs  = chunk.size;
        int air = data.tileDatabase.UndergroundAirTile?.tileID ?? 1;

        for (int ly = 1; ly < cs; ly++)
        for (int lx = 0; lx < cs; lx++)
        {
            if (chunk.frontLayerTileIndexes[lx, ly]   != air) continue;
            if (chunk.frontLayerTileIndexes[lx, ly-1] == air) continue;

            byte bid = chunk.biomeIDs[lx, ly];
            if (bid >= data.biomeDatabase.biomelist.Length) continue;
            BiomeData biome = data.biomeDatabase.biomelist[bid];

            int wx = chunk.position.x * cs + lx;
            int wy = chunk.position.y * cs + ly;
            AreaData area = world.GetArea(wx, wy);
            if (area == null) continue;

            var tblA = TableCache.GetDecor(area,  area.Decor);
            var tblB = TableCache.GetDecor(biome, biome != null ? biome.Decor : null);
            if (tblA == null && tblB == null) continue;

            float chance = area.decorSpawnChance *
                           (biome != null ? biome.decorChanceMul : 1f);
            if (UnityEngine.Random.value > chance) continue;

            TileData deco =
                  tblA != null && tblB != null
                ? (UnityEngine.Random.value < 0.5f ? tblA.Roll() : tblB.Roll())
                : (tblA != null ? tblA.Roll() : tblB.Roll());

            if (deco != null)
                world.SetTileID(wx, wy, deco.tileID);
        }
    }
}

/* ─────────────────────────── Foliage ─────────────────────────── */
sealed class FoliagePass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        CheckAndPlaceFoliage(world, chunk, data);
    }

    private static void CheckAndPlaceFoliage(World world, Chunk chunk, WorldData data)
    {
        int cs  = chunk.size;
        int sky = data.tileDatabase.SkyAirTile?.tileID          ?? 0;
        int cave= data.tileDatabase.UndergroundAirTile?.tileID ?? 1;

        for (int ly = 0; ly < cs; ly++)
        for (int lx = 0; lx < cs; lx++)
        {
            int groundID = chunk.frontLayerTileIndexes[lx, ly];
            if (groundID == 0) continue;

            byte bid = chunk.biomeIDs[lx, ly];
            if (bid >= data.biomeDatabase.biomelist.Length) continue;
            BiomeData biome = data.biomeDatabase.biomelist[bid];

            int wx = chunk.position.x * cs + lx;
            int wy = chunk.position.y * cs + ly;
            AreaData area = world.GetArea(wx, wy);
            if (area == null) continue;

            var tblA = TableCache.GetFoliage(area,  area.Foliage);
            var tblB = TableCache.GetFoliage(biome, biome != null ? biome.Foliage : null);
            if (tblA == null && tblB == null) continue;

            float chance = area.foliageSpawnChance *
                           (biome != null ? biome.foliageChanceMul : 1f);
            if (UnityEngine.Random.value > chance) continue;

            /* overhead must be air */
            int aboveY = ly + 1;
            if (aboveY >= cs) continue;
            int aboveID = chunk.frontLayerTileIndexes[lx, aboveY];
            if (aboveID != sky && aboveID != cave) continue;

            /* roll */
            TileData chosen =
                  tblA != null && tblB != null
                ? (UnityEngine.Random.value < 0.5f ? tblA.Roll() : tblB.Roll())
                : (tblA != null ? tblA.Roll() : tblB.Roll());
            if (chosen == null) continue;

            /* ground whitelist */
            if (chosen.canPlaceOnTiles != null && chosen.canPlaceOnTiles.Count > 0)
            {
                bool ok = false;
                foreach (var g in chosen.canPlaceOnTiles)
                    if (g && g.tileID == groundID) { ok = true; break; }
                if (!ok) continue;
            }

            world.SetTileID(wx, wy + 1, chosen.tileID);
        }
    }
}

/* ─────────────────────────── Tree ─────────────────────────── */
sealed class TreePass : IPostPass
{
    public void Execute(World world, Chunk chunk, WorldData data)
    {
        CheckAndPlantTrees(world, chunk, data);
    }

    private static void CheckAndPlantTrees(World world, Chunk chunk, WorldData data)
    {
        int cs = chunk.size;

        for (int ly = 0; ly < cs; ly++)
        for (int lx = 0; lx < cs; lx++)
        {
            int groundID = chunk.frontLayerTileIndexes[lx, ly];
            if (groundID == 0) continue;

            byte bid = chunk.biomeIDs[lx, ly];
            if (bid >= data.biomeDatabase.biomelist.Length) continue;
            BiomeData biome = data.biomeDatabase.biomelist[bid];

            int wx = chunk.position.x * cs + lx;
            int wy = chunk.position.y * cs + ly;
            AreaData area = world.GetArea(wx, wy);
            if (area == null) continue;

            var tblA = TableCache.GetTrees(area,  area.Trees);
            var tblB = TableCache.GetTrees(biome, biome != null ? biome.Trees : null);
            if (tblA == null && tblB == null) continue;

            float chance = area.treeSpawnChance *
                           (biome != null ? biome.treeChanceMul : 1f);
            if (UnityEngine.Random.value > chance) continue;

            /* ensure space above ground */
            if (ly + 1 >= cs ||
    !world.IsAir(chunk.frontLayerTileIndexes[lx, ly + 1]))
    continue;

            BlueprintTree tree =
                  tblA != null && tblB != null
                ? (UnityEngine.Random.value < 0.5f ? tblA.Roll() : tblB.Roll())
                : (tblA != null ? tblA.Roll() : tblB.Roll());
            if (tree == null) continue;

            /* ground whitelist */
            if (tree.SuitableGroundTiles != null && tree.SuitableGroundTiles.Length > 0)
            {
                bool ok = false;
                foreach (var g in tree.SuitableGroundTiles)
                    if (g && g.tileID == groundID) { ok = true; break; }
                if (!ok) continue;
            }

            tree.PlaceStructure(world, wx, wy + 1);
        }
    }
}

