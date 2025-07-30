using UnityEngine;
using System;
using System.Collections.Generic;

public sealed class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Drag the QuestDatabase here")]
    [SerializeField] QuestDatabase questDatabase;

    readonly List<Quest>    active     = new();
    readonly HashSet<string> completed = new();
    readonly HashSet<string> failed    = new();

    public event Action<Quest> OnStarted, OnCompleted, OnFailed;
    public IEnumerable<Quest> ActiveQuests => active;

    void Awake()
    { if(Instance){Destroy(gameObject);return;} Instance=this; }

    public void Initialize()
    {
        foreach(var def in questDatabase.quests)
            if (MayStart(def)) StartQuest(def);
    }

    bool MayStart(QuestDef d)
    {
        if(active.Exists(q=>q.Def==d)) return false;
        if(completed.Contains(d.questID) && !d.repeatable) return false;

        foreach(var req in d.skillRequirements)
            if(GameManager.Instance.SkillManager.GetLevel(req.skill) < req.level)
                return false;

        foreach(var id in d.prerequisiteQuestIDs)
            if(!completed.Contains(id)) return false;

        if(d.restrictTimeOfDay)
        {
            float hour = GameManager.Instance.EnvironmentManager.TimeOfDayHours;
            if(!d.timeOfDay.IsMet(hour))
                return false;
        }

        return true;
    }

    public void StartQuest(QuestDef def)
    {
        if(!MayStart(def)) return;
        var q=new Quest(def);
        active.Add(q);
        OnStarted?.Invoke(q);
    }

    void Update()
    {
        for(int i=active.Count-1;i>=0;i--)
        {
            var q=active[i];
            q.Tick(Time.deltaTime);

            if(q.Completed){ Finish(q,true); }
            else if(q.Failed){ Finish(q,false); }
        }
    }

    void Finish(Quest q,bool success)
    {
        active.Remove(q);
        if(success)
        {
            completed.Add(q.Def.questID);
            Grant(q.Def.rewards);
            OnCompleted?.Invoke(q);
            foreach(var id in q.Def.followUpQuestIDs)
            {
                var next=questDatabase.GetByID(id);
                if(next) StartQuest(next);
            }
        }
        else
        {
            failed.Add(q.Def.questID);
            OnFailed?.Invoke(q);
        }
    }

    void Grant(IEnumerable<Reward> rr)
    {
        foreach(var r in rr)
        {
            switch(r.type)
            {
                case RewardType.Item:
                    GameManager.Instance.PlayerManager.PlayerInventory?.AddItem(r.id, r.amount);
                    break;
                case RewardType.Currency:   GameManager.Instance.PlayerManager.AddCurrency(r.amount);             break;
                case RewardType.Experience: GameManager.Instance.PlayerManager.AddXP(r.amount);                  break;
                case RewardType.Unlock:     GameManager.Instance.PlayerManager.AddUnlock(r.id);                  break;
            }
        }
    }
}

/* ───────────────────────────── HOW TO HOOK UP ─────────────────────────────
 * 1.  Create a QuestDatabase asset (Right-click ▸ Create ▸ Game ▸ Quest Database)
 * 2.  Create QuestDef and Objective assets, add them to the database list.
 * 3.  Add QuestManager to your GameManager:
 *          public QuestManager QuestManager { get; private set; }
 *          QuestManager = gameObject.AddComponent<QuestManager>();
 *          QuestManager.Initialize();
 * 4.  Fire gameplay events, e.g.:
 *          QuestEventBus.OnItemCollected?.Invoke(itemID, amount);
 *          QuestEventBus.OnEnemyKilled?.Invoke(enemyID);
 *          QuestEventBus.OnChunkEntered?.Invoke(chunk.GetFlags());
 * 5.  (Optional) listen to QuestManager events for UI.
 * ------------------------------------------------------------------------*/
