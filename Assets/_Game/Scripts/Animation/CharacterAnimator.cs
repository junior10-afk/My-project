using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Drives the active party member's Humanoid.controller (Quaternius mocap clips) from gameplay state:
    /// locomotion blend (idle/walk/jog/sprint, sword stance in combat), jump/fall, glide, swim, the 5-step
    /// combo, charged attack, skill, burst, dodge, parry, hit and down. Combat events are detected from
    /// PlayerCombat's timers resetting. Climbing has no mocap clip: the ProceduralAnimator takes over while
    /// climbing only. Cinematics can trigger any state through PlayAction.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public PlayerController controller;
        public ProceduralAnimator climbAnimator;
        public RuntimeAnimatorController controllerAsset;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        Animator anim;
        string baseState;
        float actionUntil;
        float airTime;
        bool wasDown;
        MoveState lastMove;
        float lastStep = 99f, lastCast = 99f, lastDodge = 99f, lastHurt = 99f, lastSpin = 99f;

        public Animator Rig => anim;

        void Start()
        {
            if (!controller) controller = GetComponent<PlayerController>();
            if (!climbAnimator) climbAnimator = GetComponent<ProceduralAnimator>();
            var party = GetComponent<PartyManager>();
            if (!anim && party && party.Active != null && party.Active.model) SetRig(party.Active.model.GetComponent<Animator>());
        }

        /// <summary>Binds to a party member's Animator (called on party switch).</summary>
        public void SetRig(Animator animator)
        {
            anim = animator;
            if (!anim) return;
            anim.enabled = true;
            if (controllerAsset) anim.runtimeAnimatorController = controllerAsset;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            baseState = null;
            actionUntil = 0f;
            if (climbAnimator)
            {
                climbAnimator.overlay = true;
                climbAnimator.SetRig(anim);
                climbAnimator.enabled = false;
            }
        }

        /// <summary>Plays a one-shot state stretched to <paramref name="duration"/> seconds, then returns to locomotion.</summary>
        public void PlayAction(string state, float duration, float fade = 0.1f)
        {
            if (!anim) return;
            HumanoidAnim.Play(anim, state, duration, fade);
            actionUntil = Time.time + duration * 0.9f;
            baseState = null;
        }

        void Update()
        {
            if (!anim || !controller) return;
            var state = controller.State;
            anim.SetFloat(SpeedId, controller.PlanarVelocity.magnitude, 0.08f, Time.deltaTime);
            if (climbAnimator) climbAnimator.enabled = state == MoveState.Climbing;

            CombatEvents();

            var party = PartyManager.Instance;
            bool down = party && party.Active != null && party.Active.Down;
            if (down && !wasDown) PlayAction("Death", AnimationLengths.Of("Death"));
            wasDown = down;
            if (down) { actionUntil = Time.time + 1f; return; }

            // Jump take-off and landing accents
            if (lastMove == MoveState.Grounded && state == MoveState.Airborne && controller.VerticalSpeed > 1f) PlayAction("JumpStart", 0.25f);
            airTime = state == MoveState.Airborne ? airTime + Time.deltaTime : 0f;
            if (lastMove == MoveState.Airborne && state == MoveState.Grounded && airTimeAtLanding > 0.6f) PlayAction("Land", 0.3f);
            airTimeAtLanding = state == MoveState.Airborne ? airTime : airTimeAtLanding;
            lastMove = state;

            if (Time.time < actionUntil) return;
            var combat = PlayerCombat.Instance;
            string want = state switch
            {
                MoveState.Airborne => "Air",
                MoveState.Gliding => "Glide",
                MoveState.Swimming => "Swim",
                MoveState.Climbing => "Idle",
                _ => combat && combat.InCombat ? "CombatLocomotion" : "Locomotion",
            };
            if (want == baseState) return;
            anim.CrossFadeInFixedTime(want, 0.2f, 0);
            baseState = want;
        }

        float airTimeAtLanding;

        void CombatEvents()
        {
            var c = PlayerCombat.Instance;
            if (!c) return;
            if (c.StepTime < lastStep)
            {
                if (c.ComboStep >= 0) PlayAction("Attack" + Mathf.Min(c.ComboStep, 4), c.stepDuration * 1.3f, 0.05f);
                else PlayAction("Parry", 0.5f, 0.05f);
            }
            if (c.SpinTime < lastSpin) PlayAction("Charged", 0.8f, 0.05f);
            if (c.CastTime < lastCast) PlayAction(c.LastCastWasBurst ? "Burst" : "Skill", c.LastCastWasBurst ? 1.0f : 0.55f, 0.05f);
            if (c.DodgeTime < lastDodge) PlayAction("Dodge", 0.45f, 0.05f);
            if (c.HurtTime < lastHurt) PlayAction("Hurt", 0.35f, 0.05f);
            lastStep = c.StepTime;
            lastSpin = c.SpinTime;
            lastCast = c.CastTime;
            lastDodge = c.DodgeTime;
            lastHurt = c.HurtTime;
        }
    }
}
