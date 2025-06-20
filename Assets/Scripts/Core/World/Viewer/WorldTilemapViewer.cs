using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Keeps a sliding “window” of chunks around the camera in memory & on-screen.
/// ─ Fully draws the interior rectangle (<see cref="_radius" />)
/// ─ Draws Solid + Liquid layers only in the padding band (<see cref="_padding" />)
/// ─ Drops everything outside radius + padding
///
/// Fires:
///   • <see cref="ChunkRendered" /> – first time a chunk appears after being off-screen  
///   • <see cref="ChunkCleared"  /> – when a previously rendered chunk is removed
/// </summary>
public sealed class WorldTilemapViewer : MonoBehaviour
{
    /* ───────── inspector tunables ───────── */
    [SerializeField] Vector2Int defaultRadius  = new(4, 2);   // fully rendered
    [SerializeField] Vector2Int defaultPadding = new(2, 1);   // collision-only band

    /* ───────── public events ───────── */
    public event Action<Vector2Int, bool> ChunkRendered;
    public event Action<Vector2Int>       ChunkCleared;

    /* ───────── internal state ───────── */
    Vector2Int _radius, _padding;

    Camera _cam;
    World  _world;

    Grid      _grid;
    Tilemap[] _maps;


    HashSet<Vector2Int> _rendered = new();
    HashSet<Vector2Int> _collisionOnly = new();
    HashSet<Vector2Int> _scratch = new();
    readonly List<Vector2Int> _outOfView = new();

    Vector2Int _lastCenter = new(int.MinValue, int.MinValue);
    bool _dirtyConfig;

    readonly List<Vector3Int>[] _pos   = new List<Vector3Int>[(int)RenderGroup.Count];
    readonly List<TileBase>[]   _tiles = new List<TileBase>  [(int)RenderGroup.Count];
    TileBase[] _clearBlock;

    /* ───────── lighting hook ───────── */
    LightingSystem _lighting;

    /* ───────── public API ───────── */
    public IReadOnlyCollection<Vector2Int> RenderedChunks  => _rendered;
    public IReadOnlyList<Vector2Int>       OutOfViewChunks => _outOfView;
    public Tilemap this[RenderGroup g] => _maps[(int)g];
    public World   CurrentWorld       => _world;

    public bool ChunkIsVisible(Vector2Int ck) =>
        _rendered.Contains(ck) && !_collisionOnly.Contains(ck);

    /* ───────── initialisation ───────── */

    public void Initialize(WorldManager wm)
    {
        _world   = wm.GetCurrentWorld();
        _radius  = defaultRadius;
        _padding = defaultPadding;

        BuildLayerStack();
        _clearBlock = new TileBase[_world.chunkSize * _world.chunkSize];

        /* Lighting system */
        _lighting = new GameObject("Lighting").AddComponent<LightingSystem>();
        _lighting.Initialize(this);

        wm.WorldReady += _ => RefreshAround(FocusChunk());
    }

    /* Keep inspector tweaks live while running */
    void Update()
    {
        _radius  = defaultRadius;
        _padding = defaultPadding;
    }

    public void SetCamera(Camera cam)
    {
        _cam        = cam;
        _lastCenter = new(int.MinValue, int.MinValue);
    }

    public void SetRenderRadius(Vector2Int r)
    {
        if (r != _radius) { _radius = r; _dirtyConfig = true; }
    }
    public void SetOutOfViewPadding(Vector2Int p)
    {
        if (p != _padding) { _padding = p; _dirtyConfig = true; }
    }

    /* ───────── Unity loop ───────── */

    void LateUpdate()
    {
        if (_world == null || _cam == null) return;

        Vector2Int center = FocusChunk();
        if (center != _lastCenter || _dirtyConfig)
        {
            _lastCenter  = center;
            _dirtyConfig = false;
            RefreshAround(center);
        }
    }

    /* ───────── refresh logic ───────── */

