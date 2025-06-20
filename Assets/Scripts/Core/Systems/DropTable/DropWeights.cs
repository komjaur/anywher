using UnityEngine;

[System.Serializable]
public class WeightedUnit
{
    public UnitTemplate template;   // points at the ScriptableObject you made
    [Min(0)] public float weight = 1;
}
