using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

/// Heads-up display: shows clock, biome, area and (optionally) the
/// tile under the mouse cursor.
public sealed class HudUI : MonoBehaviour
{
    /* public accessor so ToastSystem knows where to place its pop-ups */
    public Canvas Canvas => hudCanvas;

    Canvas   hudCanvas;
    TMP_Text hudLabel;
    bool     showTileUnderMouse;

    EnvironmentManager env;
    PlayerManager      pm;
    Camera             cam;
    World              world;

    readonly StringBuilder sb = new();

    /* ----------------------------------------------------------- */
    public void Initialize(Canvas canvasPrefab,
                           bool   showTileDebug,
                           EnvironmentManager envMgr,
                           PlayerManager      playerMgr)
    {
        env               = envMgr;
        pm                = playerMgr;
        showTileUnderMouse = showTileDebug;

        BuildCanvas(canvasPrefab);
        HookEvents();
        UpdateHudText();                 // first draw
    }

    /* ---------------- canvas builder ---------------- */
    void BuildCanvas(Canvas prefab)
    {
        if (prefab)
        {
            hudCanvas = Instantiate(prefab, transform);
            hudLabel  = hudCanvas.GetComponentInChildren<TMP_Text>();
        }
        else
        {
            hudCanvas            = new GameObject("HUD").AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.transform.SetParent(transform, false);

            hudLabel = new GameObject("HudLabel").AddComponent<TextMeshProUGUI>();
            hudLabel.transform.SetParent(hudCanvas.transform, false);
            hudLabel.fontSize  = 11;
            hudLabel.alignment = TextAlignmentOptions.TopLeft;
        }

        var rt = hudLabel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(20, -20);
    }

    /* ---------------- event hooks ---------------- */
    void HookEvents()
    {
        if (env)
        {
            env.OnDayBegin    += _ => UpdateHudText();
            env.OnSeasonBegin += _ => UpdateHudText();
        }
        if (pm)
        {
            pm.OnBiomeChanged += _ => UpdateHudText();
            pm.OnAreaChanged  += _ => UpdateHudText();
        }
    }

    /* ---------------- per-frame ---------------- */
    void LateUpdate()
    {
        /* ensure we have camera & world references */
        if (!cam)
            TryFetchCamera();

        if (world == null)
            world = GameManager.Instance.WorldManager?.GetCurrentWorld();

        UpdateHudText();
    }

    void TryFetchCamera() =>
        cam = pm && pm.ActiveCamera ? pm.ActiveCamera.GetComponent<Camera>()
                                    : Camera.main;

    /* ---------------- HUD text builder ---------------- */
    void UpdateHudText()
    {
        if (!hudLabel || env == null || pm == null) return;

        sb.Clear();

        /* time + day */
        float hrs = env.TimeOfDayHours;
        int   hh  = (int)hrs % 24;
        int   mm  = (int)((hrs - hh) * 60f);

        sb.AppendFormat("{0:00}:{1:00} | Day {2} ({3})\n",
                        hh, mm, env.DayCount + 1, SeasonName(env.SeasonIndex));

        /* biome & area */
        sb.Append("Biome : ").Append(pm.CurrentBiome ? pm.CurrentBiome.name : "—").Append('\n')
          .Append("Area  : ").Append(pm.CurrentArea  ? pm.CurrentArea.name  : "—");

        /* optional tile-under-cursor read-out */
        if (showTileUnderMouse && cam != null && world != null && world.tiles != null)

        {
            Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
            int wx = Mathf.FloorToInt(wp.x);
            int wy = Mathf.FloorToInt(wp.y);

            (ChunkLayer layer,string tag)[] L = {
                (ChunkLayer.Front,"F"), (ChunkLayer.Background,"B"),
                (ChunkLayer.Liquid,"L"),(ChunkLayer.Overlay,"O")
     
            };

            sb.Append('\n');
            foreach (var (layer, tag) in L)
            {
                int id = world.GetTileID(wx, wy, layer);
                sb.Append("Tile ").Append(tag).Append(": ");
                AppendTileInfo(id >= 0 ? world.tiles.GetTileDataByID(id) : null, id);
                if (tag != "R") sb.Append("  |  ");
            }
        }

        hudLabel.text = sb.ToString();
    }

    static string SeasonName(int i) =>
        (i & 3) switch { 0 => "Spring", 1 => "Summer", 2 => "Autumn", _ => "Winter" };

    void AppendTileInfo(TileData td, int id) =>
        sb.Append(td ? $"{td.tileName} ({id})" : "—");
}
