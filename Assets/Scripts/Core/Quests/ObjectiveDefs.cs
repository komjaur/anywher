using UnityEngine;

public abstract class ObjectiveDef : ScriptableObject
{
    [Tooltip("Shown in quest log"), TextArea] public string description;
    public abstract ObjectiveTracker CreateTracker();               // runtime binder
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Gather Item")]
public class GatherItemObjectiveDef : ObjectiveDef
{
    public string itemID;
    public int    amount = 1;
    public override ObjectiveTracker CreateTracker() => new GatherItemTracker(this);
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Kill Enemy")]
public class KillEnemyObjectiveDef : ObjectiveDef
{
    public string enemyID;
    public int    amount = 1;
    public override ObjectiveTracker CreateTracker() => new KillEnemyTracker(this);
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Reach Chunk Flag")]
public class ReachChunkFlagObjectiveDef : ObjectiveDef
{
    public ChunkFlags targetFlag;
    public override ObjectiveTracker CreateTracker() => new ReachChunkFlagTracker(this);
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Mine Tile")]
public class MineTileObjectiveDef : ObjectiveDef
{
    public TileData tile;
    public int      amount = 1;
    public override ObjectiveTracker CreateTracker() => new MineTileTracker(this);
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Talk To NPC")]
public class TalkToNpcObjectiveDef : ObjectiveDef
{
    public string npcID;
    public override ObjectiveTracker CreateTracker() => new TalkToNpcTracker(this);
}

[CreateAssetMenu(menuName = "Game/Quests/Objective/Craft Item")]
public class CraftItemObjectiveDef : ObjectiveDef
{
    public string itemID;
    public int    amount = 1;
    public override ObjectiveTracker CreateTracker() => new CraftItemTracker(this);
}
