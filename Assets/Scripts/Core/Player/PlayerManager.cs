/* =======================================================================
 *  PlayerManager.cs
 *  ---------------------------------------------------------------------
 *  • Spawns / keeps the player and main camera alive
 *  • Owns hot-bar selection (number keys + mouse-wheel)
 *  • Developer tile-edit hooks (mine / place) — placement only works if
 *    the active slot contains a placeable tile and at least one item
 *  • Keeps biome / area data and exposes it to the HUD
 *  • Guarantees inventory ↔ world sync (no “infinite” blocks)
 * ======================================================================= */
using UnityEngine;

[RequireComponent(typeof(GameManager))]
public sealed class PlayerManager : MonoBehaviour
{
    /* ───────── Inspector ───────── */
    [Header("Player (prefab optional)")]
    [SerializeField] GameObject playerPrefab;

    [Header("Tile-edit demo")]
    [Tooltip("Fallback tile ID before the player picks one")]
    [SerializeField] int defaultTileID = 1;

    /* ───────── Runtime refs ───────── */
    public Transform  PlayerTransform { get; private set; }
    public GameCamera ActiveCamera    { get; private set; }
    public Inventory  PlayerInventory { get; private set; }

    /* ───────── Biome / Area for HUD ───────── */
    public BiomeData CurrentBiome { get; private set; }
    public AreaData  CurrentArea  { get; private set; }

    /* ───────── Exposed to UI ───────── */
    public int  CurrentPlaceTileID => placeTileID;   // –1 ⇒ none
    public bool PlaceInBackground  => placeInBackground;

    public event System.Action<BiomeData> OnBiomeChanged;
    public event System.Action<AreaData > OnAreaChanged;

    /* ───────── internals ───────── */
    World              world;
    WorldTilemapViewer viewer;
    Camera             cam;
    TerrainEditService terrain;

    int  placeTileID  = -1;
    bool placeInBackground;

    public bool IsInitialized;

    /* =================================================================== */
    #region Bootstrap
    /* =================================================================== */

    public void Initialize()
    {
        if (IsInitialized) return;

        SpawnPlayer();
        BuildCamera();

        viewer = GameManager.Instance.WorldTilemapViewer;
        if (viewer && ActiveCamera) { viewer.SetCamera(ActiveCamera.Cam); cam = ActiveCamera.Cam; }

        world   = GameManager.Instance.WorldManager.GetCurrentWorld();
        terrain = (world != null && viewer != null) ? new TerrainEditService(world, viewer) : null;

        if (PlayerInventory != null)
        {
            PlayerInventory.OnHotbarChanged += _ => UpdatePlaceTileID();
            PlayerInventory.OnSlotChanged   += idx =>
            {
                if (idx == PlayerInventory.ActiveHotbarIndex) UpdatePlaceTileID();
            };
            UpdatePlaceTileID();
        }

        IsInitialized = true;
    }

    #endregion

    void Update()
    {
        if (!IsInitialized || cam == null || terrain == null) return;

        HandleHotbarInput();
        HandleDevTileEditing();
    }

    void LateUpdate()
    {
        if (!IsInitialized || world == null) return;

        int wx = Mathf.FloorToInt(PlayerTransform.position.x);
        int wy = Mathf.FloorToInt(PlayerTransform.position.y);

        var b = world.GetBiome(wx, wy);
        if (b != CurrentBiome) { CurrentBiome = b; OnBiomeChanged?.Invoke(b); }

        var a = world.GetArea(wx, wy);
        if (a != CurrentArea)
        {
            CurrentArea = a;
            OnAreaChanged?.Invoke(a);
            if (ActiveCamera) ActiveCamera.SetParallaxData(a ? a.parallax : null);
        }
    }

    /* =================================================================== */
    #region Input helpers
    /* =================================================================== */

