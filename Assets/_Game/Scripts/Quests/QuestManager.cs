using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    public enum StepType { Talk, Interact, Kill, Camp, Waypoints, OpenChest, Item, Boss, Reach }

    public class QuestStep
    {
        public string text;
        public StepType type;
        public string target;
        public int required = 1;
        public string markerId;
        public Action onComplete;
        /// <summary>Talk steps: the dialogue played when speaking to <see cref="target"/> at this step.</summary>
        public Func<List<DialogueLine>> lines;
        /// <summary>Reach steps: distance (m) to the marker that completes the step.</summary>
        public float radius = 30f;
    }

    public class Quest
    {
        public string id, title, summary;
        public bool main;
        /// <summary>Id of the quest that must be finished first (chapters chain one after the other).</summary>
        public string requires;
        public List<QuestStep> steps = new();
        public int current;
        public int progress;
        public bool done;
        public Action onFinish;
        public QuestStep Step => done || current >= steps.Count ? null : steps[current];
    }

    /// <summary>Static registry of world positions used by quest markers and the map.</summary>
    public static class WorldMarkers
    {
        static readonly Dictionary<string, Transform> markers = new();
        public static void Register(string id, Transform t) { if (!string.IsNullOrEmpty(id)) markers[id] = t; }
        public static bool TryGet(string id, out Vector3 pos)
        {
            pos = default;
            if (string.IsNullOrEmpty(id) || !markers.TryGetValue(id, out var t) || !t) return false;
            pos = t.position;
            return true;
        }
    }

    /// <summary>
    /// Story and side quests (Chapter I « Les Vents Oubliés »), progress tracking from world events,
    /// party recruitment and rewards.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public readonly List<Quest> quests = new();
        public Quest Tracked { get; private set; }
        public static event Action Changed;

        int activatedWaypoints;

        void Awake()
        {
            Instance = this;
            BuildQuests();
            ChapterQuests.Add(this);
            Tracked = quests[0];
        }

        public bool IsAvailable(Quest q) => q != null && (string.IsNullOrEmpty(q.requires) || quests.Find(x => x.id == q.requires) is { done: true });

        /// <summary>The quest the HUD should follow: the current chapter first, then side quests.</summary>
        Quest NextTracked() => quests.Find(x => x.main && !x.done && IsAvailable(x)) ?? quests.Find(x => !x.done && IsAvailable(x)) ?? quests[0];

        public Quest CurrentChapter => quests.Find(x => x.main && !x.done && IsAvailable(x));

        /// <summary>Number of finished main chapters (0 = still in Chapter I).</summary>
        public int ChaptersDone => quests.FindAll(x => x.main && x.done).Count;

        float reachTimer;

        void Update()
        {
            reachTimer -= Time.deltaTime;
            if (reachTimer > 0f) return;
            reachTimer = 0.25f;
            var p = PlayerController.Instance;
            if (!p || CinematicDirector.Playing) return;
            foreach (var q in quests.ToArray())
            {
                var s = q.Step;
                if (s == null || s.type != StepType.Reach || !IsAvailable(q)) continue;
                if (WorldMarkers.TryGet(s.markerId, out var pos) && Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(p.transform.position.x, p.transform.position.z)) < s.radius)
                {
                    Report(StepType.Reach, s.target);
                    break;
                }
            }
        }

        /// <summary>Dialogue attached to the current Talk step of an available quest that targets this NPC.</summary>
        public List<DialogueLine> StepLines(string npc)
        {
            foreach (var q in quests)
            {
                var s = q.Step;
                if (s != null && s.type == StepType.Talk && s.target == npc && s.lines != null && IsAvailable(q)) return s.lines();
            }
            return null;
        }

        void BuildQuests()
        {
            var party = (Func<PartyManager>)(() => PartyManager.Instance);

            quests.Add(new Quest
            {
                id = "main1",
                title = "Chapitre I — Les Vents Oubliés",
                summary = "Tu es tombé du ciel au pied de l'Autel des Vents, sans souvenirs. Aerendal est le dernier monde : depuis que le Sceau Céleste s'est brisé, ses vents se sont tus et la Pâleur ronge l'horizon.",
                main = true,
                steps =
                {
                    new QuestStep { text = "Parler à Elara, la Gardienne de l'Autel", type = StepType.Talk, target = "elara", markerId = "elara" },
                    new QuestStep { text = "Toucher l'Autel des Vents", type = StepType.Interact, target = "altar", markerId = "altar",
                        onComplete = () =>
                        {
                            EnemySpawner.Find("camp_tutorial")?.EnsureSpawned();
                            StoryEvents.AltarTouched();
                        } },
                    new QuestStep { text = "Repousser les slimes près du camp", type = StepType.Camp, target = "camp_tutorial", markerId = "camp_tutorial",
                        onComplete = () => Recruit("Kaïra") },
                    new QuestStep { text = "Réveiller des Sentinelles", type = StepType.Waypoints, required = 3, markerId = "nearest_waypoint",
                        onComplete = () => Recruit("Nerys") },
                    new QuestStep { text = "Retrouver l'éclat tombé dans les ruines anciennes", type = StepType.OpenChest, target = "ruin_chest", markerId = "ruin_chest",
                        onComplete = () => Recruit("Torvan") },
                    new QuestStep { text = "Rapporter le fragment à Elara", type = StepType.Talk, target = "elara", markerId = "elara",
                        onComplete = () => EnemySpawner.Find("boss_arena")?.EnsureSpawned() },
                    new QuestStep { text = "Gravir la Tour des Vents et ouvrir le coffre ancien", type = StepType.OpenChest, target = "tower_chest", markerId = "tower_chest",
                        onComplete = () => EnemySpawner.Find("boss_arena")?.EnsureSpawned() },
                    new QuestStep { text = "Délivrer le Gardien Ancien", type = StepType.Boss, target = "boss", markerId = "boss",
                        onComplete = StoryEvents.BossDefeated },
                    new QuestStep { text = "Retourner auprès d'Elara", type = StepType.Talk, target = "elara", markerId = "elara" },
                },
                onFinish = () =>
                {
                    Inventory.Add("star_crystal", 1600);
                    Inventory.Add("xp_book", 10);
                    StoryEvents.ChapterEnd();
                },
            });

            void Recruit(string member)
            {
                party()?.Unlock(member);
                StoryEvents.Recruited(member);
            }

            quests.Add(new Quest
            {
                id = "orbs",
                title = "Les Âmes du Vent",
                summary = "Des Orbes des Vents flottent sur les hauteurs d'Aerendal. Elara dit que l'Autel en récompense la collecte.",
                steps = { new QuestStep { text = "Collecter des Orbes des Vents", type = StepType.Item, target = "wind_orb", required = 10 } },
                onFinish = () =>
                {
                    var pc = PlayerController.Instance;
                    if (pc) pc.SetMaxStamina(pc.MaxStamina + 40f);
                    Inventory.Add("star_crystal", 400);
                    GameUI.Banner("Endurance maximale augmentée !", Elements.Color(Element.Anemo));
                },
            });

            quests.Add(new Quest
            {
                id = "hunter",
                title = "Chasseur de monstres",
                summary = "Bram, le marchand du camp, paie bien pour chaque monstre éliminé dans la région.",
                steps = { new QuestStep { text = "Vaincre des monstres", type = StepType.Kill, target = "*", required = 20 } },
                onFinish = () =>
                {
                    Inventory.Add("mora", 5000);
                    Inventory.Add("xp_book", 5);
                },
            });
        }

        public static string StepProgress(Quest q)
        {
            var s = q.Step;
            if (s == null) return "Terminée";
            return s.required > 1 ? $"{s.text} ({Mathf.Min(q.progress, s.required)}/{s.required})" : s.text;
        }

        public static bool TryGetMarker(Quest q, out Vector3 pos)
        {
            pos = default;
            var s = q?.Step;
            if (s == null) return false;
            if (s.markerId == "nearest_waypoint") return Waypoint.NearestInactive(out pos);
            return WorldMarkers.TryGet(s.markerId, out pos);
        }

        public void Track(Quest q)
        {
            Tracked = q;
            Changed?.Invoke();
        }

        void Report(StepType type, string target, int amount = 1)
        {
            // Freeze who is eligible first: finishing a chapter unlocks the next one, which must not eat the same event.
            var eligible = quests.FindAll(IsAvailable);
            foreach (var q in eligible)
            {
                var s = q.Step;
                if (s == null || s.type != type) continue;
                if (s.target != "*" && s.target != target && type != StepType.Waypoints && type != StepType.Item) continue;
                if (type == StepType.Item && s.target != target) continue;
                q.progress = type == StepType.Item ? Inventory.Count(target) : q.progress + amount;
                if (type == StepType.Waypoints) q.progress = activatedWaypoints;
                if (q.progress >= s.required) Advance(q);
                else Changed?.Invoke();
            }
        }

        void Advance(Quest q)
        {
            var s = q.Step;
            q.current++;
            q.progress = 0;
            s.onComplete?.Invoke();
            AudioManager.Play("quest");
            if (q.current >= q.steps.Count)
            {
                q.done = true;
                q.onFinish?.Invoke();
                GameUI.Toast($"Quête terminée : {q.title}");
                if (Tracked == q || Tracked == null || Tracked.done) Tracked = NextTracked();
            }
            else
            {
                GameUI.Toast($"Nouvel objectif : {q.Step.text}");
                // Some steps may already be satisfied (e.g. waypoints activated earlier)
                if (q.Step.type == StepType.Waypoints && activatedWaypoints >= q.Step.required) Advance(q);
                if (q.Step != null && q.Step.type == StepType.Item && Inventory.Count(q.Step.target) >= q.Step.required) Advance(q);
            }
            SaveSystem.Save();
            Changed?.Invoke();
        }

        public bool IsAtStep(string questId, int step) => quests.Find(x => x.id == questId) is { } q && !q.done && q.current == step && IsAvailable(q);

        /// <summary>Test helper: marks every main quest before <paramref name="questId"/> as done and puts it at <paramref name="step"/>, without side effects.</summary>
        public void DebugJump(string questId, int step)
        {
            bool before = true;
            foreach (var q in quests)
            {
                if (!q.main) continue;
                if (q.id == questId)
                {
                    q.done = false;
                    q.current = step;
                    q.progress = 0;
                    before = false;
                }
                else if (before) { q.done = true; q.current = q.steps.Count; }
                else { q.done = false; q.current = 0; q.progress = 0; }
            }
            if (questId != "main1")
                foreach (var m in new[] { "Kaïra", "Nerys", "Torvan" }) PartyManager.Instance?.Unlock(m);
            Tracked = NextTracked();
            Changed?.Invoke();
        }

        public static void ReportTalk(string npc) => Instance?.Report(StepType.Talk, npc);
        public static void ReportInteract(string id) => Instance?.Report(StepType.Interact, id);
        public static void ReportKill(string tag)
        {
            Instance?.Report(StepType.Kill, tag);
            if (tag.StartsWith("boss")) Instance?.Report(StepType.Boss, tag);
        }
        public static void ReportReach(string id) => Instance?.Report(StepType.Reach, id);
        public static void ReportCampCleared(string camp) => Instance?.Report(StepType.Camp, camp);
        public static void ReportChest(string chest) => Instance?.Report(StepType.OpenChest, chest);
        public static void ReportItem(string item) => Instance?.Report(StepType.Item, item, 0);

        public static void ReportWaypoints(int activated)
        {
            if (!Instance) return;
            Instance.activatedWaypoints = activated;
            Instance.Report(StepType.Waypoints, "", 0);
        }

        // ---------------------------------------------------------------- Save support

        [Serializable]
        public class QuestState
        {
            public string id;
            public int current, progress;
            public bool done;
        }

        public List<QuestState> Snapshot()
        {
            var list = new List<QuestState>();
            foreach (var q in quests) list.Add(new QuestState { id = q.id, current = q.current, progress = q.progress, done = q.done });
            return list;
        }

        public void Restore(List<QuestState> states)
        {
            foreach (var st in states)
            {
                var q = quests.Find(x => x.id == st.id);
                if (q == null) continue;
                q.current = st.current;
                q.progress = st.progress;
                q.done = st.done;
            }
            Tracked = NextTracked();
            Changed?.Invoke();
            // Story encounters only appear on demand: after loading in the middle of one, bring it back
            foreach (var q in quests)
            {
                var step = q.Step;
                if (step != null && IsAvailable(q) && (step.type == StepType.Boss || step.type == StepType.Camp))
                    EnemySpawner.Find(step.target)?.EnsureSpawned();
            }
        }
    }
}
