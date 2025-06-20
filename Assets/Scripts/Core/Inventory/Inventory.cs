// Inventory.cs
// -----------------------------------------------------------------------------
//  • Stack-aware inventory component that satisfies IInventory
//  • First <hotbarSize> slots are the hot-bar
//  • Fires events so a UI layer can react when the contents change
// -----------------------------------------------------------------------------

using UnityEngine;

[System.Serializable]
public struct InvSlot
{
    public ItemData item;
    public int      amount;

    public bool IsEmpty => item == null || amount <= 0;
}

public sealed class Inventory : MonoBehaviour, IInventory
{
    /* ───────── inspector ───────── */
    [SerializeField, Min(1)] int slotCount  = 40;   // backpack
    [SerializeField, Min(1)] int hotbarSize = 10;   // ≤ slotCount

    /* ───────── runtime data ───────── */
    public InvSlot[] Slots { get; private set; }
    public int  HotbarSize => hotbarSize;

    public int     ActiveHotbarIndex { get; private set; } = 0;
    public InvSlot ActiveSlot        => Slots[ActiveHotbarIndex];

    /* ───────── events (UI hooks) ───────── */
    public event System.Action<int> OnSlotChanged;   // slot index
    public event System.Action<int> OnHotbarChanged; // new active index

    /* ───────── lifecycle ───────── */
    void Awake()
    {
        Slots = new InvSlot[slotCount];
    }
/* ───────── item-query helpers (needed by CraftingManager) ───────── */
/// Total number of `data` currently stored in this inventory.
public int CountItem(ItemData data)
{
    if (data == null) return 0;
    int total = 0;
    for (int i = 0; i < Slots.Length; ++i)
        if (!Slots[i].IsEmpty && Slots[i].item == data)
            total += Slots[i].amount;
    return total;
}

/// Remove up to `amount` of `data`.  
/// Returns **true** only if the full amount was removed; nothing is
/// removed (and it returns false) if there isn’t enough in stock.
public bool TryRemoveItem(ItemData data, int amount)
{
    if (data == null || amount <= 0) return false;
    if (CountItem(data) < amount)    return false;   // not enough

    int remaining = amount;

    // walk forward so UI feels natural (take from lower slots first)
    for (int i = 0; i < Slots.Length && remaining > 0; ++i)
    {
        ref InvSlot s = ref Slots[i];
        if (s.IsEmpty || s.item != data) continue;

        int take = Mathf.Min(s.amount, remaining);
        s.amount -= take;
        remaining -= take;

        if (s.amount == 0) s.item = null;
        OnSlotChanged?.Invoke(i);
    }

    return remaining == 0;
}

    /* ───────── IInventory implementation ───────── */
    public int TryAddItem(ItemData data, int amount)
    {
        if (data == null || amount <= 0) return 0;

        int remaining = amount;

        /* (1) fill existing stacks */
        for (int i = 0; i < slotCount && remaining > 0; ++i)
        {
            if (Slots[i].IsEmpty || Slots[i].item != data) continue;

            int cap   = data.IsStackable ? data.maxStack : 1;
            int space = cap - Slots[i].amount;
            if (space <= 0) continue;

            int move = Mathf.Min(space, remaining);
            Slots[i].amount += move;
            remaining       -= move;
            OnSlotChanged?.Invoke(i);
        }

        /* (2) use empty slots */
        for (int i = 0; i < slotCount && remaining > 0; ++i)
        {
            if (!Slots[i].IsEmpty) continue;

            int cap  = data.IsStackable ? data.maxStack : 1;
            int move = Mathf.Min(cap, remaining);

            Slots[i].item   = data;
            Slots[i].amount = move;
            remaining      -= move;
            OnSlotChanged?.Invoke(i);
        }

        return amount - remaining; // actually stored
    }

    public bool TryConsumeFromSlot(int slot, int amount = 1)
    {
        if (slot < 0 || slot >= slotCount) return false;
        ref InvSlot s = ref Slots[slot];
        if (s.IsEmpty || s.amount < amount) return false;

        s.amount -= amount;
        if (s.amount == 0) s.item = null;
        OnSlotChanged?.Invoke(slot);
        return true;
    }

    /* ───────── hot-bar helpers ───────── */
    public void SetActiveHotbarIndex(int idx)
    {
        idx = Mathf.Clamp(idx, 0, hotbarSize - 1);
        if (idx == ActiveHotbarIndex) return;

        ActiveHotbarIndex = idx;
        OnHotbarChanged?.Invoke(idx);
    }

    public void CycleHotbar(int direction)
    {
        int next = (ActiveHotbarIndex + direction) % hotbarSize;
        if (next < 0) next += hotbarSize;
        SetActiveHotbarIndex(next);
    }
}
