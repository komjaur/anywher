using UnityEngine;
using System;

[Serializable]
public class QuestStep
{
    [TextArea] public string narrative;
    public ObjectiveLogic   logic   = ObjectiveLogic.All;
    public ObjectiveDef[]   objectives;
}

[Serializable]
public struct SkillRequirement
{
    public SkillId skill;
    public int     level;
}

// Restrict quest availability to a time-of-day window
[Serializable]
public struct TimeOfDayRequirement
{
    [Range(0f,24f)] public float startHour;  // inclusive
    [Range(0f,24f)] public float endHour;    // exclusive

    public bool IsMet(float currentHour)
    {
        return endHour >= startHour
            ? currentHour >= startHour && currentHour < endHour
            : currentHour >= startHour || currentHour < endHour;
    }
}

[CreateAssetMenu(menuName = "Game/Quest")]
public class QuestDef : ScriptableObject
{
    [Header("Meta")]
    public string questID;
    public string displayName;
    [TextArea] public string description;
    public bool   repeatable;

    [Header("Requirements")]
    public SkillRequirement[] skillRequirements;
    public string[]           prerequisiteQuestIDs;
    public bool               restrictTimeOfDay;
    public TimeOfDayRequirement timeOfDay;

    [Header("Flow")]
    public QuestStep[] steps;
    public Reward[]    rewards;
    public string[]    followUpQuestIDs;
    public float       timeLimitSeconds;      // 0 = none
}
