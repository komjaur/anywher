
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
public enum SkillId
{
    Woodcutting,
    Mining,
    Fishing,
    Cooking,
    Agility,
    Smithing,
    Crafting,
    Melee,
    Ranged,
    Magic,
    Exploration
    // …add more as needed
}







[CreateAssetMenu(fileName = "SkillDatabase",
                 menuName  = "Game/Database/Skill Database")]
public sealed class SkillDatabase : ScriptableObject
{
    [Tooltip("Drag any SkillDefinition assets here (order no longer matters).")]
    public List<SkillDefinition> definitions = new();

    Dictionary<SkillId, SkillDefinition> map;

    /* ---------- build map once when asset wakes ---------- */
    void OnEnable()
    {
        map = new();
        foreach (var def in definitions)
        {
            if (!def) continue;                      // skip null slots
            if (!map.TryAdd(def.id, def))
                Debug.LogWarning($"SkillDatabase: duplicate entry for {def.id}");
        }
    }

    /* ---------- public api ---------- */
    public SkillDefinition Get(SkillId id) =>
        map.TryGetValue(id, out var def) ? def : null;

    /// All valid definitions in deterministic enum order
    public IEnumerable<SkillDefinition> All =>
        map.Values.OrderBy(d => (int)d.id);
}
