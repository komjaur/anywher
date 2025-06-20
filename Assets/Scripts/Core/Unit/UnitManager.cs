/*********************************************************
 *  UnitManager.cs — spawns / despawns mobs off-screen,
 *                   restores them with their HP,
 *                   optionally drops a mob the very first
 *                   time a chunk is discovered,
 *                   and drives a global “ambient moo/bark”
 *                   system that makes one visible unit
 *                   play a 3-D clip every N seconds.
 *********************************************************/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
    /* ───────── singleton ───────── */
    public static UnitManager Instance { get; private set; }

    /* ───────── inspector ───────── */
    [Header("Spawn cadence (off-screen)")]
    [Min(0.1f)] public float spawnInterval = 1f;   // seconds between edge-chunk attempts
    [Min(1)]    public int   maxUnits      = 20;   // hard cap in the scene

    [Header("Global ambience")]
    [Min(1f)] public float ambientInterval = 30f;  // seconds between moos/barks

    [Header("Rendering")]
    public int unitSortingOrder = 10;              // sprite-renderer sorting order

    /* ───────── runtime ───────── */
    readonly List<Unit>          live            = new();
    readonly HashSet<Vector2Int> chunksWithSaves = new();

    DropTable<UnitTemplate> areaTable;

    PlayerManager      player;
    World              world;
    WorldTilemapViewer viewer;

    Transform mobRoot;
    Coroutine spawnLoop, ambientRoutine;

    /* ================================================================= */

    #region INITIALISE / SHUTDOWN
    public void Initialize()
    {
        if (spawnLoop != null) return;
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;

        player  = GameManager.Instance.PlayerManager;
        world   = GameManager.Instance.WorldManager.GetCurrentWorld();
        viewer  = GameManager.Instance.WorldTilemapViewer;

        if (!player || !player.IsInitialized || !viewer)
        {
            Debug.LogError("[UnitManager] Dependencies not ready."); return;
        }

        mobRoot = new GameObject("Mobs").transform;
        mobRoot.SetParent(transform);

        player.OnAreaChanged += OnAreaChanged;
        viewer.ChunkCleared  += OnViewerChunkCleared;
        viewer.ChunkRendered += OnViewerChunkRendered; // note (coord, firstTime)

        OnAreaChanged(player.CurrentArea);

        spawnLoop     = StartCoroutine(SpawnLoop());
        ambientRoutine = StartCoroutine(GlobalAmbientLoop());
    }

    void OnDisable()
    {
        if (player) player.OnAreaChanged -= OnAreaChanged;
        if (viewer)
        {
            viewer.ChunkCleared  -= OnViewerChunkCleared;
            viewer.ChunkRendered -= OnViewerChunkRendered;
        }

        if (spawnLoop      != null) StopCoroutine(spawnLoop);
        if (ambientRoutine != null) StopCoroutine(ambientRoutine);
    }
    #endregion

    /* ───────────────── viewer callbacks ───────────────── */

    void OnViewerChunkCleared(Vector2Int ck)
    {
        Chunk ch = world.GetChunk(ck);
        if (ch == null) return;

        /* save and despawn units inside that chunk */
        for (int i = live.Count - 1; i >= 0; --i)
        {
            Unit u = live[i];
            if (!u || u.Dead) { live.RemoveAt(i); continue; }
            if (WorldPosToChunk(u.transform.position) != ck) continue;

            ch.savedUnits.Add(new SavedUnit
            {
                tpl = u.template,
                pos = u.transform.position,
                hp  = u.HP
            });
            chunksWithSaves.Add(ck);

            Destroy(u.gameObject);
            live.RemoveAt(i);
        }
    }

    /// <summary>
    /// Called each time the viewer draws <paramref name="ck"/>.
    /// <paramref name="firstTime"/> is TRUE only on the very first appearance.
    /// </summary>
    void OnViewerChunkRendered(Vector2Int ck, bool firstTime)
    {
        Chunk ch = world.GetChunk(ck);
        if (ch == null) return;

        /* restore saved mobs */
        if (ch.HasSavedUnits)
        {
            foreach (var s in ch.savedUnits)
            {
                Unit u = Spawn(s.tpl, s.pos, 2);
                if (u) { u.HP = s.hp; live.Add(u); }
            }
            ch.ClearSaved();
            chunksWithSaves.Remove(ck);
        }

        /* drop a bonus mob on discovery */
        if (firstTime && Random.value < 0.30f)
            TrySpawnInsideChunk(ch);
    }

    /* ───────────────── spawner coroutine (off-screen) ───────────────── */

    IEnumerator SpawnLoop()
    {
        WaitForSeconds wait = new(spawnInterval);
        int cs              = world.chunkSize;

        const int chunkTries = 8;   // edge chunks per tick
        const int tileTries  = 16;  // tiles per chunk

        while (true)
        {
            yield return wait;

            live.RemoveAll(u => !u || u.Dead);
            if (areaTable == null || areaTable.IsEmpty)    continue;
            if (live.Count >= maxUnits)                    continue;
            if (viewer.OutOfViewChunks.Count == 0)         continue;

            UnitTemplate tpl = areaTable.Roll();
            if (!tpl) continue;

            int w = Mathf.Max(1, tpl.sizeInTiles.x);
            int h = Mathf.Max(1, tpl.sizeInTiles.y);

            bool spawned = false;

            for (int cTry = 0; cTry < chunkTries && !spawned; ++cTry)
            {
                Vector2Int ck = viewer.OutOfViewChunks[Random.Range(0, viewer.OutOfViewChunks.Count)];
                if (chunksWithSaves.Contains(ck)) continue;

                Chunk ch = world.GetChunk(ck);
                if (ch == null) continue;

                if (tpl.allowedFlags != ChunkFlags.None &&
                    (ch.GetFlags() & tpl.allowedFlags) == 0)
                    continue;

                for (int tTry = 0; tTry < tileTries && !spawned; ++tTry)
                {
                    int wx = ck.x * cs + Random.Range(0, cs - w + 1);
                    int wy = ck.y * cs + Random.Range(0, cs - h + 1);

                    if (!CheckSurface(tpl, wx, wy)) continue;

                    Vector3 pos = new(wx + w * 0.5f, wy + h * 0.5f, 0);
                    Unit u = Spawn(tpl, pos, 2);
                    if (u)
                    {
                        live.Add(u);
                        spawned = true;
                    }
                }
            }
        }
    }

    /* ───────── helper: in-view discovery spawn ───────── */

    void TrySpawnInsideChunk(Chunk ch)
    {
        if (areaTable == null || areaTable.IsEmpty) return;

        UnitTemplate tpl = areaTable.Roll();
        if (!tpl) return;

        int cs = world.chunkSize;
        int w  = Mathf.Max(1, tpl.sizeInTiles.x);
        int h  = Mathf.Max(1, tpl.sizeInTiles.y);

        const int tries = 20;
        for (int t = 0; t < tries; ++t)
        {
            int lx = Random.Range(0, cs - w + 1);
            int ly = Random.Range(0, cs - h + 1);

            int wx = ch.position.x * cs + lx;
            int wy = ch.position.y * cs + ly;

            if (!CheckSurface(tpl, wx, wy)) continue;

            Vector3 pos = new(wx + w * 0.5f, wy + h * 0.5f, 0);
            Unit u = Spawn(tpl, pos, 2);
            if (u) live.Add(u);
            break;
        }
    }

    /* ───────────────── global ambience loop ───────────────── */

    IEnumerator GlobalAmbientLoop()
    {
        WaitForSeconds wait = new(ambientInterval);
        Camera cam = Camera.main;

        while (true)
        {
            yield return wait;

            if (cam == null) cam = Camera.main;
            if (cam == null) continue;

            var visible = live.Where(u =>
                           u && !u.Dead &&
                           u.gameObject.activeInHierarchy &&
                           IsOnScreen(cam, u.transform.position))
                          .ToList();

            if (visible.Count == 0) continue;

            visible[Random.Range(0, visible.Count)].PlayAmbient();
        }
    }

    static bool IsOnScreen(Camera cam, Vector3 worldPos)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
    }

    /* ───────────────── surface validation ───────────────── */

    bool CheckSurface(UnitTemplate tpl, int wx, int wy)
    {
        int w = Mathf.Max(1, tpl.sizeInTiles.x);
        int h = Mathf.Max(1, tpl.sizeInTiles.y);

        switch (tpl.surface)
        {
            case SpawnSurface.Any:
            case SpawnSurface.Air:
                return BodyEmptyAndDry(wx, wy, w, h);

            case SpawnSurface.Ground:
                return BodyEmptyAndDry(wx, wy, w, h) && SolidFloorUnder(wx, wy, w);

            case SpawnSurface.Liquid:
                for (int dx = 0; dx < w; ++dx)
                for (int dy = 0; dy < h; ++dy)
                    if (!world.IsLiquid(world.GetLiquidID(wx + dx, wy + dy)))
                        return false;
                return true;
        }
        return false;
    }

    bool BodyEmptyAndDry(int wx, int wy, int w, int h)
    {
        for (int dx = 0; dx < w; ++dx)
        for (int dy = 0; dy < h; ++dy)
        {
            if (world.GetTileID(wx + dx, wy + dy, ChunkLayer.Front) > 0) return false;
            if (world.GetLiquidID(wx + dx, wy + dy)                > 0) return false;
        }
        return true;
    }

    bool SolidFloorUnder(int wx, int wy, int w)
    {
        for (int dx = 0; dx < w; ++dx)
        {
            int idBelow = world.GetTileID(wx + dx, wy - 1, ChunkLayer.Front);
            if (idBelow <= 0 || world.IsLiquid(idBelow)) return false;
        }
        return true;
    }

    /* ───────────────── misc helpers ───────────────── */

    void OnAreaChanged(AreaData a) =>
        areaTable = TableCache.GetUnits(a, a ? a.Units : null);

    Vector2Int WorldPosToChunk(Vector3 p)
    {
        int cs = world.chunkSize;
        return new(Mathf.FloorToInt(p.x / cs),
                   Mathf.FloorToInt(p.y / cs));
    }

    Unit Spawn(UnitTemplate tpl, Vector3 pos, int team = 0)
    {
        if (!tpl || !tpl.modelPrefab) return null;

        GameObject go = Instantiate(tpl.modelPrefab, pos, Quaternion.identity, mobRoot);
        go.name = tpl.displayName;

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            sr.sortingOrder = unitSortingOrder;

        Unit u = go.GetComponent<Unit>() ?? go.AddComponent<Unit>();
        u.template = tpl;
        u.team     = team;
        return u;
    }

    /* ───────────────── public helpers ───────────────── */

    public IEnumerable<Unit> Units => live.Where(u => u && !u.Dead);

    public void KillAllExceptTeam(int keepTeam) =>
        live.Where(u => u && u.team != keepTeam).ToList()
            .ForEach(u => u.TakeDamage(int.MaxValue));
}
