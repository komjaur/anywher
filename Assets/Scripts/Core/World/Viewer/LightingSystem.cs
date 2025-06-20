/****************************************************
 *  LightingSystem – coloured lights + darkness mask
 *  -------------------------------------------------
 *  • Added & initialised by WorldTilemapViewer
 *  • Lights every chunk the viewer keeps loaded
 *  • Public hooks:
 *        MarkTileDirty(wx,wy)   – queue a relight
 *        InvalidateLighting()   – force full rebuild next frame
 ****************************************************/
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightingSystem : MonoBehaviour
{
    /* ───────── injected refs ───────── */
    private WorldTilemapViewer viewer;   // set by Initialize
    private World              world;    // lazy-cached

    /* ───────── renderers & buffers ───────── */
    private SpriteRenderer darknessSR;   // α-mask (multiplies scene)
    private SpriteRenderer lightSR;      // RGB add-blend

    private Texture2D darknessTex;
    private Texture2D lightTex;
    private Color[]   darknessPixels;
    private Color[]   lightPixels;

    /* ───────── dynamic lights (optional) ───────── */
    private readonly HashSet<DynamicLight> dynLights = new();

    /* ───────── state ───────── */
    private readonly HashSet<Vector2Int> litChunks  = new();  // chunks in view
    private readonly Queue<Vector2Int>   dirtyTiles = new();  // single-tile edits
    private bool textureDirty;

    /* ===================================================================
     *  External single-entry initialiser
     * =================================================================*/
    public void Initialize(WorldTilemapViewer v)
    {
        viewer = v;
        world  = viewer.CurrentWorld;

        if (!viewer)
        {
            Debug.LogError("[LightingSystem] Initialize failed – viewer null.");
            enabled = false;
            return;
        }

        CreateRenderers();
        textureDirty = true;       // force first build
    }

    /* ===================================================================
     *  Hooks called by viewer / others
     * =================================================================*/
    public void MarkTileDirty(int wx, int wy)
    {
        dirtyTiles.Enqueue(new Vector2Int(wx, wy));
        textureDirty = true;               // simplest: full rebuild next frame
    }

    public void InvalidateLighting() => textureDirty = true;

    public void Register  (DynamicLight l) { if (l) dynLights.Add(l); }
    public void Unregister(DynamicLight l) {           dynLights.Remove(l); }

    /* ===================================================================
     *  Main update – executed *after* viewer renders
     * =================================================================*/
    private void LateUpdate()
    {
        if (!viewer) return;

        world ??= viewer.CurrentWorld;
        if (world == null) return;

        /* gather current chunk set */
        var current = new HashSet<Vector2Int>(viewer.RenderedChunks);
        if (!current.SetEquals(litChunks))
        {
            litChunks.Clear();
            litChunks.UnionWith(current);
            textureDirty = true;
        }

        if (!textureDirty) return;

        textureDirty = false;
        dirtyTiles.Clear();    // full rebuild covers them all

        if (litChunks.Count == 0) { ClearMasks(); return; }
        RebuildMasks();
    }

    /* ===================================================================
     *  Mask management
     * =================================================================*/
    private void ClearMasks()
    {
        darknessSR.sprite = null;
        lightSR.sprite    = null;

        if (darknessTex) Destroy(darknessTex);
        if (lightTex)    Destroy(lightTex);

        darknessTex = lightTex = null;
        darknessPixels = lightPixels = null;
    }

    private void RebuildMasks()
    {
        if (!ComputeTileBounds(out Vector2Int min, out Vector2Int max))
        {
            ClearMasks();
            return;
        }

        int w = max.x - min.x;
        int h = max.y - min.y;
        if (w <= 0 || h <= 0) { ClearMasks(); return; }

        EnsureTextures(w, h);

        /* -------- gather light emitters (tiles + dynamic) -------- */
        var light = new Color[w * h];
        var q     = new Queue<(Vector2Int pos, Color energy)>();

        viewer.CollectChunkLights(litChunks, min, max, ref light, q);

        /* dynamic point lights */
        foreach (var dl in dynLights)
        {
            if (!dl) continue;
            Vector2 worldPos = dl.transform.position;
            var p = new Vector2Int(
                        Mathf.FloorToInt(worldPos.x) - min.x,
                        Mathf.FloorToInt(worldPos.y) - min.y);
            if (p.x < 0 || p.y < 0 || p.x >= w || p.y >= h) continue;

            light[p.y * w + p.x] = dl.color * dl.intensity;
            q.Enqueue((p, light[p.y * w + p.x]));
        }

        /* flood-fill light propagation */
        while (q.Count > 0)
        {
            var (p, c) = q.Dequeue();
            int idx = p.y * w + p.x;
            if (c.maxColorComponent < light[idx].maxColorComponent) continue;

            foreach (var n in Neigh4(p))
            {
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;

                float fall = 1f - viewer.GetTileFalloff(n.x + min.x, n.y + min.y);
                Color nxt  = c * fall;

                int ni = n.y * w + n.x;
                if (nxt.maxColorComponent > light[ni].maxColorComponent)
                {
                    light[ni] = nxt;
                    if (nxt.maxColorComponent > 0.02f)
                        q.Enqueue((n, nxt));
                }
            }
        }

        /* -------- compose texels -------- */
        for (int i = 0; i < light.Length; ++i)
        {
            float bright = Mathf.Clamp01(light[i].maxColorComponent);
            darknessPixels[i] = new Color(0, 0, 0, 1f - bright);
            lightPixels[i]    = light[i];
        }

        darknessTex.SetPixels(darknessPixels);
        darknessTex.Apply();
        lightTex.SetPixels(lightPixels);
        lightTex.Apply();

        Vector3 pos = new(min.x - 0.5f, min.y - 0.5f, 0);
        darknessSR.transform.position = pos;
        lightSR.transform.position    = pos;
    }

    /* ===================================================================
     *  Helpers
     * =================================================================*/
    private bool ComputeTileBounds(out Vector2Int min, out Vector2Int max)
    {
        min = new Vector2Int(int.MaxValue, int.MaxValue);
        max = new Vector2Int(int.MinValue, int.MinValue);

        int cs = world.chunkSize;
        if (cs <= 0 || litChunks.Count == 0) return false;

        foreach (var ck in litChunks)
        {
            if (ck.x < min.x) min.x = ck.x;
            if (ck.y < min.y) min.y = ck.y;
            if (ck.x > max.x) max.x = ck.x;
            if (ck.y > max.y) max.y = ck.y;
        }

        /* convert chunk bounds → tile bounds (max is exclusive) */
        min *= cs;
        max  = (max + Vector2Int.one) * cs;
        return true;
    }

    private static IEnumerable<Vector2Int> Neigh4(Vector2Int p)
    {
        yield return new Vector2Int(p.x + 1, p.y);
        yield return new Vector2Int(p.x - 1, p.y);
        yield return new Vector2Int(p.x,     p.y + 1);
        yield return new Vector2Int(p.x,     p.y - 1);
    }

    private void EnsureTextures(int w, int h)
    {
        if (darknessTex && darknessTex.width == w && darknessTex.height == h) return;

        if (darknessTex) Destroy(darknessTex);
        if (lightTex)    Destroy(lightTex);

        darknessTex    = new Texture2D(w, h, TextureFormat.RGBA32, false);
        lightTex       = new Texture2D(w, h, TextureFormat.RGBA32, false);
        darknessPixels = new Color[w * h];
        lightPixels    = new Color[w * h];

        darknessSR.sprite = Sprite.Create(
            darknessTex, new Rect(0, 0, w, h), Vector2.zero, 1f);
        lightSR.sprite = Sprite.Create(
            lightTex, new Rect(0, 0, w, h), Vector2.zero, 1f);
    }

    private void CreateRenderers()
    {
        /* darkness overlay (alpha mask) */
        var darkGO = new GameObject("DarknessRenderer");
        darkGO.transform.SetParent(transform, false);
        darknessSR              = darkGO.AddComponent<SpriteRenderer>();
        darknessSR.sortingOrder = 30;
        darknessSR.sharedMaterial =
            new Material(Shader.Find("Custom/Lighting/DarknessMask"));

        /* additive coloured lights */
        var lightGO = new GameObject("LightRenderer");
        lightGO.transform.SetParent(transform, false);
        lightSR              = lightGO.AddComponent<SpriteRenderer>();
        lightSR.sortingOrder = 31;
        lightSR.sharedMaterial =
            new Material(Shader.Find("Custom/Lighting/AdditiveLight"));
    }
}
