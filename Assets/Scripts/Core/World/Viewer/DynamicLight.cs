using UnityEngine;

/// <summary>Attach to any GameObject (items, NPCs, projectiles) that should glow.</summary>
[DisallowMultipleComponent]
public sealed class DynamicLight : MonoBehaviour
{
    public Color color   = Color.white;
    public float radius  = 6f;      // world units (tiles)
    public float falloffPower = 2f; // 2 = quadratic, 1 = linear
    public float intensity = 1.00f; // 2 = quadratic, 1 = linear
    
    void OnEnable ()
    {
        FindSystem()?.Register(this);
    }
    void OnDisable()
    {
        FindSystem()?.Unregister(this);
    }

    LightingSystem FindSystem()
    {
        return FindFirstObjectByType<LightingSystem>();
    }
}
