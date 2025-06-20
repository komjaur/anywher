using UnityEngine;
using System;   // for [Flags] enum below

public enum SpawnSurface
{
    Any,
    Ground,
    Air,
    Liquid
}



[CreateAssetMenu(fileName = "Unit_", menuName = "Game/Unit/New Unit")]
public class UnitTemplate : ScriptableObject
{
    [Header("Presentation")]
    public string     displayName;
    public GameObject modelPrefab;

    [Header("Spawn rules")]
    public SpawnSurface surface = SpawnSurface.Any;   // ✔ matches UnitManager

    public Vector2Int sizeInTiles = new(1, 1);
    public ChunkFlags allowedFlags = ChunkFlags.None;

    [Header("Stats")]
    public int defaultHP = 100;                       // ✔ resolves tpl.defaultHP
}
