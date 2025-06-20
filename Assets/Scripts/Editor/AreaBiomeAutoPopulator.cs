// ------------------------------------------------------------
// Populates AreaData.biomes by reading each Area's "biomes" sub-folder
// ------------------------------------------------------------
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class AreaBiomePopulator
{
    /* ──────────────────── MENU ENTRIES ──────────────────── */

    // Top-bar Assets menu
    [MenuItem(WorldMenuRoot.PATH + "Populate Area Biomes",       // ← same location
              false,
              WorldMenuRoot.PRIORITY + 0)]
    private static void PopulateFromAssetsMenu() => RunOnCurrentSelection();

    // Validation so the menu is greyed-out when nothing useful is selected
    [MenuItem(WorldMenuRoot.PATH + "Populate Area Biomes", true)]
    private static bool ValidateAssetsMenu() => HasPopulatableSelection();

    // Project-window context menu (right-click on any asset / folder)
    [MenuItem("CONTEXT/DefaultAsset/World/Populate Area Biomes")]
    private static void PopulateFromContextMenu() => RunOnCurrentSelection();

    /* ──────────────────── INSPECTOR BUTTON ──────────────────── */

    [CustomEditor(typeof(AreaData))]
    private class AreaDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(6);

            if (GUILayout.Button("Populate Biomes From Folder"))
                PopulateArea((AreaData)target);
        }
    }

    /* ──────────────────── CORE LOGIC ──────────────────── */

    private static void RunOnCurrentSelection()
    {
        HashSet<AreaData> touched = new();

        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            if (AssetDatabase.IsValidFolder(path))
            {
                // Scan folder recursively for AreaData
                string[] guids = AssetDatabase.FindAssets("t:AreaData", new[] { path });
                foreach (string g in guids)
                {
                    var a = AssetDatabase.LoadAssetAtPath<AreaData>(AssetDatabase.GUIDToAssetPath(g));
                    if (a) touched.Add(a);
                }
            }
            else if (obj is AreaData a)
            {
                touched.Add(a);
            }
        }

        foreach (AreaData a in touched)
            PopulateArea(a);

        if (touched.Count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AreaBiomePopulator] Updated {touched.Count} AreaData assets.");
        }
    }

    private static bool HasPopulatableSelection()
    {
        foreach (Object obj in Selection.objects)
        {
            if (obj is AreaData) return true;
            if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj))) return true;
        }
        return false;
    }

    private static void PopulateArea(AreaData area)
    {
        if (area == null) return;

        string areaFolder   = Path.GetDirectoryName(AssetDatabase.GetAssetPath(area));
        string biomesFolder = Path.Combine(areaFolder, "biomes").Replace("\\", "/");

        if (!AssetDatabase.IsValidFolder(biomesFolder))
        {
            Debug.LogWarning($"[AreaBiomePopulator] No 'biomes' folder for “{area.name}”.");
            return;
        }

        string[] biomeGuids = AssetDatabase.FindAssets("t:BiomeData", new[] { biomesFolder });
        List<BiomeData> list = new(biomeGuids.Length);
        foreach (string g in biomeGuids)
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeData>(AssetDatabase.GUIDToAssetPath(g));
            if (b) list.Add(b);
        }

        if (list.Count == 0)
        {
            Debug.LogWarning($"[AreaBiomePopulator] Found 0 biomes in '{biomesFolder}'.");
            return;
        }

        Undo.RecordObject(area, "Populate Area Biomes");
        area.biomes = list.ToArray();
        EditorUtility.SetDirty(area);
    }
}
#endif
