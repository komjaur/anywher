// ------------------------------------------------------------
// Fills AreasDatabase & BiomeDatabase from a folder selection
// ------------------------------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class DatabasePopulator
{
    /* ──────────────────── MENU ENTRIES ──────────────────── */

    [MenuItem(WorldMenuRoot.PATH + "Populate Databases From Selection",
              false,
              WorldMenuRoot.PRIORITY + 1)]
    private static void Populate() => PopulateImpl();

    [MenuItem(WorldMenuRoot.PATH + "Populate Databases From Selection", true)]
    private static bool ValidatePopulate() =>
        TryGetSelection(out _, out _, out _);     // enables / disables the item

    // Context-menu variant (right-click any asset / folder)
    [MenuItem("CONTEXT/DefaultAsset/World/Populate Databases From Selection")]
    private static void PopulateFromContext() => PopulateImpl();

    /* ──────────────────── CORE ──────────────────── */

    private static void PopulateImpl()
    {
        if (!TryGetSelection(out string folderPath,
                             out AreasDatabase areasDB,
                             out BiomeDatabase biomeDB))
            return;

        /* -- collect AreaData ------------------------------------------------ */
        var areaGuids = AssetDatabase.FindAssets("t:AreaData", new[] { folderPath });
        var areaList  = new List<AreaData>();
        foreach (string g in areaGuids)
        {
            var a = AssetDatabase.LoadAssetAtPath<AreaData>(
                        AssetDatabase.GUIDToAssetPath(g));
            if (a != null && !areaList.Contains(a))
                areaList.Add(a);
        }

        /* -- collect BiomeData (direct + referenced) ------------------------- */
        var biomeSet = new HashSet<BiomeData>();

        // (a) direct BiomeData inside the folder
        var biomeGuids = AssetDatabase.FindAssets("t:BiomeData", new[] { folderPath });
        foreach (string g in biomeGuids)
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeData>(
                        AssetDatabase.GUIDToAssetPath(g));
            if (b != null) biomeSet.Add(b);
        }

        // (b) biomes referenced by areas
        foreach (var a in areaList)
            if (a != null && a.biomes != null)
                foreach (var b in a.biomes)
                    if (b != null) biomeSet.Add(b);

        /* -- write back to databases ---------------------------------------- */
        areasDB.areas     = areaList.ToArray();
        biomeDB.biomelist = biomeSet.ToArray();

        EditorUtility.SetDirty(areasDB);
        EditorUtility.SetDirty(biomeDB);
        AssetDatabase.SaveAssets();

        Debug.Log($"[DatabasePopulator] Updated '{areasDB.name}' with {areasDB.areas.Length} areas "
                + $"and '{biomeDB.name}' with {biomeDB.biomelist.Length} biomes.");
    }

    /* ──────────────────── VALIDATION HELPERS ──────────────────── */

    /// <summary>Checks selection & extracts folder + two databases.</summary>
    private static bool TryGetSelection(
        out string folderPath,
        out AreasDatabase areasDB,
        out BiomeDatabase biomeDB)
    {
        folderPath = null;
        areasDB    = null;
        biomeDB    = null;

        var sel = Selection.objects;
        if (sel.Length < 3) return false;

        foreach (var obj in sel)
        {
            switch (obj)
            {
                case AreasDatabase a: areasDB = a; break;
                case BiomeDatabase b: biomeDB = b; break;
                default:
                    if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                        folderPath = AssetDatabase.GetAssetPath(obj);
                    break;
            }
        }

        return folderPath != null && areasDB != null && biomeDB != null;
    }
}
#endif
