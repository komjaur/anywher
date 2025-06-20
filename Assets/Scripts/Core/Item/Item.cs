// Item.cs
// -----------------------------------------------------------------------------
//  Runtime component for a dropped / world item.
//  • Holds an ItemData reference + current stack amount
//  • Shows the icon on a SpriteRenderer
//  • Plays a simple hover animation
//  • On trigger with Player picks itself up via IInventory interface
// -----------------------------------------------------------------------------

using UnityEngine;



[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
public sealed class Item : MonoBehaviour
{
    public ItemData data;
    public int      amount = 1;
    public float    spawnTime { get; set; }

    Rigidbody2D _rb;

    void Awake()
    {
        ItemManager.Instance?.Register(this);

        // cache rigidbody so other scripts can tweak if needed
        _rb = GetComponent<Rigidbody2D>();
    }

    void OnDestroy() => ItemManager.Instance?.Unregister(this);

    // ──────────────────────────
    //  No hover animation needed
    // ──────────────────────────
    void Update() { /* intentionally empty */ }

    /* ---------- pick-up ---------- */
    void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponent<IInventory>();
        if (inv == null) return;

        int stored = inv.TryAddItem(data, amount);
        amount -= stored;
        if (amount <= 0) Destroy(gameObject);
    }
}


/* ---------------------------------------------------------------------------
   Simple inventory contract so Item.cs can talk to *any* player/collector
--------------------------------------------------------------------------- */
public interface IInventory
{
    /// <summary>
    /// Attempt to add some quantity of <paramref name="item"/> to this inventory.
    /// Returns the amount actually stored (≤ requested).
    /// </summary>
    int TryAddItem(ItemData item, int amount);
}
