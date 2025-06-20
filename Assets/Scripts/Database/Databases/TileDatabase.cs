using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
#endif

/*───────────────────────────────────────────────────────────────────────────
 *  TileDatabase  (runtime asset)
 *─────────────────────────────────────────────────────────────────────────*/
[CreateAssetMenu(fileName = "TileDatabase",
                 menuName  = "Game/Database/new Tile Database")]
public class TileDatabase : ScriptableObject
{
    /* ───────── Tiles ───────── */
    [Header("Tiles")]
    public TileData[] tiles;                    // auto-filled by editor button
    public TileData   NULLTile;               // ID ⇢ 0   (reserved)
    public TileData SkyAirTile;               // ID ⇢ 1   (reserved)
    public TileData   UndergroundAirTile;       // ID ⇢ 2   (sentinel)
    public TileData   PointOfIntrestTile;

    /* ───────────────────────────────────────────────────────── */
    /*  Public look-ups                                         */
    /* ───────────────────────────────────────────────────────── */

    /// <summary>Direct array access (index == array slot).</summary>
    public TileData GetTileDataByIndex(int index)
    {
        if (index >= 0 && index < tiles.Length)
            return tiles[index];

        Debug.LogWarning($"Tile index [{index}] is out of range.");
        return null;
    }

    /// <summary>Lookup by tileID (unique integer — may be –1, 0, 1…N).</summary>
public TileData GetTileDataByID(int tileID)
{
    if (tileID == 0) return NULLTile;          // 0  → NULL
    if (tileID == 1) return SkyAirTile;        // 1  → sky
    if (tileID == 2) return UndergroundAirTile;// 2  → underground

    if (tileID >= 0 && tileID < tiles.Length)  // still catches 0-2, but fast-out above
        return tiles[tileID];

    Debug.LogWarning($"Requested tileID [{tileID}] not found in the database.");
    return null;
}


    /// <summary>Slow path: search by asset name.</summary>
    public TileData GetTileDataByName(string tileName)
    {
        foreach (var t in tiles)
            if (t && t.name == tileName)
                return t;

        if (SkyAirTile         && SkyAirTile.name         == tileName) return SkyAirTile;
        if (UndergroundAirTile && UndergroundAirTile.name == tileName) return UndergroundAirTile;

        Debug.LogWarning($"Tile with name [{tileName}] not found in the database.");
        return null;
    }
}
