using UnityEngine;

[CreateAssetMenu(fileName = "Area_OW_", menuName = "Game/World/Area/Overworld Area")]
public class AreaOverworldData : AreaData
{
    /* ── Skyline modulation relative to the global cosine ───────── */
    [Header("Skyline Modifiers")]
    [Tooltip("Vertical shift applied to the cosine skyline (-0.05 … +0.05)")]
    [Range(-0.05f, 0.05f)] public float skyCosOffset = 0f;

    [Tooltip("Amplitude multiplier for the skyline wave (0.5 … 2.0)")]
    public float skyCosAmpMul = 1f;

    /* ── Single control that replaces four separate noise overrides ─ */
    [Header("Skyline Ruggedness")]
    [Tooltip("0 = smooth rolling hills, 1 = dramatic jagged peaks")]
   public float skylineRuggedness = -1f;   // <0 → auto

    /* ── Optional temperature tag ─────────────────────────────────── */
    [Header("Thermal Signature")]
    [Range(-1f, 1f)] public float heat = 0f;
}
