// InventorySlotUI.cs
// -----------------------------------------------------------------------------
//  Visual for a single inventory slot (icon + amount text).
//  If the serialized references are not set in the inspector, it will look for
//  suitable components on itself / its children at runtime.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image    icon;         // background or item sprite
    [SerializeField] private TMP_Text amountLabel;  // stack size

    private Inventory owner;
    private int       index;

    /* ---------- public API ---------- */
    public void Initialise(Inventory inv, int slotIndex)
    {
        owner = inv;
        index = slotIndex;

        // subscribe once
        owner.OnSlotChanged += HandleSlotChanged;
        Refresh();
    }

    public void Refresh()
    {
        if (owner == null) return;

        /* auto-wire children if left null */
        if (icon == null)        icon        = GetComponent<Image>();
        if (amountLabel == null) amountLabel = GetComponentInChildren<TMP_Text>();

        var slot = owner.Slots[index];
        bool empty = slot.IsEmpty;

        if (icon       != null) icon.enabled        = !empty;
        if (amountLabel!= null) amountLabel.enabled = !empty;

        if (!empty)
        {
            if (icon != null)        icon.sprite = slot.item.icon;
            if (amountLabel != null) amountLabel.text = slot.amount > 1
                                                      ? slot.amount.ToString()
                                                      : string.Empty;
        }
    }

    /* ---------- events ---------- */
    private void HandleSlotChanged(int idx)
    {
        if (idx == index) Refresh();
    }

    /* ---------- cleanup ---------- */
    private void OnDestroy()
    {
        if (owner != null)
            owner.OnSlotChanged -= HandleSlotChanged;
    }
}
