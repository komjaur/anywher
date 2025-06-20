/*  MapPreviewer.cs
 *  ---------------------------------------------------------------------------
 *  – Shows every storage layer of a procedurally-generated World in-editor
 *  – Layer order (back ➜ front):
 *        1) background      2) liquids      3) front blocks
 *        4) ore decals      5) overlay / wiring
 *  – Also supports biome / area colour maps and POI gizmos
 *  -------------------------------------------------------------------------*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;      // Handles.Label in gizmos
#endif

[RequireComponent(typeof(SpriteRenderer))]
public sealed class MapPreviewer : MonoBehaviour
{
    /* ─────────────  SPRITERENDERERS & RUNTIME TEXTURES  ───────────── */
    [Header("Per-layer SpriteRenderers (left null = auto create)")]
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private SpriteRenderer liquidRenderer;
    [SerializeField] private SpriteRenderer frontRenderer;
    [SerializeField] private SpriteRenderer oreRenderer;
    [SerializeField] private SpriteRenderer overlayRenderer;

    private Texture2D backgroundTex;
    private Texture2D liquidTex;
    private Texture2D frontTex;
    private Texture2D oreTex;
    private Texture2D overlayTex;

    /* single-sprite canvas for biome / area maps */
    private SpriteRenderer biomeSpriteRenderer;
    private Texture2D      biomeTex;

    /* optional colour sources */
    public AreasDatabase areasDatabase;
    public BiomeDatabase biomeDatabase;

    /* POI gizmo support */
    public World  currentWorld;
    [Header("POI gizmo settings")]
    public Color  poiColor  = Color.yellow;
    public float  poiRadius = 2f;

    /* ─────────────────────────────  LIFECYCLE  ────────────────────── */
    void Awake()
    {
        /* this renderer is only used for single-sprite biome/area view */
        biomeSpriteRenderer = GetComponent<SpriteRenderer>();
        biomeSpriteRenderer.sortingOrder = -300;

        EnsureRenderer(ref backgroundRenderer, "BackgroundLayer", -205);
        EnsureRenderer(ref liquidRenderer,     "LiquidLayer",     -204);
        EnsureRenderer(ref frontRenderer,      "FrontLayer",      -203);
        EnsureRenderer(ref oreRenderer,        "OreLayer",        -202);
        EnsureRenderer(ref overlayRenderer,    "OverlayLayer",    -201);
    }

    void EnsureRenderer(ref SpriteRenderer r, string name, int order)
    {
        if (r != null) { r.sortingOrder = order; return; }

        var go = new GameObject(name);
        go.transform.SetParent(transform);
        r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = order;
    }

    /* ─────────────────────────────  PUBLIC API  ───────────────────── */

    /// <summary>Draws all world layers (background, liquid, front, ore, overlay).</summary>
    public void RenderWorldCanvasBoth(World world)
    {
        if (!Application.isPlaying || world == null) return;

        PositionFor(world);
        biomeSpriteRenderer.enabled = false;            // hide area / biome sheet

        RenderLayer(world, ChunkLayer.Background, ref backgroundTex, backgroundRenderer);
        RenderLayer(world, ChunkLayer.Liquid,     ref liquidTex,     liquidRenderer);
        RenderLayer(world, ChunkLayer.Front,      ref frontTex,      frontRenderer);

        RenderLayer(world, ChunkLayer.Overlay,    ref overlayTex,    overlayRenderer);

        currentWorld = world;                           // POI gizmos
    }

    /// <summary>Draw colour-coded chunk-area map.</summary>
    public void RenderAreaCanvas(World world)
    {
        if (!Application.isPlaying || world == null) return;
        PositionFor(world);
        GenerateAreaOrBiomeTexture(world, isBiome: false);
    }

    /// <summary>Draw colour-coded biome map.</summary>
    public void RenderBiomeCanvas(World world)
    {
        if (!Application.isPlaying || world == null) return;
        PositionFor(world);
        GenerateAreaOrBiomeTexture(world, isBiome: true);
    }

    /* ─────────────────────────────  INTERNALS  ────────────────────── */

    /* ----- world-layer renderer ------------------------------------ */
    void RenderLayer(World world, ChunkLayer layer,
                     ref Texture2D tex, SpriteRenderer sr)
    {
        int W = world.widthInChunks  * world.chunkSize;
        int H = world.heightInChunks * world.chunkSize;

        int[,] ids = new int[W, H];

        for (int cy = 0; cy < world.heightInChunks; ++cy)
        for (int cx = 0; cx < world.widthInChunks;  ++cx)
        {
            Chunk ck = world.GetChunk(new Vector2Int(cx, cy));
            if (ck == null) continue;

            int wx0 = cx * world.chunkSize;
            int wy0 = cy * world.chunkSize;

            for (int ly = 0; ly < ck.size; ++ly)
            for (int lx = 0; lx < ck.size; ++lx)
                ids[wx0 + lx, wy0 + ly] = ck.GetTile(layer, lx, ly);
        }

        BlitIDsToTexture(ids, world.tiles, ref tex, sr);
    }

    /* ----- area / biome colour sheet ------------------------------- */
    void GenerateAreaOrBiomeTexture(World world, bool isBiome)
    {
        int W = world.widthInChunks * world.chunkSize;
        int H = world.heightInChunks * world.chunkSize;

        if (biomeTex == null || biomeTex.width != W || biomeTex.height != H)
        {
            if (biomeTex) Destroy(biomeTex);
            biomeTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                       { filterMode = FilterMode.Point };
            biomeSpriteRenderer.sprite = Sprite.Create(
                biomeTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f, 0,
                SpriteMeshType.FullRect);
        }

        Color32[] px = new Color32[W * H];

        for (int cy = 0; cy < world.heightInChunks; ++cy)
        for (int cx = 0; cx < world.widthInChunks;  ++cx)
        {
            Chunk ck = world.GetChunk(new Vector2Int(cx, cy));
            if (ck == null) continue;

            int wx0 = cx * world.chunkSize;
            int wy0 = cy * world.chunkSize;

            for (int ly = 0; ly < ck.size; ++ly)
            for (int lx = 0; lx < ck.size; ++lx)
            {
                byte id = isBiome ? ck.biomeIDs[lx, ly]
                                  : ck.areaIDs [lx, ly];
                px[(wy0 + ly) * W + (wx0 + lx)] =
                    isBiome ? ColorForBiome(id) : ColorForArea(id);
            }
        }

        biomeTex.SetPixels32(px);
        biomeTex.Apply(false);
        biomeSpriteRenderer.enabled = true;
    }

    /* ----- helpers -------------------------------------------------- */
    void PositionFor(World w)
    {
        float ox = w.widthInChunks  * w.chunkSize * 0.5f;
        float oy = w.heightInChunks * w.chunkSize * 0.5f;
        transform.position = new Vector3(ox, oy, 0f);
    }

    void BlitIDsToTexture(int[,] ids, TileDatabase db,
                          ref Texture2D tex, SpriteRenderer sr)
    {
        int W = ids.GetLength(0), H = ids.GetLength(1);

        if (tex == null || tex.width != W || tex.height != H)
        {
            if (tex) Destroy(tex);
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                   { filterMode = FilterMode.Point };
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, W, H),
                                      new Vector2(0.5f, 0.5f), 1f, 0,
                                      SpriteMeshType.FullRect);
            sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        Color32[] px = new Color32[W * H];
        int k = 0;
        for (int y = 0; y < H; ++y)
        for (int x = 0; x < W; ++x)
        {
            int id = ids[x, y];
            px[k++] = id > 0 ? (Color32)db.GetTileDataByID(id).color
                             : new Color32(0, 0, 0, 0);
        }

        tex.SetPixels32(px);
        tex.Apply(false);
    }

    Color32 ColorForArea(byte id)
    {
        if (areasDatabase && areasDatabase.areas != null &&
            id < areasDatabase.areas.Length)
            return (Color32)areasDatabase.areas[id].color;
        return new Color32(0, 0, 0, 0);
    }

    Color32 ColorForBiome(byte id)
    {
        if (biomeDatabase && biomeDatabase.biomelist != null &&
            id < biomeDatabase.biomelist.Length && biomeDatabase.biomelist[id])
            return (Color32)biomeDatabase.biomelist[id].color;
        return new Color32(0, 0, 0, 0);
    }

    /* ─────────────────────────────  GIZMOS  ───────────────────────── */