    void RefreshAround(Vector2Int center)
    {
        int rangeX = _radius.x + _padding.x;
        int rangeY = _radius.y + _padding.y;

        /* 1) collision-only band */
        _outOfView.Clear();
        for (int dy = -rangeY; dy <= rangeY; ++dy)
            for (int dx = -rangeX; dx <= rangeX; ++dx)
                if (Mathf.Abs(dx) > _radius.x || Mathf.Abs(dy) > _radius.y)
                    _outOfView.Add(new Vector2Int(center.x + dx, center.y + dy));

        /* 2) working set (radius + padding) */
        _scratch.Clear();
        for (int dy = -rangeY; dy <= rangeY; ++dy)
            for (int dx = -rangeX; dx <= rangeX; ++dx)
                _scratch.Add(new Vector2Int(center.x + dx, center.y + dy));

        /* 3) redraw / mode-swap */
        HashSet<Vector2Int> newCollisionOnly = new(_outOfView);
        int newlyDiscovered = 0;

        foreach (var ck in _scratch)
        {
            bool nowEdge  = newCollisionOnly.Contains(ck);
            bool wasEdge  = _collisionOnly.Contains(ck);
            bool rendered = _rendered.Contains(ck);

            Chunk ch = _world.GetChunk(ck);
            if (ch == null) continue;

            if (rendered && (nowEdge ^ wasEdge)) ClearChunk(ch);
            if (!rendered || (nowEdge ^ wasEdge))
            {
                DrawChunk(ch, nowEdge);

                if (!rendered)
                {
                    bool firstTime = !ch.IsDiscovered;
                    if (firstTime)
                    {
                        ch.MarkDiscovered();
                        newlyDiscovered++;
                    }
                    ChunkRendered?.Invoke(ck, firstTime);
                }
            }
        }

        /* Batched exploration XP */
        if (newlyDiscovered > 0 && GameManager.Instance != null)
        {
            const int xpPerChunk = 3;
            GameManager.Instance.SkillManager?
                .AddXp(SkillId.Exploration, newlyDiscovered * xpPerChunk);
        }

        /* 4) purge chunks that slid outside padding */
        foreach (var ck in _rendered)
            if (!_scratch.Contains(ck))
            {
                Chunk ch = _world.GetChunk(ck);
                if (ch != null) ClearChunk(ch);
                ChunkCleared?.Invoke(ck);
            }

        /* 5) swap tracking sets */
            /* 5) swap tracking sets ------------------------------------------- */
    (_rendered, _scratch) = (_scratch, _rendered);
    _collisionOnly = newCollisionOnly;
    }

    /* ───────── draw helpers ───────── */

    void DrawChunk(Chunk ch, bool collisionOnly)
    {
        int cs  = ch.size;
        int wx0 = ch.position.x * cs;
        int wy0 = ch.position.y * cs;

        for (int g = 0; g < _pos.Length; ++g)
        { _pos[g].Clear(); _tiles[g].Clear(); }

        for (int y = 0; y < cs; ++y)
            for (int x = 0; x < cs; ++x)
            {
                int wx = wx0 + x, wy = wy0 + y;

                if (collisionOnly)
                {
                    /* solid */
                    int idF = ch.frontLayerTileIndexes[x, y];
                    if (idF > 0)
                    {
                        var td = _world.tiles.GetTileDataByID(idF);
                        if (td && td.renderLayer == RenderGroup.Solid)
                            Add(wx, wy, idF, RenderGroup.Solid);
                    }
                    /* liquid */
                    int idL = ch.liquidLayerTileIndexes[x, y];
                    if (idL > 0) Add(wx, wy, idL, RenderGroup.Liquid);
                    continue;
                }

                /* full visuals */
                Add(wx, wy, ch.backgroundLayerTileIndexes[x, y], RenderGroup.Wall);
                Add(wx, wy, ch.liquidLayerTileIndexes   [x, y], RenderGroup.Liquid);
                Add(wx, wy, ch.overlayLayerTileIndexes  [x, y], RenderGroup.Wiring);

                int idFront = ch.frontLayerTileIndexes[x, y];
                if (idFront > 0)
                {
                    var td = _world.tiles.GetTileDataByID(idFront);
                    Add(wx, wy, idFront, td ? td.renderLayer : RenderGroup.Solid);
                }
            }

        for (int g = 0; g < _maps.Length; ++g)
            if (_pos[g].Count > 0)
                _maps[g].SetTiles(_pos[g].ToArray(), _tiles[g].ToArray());
    }

    void ClearChunk(Chunk ch)
    {
        int cs  = ch.size;
        int wx0 = ch.position.x * cs;
        int wy0 = ch.position.y * cs;
        var area = new BoundsInt(wx0, wy0, 0, cs, cs, 1);
        foreach (var map in _maps)
            map.SetTilesBlock(area, _clearBlock);
    }

    void Add(int wx, int wy, int id, RenderGroup g)
    {
        if (id <= 0) return;
        var td = _world.tiles.GetTileDataByID(id);
        if (td == null) return;
        _pos  [(int)g].Add(new Vector3Int(wx, wy, 0));
        _tiles[(int)g].Add(td.tileBase);
    }
      static class SpriteTileCache
    {
        static readonly Dictionary<Sprite, TileBase> cache = new();

