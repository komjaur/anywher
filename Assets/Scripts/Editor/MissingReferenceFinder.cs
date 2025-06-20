
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MissingReferenceFinder
{
    private const string MENU = "Tools/Validation/Find Missing References";

    [MenuItem(MENU)]
    private static void FindMissing()
    {
        var problems = new List<string>();
        var assetPaths = AssetDatabase.GetAllAssetPaths();

        for (int i = 0; i < assetPaths.Length; i++)
        {
            string path = assetPaths[i];
            if (!path.StartsWith("Assets/")) continue;               // ignore packages

            // show a cancellable progress-bar so huge projects don’t lock the editor
            if (EditorUtility.DisplayCancelableProgressBar(
                    "Scanning assets…", path, i / (float)assetPaths.Length))
            {
                Debug.LogWarning("Missing-reference scan cancelled.");
                break;
            }

            // An .asset can hold many sub-assets (sprites in an atlas, etc.)
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subAsset == null) continue;                      // happens with broken sub-assets
                ScanObject(subAsset, path, problems);
            }
        }

        EditorUtility.ClearProgressBar();

        if (problems.Count == 0)
        {
            Debug.Log("<b>No missing references found 🎉</b>");
        }
        else
        {
            Debug.LogWarning($"<b>Found {problems.Count} missing reference(s):</b>\n" +
                             string.Join("\n", problems));
        }
    }

    // ---------------------------------------------------------------------

    private static void ScanObject(Object obj, string assetPath, List<string> problems)
    {
        var so = new SerializedObject(obj);
        var prop = so.GetIterator();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = true;

            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            // Missing reference ⇢ value is null **but** YAML still holds an ID
            if (prop.objectReferenceValue == null &&
                prop.objectReferenceInstanceIDValue != 0)
            {
                problems.Add($"⚠ {assetPath}\n   → {prop.propertyPath}");
            }
        }
    }
}
