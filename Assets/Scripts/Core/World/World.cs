using UnityEngine;
using System;
using System.Collections.Generic;

/* ───────────────────────────────  DATA TYPES  ───────────────────────────── */

[Flags]
public enum ChunkFlags
{
    None      = 0,
    Surface   = 1 << 0,
    Cave      = 1 << 1,
    Liquids   = 1 << 2,
    ClearSky  = 1 << 3,
    CaveAir   = 1 << 4
}

/// <summary>Logical storage slots inside a chunk (rendering-agnostic).</summary>
public enum ChunkLayer
{
    Front,
    Background,
    Liquid,
    Overlay
}

public struct PointOfInterest
{
    public Vector2Int position;
    public ChunkFlags poiFlags;

    public PointOfInterest(Vector2Int pos, ChunkFlags flags)
    {
        position = pos;
        poiFlags = flags;
    }
}

public struct SavedUnit
{
    public UnitTemplate tpl;
    public Vector3      pos;
    public int          hp;
}

/* ────────────────────────────────  CHUNK  ───────────────────────────────── */

public class Chunk
{
    public bool IsDiscovered { get; private set; } = false;
    public void MarkDiscovered() => IsDiscovered = true;

    public readonly int  size;
    public readonly Vector2Int position;

    /* ─── TILE-ID GRIDS ─────────────────────────────────────────────── */
    public readonly int[,] frontLayerTileIndexes;      // foreground blocks (incl. ores)
    public readonly int[,] backgroundLayerTileIndexes; // walls / filler
    public readonly int[,] liquidLayerTileIndexes;     // water / lava / …
    public readonly int[,] overlayLayerTileIndexes;    // wires / logic

    public readonly List<SavedUnit> savedUnits = new();

    /* ─── META GRIDS ────────────────────────────────────────────────── */
    public readonly byte[,] biomeIDs;
    public readonly byte[,] areaIDs;

    private readonly HashSet<byte> chunkBiomes = new();
    private readonly HashSet<byte> chunkAreas  = new();
    private ChunkFlags flags = ChunkFlags.None;

    /* ─── CONSTRUCTOR ──────────────────────────────────────────────── */
    public Chunk(Vector2Int position, int size)
    {
        this.position = position;
        this.size     = size;

        frontLayerTileIndexes      = new int[size, size];
        backgroundLayerTileIndexes = new int[size, size];
        liquidLayerTileIndexes     = new int[size, size];
        overlayLayerTileIndexes    = new int[size, size];

        biomeIDs = new byte[size, size];
        areaIDs  = new byte[size, size];
    }

    /* ─── TILE-QUERY HELPERS ───────────────────────────────────────── */
    public bool IsCompletelySky(TileData skyAir)
    {
        if (skyAir == null) return false;
        int id = skyAir.tileID;
        for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
                if (frontLayerTileIndexes[x, y] != id) return false;
        return true;
    }

    public bool IsCompletelyUndergroundAir(TileData ugAir)
    {
        if (ugAir == null) return false;
        int id = ugAir.tileID;
        for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
                if (frontLayerTileIndexes[x, y] != id) return false;
        return true;
    }

    /* ─── GENERIC TILE ACCESSORS ───────────────────────────────────── */
    public int GetTile(ChunkLayer layer, int x, int y) => layer switch
    {
        ChunkLayer.Front      => frontLayerTileIndexes     [x, y],
        ChunkLayer.Background => backgroundLayerTileIndexes[x, y],
        ChunkLayer.Liquid     => liquidLayerTileIndexes    [x, y],
        ChunkLayer.Overlay    => overlayLayerTileIndexes   [x, y],
        _                     => -1
    };

    public void SetTile(ChunkLayer layer, int x, int y, int id)
    {
        switch (layer)
        {
            case ChunkLayer.Front:      frontLayerTileIndexes     [x, y] = id; break;
            case ChunkLayer.Background: backgroundLayerTileIndexes[x, y] = id; break;
            case ChunkLayer.Liquid:     liquidLayerTileIndexes    [x, y] = id; break;
            case ChunkLayer.Overlay:    overlayLayerTileIndexes   [x, y] = id; break;
            default: throw new ArgumentOutOfRangeException(nameof(layer));
        }
    }