        public static TileBase FromSprite(Sprite sprite)
        {
            if (sprite == null) return null;
            if (!cache.TryGetValue(sprite, out var tile))
            {
                var t  = ScriptableObject.CreateInstance<Tile>();
                t.sprite = sprite;
                cache[sprite] = t;
                tile = t;
            }
            return tile;
        }
    }
    public void SetOverlayTile(int wx, int wy, Sprite sprite)
    {
        var map = _maps[(int)RenderGroup.Overlay];
        Vector3Int pos = new(wx, wy, 0);

        map.SetTile(pos, null);                      // always clear first
        if (sprite) map.SetTile(pos, SpriteTileCache.FromSprite(sprite));
    }
    /* ───────── layer-stack setup ───────── */

   void BuildLayerStack()
    {
        if (_grid) return;

        _grid = new GameObject("Grid", typeof(Grid)).GetComponent<Grid>();
        _grid.transform.SetParent(transform, false);

        _maps = new Tilemap[(int)RenderGroup.Count];

        for (int i = 0; i < _maps.Length; ++i)
        {
            var go = new GameObject(((RenderGroup)i).ToString(),
                                     typeof(Tilemap),
                                     typeof(TilemapRenderer));
            go.transform.SetParent(_grid.transform, false);

            _maps[i] = go.GetComponent<Tilemap>();
            var rend = go.GetComponent<TilemapRenderer>();

            if ((RenderGroup)i == RenderGroup.Overlay)
                rend.sortingOrder = 1000;                               // always on top
            else
                rend.sortingOrder = i;

            if ((RenderGroup)i == RenderGroup.Solid)
            {
                go.layer = LayerMask.NameToLayer("Ground");
                var col = go.AddComponent<TilemapCollider2D>();
#if UNITY_6000_1_OR_NEWER
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
                col.usedByComposite = true;
#endif
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                go.AddComponent<CompositeCollider2D>().geometryType =
                        CompositeCollider2D.GeometryType.Polygons;
            }

            _pos  [i] = new List<Vector3Int>(256);
            _tiles[i] = new List<TileBase>  (256);
        }
    }


    /* ───────────────────────────────────────────────────────────────
       Apply a *single* tile change already written to the World data.
       Call this right after world.SetTileID(…)
    ───────────────────────────────────────────────────────────────*/
    public void ApplyTileEdit(int wx, int wy)
    {
        int cs = _world.chunkSize;
        var ck = new Vector2Int(wx / cs, wy / cs);

        /* chunk off-screen? bail */
        if (!_rendered.Contains(ck)) return;

        /* edge band → cheap full redraw */
        if (_collisionOnly.Contains(ck))
        {
            Chunk chEdge = _world.GetChunk(ck);
            ClearChunk(chEdge);
            DrawChunk(chEdge, true);
            _lighting?.InvalidateLighting();
            return;
        }

        /* update one cell inside fully rendered area */
        Chunk ch = _world.GetChunk(ck);
        int lx = wx - ck.x * cs;
        int ly = wy - ck.y * cs;

        int idBack   = ch.backgroundLayerTileIndexes[lx, ly];
        int idLiquid = ch.liquidLayerTileIndexes   [lx, ly];
        int idOver   = ch.overlayLayerTileIndexes  [lx, ly];
        int idFront  = ch.frontLayerTileIndexes    [lx, ly];

        Vector3Int pos = new(wx, wy, 0);

        for (int g = 0; g < (int)RenderGroup.Count; ++g)
            _maps[g].SetTile(pos, null);

        if (idBack > 0)
        {
            var td = _world.tiles.GetTileDataByID(idBack);
            if (td) _maps[(int)RenderGroup.Wall].SetTile(pos, td.tileBase);
        }
        if (idLiquid > 0)
        {
            var td = _world.tiles.GetTileDataByID(idLiquid);
            if (td) _maps[(int)RenderGroup.Liquid].SetTile(pos, td.tileBase);
        }
        if (idOver > 0)
        {
            var td = _world.tiles.GetTileDataByID(idOver);
            if (td) _maps[(int)RenderGroup.Wiring].SetTile(pos, td.tileBase);
        }
        if (idFront > 0)
        {
            var td = _world.tiles.GetTileDataByID(idFront);
            if (td)
            {
                RenderGroup grp = td.renderLayer != RenderGroup.None
                                ? td.renderLayer
                                : RenderGroup.Solid;
                _maps[(int)grp].SetTile(pos, td.tileBase);
            }
        }

        /* notify lighting */
        _lighting?.MarkTileDirty(wx, wy);
    }
    /// <summary>Collect emissive tiles inside the given chunks.</summary>
    public void CollectChunkLights(HashSet<Vector2Int> chunks,
                                   Vector2Int min, Vector2Int max,
                                   ref Color[] buffer,
                                   Queue<(Vector2Int, Color)> q)
    {
        int cs = _world.chunkSize;
        int w = max.x - min.x;
        TileDatabase db = _world.tiles;

        foreach (var ck in chunks)
        {
            Chunk ch = _world.GetChunk(ck);
            if (ch == null) continue;

            for (int y = 0; y < cs; ++y)
                for (int x = 0; x < cs; ++x)
                {
                    int fid = ch.frontLayerTileIndexes[x, y];
                    int bid = ch.backgroundLayerTileIndexes[x, y];

                    TileData td = db.GetTileDataByIndex(fid),
                             bd = db.GetTileDataByIndex(bid);

                    TileData emitter = td ? td : bd;
                    if (emitter == null || emitter.lightStrength <= 0f) continue;

                    int wx = ck.x * cs + x;
                    int wy = ck.y * cs + y;
                    int idx = (wy - min.y) * w + (wx - min.x);

                    Color energy = emitter.lightColor * emitter.lightStrength;
                    if (energy.maxColorComponent <= buffer[idx].maxColorComponent) continue;

                    buffer[idx] = energy;
                    q.Enqueue((new Vector2Int(wx - min.x, wy - min.y), energy));
                }
        }
    }

