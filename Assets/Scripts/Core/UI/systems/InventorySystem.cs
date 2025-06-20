using UnityEngine;

/// Builds the player’s inventory / hot-bar once the PlayerManager owns an inventory.
public sealed class InventorySystem : MonoBehaviour
{
    Canvas        parentCanvas;
    PlayerManager pm;
    InventoryUI   invUI;        // created once and kept alive

    /// Call from UIManager.Initialize(...)
    public void Initialize(Canvas canvas, PlayerManager playerManager)
    {
        parentCanvas = canvas;
        pm           = playerManager;
    }

    void LateUpdate()
    {
        // Spawn exactly once, the first frame the player inventory exists
        if (invUI == null && pm && pm.PlayerInventory != null)
            CreateInventoryUI(pm.PlayerInventory);
    }

    /* ---------------- helpers ---------------- */

    void CreateInventoryUI(Inventory inv)
    {
        var go = new GameObject("InventoryUI",
                                typeof(RectTransform),
                                typeof(InventoryUI));

        go.transform.SetParent(parentCanvas.transform, false);

        invUI = go.GetComponent<InventoryUI>();
        invUI.Initialise(inv);          // your existing component API
        invUI.gameObject.SetActive(true);
    }
}
