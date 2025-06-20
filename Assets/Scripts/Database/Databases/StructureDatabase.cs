using UnityEngine;

[CreateAssetMenu(fileName = "StructureDatabase", menuName = "Game/Database/Structure Database")]
public class StructureDatabase : ScriptableObject
{
    [System.Serializable]
    public class StructureEntry
    {
        [Header("Structure Info")]
        public string structureName = "Unnamed Structure";
        public BlueprintStructure structureBlueprint;

        [Tooltip("Max times this structure can appear in a single world.")]
        public int maxSpawnsPerWorld = 1;

        [Tooltip("List of areas where this structure can spawn. If empty, it can spawn in ANY area.")]
        public AreaData[] allowedAreas;          // ⬅️ UUS

        [Header("Chunk Flags Requirement")]
        public ChunkFlags requiredFlags = ChunkFlags.None;

        /* ----------  HELPER FUNKTIOONID  ---------- */

        /// <summary>True = võib selles areas spawn’ida.</summary>
        public bool CanSpawnInArea(AreaData area)
        {
            if (allowedAreas == null || allowedAreas.Length == 0) return true;

            foreach (var a in allowedAreas)
                if (a == area) return true;

            return false;
        }

        /// <summary>Kontrollib, kas tüki flag’id sobivad.</summary>
        public bool CanSpawnWithFlags(ChunkFlags chunkFlags)
        {
            if (requiredFlags == ChunkFlags.None) return true;
            return (chunkFlags & requiredFlags) == requiredFlags;
        }
    }

    [Header("Available Structures")]
    public StructureEntry[] structures;

    public StructureEntry GetStructureEntry(int index)
    {
        if (structures == null || structures.Length == 0) return null;
        if (index >= 0 && index < structures.Length) return structures[index];

        Debug.LogWarning($"Structure index [{index}] is out of range.");
        return null;
    }
}
