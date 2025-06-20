using UnityEngine;

// AreaSkyworldData.cs
[CreateAssetMenu(fileName = "Area_SW_", menuName = "Game/World/Area/Skyworld Area")]
public class AreaSkyworldData : AreaData
{
    [Header("Vertical band")]
    [Range(0f, 1f)] public float minDepth = 0f;
    [Range(0f, 1f)] public float maxDepth = 1f;

    [Header("Horizontal heat band  (+1 = far‑left, −1 = far‑right)")]
    [Range(-1f, 1f)] public float minHeat = -1f;
    [Range(-1f, 1f)] public float maxHeat =  1f;
}