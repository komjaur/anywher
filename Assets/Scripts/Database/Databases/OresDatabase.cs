using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* ================================================================
   GlobalOresDatabase.cs
   ----------------------------------------------------------------
   Now supports an *exclusion* list of areas (AreaData) instead of
   a biome whitelist.
   ================================================================*/

[System.Serializable]
public class OreSetting
{
    [Header("Meta")]
    [Tooltip("Friendly name shown in the Inspector list")]
    public string oreName = "New Ore";
    /* ─────────── core ─────────── */
    [Header("Core")]
    public TileData oreTile;

    [Tooltip("Perlin frequency used to scatter this ore")]
    public float noiseFrequency = 0.02f;

    [Range(0f,1f)] public float threshold = 0.55f;
    [Range(0f,1f)] public float chance    = 0.25f;
    [Tooltip("XY offset added to (wx, wy) before sampling Perlin")]
    public Vector2 noiseOffset = Vector2.zero;
    /* ─────────── depth gate ───── */
    [Header("Depth range (0 = surface, 1 = bedrock)")]
    [Range(0f,1f)] public float minDepthNorm = 0f;
    [Range(0f,1f)] public float maxDepthNorm = 1f;

    /* ─────────── area filter ──── */
    [Header("Areas where this ore **CAN** spawn (leave empty = anywhere)")]
    public List<AreaData> allowedAreas;

    /* ─────────── host filter ──── */
    [Header("Valid host tiles (empty = any solid)")]
    public List<TileData> validHostTiles;
}

/* ---------------------------------------------------------------- */
[CreateAssetMenu(fileName = "OresDatabase",
                 menuName = "Game/Database/Global Ore Database")]
public class OresDatabase : ScriptableObject
{
    public OreSetting[] ores;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (ores == null) return;
        foreach (var o in ores)
        {
            o.minDepthNorm = Mathf.Clamp01(o.minDepthNorm);
            o.maxDepthNorm = Mathf.Clamp01(o.maxDepthNorm);
            if (o.maxDepthNorm < o.minDepthNorm)
                o.maxDepthNorm = o.minDepthNorm;
        }
    }
#endif
}
