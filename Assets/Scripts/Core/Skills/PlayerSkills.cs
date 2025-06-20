using System;
using UnityEngine;

/// Pure data, JSON-friendly.  No dictionaries, no GC during play.
[Serializable]
public sealed class PlayerSkills
{
    // one XP slot per SkillId enum value
    [SerializeField] private int[] xp =
        new int[Enum.GetValues(typeof(SkillId)).Length];

    /* ---- canonical methods ---- */
    public int  Get  (SkillId id)          => xp[(int)id];
    public void Set  (SkillId id, int val) => xp[(int)id] = Mathf.Max(0, val);
    public void Add  (SkillId id, int d)   => Set(id, Get(id) + d);

}
