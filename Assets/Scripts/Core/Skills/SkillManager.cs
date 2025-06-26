using UnityEngine;
using System;
using System.Collections.Generic;

/// ──────────────────────────────────────────────────────────────
///  SkillManager
///     • stores per-skill XP
///     • converts XP ⇆ level
///     • raises OnXpGained / OnLevelUp
///     • never throws when a SkillId is missing from SkillDatabase
/// ──────────────────────────────────────────────────────────────
public sealed class SkillManager : MonoBehaviour
{
    /* ───────── events ───────── */
    public event Action<SkillId, int> OnXpGained;
    public event Action<SkillId, int> OnXpLost;
    public event Action<SkillId, int> OnLevelUp;
    public event Action<SkillId, int> OnLevelDown;

    /* ───────── public API ───────── */
    public int  GetXp       (SkillId id) => data.Get(id);
    public int  GetLevel    (SkillId id) => TryGetDef(id, out var d) ? d.GetLevelForXp(GetXp(id)) : 0;
    public int  GetTotalXp  (SkillId id) => GetXp(id);

    public int  GetXpToNextLevel(SkillId id)
    {
        if (!TryGetDef(id, out var def)) return 0;

        int lv = def.GetLevelForXp(GetXp(id));
        if (lv >= SkillDefinition.MaxLevel) return 0;

        int next = def.GetXpForLevel(lv + 1);
        return next - GetXp(id);
    }

    public float GetProgress(SkillId id)
    {
        if (!TryGetDef(id, out var def)) return 0f;

        int xp = GetXp(id);
        int lvl = def.GetLevelForXp(xp);
        if (lvl >= SkillDefinition.MaxLevel)
            return 1f;

        int cur = def.GetXpForLevel(lvl);
        int next = def.GetXpForLevel(lvl + 1);
        return Mathf.InverseLerp(cur, next, xp);
    }

    public int TotalXp
    {
        get { int s = 0; foreach (var kv in defs) s += GetXp(kv.Key);   return s; }
    }
    public int TotalLevel
    {
        get { int s = 0; foreach (var kv in defs) s += GetLevel(kv.Key); return s; }
    }

    /// Add XP. If the SkillId has no definition it is ignored.
    public int AddXp(SkillId id, int delta)
    {
        if (delta <= 0 || !TryGetDef(id, out var def)) return GetLevel(id);

        int beforeLevel = def.GetLevelForXp(GetXp(id));

        data.Add(id, delta);
        data.Set(id, Mathf.Min(GetXp(id), SkillDefinition.MaxXp));

        OnXpGained?.Invoke(id, delta);

        int newLevel = def.GetLevelForXp(GetXp(id));
        if (newLevel > beforeLevel)
            OnLevelUp?.Invoke(id, newLevel);

        return newLevel;
    }

    /// Remove XP. If the SkillId has no definition it is ignored.
    public int RemoveXp(SkillId id, int delta)
    {
        if (delta <= 0 || !TryGetDef(id, out var def)) return GetLevel(id);

        int beforeLevel = def.GetLevelForXp(GetXp(id));

        data.Add(id, -delta);
        data.Set(id, Mathf.Max(GetXp(id), 0));

        OnXpLost?.Invoke(id, delta);

        int newLevel = def.GetLevelForXp(GetXp(id));
        if (newLevel < beforeLevel)
            OnLevelDown?.Invoke(id, newLevel);

        return newLevel;
    }

    /* ───────── internals ───────── */
    [Header("Data source")]
    [SerializeField] SkillDatabase skillDatabase;

    readonly Dictionary<SkillId, SkillDefinition> defs = new();
    readonly PlayerSkills data = new();

    void Awake()
    {
        if (!skillDatabase && GameManager.Instance)
            skillDatabase = GameManager.Instance.GameData.skillDatabase;

        if (!skillDatabase)
        {
            Debug.LogError("SkillManager: SkillDatabase asset missing.");
            enabled = false;
            return;
        }

        foreach (var def in skillDatabase.All)
            if (def) defs[def.id] = def;         // build fast lookup table
    }

    /* helper ------------------------------------------------------- */
    bool TryGetDef(SkillId id, out SkillDefinition def) =>
        defs.TryGetValue(id, out def) && def != null;
}
