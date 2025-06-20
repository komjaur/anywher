using UnityEngine;

/// Root-level entry point for every UI element.
/// Heavy behaviour is delegated to child components.
public sealed class UIManager : MonoBehaviour
{
    /* ------------- inspector ------------- */
    [Header("Mini-map")]
    [SerializeField] MapPreviewer mapPreviewer;

    [Header("Prefabs & Shared Assets")]
    [SerializeField] Canvas   hudCanvasPrefab;
    [SerializeField] GameObject toastPrefab;

    /* If you have CraftingUI ready, uncomment the two lines below */
    //CraftingUI craftingUI;

    [Header("Debug Flags")]
    [SerializeField] bool showTileUnderMouse = true;
    
    /* ------------- children ------------- */
    HudUI           hudUI;
    ToastSystem     toastSystem;
    InventorySystem inventorySystem;
    SkillStatsUI    statsUI;
    private CraftingUI craftingUI;          // ← add / un-comment this

    /* ------------- singletons ------------- */
    EnvironmentManager env;
    PlayerManager      pm;
    SkillDatabase      skillDb;

    /* ---------------------------------------------------------- */
    public void Initialize()
    {
        env     = GameManager.Instance.EnvironmentManager;
        pm      = GameManager.Instance.PlayerManager;
        skillDb = GameManager.Instance.GameData.skillDatabase;

        /* ───────── Mini-map previewer ───────── */
        if (!mapPreviewer)
            mapPreviewer = new GameObject("MapPreviewer")
                               .AddComponent<MapPreviewer>();
        mapPreviewer.transform.SetParent(transform, false);

        /* ───────── HUD ───────── */
        hudUI = new GameObject("HudUI").AddComponent<HudUI>();
        hudUI.transform.SetParent(transform, false);
        hudUI.Initialize(hudCanvasPrefab, showTileUnderMouse, env, pm);

        /* ───────── XP toasts ───────── */
        var prefabSrc = toastPrefab
                      ? toastPrefab
                      : GameManager.Instance.GameData.uiToast;

        toastSystem = new GameObject("ToastSystem").AddComponent<ToastSystem>();
        toastSystem.transform.SetParent(transform, false);
        toastSystem.Initialize(prefabSrc, hudUI.Canvas, skillDb);

        /* ───────── Inventory ───────── */
        inventorySystem = new GameObject("InventorySystem")
                              .AddComponent<InventorySystem>();
        inventorySystem.transform.SetParent(transform, false);
        inventorySystem.Initialize(hudUI.Canvas, pm);

        /* ───────── Skill stats panel ───────── */
        statsUI = new GameObject("SkillStatsUI").AddComponent<SkillStatsUI>();
        statsUI.transform.SetParent(transform, false);
        statsUI.Initialize(
            hudUI.Canvas,
            GameManager.Instance.SkillManager,
            skillDb);

      
        craftingUI = new GameObject("CraftingUI").AddComponent<CraftingUI>();
        craftingUI.Initialize(
            hudUI.Canvas,
            GameManager.Instance.CraftingManager,
            GameManager.Instance.PlayerManager.PlayerInventory);
        
    }

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.G))
            craftingUI.Toggle();
     
    }

    /* ---------- mini-map pass-throughs ---------- */
    public void DisplayAreaMap(World w, WorldData d)
    {
        if (mapPreviewer != null && w != null && d != null)
        {
            mapPreviewer.areasDatabase = d.areasDatabase;
            mapPreviewer.RenderAreaCanvas(w);
        }
    }

    public void DisplayBiomeMap(World w, WorldData d)
    {
        if (mapPreviewer != null && w != null && d != null)
        {
            mapPreviewer.biomeDatabase = d.biomeDatabase;
            mapPreviewer.RenderBiomeCanvas(w);
        }
    }

  public void DisplayWorldMap(World w)
{
    
    if (mapPreviewer != null && w != null)
        {

            mapPreviewer.RenderWorldCanvasBoth(w);
        }
}

}
