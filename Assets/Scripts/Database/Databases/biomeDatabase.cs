using UnityEngine;

[CreateAssetMenu(fileName = "BiomeDatabase", menuName = "Game/Database/New Biome Database")]
public class BiomeDatabase : ScriptableObject
{
    public BiomeData[] biomelist;

    public BiomeData GetBiomeData(int index)
    {
        if (index >= 0 && index < biomelist.Length)
        {
            return biomelist[index];
        }

        Debug.LogWarning($"BioMe index [{index}] is out of range.");
        return null;
    }
}
