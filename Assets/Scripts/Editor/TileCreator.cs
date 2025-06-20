using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

public static class TileCreator
{
    private const string DefaultTileFolder = "Assets"; // Where new tiles go if no folder is selected

    [MenuItem("Assets/Create/New Tile", priority = 1)]
    private static void CreateNewTile_AssetsMenu()
    {
        // Try to get a TileDatabase from the current selection or look up in the project
        TileDatabase db = TryGetSelectedTileDatabase() ?? FindAnyTileDatabase();
        if (db == null)
        {
            Debug.LogWarning("No TileDatabase selected or found. Tile creation canceled.");
            return;
        }

        // Get the path of whatever is selected in the Project window
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(folderPath))
        {
            // If user hasn't selected anything, place the new file under DefaultTileFolder
            folderPath = DefaultTileFolder;
        }
        else if (!AssetDatabase.IsValidFolder(folderPath))
        {
            // If user selected a file instead of a folder, get its parent folder
            folderPath = Path.GetDirectoryName(folderPath);
        }

        CreateNewTile(db, folderPath);
    }

    private static void CreateNewTile(TileDatabase tileDb, string folderPath)
    {
        // Determine next available tileID
        int nextID = 0;
        foreach (var existing in tileDb.tiles)
        {
            nextID = Mathf.Max(nextID, existing.tileID + 1);
        }

        // Create the TileData asset in memory
        TileData newTile = ScriptableObject.CreateInstance<TileData>();
        newTile.tileID = nextID;
        newTile.name = "Tile_" + nextID;
        newTile.tileName = "New Tile " + nextID;

        // Generate unique path
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(folderPath, newTile.name + ".asset")
        );

        // Create the asset on disk
        AssetDatabase.CreateAsset(newTile, assetPath);
        AssetDatabase.SaveAssets();

        // Append it to the TileDatabase array
        var tileList = tileDb.tiles.ToList();
        tileList.Add(newTile);
        tileDb.tiles = tileList.ToArray();

        // Mark the TileDatabase as dirty so changes persist
        EditorUtility.SetDirty(tileDb);
        AssetDatabase.SaveAssets();

        Debug.Log($"Created new TileData: '{assetPath}' with tileID={nextID}. Added to {tileDb.name}.");
    }

    private static TileDatabase TryGetSelectedTileDatabase()
    {
        // If the user selected a TileDatabase, return it
        if (Selection.activeObject is TileDatabase db)
            return db;
        return null;
    }

    private static TileDatabase FindAnyTileDatabase()
    {
        // Look for any TileDatabase in the entire project
        string[] guids = AssetDatabase.FindAssets("t:TileDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<TileDatabase>(path);
        }
        return null;
    }
}