    /// <summary>Return opacity (0 transparent–1 opaque) of the tile at world-tile (wx, wy).</summary>
    public float GetTileFalloff(int wx, int wy)
{
    int cs = _world.chunkSize;

    // Correct – floor-division, works for negatives
    int cx = Mathf.FloorToInt((float)wx / cs);
    int cy = Mathf.FloorToInt((float)wy / cs);

    Chunk ch = _world.GetChunk(new Vector2Int(cx, cy));
    if (ch == null) return 1f;

    int lx = wx - cx * cs;   // 0 … cs-1 guaranteed
    int ly = wy - cy * cs;

    TileData td = _world.tiles.GetTileDataByIndex(ch.frontLayerTileIndexes[lx, ly]);
    TileData bd = _world.tiles.GetTileDataByIndex(ch.backgroundLayerTileIndexes[lx, ly]);

    if (td) return td.lightFalloff;
    if (bd) return bd.lightFalloff;
    return 0.1f;
}
    /* ───────── misc helpers ───────── */

    Vector2Int FocusChunk()
    {
        int cs = _world.chunkSize;
        return new(
            Mathf.FloorToInt(_cam.transform.position.x) / cs,
            Mathf.FloorToInt(_cam.transform.position.y) / cs);
    }

    /* ───────── editor gizmo aid (unchanged) ───────── */
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (_world == null) return;

        int cs  = _world.chunkSize;
        float sz = cs * 0.9f;
        Vector3 half = new(sz * 0.5f, sz * 0.5f, 0);

        (ChunkFlags flag, Color col)[] layers =
        {
            (ChunkFlags.ClearSky, new Color(0.60f,0.90f,1.00f,0.60f)),
            (ChunkFlags.Surface , new Color(0.00f,1.00f,0.00f,0.60f)),
            (ChunkFlags.Liquids , new Color(0.00f,0.50f,1.00f,0.60f)),
            (ChunkFlags.Cave    , new Color(1.00f,0.00f,1.00f,0.60f)),
            (ChunkFlags.CaveAir , new Color(1.00f,1.00f,1.00f,0.30f))
        };

        foreach (var ck in _outOfView)
        {
            Chunk ch = _world.GetChunk(ck);
            if (ch == null) continue;

            Vector3 center = new(
                ck.x * cs + cs * 0.5f,
                ck.y * cs + cs * 0.5f, 0f);

            ChunkFlags f = ch.GetFlags();
            int layer = 0;

            foreach (var (flag, col) in layers)
            {
                if ((f & flag) == 0) continue;
                Gizmos.color = col;
                Gizmos.DrawCube(center + Vector3.forward * (-0.05f * layer++), half);
            }

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center, new Vector3(cs, cs, 0));
        }
    }
#endif
}
