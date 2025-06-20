/*********************************************************
 *  SimpleAI.cs – left/right walker with optional
 *                on-screen probe visualisation
 *********************************************************/

using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "AI_Walker", menuName = "Game/Unit/Simple Walker")]
public sealed class SimpleAI : ScriptableAI
{
    [Header("Probes")]
    [Tooltip("World-space distance used for ground & wall probes")]
    public float probeDistance = 0.6f;

    [Tooltip("Vertical offset below pivot to start the ground ray")]
    public float groundRayYOffset = 0.1f;

    [Header("Debug")]
    public bool drawProbes = true;

    /* ─── Layer to test against ─── */
    [SerializeField]            // visible in Inspector
    LayerMask groundMask = 0;   // leave 0 → automatic default in OnEnable

    void OnEnable()
    {
        if (groundMask == 0)     // pick a reasonable default once
            groundMask = LayerMask.GetMask("Ground", "Platforms");
    }

    /* ─── internal state ─── */
    static readonly Dictionary<Unit,int> facingDir = new();

    /* -------------------------------------------------------------- */
    public override void Tick(Unit self, float dt)
    {
        /* initialise per-unit direction once */
        if (!facingDir.TryGetValue(self, out int dir))
            dir = facingDir[self] = (Random.value < .5f) ? 1 : -1;

        Vector2 pos   = self.transform.position;
        Vector2 right = Vector2.right * dir;

        /* ---------- probes ---------- */

        // 1) ground check – one tile ahead, one tile down
        Vector2 gStart = pos + right * probeDistance + Vector2.down * groundRayYOffset;
        bool groundAhead = Physics2D.Raycast(gStart, Vector2.down, probeDistance, groundMask);

        // 2) wall check – straight in front
        bool wallAhead = Physics2D.Raycast(pos, right, probeDistance * .9f, groundMask);

        /* optional debug lines (last for one frame) */
        if (drawProbes)
        {
            Debug.DrawLine(gStart, gStart + Vector2.down * probeDistance,
                           groundAhead ? Color.cyan : Color.red, dt, false);

            Debug.DrawLine(pos, pos + right * probeDistance * .9f,
                           wallAhead ? Color.magenta : Color.yellow, dt, false);
        }

        /* ---------- behaviour ---------- */
        if (!groundAhead || wallAhead)
        {
            facingDir[self] = -dir;   // flip direction
            return;                   // pause 1 frame before moving
        }

        self.Move(right);             // Unit.Move normalises internally
    }
}
