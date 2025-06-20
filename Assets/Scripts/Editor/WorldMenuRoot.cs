// ------------------------------------------------------------
// Shared constants so every script uses the very same menu path
// ------------------------------------------------------------
#if UNITY_EDITOR
internal static class WorldMenuRoot
{
    /// <summary>Root path that appears under the Assets menu.</summary>
    public const string PATH = "Assets/World/";

    /// <summary>
    /// Base priority so the commands are grouped together yet stay
    /// below Unity’s built-in items (lower number = nearer the top).
    /// </summary>
    public const int PRIORITY = 500;
}
#endif
