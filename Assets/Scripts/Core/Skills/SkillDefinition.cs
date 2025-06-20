using UnityEngine;





[CreateAssetMenu(menuName = "Game/RPG/Skill Definition")]
public sealed class SkillDefinition : ScriptableObject
{
    /* ─────────── Identity & art ─────────── */
    public SkillId id;
    public Sprite  icon;
    public string  displayName;      // human-readable name shown in UI

    [Header("Palette")]
     
    public Color color = Color.white;   // ← set in the inspector
    /* ─────────── Curve constants ─────────── */

    public const int   MaxLevel    = 99;          // hard cap
    public const int   MaxXp       = 20_000_000;  // total XP at Lv-99
    public const float GrowthRate  = 1.12f;       // per-level increase (12 %)
                                                   // tweak 1.05‥1.20 to taste

    /* ─────────── Pre-baked tables ─────────── */

    /// totalXp[L-1]  ⇒ cumulative XP required for level L (1-99)
    static readonly int[] totalXp;

    static SkillDefinition()
    {
        int n = MaxLevel - 1;                     // 98 gaps
        float rPowN = Mathf.Pow(GrowthRate, n);
        float A     = MaxXp * (GrowthRate - 1f) / (rPowN - 1f);  // base term

        totalXp = new int[MaxLevel];
        float cumulative = 0f;

        for (int k = 0; k < n; ++k)
        {
            float inc = A * Mathf.Pow(GrowthRate, k); // XP gain for this gap
            cumulative += inc;
            totalXp[k + 1] = Mathf.RoundToInt(cumulative);
        }
        totalXp[0]        = 0;                       // Lv-1
        totalXp[^1]       = MaxXp;                   // ensure exact cap
    }

    /* ─────────── Public helpers ─────────── */

    /// Total XP required to reach `level` (1-99).
    public int GetXpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        return totalXp[level - 1];
    }

    /// Level reached for a given total-XP value.
    public int GetLevelForXp(int xp)
    {
        xp = Mathf.Clamp(xp, 0, MaxXp);

        // binary search over the 99-element table → at most 7 comparisons
        int lo = 0, hi = MaxLevel - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (xp < totalXp[mid]) hi = mid - 1;
            else                   lo = mid + 1;
        }
        return Mathf.Clamp(lo, 1, MaxLevel);
    }
}
