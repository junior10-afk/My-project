using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastWorld
{
    /// <summary>
    /// JSON save/load: player position, time of day, party, inventory, quests, activated waypoints,
    /// opened chests and collected orbs. "Continue" reloads the scene with the save applied.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SaveSystem : MonoBehaviour
    {
        [Serializable]
        class MemberSave
        {
            public string name;
            public int level, xp;
            public float hp, energy;
            public bool unlocked;
        }

        [Serializable]
        class ItemSave
        {
            public string id;
            public int count;
        }

        [Serializable]
        class Data
        {
            public Vector3 position;
            public float timeOfDay;
            public int activeMember;
            public float maxStamina;
            public Vector3 respawn;
            public List<MemberSave> members = new();
            public List<ItemSave> items = new();
            public List<QuestManager.QuestState> quests = new();
            public List<string> waypoints = new();
            public List<string> chests = new();
            public List<string> orbs = new();
        }

        public static SaveSystem Instance { get; private set; }
        public static bool ContinueRequested;
        public static bool SkipTitle => ContinueRequested;

        static readonly HashSet<string> waypoints = new();
        static readonly HashSet<string> chests = new();
        static readonly HashSet<string> orbs = new();
        static Data pending;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "lastworld_save.json");
        public static bool HasSave => File.Exists(FilePath);

        float autosaveTimer;
        public bool savingEnabled;

        void Awake()
        {
            Instance = this;
            waypoints.Clear();
            chests.Clear();
            orbs.Clear();
            pending = null;
            if (ContinueRequested && HasSave)
            {
                try
                {
                    pending = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                    foreach (var w in pending.waypoints) waypoints.Add(w);
                    foreach (var c in pending.chests) chests.Add(c);
                    foreach (var o in pending.orbs) orbs.Add(o);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] Could not read save: {e.Message}");
                    pending = null;
                }
            }
        }

        void Start()
        {
            if (pending == null) return;
            var party = PartyManager.Instance;
            foreach (var ms in pending.members)
            {
                var m = Array.Find(party.members, x => x.name == ms.name);
                if (m == null) continue;
                m.level = ms.level;
                m.xp = ms.xp;
                m.unlocked = ms.unlocked;
                m.hp = Mathf.Max(1f, ms.hp);
                m.energy = ms.energy;
            }
            var items = new Dictionary<string, int>();
            foreach (var it in pending.items) items[it.id] = it.count;
            Inventory.Restore(items);
            QuestManager.Instance.Restore(pending.quests);
            var pc = PlayerController.Instance;
            if (pending.maxStamina > 0f) pc.SetMaxStamina(pending.maxStamina);
            pc.Teleport(pending.position);
            party.RespawnPoint = pending.respawn;
            party.TrySwitch(pending.activeMember);
            party.NotifyChanged();
            var cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle) cycle.timeOfDay = pending.timeOfDay;
            QuestManager.ReportWaypoints(waypoints.Count);
            savingEnabled = true;
            ContinueRequested = false;
            pending = null;
        }

        void Update()
        {
            if (!savingEnabled) return;
            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer > 90f)
            {
                autosaveTimer = 0f;
                Save();
            }
        }

        public static void Save()
        {
            if (!Instance || !Instance.savingEnabled) return;
            var party = PartyManager.Instance;
            var pc = PlayerController.Instance;
            if (!party || !pc) return;
            var d = new Data
            {
                position = pc.transform.position,
                timeOfDay = FindAnyObjectByType<DayNightCycle>()?.timeOfDay ?? 9f,
                activeMember = party.ActiveIndex,
                maxStamina = pc.MaxStamina,
                respawn = party.RespawnPoint,
            };
            foreach (var m in party.members)
                d.members.Add(new MemberSave { name = m.name, level = m.level, xp = m.xp, hp = m.hp, energy = m.energy, unlocked = m.unlocked });
            foreach (var kv in Inventory.All()) d.items.Add(new ItemSave { id = kv.Key, count = kv.Value });
            d.quests = QuestManager.Instance.Snapshot();
            d.waypoints.AddRange(waypoints);
            d.chests.AddRange(chests);
            d.orbs.AddRange(orbs);
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(d, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] {e.Message}");
            }
        }

        public static void ContinueGame()
        {
            ContinueRequested = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void NewGame()
        {
            if (HasSave) File.Delete(FilePath);
            if (Instance) Instance.savingEnabled = true;
            Save();
        }

        public static bool IsChestOpened(string id) => chests.Contains(id);
        public static bool IsWaypointActive(string id) => waypoints.Contains(id);
        public static bool IsOrbCollected(string id) => orbs.Contains(id);

        public static void MarkChest(string id)
        {
            chests.Add(id);
            Save();
        }

        public static void MarkWaypoint(string id)
        {
            waypoints.Add(id);
            Save();
        }

        public static void MarkOrb(string id) => orbs.Add(id);
    }
}
