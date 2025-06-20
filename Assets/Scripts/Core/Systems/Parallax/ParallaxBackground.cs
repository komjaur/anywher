// ─────────────────────────────────────────────────────────────
//  ParallaxBackground.cs
//  Execute-Always seamless parallax with live rebuild
//  and instant response to layer.offset changes.
// ─────────────────────────────────────────────────────────────
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;            // only needed in the editor
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ParallaxBackground : MonoBehaviour
{
    /* ───── inspector options ───── */
    [Header("Global options")]
    public bool   repeatX          = true;
    public bool   repeatY          = false;
    public string sortingLayerName = "Default";
    public int    baseSortingOrder = -100;
    public int    orderStride      = 10;
    public Color  tint             = Color.white;

    [Tooltip("Rebuild automatically whenever you tweak the ParallaxData " +
             "asset or these inspector values.")]
    public bool   liveRebuild      = true;

    /* ───── helpers + state ───── */
    class RuntimeLayer
    {
        public Transform   root;
        public ParallaxLayer cfg;
        public Vector2      tile;
        public Vector2      prevOffset;   // tracks live offset edits
    }

    Camera   cam;
    Transform camT;
    Vector2  prevCamPos;
    readonly List<RuntimeLayer> layers = new();
    ParallaxData currentData;
    int dataSignature;                   // hash of currentData

    /* ───────────── INITIALISE ───────────── */
    public void Initialise(Camera follow, ParallaxData firstData)
    {
        cam   = follow;
        camT  = cam.transform;
        prevCamPos = camT ? camT.position : Vector2.zero;

        // keep PB centred on camera (z unchanged)
        transform.position = new Vector3(prevCamPos.x, prevCamPos.y, transform.position.z);

        ApplyData(firstData);
    }

    /* ───────────── BUILD / REBUILD ───────────── */
    public void ApplyData(ParallaxData data)
    {
        if (data == currentData && layers.Count != 0) return;   // nothing new

        ClearLayers();
        currentData = data;

        if (currentData == null || currentData.layers == null) { dataSignature = 0; return; }

        float halfW = cam.orthographicSize * cam.aspect;
        float halfH = cam.orthographicSize;

        int nextAuto = 0;
        foreach (var src in currentData.layers)
        {
            if (src == null || src.sprite == null) continue;

            float upp  = 1f / src.sprite.pixelsPerUnit;
            Vector2 size = src.sprite.rect.size * upp;

            int copiesX = repeatX ? Mathf.CeilToInt(halfW * 2 / size.x) + 2 : 1;
            int copiesY = repeatY ? Mathf.CeilToInt(halfH * 2 / size.y) + 2 : 1;
            if ((copiesX & 1) == 0) copiesX++;
            if ((copiesY & 1) == 0) copiesY++;

            var root = new GameObject($"Parallax_{src.sprite.name}").transform;
            root.SetParent(transform, false);
            root.localPosition = src.offset;

            int localOrder = src.sortingOrder != 0 ? src.sortingOrder
                                                   : nextAuto++ * orderStride;
            int finalOrder = baseSortingOrder + localOrder;

            int x0 = -copiesX / 2, y0 = -copiesY / 2;
            for (int ix = 0; ix < copiesX; ++ix)
            for (int iy = 0; iy < copiesY; ++iy)
            {
                var tile = new GameObject($"T{ix}_{iy}");
                tile.transform.SetParent(root, false);
                tile.transform.localPosition = new Vector3((x0 + ix) * size.x,
                                                           (y0 + iy) * size.y);
                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite           = src.sprite;
                sr.color            = tint;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder     = finalOrder;
            }

            layers.Add(new RuntimeLayer
            {
                root       = root,
                cfg        = src,
                tile       = size,
                prevOffset = src.offset     // remember starting offset
            });
        }

        dataSignature = ComputeSignature(currentData);
        CacheInspector();
    }

    /* ───────────── PER-FRAME LOOP ───────────── */
    void LateUpdate()
    {
        if (!cam) return;

        if (liveRebuild && (InspectorChanged() || DataChanged()))
            Rebuild();

        Vector2 camPos = camT.position;
        Vector2 delta  = camPos - prevCamPos;

        foreach (var rl in layers)
        {
            /* --- live offset edit support --------------------- */
            if (rl.cfg.offset != rl.prevOffset)
            {
                Vector2 diff = rl.cfg.offset - rl.prevOffset;
                rl.root.localPosition += (Vector3)diff;
                rl.prevOffset = rl.cfg.offset;
            }
            /* -------------------------------------------------- */

            float s = rl.cfg.speed;
            rl.root.localPosition -= new Vector3(delta.x * (1f - s),
                                                 delta.y * (1f - s));

            if (rl.cfg.autoScrollSpeed != Vector2.zero)
                rl.root.localPosition += (Vector3)(rl.cfg.autoScrollSpeed * Time.deltaTime);

            Vector3 p = rl.root.localPosition;
            if (repeatX) p.x = Wrap(p.x, rl.tile.x);
            if (repeatY) p.y = Wrap(p.y, rl.tile.y);
            rl.root.localPosition = p;
        }

        prevCamPos = camPos;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }

    /* ───────────── UTILITIES ───────────── */
    static float Wrap(float pos, float len) =>
        Mathf.Repeat(pos + len * 0.5f, len) - len * 0.5f;

    int ComputeSignature(ParallaxData pd)
    {
        if (pd == null || pd.layers == null) return 0;
        unchecked
        {
            int hash = 17;
            foreach (var l in pd.layers)
            {
                if (l == null) { hash = hash * 23; continue; }
                hash = hash * 23 + (l.sprite ? l.sprite.GetInstanceID() : 0);
                hash = hash * 23 + Mathf.RoundToInt(l.speed * 1000);
                hash = hash * 23 + l.offset.GetHashCode();
                hash = hash * 23 + l.autoScrollSpeed.GetHashCode();
                hash = hash * 23 + l.sortingOrder;
            }
            return hash;
        }
    }

    bool DataChanged() => ComputeSignature(currentData) != dataSignature;

    /* inspector-option tracking for live rebuild */
    bool _repX, _repY; int _base, _stride; string _layer; Color _tint;

    void CacheInspector()
    {
        _repX = repeatX; _repY = repeatY;
        _base = baseSortingOrder; _stride = orderStride;
        _layer = sortingLayerName; _tint = tint;
    }
    bool InspectorChanged() =>
        repeatX != _repX || repeatY != _repY ||
        baseSortingOrder != _base || orderStride != _stride ||
        sortingLayerName != _layer || tint != _tint;

    /* public helpers */
    public void Rebuild() => ApplyData(currentData);

    void ClearLayers()
    {
        foreach (var rl in layers)
            if (rl.root) DestroyImmediate(rl.root.gameObject);
        layers.Clear();
    }
}
