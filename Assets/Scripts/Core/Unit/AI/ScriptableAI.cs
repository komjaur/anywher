/*********************************************************
 *  ScriptableAI.cs – abstract base class for AI brains
 *********************************************************/

using UnityEngine;

public abstract class ScriptableAI : ScriptableObject
{
    public abstract void Tick(Unit self, float dt);

    /* shared helper – find nearest enemy within range */
    protected static Unit ClosestEnemy(Unit self, float range)
    {
        Unit best = null; float bestDist = range;
        foreach (Unit u in Unit.All)
        {
            if (u == self || u.Dead || u.team == self.team) continue;
            float d = Vector2.Distance(u.transform.position, self.transform.position);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }
}
