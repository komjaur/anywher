using UnityEngine;

/// <summary>Base asset for anything that paints into a <see cref="World"/>.</summary>
public abstract class Blueprint : ScriptableObject
{
    [TextArea] public string description = "Optional notes";

    /* ───────────── public entry point ───────────── */

    public abstract void PlaceStructure(World world, int x, int y);


}
