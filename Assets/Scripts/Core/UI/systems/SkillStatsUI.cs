using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public sealed class SkillStatsUI : MonoBehaviour
{
    /* ------- layout constants ------- */
    const float RowW = 300f, HeaderY = -20, FirstRowY = -40, LineH = 16;

    [SerializeField] TMP_Text labelPrefab;   // optional
    [SerializeField] TMP_Text headerPrefab;  // optional (bold)

    readonly Dictionary<SkillId, TMP_Text> rows = new();
    List<SkillDefinition> defs;          // only those that exist

    SkillManager  sm;
    SkillDatabase db;
    Canvas        root;
    TMP_Text      header;

    /* -------- public entry --------- */
    public void Initialize(Canvas canvas, SkillManager mgr, SkillDatabase database)
    {
        root = canvas; sm = mgr; db = database;

        EnsurePrefabs();
        BuildUI();

        sm.OnXpGained += (_, __) => Redraw();
        sm.OnLevelUp  += (_, __) => Redraw();
    }

    /* -------- prefab helpers ------- */
    void EnsurePrefabs()
    {
        if (!labelPrefab )
            labelPrefab  = MakePrefab("Row",    14, FontStyles.Normal);
        if (!headerPrefab)
            headerPrefab = MakePrefab("Header", 15, FontStyles.Bold);
    }
    static TMP_Text MakePrefab(string n,int size,FontStyles style)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(RowW, 18);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.TopRight;
        t.color = Color.white; t.raycastTarget = false;
        go.SetActive(false);                     // template only
        return t;
    }

    /* -------- build UI ------------- */
    void BuildUI()
    {
        // collect only skills that really exist
        defs = db.All.Where(d => d != null)
                     .OrderBy(d => (int)d.id)
                     .ToList();

        header = Instantiate(headerPrefab, root.transform);
        Show(header, new Vector2(-20, HeaderY));

        int i = 0;
        foreach (var d in defs)
        {
            var txt = Instantiate(labelPrefab, root.transform);
            Show(txt, new Vector2(-20, FirstRowY - i * LineH));
            rows[d.id] = txt;
            ++i;
        }

        Redraw();
    }

    static void Show(TMP_Text t, Vector2 anchoredPos)
    {
        t.gameObject.SetActive(true);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(RowW, rt.sizeDelta.y);
        rt.anchoredPosition = anchoredPos;
    }

    /* -------- redraw --------------- */
    void Redraw()
    {
        header.text = $"<b>Total Lv {sm.TotalLevel}   {sm.TotalXp:N0} XP</b>";

        foreach (var d in defs)
        {
            int lvl   = sm.GetLevel(d.id);
            int xp    = sm.GetXp  (d.id);
            int next  = d.GetXpForLevel(Mathf.Min(lvl + 1, SkillDefinition.MaxLevel));
            int left  = next - xp;

            rows[d.id].text =
                $"{d.id,-12} Lv {lvl,2}  {xp:N0} XP  ← {left:N0}";
        }
    }
}
