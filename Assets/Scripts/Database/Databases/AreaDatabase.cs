using UnityEngine;

[CreateAssetMenu(fileName = "AreasDatabase", menuName = "Game/Database/New Areas Database")]
public class AreasDatabase : ScriptableObject
{
    public AreaData[] areas;

    public AreaData GetAreaData(int index)
    {
        if (index >= 0 && index < areas.Length)
            return areas[index];

        Debug.LogWarning($"Area index [{index}] is out of range.");
        return null;
    }
}