    /* ─── FLAGS / ID SETS ──────────────────────────────────────────── */
    public ChunkFlags GetFlags()                 => flags;
    public void       SetFlags(ChunkFlags f)     => flags  = f;
    public void       AddFlags(ChunkFlags f)     => flags |= f;
    public void       RemoveFlags(ChunkFlags f)  => flags &= ~f;
    public bool       HasFlags(ChunkFlags f)     => (flags & f) == f;

    public IEnumerable<byte> GetBiomeIDs() => chunkBiomes;
    public IEnumerable<byte> GetAreaIDs()  => chunkAreas;

    public void AddBiomeID(byte b)   => chunkBiomes.Add(b);
    public void AddAreaID(byte a)    => chunkAreas.Add(a);
    public void ClearBiomeIDs()      => chunkBiomes.Clear();
    public void ClearAreaIDs()       => chunkAreas.Clear();

    /* ─── SAVED UNIT HELPERS ───────────────────────────────────────── */
    public void AddSavedUnit(SavedUnit u) => savedUnits.Add(u);
    public bool HasSavedUnits => savedUnits.Count > 0;
    public void ClearSaved()   => savedUnits.Clear();
}

/* ────────────────────────────────  WORLD  ───────────────────────────────── */

public class World
{
    /* ─────────────  PUBLIC READ-ONLY DATA  ───────────── */
    public WorldData Data { get; }

    public int chunkSize       => Data.chunkSize;
    public int widthInChunks   => Data.widthInChunks;
    public int heightInChunks  => Data.heightInChunks;

    // convenient aliases
    public TileDatabase      tiles      => Data.tileDatabase;
    public AreasDatabase     areas      => Data.areasDatabase;
    public BiomeDatabase     biomes     => Data.biomeDatabase;
    public StructureDatabase structures => Data.structureDatabase;
    public OresDatabase      ores       => Data.oresDatabase;
    public int               seed       => Data.seed;

    /* ─────────────  RUNTIME STATE  ───────────── */
    private readonly Dictionary<Vector2Int, Chunk> chunks = new();
    private readonly List<PointOfInterest> pointsOfInterest = new();

    /* ─────────────  CONSTRUCTOR  ───────────── */
    public World(WorldData data) =>
        Data = data ?? throw new ArgumentNullException(nameof(data));

    /* ─────────────  CHUNK HELPERS  ───────────── */
    public Chunk GetChunk(Vector2Int coord) =>
        chunks.TryGetValue(coord, out var c) ? c : null;

    public Chunk AddChunk(Vector2Int coord)
    {
        if (!chunks.TryGetValue(coord, out var chunk))
        {
            chunk = new Chunk(coord, chunkSize);
            chunks[coord] = chunk;
        }
        return chunk;
    }

    public void CreateAllChunks()
    {
        for (int cy = 0; cy < heightInChunks; ++cy)
            for (int cx = 0; cx < widthInChunks; ++cx)
                AddChunk(new Vector2Int(cx, cy));
    }

    /* ─────────────  TILE ACCESS BY LAYER  ───────────── */
    public int GetTileID(int wx, int wy, ChunkLayer layer = ChunkLayer.Front)
    {
        if (!InBounds(wx, wy)) return -1;
        Chunk ch = GetChunkAndLocal(wx, wy, out int lx, out int ly);
        return ch != null ? ch.GetTile(layer, lx, ly) : -1;
    }

    public void SetTileID(int wx, int wy, int tileID, ChunkLayer layer = ChunkLayer.Front)
    {
        if (!InBounds(wx, wy)) return;
        Chunk ch = GetChunkAndLocal(wx, wy, out int lx, out int ly);
        if (ch == null) return;

        ch.SetTile(layer, lx, ly, tileID);
        UpdateChunkFlags(ch, tileID, layer);
    }

    /* ─── LIQUID CONVENIENCE ───────────────────────────────────────── */
    public int  GetLiquidID(int wx, int wy)          => GetTileID(wx, wy, ChunkLayer.Liquid);
    public void SetLiquidID(int wx, int wy, int id)  => SetTileID(wx, wy, id, ChunkLayer.Liquid);

    public bool IsPassableForLiquid(int wx, int wy)
    {
        TileData tdFront = tiles.GetTileDataByID(GetTileID(wx, wy));
        bool frontPassable = tdFront == null ||
                             tdFront.behavior == BlockBehavior.Air ||
                             tdFront.tag == BlockTag.Vine;

        return frontPassable && GetLiquidID(wx, wy) <= 0;
    }

