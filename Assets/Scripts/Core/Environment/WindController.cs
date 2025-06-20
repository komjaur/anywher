using UnityEngine;
using System;

public class WindController : MonoBehaviour
{
    [Tooltip("0 = calm, 1 = full storm")]
    [Range(0,1)] public float targetStrength = 0.25f;

    [Tooltip("How fast the wind direction slowly turns (deg/sec).")]
    public float rotSpeed = 8f;

    float  strength;
    Vector2 dir = Vector2.right;

    void Update ()
    {
        // Smoothly follow the target strength (could be tied to weather)
        strength = Mathf.MoveTowards(strength, targetStrength, Time.deltaTime * 0.25f);

        // Lazy rotation so the wind isn’t perfectly constant
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        ang += rotSpeed * (Mathf.PerlinNoise(Time.time * 0.05f, 0) - 0.5f) * Time.deltaTime;
        dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));

        Shader.SetGlobalFloat("_WindStrength", strength);
        Shader.SetGlobalVector("_WindDir",     new Vector4(dir.x, dir.y, 0, 0));
    }
}
