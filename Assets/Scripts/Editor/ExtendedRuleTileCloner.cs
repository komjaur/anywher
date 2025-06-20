// ------------------------------------------------------------
// Duplicates an ExtendedRuleTile while swapping its sprites
// ------------------------------------------------------------
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class CloneExtendedRuleTile : EditorWindow
{
    private ExtendedRuleTile sourceTile;
    private Texture2D        newSpriteSheet;

    /* ──────────────────── MENU ENTRIES ──────────────────── */

    [MenuItem(WorldMenuRoot.PATH + "Clone ExtendedRuleTile w/ New Sprites",
              false,
              WorldMenuRoot.PRIORITY + 2)]
    [MenuItem("CONTEXT/ExtendedRuleTile/World/Clone ExtendedRuleTile w/ New Sprites")]
    private static void Open() => GetWindow<CloneExtendedRuleTile>("Clone ExtendedRuleTile");

    /* ──────────────────── GUI ──────────────────── */

    private void OnGUI()
    {
        GUILayout.Label("1. Source ExtendedRuleTile:", EditorStyles.boldLabel);
        sourceTile = (ExtendedRuleTile)EditorGUILayout.ObjectField(sourceTile, typeof(ExtendedRuleTile), false);

        GUILayout.Space(6);
        GUILayout.Label("2. Texture2D containing the new sprites:", EditorStyles.boldLabel);
        newSpriteSheet = (Texture2D)EditorGUILayout.ObjectField(newSpriteSheet, typeof(Texture2D), false);

        GUI.enabled = sourceTile && newSpriteSheet;
        if (GUILayout.Button("Clone ▶ ExtendedRuleTile"))
            Clone();
        GUI.enabled = true;
    }

    /* ──────────────────── CORE ──────────────────── */

    private void Clone()
    {
        if (!sourceTile || !newSpriteSheet) return;

        /* -- determine paths & names --------------------------------------- */
        string targetDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(newSpriteSheet));

        string matSuffix = newSpriteSheet.name; // e.g. "tiles_o_coal"
        if (matSuffix.StartsWith("tiles_"))
            matSuffix = matSuffix.Substring("tiles_".Length);

        string cloneName = $"TileData_{matSuffix}";
        string clonePath = AssetDatabase.GenerateUniqueAssetPath(
                               Path.Combine(targetDir, cloneName + ".asset"));

        /* -- duplicate the ExtendedRuleTile ------------------------------- */
        var tileClone = Object.Instantiate(sourceTile);
        AssetDatabase.CreateAsset(tileClone, clonePath);

        /* -- build lookup tables ------------------------------------------ */
        // (a) sprites from NEW sheet
        var newSprites = AssetDatabase
                         .LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(newSpriteSheet))
                         .OfType<Sprite>()
                         .ToDictionary(GetNumericSuffixSafe);

        // (b) sprites from ORIGINAL sheet (to map defaults)
        Sprite firstSrcSprite = FindFirstRuleSprite(sourceTile);
        if (!firstSrcSprite)
        {
            Abort("Source tile has no sprites.", clonePath);
            return;
        }

        /* -- swap sprites, suffix-by-suffix ------------------------------- */
        foreach (var rule in tileClone.m_TilingRules)
        {
            for (int i = 0; i < rule.m_Sprites.Length; i++)
            {
                Sprite old = rule.m_Sprites[i];
                if (!old) continue;

                string id = GetNumericSuffixSafe(old);
                if (id != null && newSprites.TryGetValue(id, out Sprite replacement))
                    rule.m_Sprites[i] = replacement;
            }
        }

        /* -- also map default sprite -------------------------------------- */
        if (tileClone.m_DefaultSprite)
        {
            string id = GetNumericSuffixSafe(tileClone.m_DefaultSprite);
            if (id != null && newSprites.TryGetValue(id, out Sprite replacement))
                tileClone.m_DefaultSprite = replacement;
        }

        /* -- save & highlight --------------------------------------------- */
        EditorUtility.SetDirty(tileClone);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = tileClone;

        EditorUtility.DisplayDialog("Clone created",
            $"New ExtendedRuleTile:\n• {Path.GetFileName(clonePath)}", "Great!");
    }

    /* ──────────────────── HELPERS ──────────────────── */

    private static Sprite FindFirstRuleSprite(ExtendedRuleTile t)
    {
        foreach (var r in t.m_TilingRules)
            if (r.m_Sprites?.Length > 0 && r.m_Sprites[0])
                return r.m_Sprites[0];
        return null;
    }

    // Extract trailing digits (e.g. "_32") or null if none
    private static string GetNumericSuffixSafe(Object obj)
    {
        if (!obj) return null;
        var m = Regex.Match(obj.name, @"_(\d+)$");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static void Abort(string msg, string pathToDelete)
    {
        EditorUtility.DisplayDialog("Clone ExtendedRuleTile", msg, "OK");
        if (!string.IsNullOrEmpty(pathToDelete))
            AssetDatabase.DeleteAsset(pathToDelete);
    }
}
#endif