    void HandleHotbarInput()
    {
        for (KeyCode k = KeyCode.Alpha1; k <= KeyCode.Alpha9; ++k)
            if (Input.GetKeyDown(k)) PlayerInventory?.SetActiveHotbarIndex(k - KeyCode.Alpha1);
        if (Input.GetKeyDown(KeyCode.Alpha0)) PlayerInventory?.SetActiveHotbarIndex(9);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll >  0.01f) PlayerInventory?.CycleHotbar(+1);
        if (scroll < -0.01f) PlayerInventory?.CycleHotbar(-1);
    }


    void HandleDevTileEditing()
    {
        Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
        int wx = Mathf.FloorToInt(wp.x);
        int wy = Mathf.FloorToInt(wp.y);

        if (Input.GetKeyDown(KeyCode.Tab))
            placeInBackground = !placeInBackground;

        /* --------------------------------------------------------------
         *  MINING  (left-click)  – HP-based
         * -------------------------------------------------------------- */
        if (Input.GetMouseButton(0))
        {
            /* pull tool stats from the active hot-bar slot (if any) */
            ItemData active = PlayerInventory ? PlayerInventory.ActiveSlot.item : null;
            int toolPower = active ? active.pickaxePower : 0;                // gate
            int toolDmg   = active ? Mathf.Max(1, active.damage) : 1;        // speed

            terrain.TryMineTile(wx, wy,
                                placeInBackground,
                                toolPower: toolPower,
                                toolDmg  : toolDmg);
        }

        /* --------------------------------------------------------------
         *  PLACING  (right-click)
         * -------------------------------------------------------------- */
        if (Input.GetMouseButton(1) && placeTileID >= 0)
        {
            var res = terrain.TryPlaceTile(wx, wy, placeTileID, placeInBackground);
            if (res.success && PlayerInventory)   // consume only if placed
                PlayerInventory.TryConsumeFromSlot(PlayerInventory.ActiveHotbarIndex, 1);
        }
    }


    void UpdatePlaceTileID()
    {
        if (PlayerInventory == null)
        {
            placeTileID = defaultTileID;
            return;
        }

        var slot = PlayerInventory.ActiveSlot;
        placeTileID = (!slot.IsEmpty && slot.item.placeableTile)
                    ? slot.item.placeableTile.tileID
                    : -1;
    }

    #endregion

    /* =================================================================== */
    #region Spawn helpers
    /* =================================================================== */

    void SpawnPlayer()
    {
        GameObject go = GameObject.FindWithTag("Player");
        if (!go)
        {
            GameData gd = GameManager.Instance.GameData;
            UnitTemplate tpl = gd ? gd.playerUnit : null;

            GameObject prefab = playerPrefab != null
                ? playerPrefab
                : tpl && tpl.modelPrefab ? tpl.modelPrefab
                : null;

            world = GameManager.Instance.WorldManager.GetCurrentWorld();
            Vector3 spawn = Vector3.zero;

            if (world != null)
            {
                int cs   = world.chunkSize;
                int midX = world.widthInChunks  * cs / 2;
                int mapH = world.heightInChunks * cs;

                int y;
                for (y = mapH - 1; y >= 0; --y)
                {
                    int id = world.GetTileID(midX, y);
                    if (!world.IsAir(id) && !world.IsLiquid(id)) { y += 2; break; }
                }
                y = Mathf.Clamp(y, 2, mapH - 2);
                spawn = new Vector3(midX + 0.5f, y);
            }

            go = prefab
                ? Instantiate(prefab, spawn, Quaternion.identity)
                : new GameObject("Player",
                                 typeof(PlayerUnit),
                                 typeof(SpriteRenderer),
                                 typeof(Rigidbody2D),
                                 typeof(CapsuleCollider2D));

            var pu = go.GetComponent<PlayerUnit>() ?? go.AddComponent<PlayerUnit>();
            if (tpl) { pu.template = tpl; pu.maxHP = tpl.defaultHP; }

            var body = go.GetComponent<Rigidbody2D>();
            body.gravityScale   = 3f;
            body.freezeRotation = true;

            go.tag = "Player";
        }

        PlayerTransform = go.transform;

        var unit = go.GetComponent<PlayerUnit>();
        PlayerInventory = unit ? unit.Inventory : null;
    }

    void BuildCamera() =>
        ActiveCamera = GameCamera.Initialize(PlayerTransform, name: "MainCamera");

    #endregion
}
