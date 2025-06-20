/*********************************************************
 *  AIController.cs – tiny bridge between a Unit and a ScriptableAI
 *********************************************************/

using UnityEngine;

[RequireComponent(typeof(Unit))]
public sealed class AIController : MonoBehaviour
{
    [Tooltip("Brain (ScriptableAI asset) that drives this Unit")]
    public ScriptableAI brain;

    [Tooltip("Tick in FixedUpdate (true) or Update (false)")]
    public bool fixedTick = true;

    Unit unit;

    void Awake() => unit = GetComponent<Unit>();

    void Update ()
    {
        if (!fixedTick) Step(Time.deltaTime);
    }

    void FixedUpdate ()
    {
        if (fixedTick) Step(Time.fixedDeltaTime);
    }

    /* -------------------------------------------------- */

    void Step(float dt)
    {
        if (unit && !unit.Dead && brain != null)
            brain.Tick(unit, dt);
    }
}
