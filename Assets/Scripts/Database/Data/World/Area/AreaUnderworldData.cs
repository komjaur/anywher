using UnityEngine;

// AreaUnderworldData.cs
[CreateAssetMenu(fileName = "Area_UW_", menuName = "Game/World/Area/Underworld Area")]
public class AreaUnderworldData : AreaData
{
    [Header("Vertical band")]
    [Range(0f, 1f)] public float minDepth = 0f;
    [Range(0f, 1f)] public float maxDepth = 1f;

    [Header("Horizontal heat band  (+1 = far‑left, −1 = far‑right)")]
    [Range(-1f, 1f)] public float minHeat = -1f;
    [Range(-1f, 1f)] public float maxHeat =  1f;
}