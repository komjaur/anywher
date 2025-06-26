using System;
using UnityEngine;

public static class QuestEventBus
{
    public static Action<string,int> OnItemCollected;            // (itemID, amount)
    public static Action<string>     OnEnemyKilled;              // enemyID
    public static Action<int>        OnTileMined;                // tileID
    public static Action<ChunkFlags> OnChunkEntered;
    public static Action<string>     OnNPCDialogueFinished;      // npcID
    public static Action<string,object> OnCustom;                // (eventKey, payload)
}
