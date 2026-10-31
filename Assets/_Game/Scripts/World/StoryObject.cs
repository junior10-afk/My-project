using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// A story prop the hero can touch (memory stone, brazier, journal, spirit…): optionally plays a cinematic,
    /// then a dialogue scene (<see cref="ChapterDialogue.Scene"/>), then reports an Interact event to the quests.
    /// Can be gated to one quest step so it only reacts when the story needs it.
    /// </summary>
    public class StoryObject : Interactable
    {
        public string prompt = "Toucher";
        public string reportId;
        public string sceneKey;
        public string cinematicId;
        public bool oneShot = true;
        public string gateQuest;
        public int gateStep = -1;
        public Renderer glow;
        public Light glowLight;
        public Color glowColor = new(0.5f, 1f, 0.85f);

        bool used;
        string SaveKey => $"so_done_{id}";
        string LegacyKey => $"so_{id}";

        public override string Prompt => prompt;
        public override bool Available => !used && (gateStep < 0 || QuestManager.Instance && QuestManager.Instance.IsAtStep(gateQuest, gateStep));

        void Start()
        {
            // Legacy saves wrote the flag before the dialogue; a new flag means the quest event finished.
            bool legacyFinished = SaveSystem.IsChestOpened(LegacyKey) &&
                (gateStep < 0 || !QuestManager.Instance || !QuestManager.Instance.IsAtStep(gateQuest, gateStep));
            if (oneShot && (SaveSystem.IsChestOpened(SaveKey) || legacyFinished)) SetUsed();
            else SetGlow(1f);
        }

        public override void Interact()
        {
            if (used) return;
            if (oneShot) SetUsed();
            AudioManager.Play("quest");
            CombatFX.Burst(transform.position + Vector3.up, glowColor, 1.5f, 35);
            if (!string.IsNullOrEmpty(cinematicId) && CinematicDirector.Has(cinematicId)) CinematicDirector.Play(cinematicId, Scene);
            else Scene();
        }

        void Scene()
        {
            var lines = string.IsNullOrEmpty(sceneKey) ? null : ChapterDialogue.Scene(sceneKey);
            if (lines != null && lines.Count > 0) DialogueSystem.Show(lines, Report);
            else Report();
        }

        void Report()
        {
            if (!string.IsNullOrEmpty(reportId)) QuestManager.ReportInteract(reportId);
            // Commit the one-shot flag only after the quest event (and its save) has completed.
            // Quitting during a cinematic or dialogue will let the player retry the interaction.
            if (oneShot) SaveSystem.MarkChest(SaveKey);
        }

        void SetUsed()
        {
            used = true;
            SetGlow(0.12f);
        }

        void SetGlow(float k)
        {
            if (glowLight) glowLight.intensity = 2.5f * k;
            if (glow)
            {
                var mpb = new MaterialPropertyBlock();
                glow.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", glowColor * (3f * k));
                glow.SetPropertyBlock(mpb);
            }
        }
    }
}
