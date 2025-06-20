/* =========================================================================
 *  GameManager.cs — single global entry-point, no abbreviated names
 * ========================================================================= */
using UnityEngine;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /* design-time data */
    [Header("Data assets")]
    [SerializeField] private GameData gameData;
    public GameData GameData => gameData;               // public getter

    /* manager singletons (read-only) */
    public WorldManager       WorldManager       { get; private set; }
    public UIManager          UIManager          { get; private set; }
    public PlayerManager      PlayerManager      { get; private set; }
    public EnvironmentManager EnvironmentManager { get; private set; }
    public FluidMidi.MusicManager MusicManager   { get; private set; }
    public CraftingManager    CraftingManager    { get; private set; }
    public ProjectileManager  ProjectileManager  { get; private set; }
    public SceneManager       SceneManager       { get; private set; }
    public UnitManager        UnitManager        { get; private set; }
    public AudioManager AudioManager { get; private set; }
    public ItemManager ItemManager { get; private set; }   // ← NEW
    public SkillManager SkillManager { get; private set; }   // add near the other managers

    public WorldTilemapViewer WorldTilemapViewer { get; private set; }

    /* Awake */
    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        /* core services */
        WorldManager = gameObject.AddComponent<WorldManager>();
        UIManager = gameObject.AddComponent<UIManager>();
        EnvironmentManager = gameObject.AddComponent<EnvironmentManager>();
        //MusicManager        = gameObject.AddComponent<FluidMidi.MusicManager>();
        CraftingManager = gameObject.AddComponent<CraftingManager>();
        ProjectileManager = gameObject.AddComponent<ProjectileManager>();
        SceneManager = gameObject.AddComponent<SceneManager>();
        UnitManager = gameObject.AddComponent<UnitManager>();
        WorldTilemapViewer = gameObject.AddComponent<WorldTilemapViewer>();
        ItemManager = gameObject.AddComponent<ItemManager>();  // ← NEW
        /* player and camera (lazy-initialised) */
        PlayerManager = gameObject.AddComponent<PlayerManager>();
        SkillManager = gameObject.AddComponent<SkillManager>();
        AudioManager = gameObject.AddComponent<AudioManager>();
    }

    void Start()
    {
        
        WorldManager.InitializeWorld(gameData.world);

        /* start day and season clock after world exists */
        UIManager.Initialize();
        EnvironmentManager.Initialize();
    }

    /* debug keys */
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
            gameData.world.seed = Random.Range(0, 1000);

        if (Input.GetKeyDown(KeyCode.Alpha1))
            WorldManager.GenerateBiomeMap();

        if (Input.GetKeyDown(KeyCode.Alpha2))
            WorldManager.GenerateWorldMap(OnWorldFinished);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            WorldManager.GenerateWorldPOI(OnWorldFinished);

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            WorldManager.GenerateWorldPost(OnWorldFinished);
            WorldTilemapViewer.Initialize(WorldManager);
            PlayerManager.Initialize();
            UnitManager.Initialize();
            ItemManager.Initialize();
            
            AudioManager.Initialize(gameData.levelUpClip);   // ← NEW
            //MusicManager.Initialize(gameData.soundFont);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
            UIManager.DisplayAreaMap(WorldManager.GetCurrentWorld(), gameData.world);

        if (Input.GetKeyDown(KeyCode.Alpha6))
            UIManager.DisplayBiomeMap(WorldManager.GetCurrentWorld(), gameData.world);

  
    }

    void OnWorldFinished()
    {
        UIManager.DisplayWorldMap(WorldManager.GetCurrentWorld());
    }
}