void OnDrawGizmos()
{
    if (currentWorld == null) return;

    /* ───── 1. Points-of-interest ───── */
    var pois = currentWorld.GetPointsOfInterest();
    if (pois != null && pois.Count > 0)
    {
        foreach (var poi in pois)
        {
            Color col = poiColor;

            if ((poi.poiFlags & ChunkFlags.ClearSky) != 0)        col = new Color(0.6f, 0.9f, 1f);  // sky-blue
            else if ((poi.poiFlags & ChunkFlags.Surface) != 0)    col = Color.green;
            else if ((poi.poiFlags & ChunkFlags.CaveAir) != 0)    col = new Color(1f, 0.6f, 0f);    // orange
            else if ((poi.poiFlags & ChunkFlags.Cave) != 0)       col = Color.red;
            else if ((poi.poiFlags & ChunkFlags.Liquids) != 0)    col = Color.blue;

            Gizmos.color = col;
            Vector3 pos = new Vector3(poi.position.x, poi.position.y, 0f);
            Gizmos.DrawSphere(pos, poiRadius);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up * 0.25f, poi.poiFlags.ToString());
#endif
        }
    }

    /* ───── 2. Chunk-flag cubes ───── */
    int cs = currentWorld.chunkSize;
    float box = cs * 0.9f;
    Vector3 half = new(box * 0.5f, box * 0.5f, 0);

    for (int cy = 0; cy < currentWorld.heightInChunks; ++cy)
    for (int cx = 0; cx < currentWorld.widthInChunks;  ++cx)
    {
        var ck = new Vector2Int(cx, cy);
        var ch = currentWorld.GetChunk(ck);
        if (ch == null) continue;

        ChunkFlags f = ch.GetFlags();
        if (f == ChunkFlags.None) continue;

        Vector3 centre = new(
            cx * cs + cs * 0.5f,
            cy * cs + cs * 0.5f,
            0f);

        int layer = 0;        // z-offset stack (keeps colours visible)

                /*if ((f & ChunkFlags.ClearSky) != 0)
                {
                    Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.25f);            // sky-blue
                    Gizmos.DrawCube(centre + Vector3.forward * (-0.05f * layer++), half);
                }
                if ((f & ChunkFlags.Surface) != 0)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.25f);                // green
                    Gizmos.DrawCube(centre + Vector3.forward * (-0.05f * layer++), half);
             Gizmos.color = Color.white;
        Gizmos.DrawWireCube(centre, new Vector3(cs, cs, 0));
            
        }
        if ((f & ChunkFlags.CaveAir) != 0)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);              // orange
            Gizmos.DrawCube(centre + Vector3.forward * (-0.05f * layer++), half);
        }
        if ((f & ChunkFlags.Liquids) != 0)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.75f);              // blue
            Gizmos.DrawCube(centre + Vector3.forward * (-0.05f * layer++), half);
        }
        if ((f & ChunkFlags.Cave) != 0)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.25f);                // magenta
            Gizmos.DrawCube(centre + Vector3.forward * (-0.05f * layer++), half);
        }
        */
       
    }
}


}
