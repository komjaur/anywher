// ItemManager.cs
// -----------------------------------------------------------------------------
//  • Spawns dropped items without prefabs
//  • Solid collider uses one physics layer, trigger collider another
//  • Layer names are inspector fields (no hard-coded strings)
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

public sealed class ItemManager : MonoBehaviour
{
    /* ───────── singleton ───────── */
    public static ItemManager Instance { get; private set; }

    /* ───────── inspector tunables ───────── */
    [Header("Physics layers (set in Tags & Layers window)")]
    [Tooltip("Layer name for the item’s SOLID collider (bounces on ground).")]
    [SerializeField] string solidLayerName   = "ItemSolid";

    [Tooltip("Layer name for the item’s TRIGGER collider (pickup detection).")]
    [SerializeField] string triggerLayerName = "ItemTrigger";

    [Header("Behaviour rules")]
    [Tooltip("Seconds before a loose item despawns. ≤0 disables.")]
    [SerializeField] float  autoDespawnSeconds = 300f;  // 5 min

    [Tooltip("Merge identical stacks on the ground each second.")]
    [SerializeField] bool   enableGroundMerge  = true;

    /* ───────── private data ───────── */
    readonly HashSet<Item> live = new();
    int solidLayer;      // cached layer indices
    int triggerLayer;

    /* ───────── bootstrap helpers ───────── */
    public static ItemManager Initialize()
    {
        if (Instance != null) return Instance;

        var go = new GameObject("ItemManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ItemManager>();
        return Instance;
    }

    void Awake()
    {
        // cache layer indices (-1 if the layer doesn’t exist)
        solidLayer   = LayerMask.NameToLayer(solidLayerName);
        triggerLayer = LayerMask.NameToLayer(triggerLayerName);

        if (solidLayer   == -1) Debug.LogWarning($"[ItemManager] Layer “{solidLayerName}” not found.");
        if (triggerLayer == -1) Debug.LogWarning($"[ItemManager] Layer “{triggerLayerName}” not found.");
    }

    /* ───────── public spawn API ───────── */
public Item Spawn(ItemData data, Vector2 pos, int amount = 1)
{
    if (data == null || amount <= 0) return null;

    /* ─── root object: trigger & visuals ─── */
    var root = new GameObject($"Item_{data.itemName}");
    root.transform.position = pos;
    if (triggerLayer != -1) root.layer = triggerLayer;   // pickup layer

    // visual
    var sr = root.AddComponent<SpriteRenderer>();
    sr.sprite       = data.icon;
    sr.sortingOrder = 10;

    // physics core
    var rb = root.AddComponent<Rigidbody2D>();
    rb.gravityScale  = 1f;
    rb.linearDamping          = 0.5f;
    rb.constraints   = RigidbodyConstraints2D.FreezeRotation;

    // pickup trigger collider
    var trig = root.AddComponent<CircleCollider2D>();
    trig.radius    = 0.50f;
    trig.isTrigger = true;

    /* ─── child object: solid body ─── */
    var bodyGO = new GameObject("BodyCollider");
    bodyGO.transform.SetParent(root.transform, false);
    if (solidLayer != -1) bodyGO.layer = solidLayer;     // ground-collision layer

    var bodyCol = bodyGO.AddComponent<CircleCollider2D>();
    bodyCol.radius    = 0.40f;
    bodyCol.isTrigger = false;

    /* ─── behaviour script ─── */
    var itm = root.AddComponent<Item>();
    itm.data      = data;
    itm.amount    = amount;
    itm.spawnTime = Time.time;

    Register(itm);
    return itm;
}


    /* ───────── registry helpers ───────── */
    internal void Register(Item i)   => live.Add(i);
    internal void Unregister(Item i) => live.Remove(i);

    /* ───────── housekeeping (despawn / merge) ───────── */
    void Update()
    {
        if (live.Count == 0) return;

        float now = Time.time;

        // auto-despawn
        if (autoDespawnSeconds > 0f)
        {
            foreach (var it in new List<Item>(live))
                if (now - it.spawnTime > autoDespawnSeconds)
                    Destroy(it.gameObject);
        }

        // ground-merge once a second
        if (enableGroundMerge && Time.frameCount % 60 == 0)
        {
            const float mergeDistSq = 1f * 1f;
            var list = new List<Item>(live);

            for (int i = 0; i < list.Count; ++i)
            {
                var A = list[i];
                if (A == null) continue;

                for (int j = i + 1; j < list.Count; ++j)
                {
                    var B = list[j];
                    if (B == null || A.data != B.data) continue;
                    if ((A.transform.position - B.transform.position).sqrMagnitude > mergeDistSq) continue;

                    A.amount += B.amount;
                    Destroy(B.gameObject);
                }
            }
        }
    }
}
