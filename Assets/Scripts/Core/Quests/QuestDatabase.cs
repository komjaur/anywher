using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    public List<QuestDef> quests = new();
    public QuestDef GetByID(string id) => quests.Find(q => q.questID == id);
}
