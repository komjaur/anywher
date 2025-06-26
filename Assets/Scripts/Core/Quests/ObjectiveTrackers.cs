using UnityEngine;
using System;

public abstract class ObjectiveTracker
{
    public event Action<ObjectiveTracker> OnProgress;
    public ObjectiveState State { get; protected set; } = ObjectiveState.Inactive;
    public int  Current { get; protected set; }
    public int  Target  { get; protected set; }

    public virtual void Activate()   => State = ObjectiveState.Active;
    public virtual void Deactivate() => State = ObjectiveState.Inactive;

    protected void Add(int delta = 1)
    {
        if (State != ObjectiveState.Active) return;
        Current = Mathf.Clamp(Current + delta, 0, Target);
        if (Current >= Target) State = ObjectiveState.Complete;
        OnProgress?.Invoke(this);
    }
}

/* concrete trackers */
public sealed class GatherItemTracker : ObjectiveTracker
{
    readonly GatherItemObjectiveDef def;
    public GatherItemTracker(GatherItemObjectiveDef d){ def=d; Target=d.amount; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnItemCollected += Check; }
    public override void Deactivate(){ QuestEventBus.OnItemCollected -= Check; base.Deactivate(); }
    void Check(string id,int amt){ if(id==def.itemID) Add(amt); }
}

public sealed class KillEnemyTracker : ObjectiveTracker
{
    readonly KillEnemyObjectiveDef def;
    public KillEnemyTracker(KillEnemyObjectiveDef d){ def=d; Target=d.amount; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnEnemyKilled += Check; }
    public override void Deactivate(){ QuestEventBus.OnEnemyKilled -= Check; base.Deactivate(); }
    void Check(string id){ if(id==def.enemyID) Add(); }
}

public sealed class ReachChunkFlagTracker : ObjectiveTracker
{
    readonly ReachChunkFlagObjectiveDef def;
    public ReachChunkFlagTracker(ReachChunkFlagObjectiveDef d){ def=d; Target=1; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnChunkEntered += Check; }
    public override void Deactivate(){ QuestEventBus.OnChunkEntered -= Check; base.Deactivate(); }
    void Check(ChunkFlags f){ if(f.HasFlag(def.targetFlag)) Add(); }
}

public sealed class MineTileTracker : ObjectiveTracker
{
    readonly MineTileObjectiveDef def;
    public MineTileTracker(MineTileObjectiveDef d){ def=d; Target=d.amount; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnTileMined += Check; }
    public override void Deactivate(){ QuestEventBus.OnTileMined -= Check; base.Deactivate(); }
    void Check(int id){ if(def.tile==null || id==def.tile.tileID) Add(); }
}

public sealed class TalkToNpcTracker : ObjectiveTracker
{
    readonly TalkToNpcObjectiveDef def;
    public TalkToNpcTracker(TalkToNpcObjectiveDef d){ def=d; Target=1; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnNPCDialogueFinished += Check; }
    public override void Deactivate(){ QuestEventBus.OnNPCDialogueFinished -= Check; base.Deactivate(); }
    void Check(string id){ if(id==def.npcID) Add(); }
}

public sealed class CraftItemTracker : ObjectiveTracker
{
    readonly CraftItemObjectiveDef def;
    public CraftItemTracker(CraftItemObjectiveDef d){ def=d; Target=d.amount; }
    public override void Activate(){ base.Activate(); QuestEventBus.OnItemCrafted += Check; }
    public override void Deactivate(){ QuestEventBus.OnItemCrafted -= Check; base.Deactivate(); }
    void Check(string id,int amt){ if(id==def.itemID) Add(amt); }
}
