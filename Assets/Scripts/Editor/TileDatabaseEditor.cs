#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/*───────────────────────────────────────────────────────────────────────────
 *  TileDatabaseEditor – custom inspector
 *─────────────────────────────────────────────────────────────────────────*/
[CustomEditor(typeof(TileDatabase))]
public class TileDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(6);

        if (GUILayout.Button("Scan, Sort & Fix Tile IDs", GUILayout.Height(28)))
        {
            var db = (TileDatabase)target;

            /* 1 ◂ gather every TileData asset in the project */
            var allTiles = GatherAllTileAssets();

            /* 2 ◂ warn about duplicates before we overwrite anything */
            var dupIDs = FindDuplicateIDs(allTiles);
            if (dupIDs.Count > 0)
                Debug.LogWarning(
                    $"[TileDatabase] Duplicate tileIDs detected: {string.Join(", ", dupIDs)} – " +
                    "they will be auto-fixed by re-indexing.");

            /* 3 ◂ sort, re-index, write back */
            SortTilesAndAssignIDs(db, allTiles);

            /* 4 ◂ flag the database as dirty so Unity saves it */
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TileDatabase] {allTiles.Count} tiles scanned, sorted and IDs synchronised.");
        }
    }

    /* ───────────── helpers ───────────── */

    static List<TileData> GatherAllTileAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:TileData");
        var list = new List<TileData>(guids.Length);

        foreach (string g in guids)
        {
            var t = AssetDatabase.LoadAssetAtPath<TileData>(AssetDatabase.GUIDToAssetPath(g));
            if (t) list.Add(t);
        }
        return list.Distinct().ToList();         // remove accidental duplicates
    }

    static void SortTilesAndAssignIDs(TileDatabase db, List<TileData> tiles)
    {
        /* remove the sentinels from the working list so we don’t re-index them */
        tiles.Remove(db.SkyAirTile);
        tiles.Remove(db.UndergroundAirTile);
        tiles.Remove(db.NULLTile);

        tiles = tiles.OrderBy(t => t.tileName).ToList();

        var ordered = new List<TileData>();

        /* 0 NULL-tile (kept at index 0 so tileID == index is always safe) */
        if (db.NULLTile)
        {
            Undo.RecordObject(db.NULLTile, "Assign Tile ID");
            db.NULLTile.tileID = 0;
            EditorUtility.SetDirty(db.NULLTile);
            ordered.Add(db.NULLTile);
        }

        /* 1 Sky-air */
        if (db.SkyAirTile)
        {
            Undo.RecordObject(db.SkyAirTile, "Assign Tile ID");
            db.SkyAirTile.tileID = 1;
            EditorUtility.SetDirty(db.SkyAirTile);
            ordered.Add(db.SkyAirTile);
        }

        /* 2 Underground-air */
        if (db.UndergroundAirTile)
        {
            Undo.RecordObject(db.UndergroundAirTile, "Assign Tile ID");
            db.UndergroundAirTile.tileID = 2;
            EditorUtility.SetDirty(db.UndergroundAirTile);
            ordered.Add(db.UndergroundAirTile);
        }

        /* 3…N regular tiles, alphabetically by name */
        int nextID = 3;
        foreach (TileData t in tiles)
        {
            Undo.RecordObject(t, "Assign Tile ID");
            t.tileID = nextID++;
            EditorUtility.SetDirty(t);
            ordered.Add(t);
        }

        /* finally replace the array inside the database */
        Undo.RecordObject(db, "Update TileDatabase");
        db.tiles = ordered.ToArray();
    }

    static List<int> FindDuplicateIDs(IEnumerable<TileData> tiles)
    {
        var dup  = new List<int>();
        var seen = new HashSet<int>();

        foreach (TileData t in tiles.Where(x => x))
            if (!seen.Add(t.tileID))
                dup.Add(t.tileID);

        return dup;
    }
}
#endif