    /* ───── BIOME & AREA ACCESSORS ───── */
    public BiomeData GetBiome(int wx, int wy)
    {
        if (!InBounds(wx, wy)) return null;
        Chunk ch = GetChunkAndLocal(wx, wy, out int lx, out int ly);
        return ch != null ? biomes.GetBiomeData(ch.biomeIDs[lx, ly]) : null;
    }

    public AreaData GetArea(int wx, int wy)
    {
        if (!InBounds(wx, wy)) return null;
        Chunk ch = GetChunkAndLocal(wx, wy, out int lx, out int ly);
        return ch != null ? areas.GetAreaData(ch.areaIDs[lx, ly]) : null;
    }

    /* ─────────────  POINTS OF INTEREST  ───────────── */
    public void AddPointOfInterest(PointOfInterest poi) => pointsOfInterest.Add(poi);
    public IReadOnlyList<PointOfInterest> GetPointsOfInterest() => pointsOfInterest;

    /* ─────────────  INTERNAL UTILITIES  ───────────── */
    private bool InBounds(int wx, int wy) =>
        wx >= 0 && wy >= 0 &&
        wx < widthInChunks * chunkSize &&
        wy < heightInChunks * chunkSize;

    private Chunk GetChunkAndLocal(int wx, int wy, out int lx, out int ly)
    {
        int cx = wx / chunkSize, cy = wy / chunkSize;
        lx = wx % chunkSize;
        ly = wy % chunkSize;
        return GetChunk(new Vector2Int(cx, cy));
    }

    /* ─────────────  FLAG UPDATES  ───────────── */
    private void UpdateChunkFlags(Chunk ch, int tileID, ChunkLayer layer)
    {
        // We only watch front + liquid layers
        if (layer != ChunkLayer.Front && layer != ChunkLayer.Liquid) return;

        TileData td = tiles.GetTileDataByID(tileID);
        if (td == null) return;

        /* per-tile flags --------------------------------------------- */
        if (layer == ChunkLayer.Front)
        {
            if (tiles.SkyAirTile         && tileID == tiles.SkyAirTile.tileID)
                ch.AddFlags(ChunkFlags.Surface);

            if (tiles.UndergroundAirTile && tileID == tiles.UndergroundAirTile.tileID)
                ch.AddFlags(ChunkFlags.Cave);
        }

        if (layer == ChunkLayer.Liquid || td.behavior == BlockBehavior.Liquid)
            ch.AddFlags(ChunkFlags.Liquids);

        /* whole-chunk flags (front changes only) ---------------------- */
        if (layer == ChunkLayer.Front)
        {
            // ClearSky
            if (ch.IsCompletelySky(tiles.SkyAirTile))
                ch.AddFlags(ChunkFlags.ClearSky);
            else
                ch.RemoveFlags(ChunkFlags.ClearSky);

            // CaveAir
            if (ch.IsCompletelyUndergroundAir(tiles.UndergroundAirTile))
                ch.AddFlags(ChunkFlags.CaveAir);
            else
                ch.RemoveFlags(ChunkFlags.CaveAir);
        }
    }

    /* ─────────────  SEMANTIC HELPERS  ───────────── */
    public bool IsUndergroundAir(int id)
    {
        var ua = tiles.UndergroundAirTile;
        return id > 0 && ua != null && id == ua.tileID;
    }

    public bool IsLiquid(int id)
    {
        TileData td = tiles.GetTileDataByID(id);
        return td != null && td.behavior == BlockBehavior.Liquid;
    }

    /* -------------------------------------------------
       Returns the tile ID *visible* to the lighting
       system at (wx, wy), using the priority:
       Overlay ▸ Liquid ▸ Front ▸ Background
    -------------------------------------------------- */
    public int GetLightingTileID(int wx, int wy)
    {
        int id = GetTileID(wx, wy, ChunkLayer.Overlay);
        if (id > 0) return id;

        id = GetTileID(wx, wy, ChunkLayer.Liquid);
        if (id > 0) return id;

        id = GetTileID(wx, wy, ChunkLayer.Front);
        if (id > 0) return id;

        return GetTileID(wx, wy, ChunkLayer.Background); // may be 0 (empty)
    }

    /* -------------------------------------------------
       Helper – returns TRUE if the given front-layer
       tile ID counts as air.
    -------------------------------------------------- */
    public bool IsAir(int tileID)
    {
        if (tileID <= 0) return true;                // empty
        TileData td = tiles.GetTileDataByID(tileID);
        return td != null && td.behavior == BlockBehavior.Air;
    }
}
