using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class WorldMapPOIGenerator
{
    static readonly Dictionary<BlueprintStructure, int> s_spawnCounts = new();
private const int kSurfaceSalt = unchecked((int)0x9E3779B9);
   /* ─────────────────────────  ENTRY  ───────────────────────── */
public static void PlaceAllPois(World world, WorldData data)
{
    s_spawnCounts.Clear();

    /* pick cell sizes – tweak as you like */
    const int cellSideGeneric = 5;
    const int cellSideSurface = 4;   // a bit denser on the surface

    // 1 — iterate over jittered cells for generic / cave POIs
    for (int cellY = 0; cellY < world.Data.heightInChunks; cellY += cellSideGeneric)
    for (int cellX = 0; cellX < world.Data.widthInChunks;  cellX += cellSideGeneric)
    {
        /* anchor inside this 5×5 cell */
        Vector2Int off = PickAnchorOffsetInCell(cellX, cellY, world.seed);
        int cx = cellX + off.x;
        int cy = cellY + off.y;

        Chunk ch = world.GetChunk(new Vector2Int(cx, cy));
        if (ch == null) continue;

        if (ch.HasFlags(ChunkFlags.Cave))
            PlaceCavePoiInChunk(world, ch, data);
        else
            PlaceSinglePoiInChunk(world, ch, data);
    }

    // 2 — iterate over surface cells (different size + salt)
    for (int cellY = 0; cellY < world.Data.heightInChunks; cellY += cellSideSurface)
    for (int cellX = 0; cellX < world.Data.widthInChunks;  cellX += cellSideSurface)
    {
        Vector2Int off = PickAnchorOffsetInCell(
                             cellX, cellY, world.seed ^ kSurfaceSalt,
                             cellSideSurface);
        int cx = cellX + off.x;
        int cy = cellY + off.y;

        Chunk ch = world.GetChunk(new Vector2Int(cx, cy));
        if (ch != null && ch.HasFlags(ChunkFlags.Surface))
            PlaceSurfacePoiInChunk(world, ch, data);
    }

    // 3 — after all markers are down, assign blueprints
    AssignStructuresToPOIs(world, data);
}

/* ───────────── jitter helper ───────────── */
/// Returns a deterministic offset (0 … cellSide-1)² for this cell.
private static Vector2Int PickAnchorOffsetInCell(
    int cellOriginX, int cellOriginY, int seed, int cellSide = 5)
{
    unchecked
    {
        /* tiny 32-bit mix – fast & deterministic */
        uint h = 0x811C9DC5u;
        h = (h ^ (uint)cellOriginX) * 0x01000193u;
        h = (h ^ (uint)cellOriginY) * 0x01000193u;
        h = (h ^ (uint)seed)       * 0x01000193u;
        h ^= h >> 13; h ^= h << 7; h ^= h >> 17;

        int offX = (int)(h        % (uint)cellSide);
        int offY = (int)((h >> 5) % (uint)cellSide);
        return new Vector2Int(offX, offY);
    }
}


    static void PlaceSinglePoiInChunk(World world, Chunk chunk, WorldData data)
    {
        TileData poiTile = data.tileDatabase.PointOfIntrestTile;
        if (poiTile == null) return;

        int lx = Random.Range(0, chunk.size);
        int ly = Random.Range(0, chunk.size);
        int wx = chunk.position.x * chunk.size + lx;
        int wy = chunk.position.y * chunk.size + ly;

        world.AddPointOfInterest(new PointOfInterest(new Vector2Int(wx, wy), chunk.GetFlags()));
    }

    static void PlaceSurfacePoiInChunk(World world, Chunk chunk, WorldData data)
    {
        if (!chunk.HasFlags(ChunkFlags.Surface)) return;

        TileData poiTile = data.tileDatabase.PointOfIntrestTile;
        if (poiTile == null) return;

        int cs = chunk.size;
        int lx = Random.Range(0, cs);

        for (int ly = cs - 1; ly >= 0; ly--)
        {
            int id = chunk.frontLayerTileIndexes[lx, ly];
            if (!IsAirTile(data, id))
            {
                int yAbove = ly + 1;
                if (yAbove < cs)
                {
                    chunk.frontLayerTileIndexes[lx, yAbove] = poiTile.tileID;
                    int wx = chunk.position.x * cs + lx;
                    int wy = chunk.position.y * cs + yAbove;
                    world.AddPointOfInterest(new PointOfInterest(new Vector2Int(wx, wy), chunk.GetFlags()));
                }
                return;
            }
        }
    }

    static void PlaceCavePoiInChunk(World world, Chunk chunk, WorldData data)
    {
        if (data.tileDatabase.UndergroundAirTile == null) return;

        int undergroundID = data.tileDatabase.UndergroundAirTile.tileID;

        for (int a = 0; a < 10; a++)
        {
            int lx = Random.Range(0, chunk.size);
            int ly = Random.Range(0, chunk.size);

            if (chunk.frontLayerTileIndexes[lx, ly] == undergroundID)
            {
                int wx = chunk.position.x * chunk.size + lx;
                int wy = chunk.position.y * chunk.size + ly;
                world.AddPointOfInterest(new PointOfInterest(new Vector2Int(wx, wy), chunk.GetFlags()));
                return;
            }
        }
    }

    static void AssignStructuresToPOIs(World world, WorldData data)
    {
        if (data.structureDatabase == null ||
            data.structureDatabase.structures == null ||
            data.structureDatabase.structures.Length == 0) return;

        var poiPositions = world.GetPointsOfInterest()?.Select(p => p.position).ToList();
        if (poiPositions == null || poiPositions.Count == 0) return;

        ShuffleList(poiPositions);

        foreach (Vector2Int pos in poiPositions)
        {
            Chunk chunk = WorldToChunk(world, pos.x, pos.y);
            if (chunk == null) continue;

            int lx = pos.x % chunk.size;
            int ly = pos.y % chunk.size;

            AreaData area = GetAreaAt(world, chunk, lx, ly, data);
            if (area == null) continue;

            var entry = PickStructureForArea(data.structureDatabase, area);
            if (entry == null) continue;

            if (!entry.CanSpawnWithFlags(chunk.GetFlags())) continue;

            if (!s_spawnCounts.ContainsKey(entry.structureBlueprint))
                s_spawnCounts[entry.structureBlueprint] = 0;
            if (s_spawnCounts[entry.structureBlueprint] >= entry.maxSpawnsPerWorld) continue;

            int tileID = world.GetTileID(pos.x, pos.y);
            if (!entry.structureBlueprint.canBePlacedInAir &&
                data.tileDatabase.SkyAirTile != null &&
                tileID == data.tileDatabase.SkyAirTile.tileID) continue;

            if (entry.structureBlueprint.CanPlaceStructure(world, pos.x, pos.y))
            {
                entry.structureBlueprint.PlaceStructure(world, pos.x, pos.y);
                s_spawnCounts[entry.structureBlueprint]++;
            }
        }
    }

    static StructureDatabase.StructureEntry PickStructureForArea(StructureDatabase db, AreaData area)
    {
        var list = new List<StructureDatabase.StructureEntry>();
        foreach (var e in db.structures)
            if (e.structureBlueprint != null && e.CanSpawnInArea(area))
                list.Add(e);

        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    static AreaData GetAreaAt(World world, Chunk chunk, int lx, int ly, WorldData data)
    {
        byte areaID = chunk.areaIDs[lx, ly];
        if (areaID >= data.areasDatabase.areas.Length) return null;
        return data.areasDatabase.areas[areaID];
    }

    static bool IsAirTile(WorldData data, int id)
    {
        if (id <= 0) return true;
        if (data.tileDatabase.SkyAirTile         != null && id == data.tileDatabase.SkyAirTile.tileID) return true;
        if (data.tileDatabase.UndergroundAirTile != null && id == data.tileDatabase.UndergroundAirTile.tileID) return true;
        return false;
    }

    static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    static Chunk WorldToChunk(World world, int wx, int wy)
    {
        if (wx < 0 || wy < 0 ||
            wx >= world.Data.widthInChunks * world.Data.chunkSize ||
            wy >= world.Data.heightInChunks * world.Data.chunkSize) return null;

        int cx = wx / world.Data.chunkSize;
        int cy = wy / world.Data.chunkSize;
        return world.GetChunk(new Vector2Int(cx, cy));
    }
}
