using UnityEngine;
using System.Collections.Generic;
#if false
/// <summary>
/// A simple trap room: three solid walls, one open side, turrets on the walls,
/// and a loot chest (or spawner) in the center.
/// Anchor (ax, ay) is the *bottom-left* corner of the room’s interior.
/// </summary>
[CreateAssetMenu(
    fileName = "Blueprint_BoxTrap",
    menuName  = "Game/World/Blueprint/Structure/Box Trap")]
public class BlueprintBoxTrap : BlueprintStructure
{
    /* ───────────── inspector tunables ───────────── */

    [Header("Room Dimensions")]
    [Min(4)] public int width  = 10;
    [Min(4)] public int height = 6;
    [Min(1)] public int wallThickness = 1;

    public enum OpenSide { Bottom, Top, Left, Right }
    [Tooltip("Which side is left open as the entrance?")]
    public OpenSide openSide = OpenSide.Right;

    [Header("Tiles")]
    public TileData wallTile;        // REQUIRED – solid wall blocks
    public TileData turretTile;      // REQUIRED – auto-turret prefab tile
    public TileData chestTile;       // OPTIONAL – treasure / bait in centre
    public TileData backgroundTile;  // OPTIONAL – floor / plating look

    [Header("Turret Settings")]
    [Min(0)] public int turretCount = 3;   // how many turrets to place
    public bool randomTurretPositions = true;

    /* ───────────── main entry ───────────── */

    public override void PlaceStructure(World w, int ax, int ay)
    {
        /* 0 ─ sanity checks */
        if (!wallTile || !turretTile)
        {
            Debug.LogWarning("BlueprintBoxTrap: wallTile and turretTile are required!");
            return;
        }
        if (!w.IsUndergroundAir(w.GetTileID(ax, ay))) return;

        /* 1 ─ bounding box (interior) */
        int minX = ax;
        int maxX = ax + width  - 1;
        int minY = ay;
        int maxY = ay + height - 1;

        var innerCells   = new List<Vector2Int>(); // store interior for floor + POI
        var wallCells    = new List<Vector2Int>(); // closed-side wall tiles
        var turretSpots  = new List<Vector2Int>(); // candidate turret positions

        /* 2 ─ build walls & clear interior */
        for (int y = minY - wallThickness; y <= maxY + wallThickness; y++)
        {
            for (int x = minX - wallThickness; x <= maxX + wallThickness; x++)
            {
                bool withinInterior =
                    (x >= minX && x <= maxX && y >= minY && y <= maxY);

                if (withinInterior)
                {
                    innerCells.Add(new Vector2Int(x, y));

                    // clear front layer to air
                    if (w.Data.tileDatabase.UndergroundAirTile != null)
                        w.SetTileID(x, y,
                            w.Data.tileDatabase.UndergroundAirTile.tileID, isBackground:false);

                    // optional background / floor décor
                    if (backgroundTile != null)
                        w.SetTileID(x, y, backgroundTile.tileID, isBackground:true);
                }
                else
                {
                    // determine if this position belongs to the open side
                    bool onOpenSide = false;

                    // check each wall direction separately
                    for (int t = 0; t < wallThickness; t++)
                    {
                        switch (openSide)
                        {
                            case OpenSide.Bottom:
                                if (y == minY - 1 - t && x >= minX && x <= maxX)
                                    onOpenSide = true;
                                break;
                            case OpenSide.Top:
                                if (y == maxY + 1 + t && x >= minX && x <= maxX)
                                    onOpenSide = true;
                                break;
                            case OpenSide.Left:
                                if (x == minX - 1 - t && y >= minY && y <= maxY)
                                    onOpenSide = true;
                                break;
                            case OpenSide.Right:
                                if (x == maxX + 1 + t && y >= minY && y <= maxY)
                                    onOpenSide = true;
                                break;
                        }
                    }

                    // any tile not on open side is a wall
                    if (!onOpenSide)
                    {
                        w.SetTileID(x, y, wallTile.tileID, isBackground:false);
                        wallCells.Add(new Vector2Int(x, y));

                        // collect potential turret spots:
                        // * skip corners (looks better)
                        bool isCorner =
                            (x == minX - wallThickness && y == minY - wallThickness) ||
                            (x == minX - wallThickness && y == maxY + wallThickness) ||
                            (x == maxX + wallThickness && y == minY - wallThickness) ||
                            (x == maxX + wallThickness && y == maxY + wallThickness);

                        if (!isCorner)
                            turretSpots.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        /* 3 ─ place turrets */
        if (turretCount > 0 && turretSpots.Count > 0)
        {
            // clamp to available spots
            int tNeeded = Mathf.Min(turretCount, turretSpots.Count);

            if (randomTurretPositions)
            {
                // Fisher–Yates shuffle for random pick
                for (int i = 0; i < turretSpots.Count; i++)
                {
                    int j = Random.Range(i, turretSpots.Count);
                    (turretSpots[i], turretSpots[j]) = (turretSpots[j], turretSpots[i]);
                }
            }
            else
            {
                // simple spacing: sort then step through evenly
                turretSpots.Sort((a, b) =>
                    a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            }

            float step = (float)turretSpots.Count / tNeeded;
            for (int k = 0; k < tNeeded; k++)
            {
                int idx = randomTurretPositions
                    ? k                       // already shuffled
                    : Mathf.RoundToInt(k * step);

                Vector2Int p = turretSpots[idx];
                w.SetTileID(p.x, p.y, turretTile.tileID, isBackground:false);
            }
        }

        /* 4 ─ center chest / lure */
        if (chestTile != null)
        {
            Vector2Int center = new(
                (minX + maxX) / 2,
                (minY + maxY) / 2);

            w.SetTileID(center.x, center.y, chestTile.tileID, isBackground:false);
        }
    }
}
#endif