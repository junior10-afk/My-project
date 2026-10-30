using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Idle life for NPCs and cinematic actors on Humanoid.controller: a resting loop (idle, folded arms,
    /// lantern…), a talking loop while their name is the current dialogue or subtitle speaker, and one-shot
    /// actions triggered by cinematics.
    /// </summary>
    public class NpcAnimator : MonoBehaviour
    {
        public Animator rig;
        public string idleState = "Idle";
        public string talkState = "Talk";
        [Tooltip("Dialogue/subtitle speaker name that makes this character gesture (empty = never talks).")]
        public string speakerName;

        string current;
        float actionUntil;

        void Start()
        {
            if (!rig) rig = GetComponentInChildren<Animator>();
            if (!rig) return;
            rig.enabled = true;
            rig.applyRootMotion = false;
            rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (rig.runtimeAnimatorController) rig.Play(idleState, 0, Random.value); // desynchronise identical idles
            current = idleState;
        }

        void OnEnable()
        {
            // Re-entering a cinematic: restart from the resting pose.
            current = null;
            actionUntil = 0f;
        }

        public void PlayAction(string state, float duration, float fade = 0.15f)
        {
            if (!rig) return;
            HumanoidAnim.Play(rig, state, duration, fade);
            actionUntil = Time.time + duration * 0.9f;
            current = null;
        }

        /// <summary>Changes the resting loop (e.g. a character sitting down for a scene).</summary>
        public void SetIdle(string state)
        {
            idleState = state;
            current = null;
        }

        void Update()
        {
            if (!rig || !rig.runtimeAnimatorController || Time.time < actionUntil) return;
            bool talking = !string.IsNullOrEmpty(speakerName)
                           && (DialogueSystem.CurrentSpeaker == speakerName || CinematicDirector.CurrentSpeaker == speakerName);
            string want = talking ? talkState : idleState;
            if (want == current) return;
            rig.CrossFadeInFixedTime(want, 0.3f, 0);
            current = want;
        }
    }
}
