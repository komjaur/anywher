using System;
using System.Collections.Generic;


/* ─────────────────────────── caching util ────────────────────────── */
static class TableCache
{
    /* one cache per content-type --------------------------------------- */
    static readonly Dictionary<object, DropTable<TileData>>      foliage = new();
    static readonly Dictionary<object, DropTable<TileData>>      decor   = new();
    static readonly Dictionary<object, DropTable<BlueprintTree>> trees   = new();
    static readonly Dictionary<object, DropTable<UnitTemplate>>  units   = new();   // NEW ✔

    /// <summary>Wipe all cached tables – call once per PostProcessWorld.</summary>
    public static void Clear()
    {
        foliage.Clear();
        decor.Clear();
        trees.Clear();
        units.Clear();           // NEW ✔
    }

    /* ───── public helpers ───── */

    public static DropTable<TileData> GetFoliage(object owner, WeightedTile[]  src) =>
        Get(owner, src, foliage, w => w.tile);

    public static DropTable<TileData> GetDecor  (object owner, WeightedTile[]  src) =>
        Get(owner, src, decor,   w => w.tile);

    public static DropTable<BlueprintTree> GetTrees(object owner, WeightedTree[] src) =>
        Get(owner, src, trees,   w => w.prefab);

    public static DropTable<UnitTemplate> GetUnits(object owner, WeightedUnit[] src) =>   // NEW ✔
        Get(owner, src, units,   w => w.template);

    /* ───── generic builder ───── */

    static DropTable<T> Get<O,S,T>(O owner,
                                   S[] src,
                                   Dictionary<object, DropTable<T>> dict,
                                   Func<S,T> selector)
        where S : class
        where T : class
    {
        if (src == null || src.Length == 0) return null;
        if (dict.TryGetValue(owner, out var tbl)) return tbl;

        tbl = new DropTable<T>();
        foreach (var s in src)
            if (s != null)
            {
                T item = selector(s);
                float w = (float)s.GetType().GetField("weight").GetValue(s);
                if (item != null && w > 0f) tbl.Add(item, w);
            }
        tbl.Build();
        dict[owner] = tbl;
        return tbl;
    }
}


/*  DropTable<T>.cs
 *  ------------------------------------------------------------
 *  Generic, runtime-only weighted table.
 *  • No Unity-specific code – pure C#.
 *  • O(1) Roll() after Build().
 *  • Add() as many times as you like, then Build(), then Roll().
 *  • If you need deterministic results, feed in your own System.Random.
 *  ---------------------------------------------------------- */



public sealed class DropTable<T>
{
    /* internal entry ---------------------------------------------------- */
    struct Entry
    {
        public T     Item;
        public float Cumulative;   // running sum of weights
    }

    readonly List<Entry> _entries = new();
    float                _totalWeight;
    bool                 _dirty = true;
    readonly System.Random _rng;

    /* — ctor — */
    public DropTable(int seed = 0)
        => _rng = seed == 0 ? new System.Random()
                            : new System.Random(seed);

    /* — mutators -------------------------------------------------------- */

    /// <summary>Adds an item with the given weight (weight ≤ 0 ⇒ ignored).</summary>
    public void Add(T item, float weight)
    {
        if (weight <= 0f || item == null) return;
        _entries.Add(new Entry { Item = item, Cumulative = weight });
        _dirty = true;
    }

    /// <summary>Call once after all Add() calls.  O(n).</summary>
    public void Build()
    {
        _totalWeight = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            _totalWeight += _entries[i].Cumulative;
            _entries[i] = new Entry
            {
                Item       = _entries[i].Item,
                Cumulative = _totalWeight
            };
        }
        _dirty = false;
    }

    /* — queries --------------------------------------------------------- */

    /// <summary>
    /// Returns one item according to relative weights,
    /// or default(T) if the table is empty.
    /// </summary>
    public T Roll()
    {
        if (_entries.Count == 0) return default;
        if (_dirty) Build();                       // lazy build

        float pick = (float)_rng.NextDouble() * _totalWeight;

        /* binary search in prefix-sum array */
        int lo = 0, hi = _entries.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (pick < _entries[mid].Cumulative) hi = mid;
            else                                 lo = mid + 1;
        }
        return _entries[lo].Item;
    }

    /// <summary>True if no items have been added (or all had zero weight).</summary>
    public bool IsEmpty => _entries.Count == 0;
}
