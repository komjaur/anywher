using UnityEngine;
using System;
using System.Collections.Generic;

public sealed class Quest
{
    public QuestDef Def { get; }
    public int      StepIndex  { get; private set; }
    public bool     Completed  { get; private set; }
    public bool     Failed     { get; private set; }

    readonly List<ObjectiveTracker[]> trackers = new();
    float timeLeft;

    public Quest(QuestDef def)
    {
        Def = def;
        foreach(var s in def.steps)
        {
            var arr = new ObjectiveTracker[s.objectives.Length];
            for(int i=0;i<arr.Length;i++) arr[i]=s.objectives[i].CreateTracker();
            trackers.Add(arr);
        }
        timeLeft = def.timeLimitSeconds;
        ActivateStep(0);
    }

    public void Tick(float dt)
    {
        if (Completed || Failed) return;
        if (timeLeft > 0)
        {
            timeLeft -= dt;
            if (timeLeft <= 0) Fail();
        }
    }

    void ActivateStep(int idx)
    {
        foreach(var t in trackers[idx])
        {
            t.OnProgress += _=>Evaluate();
            t.Activate();
        }
    }
    void DeactivateStep(int idx)
    {
        foreach(var t in trackers[idx])
        {
            t.OnProgress -= _=>Evaluate();
            t.Deactivate();
        }
    }

    void Evaluate()
    {
        var logic = Def.steps[StepIndex].logic;
        bool done = logic == ObjectiveLogic.All
            ? Array.TrueForAll(trackers[StepIndex], tr=>tr.State==ObjectiveState.Complete)
            : Array.Exists(trackers[StepIndex],     tr=>tr.State==ObjectiveState.Complete);

        if (!done) return;

        DeactivateStep(StepIndex);
        StepIndex++;
        if (StepIndex >= Def.steps.Length) Completed = true;
        else ActivateStep(StepIndex);
    }
    void Fail(){ Failed = true; DeactivateStep(StepIndex); }
}
