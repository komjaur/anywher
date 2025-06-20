/*********************************************************
 *  ButterflyAI.cs – simple airborne wanderer
 *********************************************************/

using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AI_Butterfly", menuName = "Game/Unit/Butterfly AI")]
public sealed class ButterflyAI : ScriptableAI
{
    [Header("Flight pattern")]
    [Min(0.1f)] public float flapAmplitude = 0.4f;   // vertical sine amplitude
    [Min(0.1f)] public float flapSpeed     = 3f;     // sine frequency
    [Min(0.5f)] public float dirChangeMin  = 2f;     // sec before changing hori dir
    [Min(0.5f)] public float dirChangeMax  = 5f;

    [Header("Debug")]
    public bool drawHeading = false;

    /* per-unit state */
    readonly Dictionary<Unit,float> phase      = new();   // sine phase accumulator
    readonly Dictionary<Unit,int>   hDir       = new();   // current horizontal dir (-1 / +1)
    readonly Dictionary<Unit,float> nextSwitch = new();   // time until next dir flip

    public override void Tick(Unit self, float dt)
    {
        if (!phase.ContainsKey(self))
        {
            phase[self]      = Random.value * Mathf.PI * 2f;
            hDir[self]       = Random.value < .5f ? -1 : 1;
            nextSwitch[self] = Random.Range(dirChangeMin, dirChangeMax);
        }

        /* update timers */
        phase[self]      += flapSpeed * dt;
        nextSwitch[self] -= dt;

        /* horizontal direction flip */
        if (nextSwitch[self] <= 0f)
        {
            hDir[self]       = -hDir[self];
            nextSwitch[self] = Random.Range(dirChangeMin, dirChangeMax);
        }

        /* compose velocity */
        float vx = hDir[self] * self.moveSpeed;                 // constant hori speed
        float vy = Mathf.Sin(phase[self]) * flapAmplitude;      // flutter

        self.Move(new Vector2(vx, vy));

        /* visual debug */
        if (drawHeading)
        {
            Vector3 p  = self.transform.position;
            Vector3 to = p + new Vector3(vx, vy, 0).normalized * 0.7f;
            Debug.DrawLine(p, to, Color.green, dt, false);
        }
    }
}
