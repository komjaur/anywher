using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public sealed class SaveLoadSystem : MonoBehaviour
{
    public static SaveLoadSystem Instance { get; private set; }

    const string SaveFileName = "savegame.json";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [System.Serializable]
    public class SaveData
    {
        public List<InvSlotData> inventory = new();
        public List<QuestState> activeQuests = new();
        public List<string> completedQuests = new();
        public WorldState world = new();
    }

    [System.Serializable]
    public struct InvSlotData
    {
        public int itemID;
        public int amount;
    }

    [System.Serializable]
    public struct QuestState
    {
        public string questID;
        public int stepIndex;
    }

    [System.Serializable]
    public class WorldState
    {
        public List<ChunkData> chunks = new();
    }

    [System.Serializable]
    public class ChunkData
    {
        public Vector2Int coord;
        public int[] front;
        public int[] back;
    }

    string GetSavePath() => Path.Combine(Application.persistentDataPath, SaveFileName);

    /* ------------------------------------------------------------------- */
    public void SaveGame(GameManager gm)
    {
        if (gm == null) return;
        var data = new SaveData();
        SaveInventory(gm.PlayerManager.PlayerInventory, data);
        SaveQuests(gm.QuestManager, data);
        SaveWorld(gm.WorldManager.GetCurrentWorld(), data);

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSavePath(), json);
        Debug.Log($"Game saved to {GetSavePath()}");
    }

    public void LoadGame(GameManager gm)
    {
        string path = GetSavePath();
        if (!File.Exists(path))
        {
            Debug.LogWarning("SaveLoadSystem: no save file found.");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        LoadInventory(gm.PlayerManager.PlayerInventory, data);
        LoadQuests(gm.QuestManager, data);
        LoadWorld(gm.WorldManager.GetCurrentWorld(), data);
        Debug.Log("SaveLoadSystem: game loaded");
    }

    /* ---------------- inventory ---------------- */
    void SaveInventory(Inventory inv, SaveData data)
    {
        if (inv == null) return;
        foreach (var slot in inv.Slots)
        {
            InvSlotData sd = new InvSlotData { itemID = slot.item ? slot.item.itemID : 0, amount = slot.amount };
            data.inventory.Add(sd);
        }
    }

    void LoadInventory(Inventory inv, SaveData data)
    {
        if (inv == null || data.inventory.Count == 0) return;
        var items = Resources.LoadAll<ItemData>("Items");
        var map = new Dictionary<int, ItemData>();
        foreach (var it in items)
            if (it) map[it.itemID] = it;

        for (int i = 0; i < inv.Slots.Length && i < data.inventory.Count; ++i)
        {
            ref var slot = ref inv.Slots[i];
            var sd = data.inventory[i];
            slot.amount = sd.amount;
            slot.item = map.TryGetValue(sd.itemID, out var it) ? it : null;
            inv.NotifySlotChanged(i);
        }
    }

    /* ---------------- quests ---------------- */
    void SaveQuests(QuestManager qm, SaveData data)
    {
        if (qm == null) return;
        foreach (var q in qm.ActiveQuests)
            data.activeQuests.Add(new QuestState { questID = q.Def.questID, stepIndex = q.StepIndex });
        var comp = qm.GetType().GetField("completed", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(qm) as HashSet<string>;
        if (comp != null)
            data.completedQuests.AddRange(comp);
    }

    void LoadQuests(QuestManager qm, SaveData data)
    {
        if (qm == null) return;
        var completed = qm.GetType().GetField("completed", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(qm) as HashSet<string>;
        if (completed != null)
        {
            completed.Clear();
            foreach (var id in data.completedQuests) completed.Add(id);
        }

        var db = qm.GetType().GetField("questDatabase", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(qm) as QuestDatabase;
        foreach (var state in data.activeQuests)
        {
            var def = db ? db.GetByID(state.questID) : null;
            if (!def) continue;
            qm.StartQuest(def);
            var quest = qm.ActiveQuests.FirstOrDefault(q => q.Def == def);
            if (quest != null && state.stepIndex > 0)
            {
                var deactivate = typeof(Quest).GetMethod("DeactivateStep", BindingFlags.NonPublic | BindingFlags.Instance);
                var activate = typeof(Quest).GetMethod("ActivateStep", BindingFlags.NonPublic | BindingFlags.Instance);
                var field = typeof(Quest).GetField("<StepIndex>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                deactivate?.Invoke(quest, new object[]{0});
                field?.SetValue(quest, state.stepIndex);
                if (state.stepIndex < quest.Def.steps.Length)
                    activate?.Invoke(quest, new object[]{state.stepIndex});
                else
                    typeof(Quest).GetField("<Completed>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(quest, true);
            }
        }
    }

    /* ---------------- world ---------------- */
    void SaveWorld(World world, SaveData data)
    {
        if (world == null) return;
        foreach (var kv in world.GetAllChunks())
        {
            var c = kv.Value;
            var cd = new ChunkData { coord = kv.Key };
            int size = c.size;
            cd.front = new int[size * size];
            cd.back = new int[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    int idx = y * size + x;
                    cd.front[idx] = c.frontLayerTileIndexes[x, y];
                    cd.back[idx] = c.backgroundLayerTileIndexes[x, y];
                }
            data.world.chunks.Add(cd);
        }
    }

    void LoadWorld(World world, SaveData data)
    {
        if (world == null || data.world.chunks.Count == 0) return;
        foreach (var cd in data.world.chunks)
        {
            var chunk = world.GetChunk(cd.coord) ?? world.AddChunk(cd.coord);
            int size = chunk.size;
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    int idx = y * size + x;
                    chunk.SetTile(ChunkLayer.Front, x, y, cd.front[idx]);
                    chunk.SetTile(ChunkLayer.Background, x, y, cd.back[idx]);
                }
        }
    }
}
