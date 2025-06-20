using UnityEngine;

[CreateAssetMenu(fileName = "UnitDatabase",
                 menuName  = "Game/Database/Unit Database")]
public class UnitDatabase : ScriptableObject
{
    [Header("Slot = Unit ID (0, 1, 2 …)")]
    public UnitTemplate[] units;     // index *is* the ID

    /* ---------- look-ups (same style as TileDatabase) ---------- */

    /// <summary>Direct array access (id == index).</summary>
    public UnitTemplate GetByIndex(int id)
    {
        if (id >= 0 && id < units.Length)
            return units[id];

        Debug.LogWarning($"Unit index/id [{id}] is out of range.");
        return null;
    }

    /// <summary>Alias to <see cref="GetByIndex"/> for API symmetry.</summary>
    public UnitTemplate GetById(int id) => GetByIndex(id);

    /// <summary>Slow linear search by display or asset name.</summary>
    public UnitTemplate GetByName(string name)
    {
        foreach (var t in units)
            if (t && (t.displayName == name || t.name == name))
                return t;

        Debug.LogWarning($"Unit with name “{name}” not found in the database.");
        return null;
    }

    public int Count => units?.Length ?? 0;
}
