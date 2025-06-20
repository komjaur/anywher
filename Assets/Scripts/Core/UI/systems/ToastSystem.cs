using UnityEngine;
using System.Collections.Generic;

public sealed class ToastSystem : MonoBehaviour
{
    /* ------- inspector ------- */
    [Header("Layout")]
    [SerializeField] Vector2 startOffset = new(-20f, -200f);
    [SerializeField] float   stackSpacing = 28f;

    [Header("Merge window (sec)")]
    [SerializeField] float mergeWindow = 0.4f;

    /* ------- runtime state ---- */
    Canvas       canvas;
    GameObject   prefab;
    SkillDatabase db;

    readonly Queue<XpToastUI>            pool    = new();
    readonly List<XpToastUI>             live    = new();
    readonly Dictionary<SkillId,Buffer>  pending = new();
    struct Buffer { public int xp; public float t0; }

    /* -------------------------------------------------------------- */
    public void Initialize(GameObject toastPrefab,
                           Canvas      parentCanvas,
                           SkillDatabase database)
    {
        prefab = toastPrefab;
        canvas = parentCanvas;
        db     = database;

        var sm = GameManager.Instance.SkillManager;
        sm.OnXpGained += (id,xp) => BufferXp(id,xp);
        sm.OnLevelUp  += ShowLevelToast;
    }

    /* ------------------ merge logic ------------------------------ */
    void BufferXp(SkillId id, int xp)
    {
        if (db.Get(id) == null) return;            // undefined skill → ignore

        if (pending.TryGetValue(id, out var b))
        {
            b.xp += xp;
            pending[id] = b;
        }
        else pending[id] = new Buffer { xp = xp, t0 = Time.unscaledTime };
    }

    void LateUpdate()
    {
        float now = Time.unscaledTime;

        _keys ??= new List<SkillId>();
        _keys.Clear();
        _keys.AddRange(pending.Keys);

        foreach (var id in _keys)
        {
            var buf = pending[id];
            if (now - buf.t0 >= mergeWindow)
            {
                EmitXpToast(id, buf.xp);
                pending.Remove(id);
            }
        }
    }
    List<SkillId> _keys;

    /* ------------------ emit helpers ----------------------------- */
    void EmitXpToast(SkillId id, int xp)
    {
        var def = db.Get(id); if (def == null) return;
        SpawnToast($"{xp:N0} XP", def.icon, Color.white);
    }

    void ShowLevelToast(SkillId id, int lv)
    {
        var def = db.Get(id); if (def == null) return;
        SpawnToast($"{def.displayName}  Lv {lv}",
                   def.icon,
                   new Color(0.3f, 1f, 0.3f));
    }

    /* ------------------ spawn & stack ---------------------------- */
    void SpawnToast(string msg, Sprite icon, Color colour)
    {
        live.RemoveAll(t => !t.gameObject.activeSelf);      // compact list

        var ui = GetToast();
        live.Add(ui);

        var rt = (RectTransform)ui.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition =
            startOffset + Vector2.down * (stackSpacing * (live.Count - 1));

        ui.Init(msg, icon, colour, this);
        ui.transform.SetAsLastSibling();
    }

    /* ------------------ pooling ------------------------------- */
    XpToastUI GetToast()
    {
        if (pool.Count > 0)
        {
            var ui = pool.Dequeue();
            ui.gameObject.SetActive(true);
            return ui;
        }
        return Instantiate(prefab, canvas.transform).GetComponent<XpToastUI>();
    }

    public void Recycle(XpToastUI ui)
    {
        live.Remove(ui);
        ui.gameObject.SetActive(false);
        pool.Enqueue(ui);
    }
}
