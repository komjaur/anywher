using UnityEngine;

public static class InventoryExtensions
{
    /// <summary>
    /// Convenience wrapper to add an item by ID using Resources/Items lookup.
    /// </summary>
    public static void AddItem(this Inventory inv, string itemID, int amount)
    {
        if (inv == null || string.IsNullOrEmpty(itemID) || amount <= 0)
            return;

        var item = Resources.Load<ItemData>($"Items/{itemID}");
        if (!item)
        {
            Debug.LogWarning($"InventoryExtensions.AddItem: item '{itemID}' not found");
            return;
        }

        inv.TryAddItem(item, amount);
    }
}
