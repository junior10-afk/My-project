using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Code-driven character animation for any Unity Humanoid rig. Poses are authored as Euler offsets in
    /// character space relative to the avatar's T-pose (read from the Avatar's HumanDescription), then
    /// converted into each bone's local frame, so the same poses work on rigs with arbitrary bone rolls.
    /// Covers idle breathing, walk/run/sprint cycles synced to speed, jump/fall, glide, climb, swim, and
    /// combat: 5-step sword combo, charged spin, skill and burst casts, dodge and hit reactions.
    /// Axis conventions (character space, facing +Z, "L" = +X side, i.e. the character's right/sword arm):
    /// +X pitch swings limbs backward and tips the spine forward; Z lowers/raises arms from the T-pose;
    /// Y bends elbows and twists the spine.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        public enum Bone
        {
            Hips, Spine, Chest, UpperChest, Neck, Head,
            LShoulder, LUpperArm, LLowerArm, LHand,
            RShoulder, RUpperArm, RLowerArm, RHand,
            LUpperLeg, LLowerLeg, LFoot, LToes,
            RUpperLeg, RLowerLeg, RFoot, RToes,
        }

        // "L" (+X side) maps to the humanoid's Right bones and vice versa.
        static readonly HumanBodyBones[] Map =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        };

        public PlayerController controller;
        public Animator rig;
        public float blendSpeed = 12f;

        Transform[] bones = new Transform[22];
        Quaternion[] bindLocal = new Quaternion[22];
        Quaternion[] parentBindWorld = new Quaternion[22];
        Vector3 hipsBind;
        readonly Vector3[] current = new Vector3[22];
        readonly Vector3[] target = new Vector3[22];
        float hipsOffset, hipsOffsetTarget;
        float phase, climbPhase, swimPhase;
        bool ready;

        public bool isNPC;
        [Tooltip("Leave the Animator running (mocap) and only override the bones while this component is enabled.")]
        public bool overlay;

        void OnEnable()
        {
            for (int i = 0; i < current.Length; i++) current[i] = Vector3.zero;
        }

        void Start()
        {
            if (!controller && !isNPC) controller = GetComponentInParent<PlayerController>();
            if (rig) SetRig(rig);
        }

        /// <summary>Binds to a humanoid Animator (called when switching party member).</summary>
        public void SetRig(Animator animator)
        {
            rig = animator;
            ready = false;
            if (!rig || !rig.avatar || !rig.avatar.isHuman) return;
            if (!overlay) rig.enabled = false; // we drive the bones directly

            var skeleton = new Dictionary<string, SkeletonBone>();
            foreach (var sb in rig.avatar.humanDescription.skeleton) skeleton[sb.name] = sb;

            Quaternion TPoseLocal(Transform t) => skeleton.TryGetValue(t.name, out var sb) ? sb.rotation : t.localRotation;
            Quaternion TPoseWorld(Transform t)
            {
                var q = Quaternion.identity;
                for (var p = t; p && p != rig.transform; p = p.parent) q = TPoseLocal(p) * q;
                return q;
            }

            for (int i = 0; i < Map.Length; i++)
            {
                bones[i] = rig.GetBoneTransform(Map[i]);
                if (!bones[i]) continue;
                bindLocal[i] = TPoseLocal(bones[i]);
                parentBindWorld[i] = bones[i].parent && bones[i].parent != rig.transform ? TPoseWorld(bones[i].parent) : Quaternion.identity;
            }
            hipsBind = skeleton.TryGetValue(bones[0].name, out var hb) ? hb.position : bones[0].localPosition;
            for (int i = 0; i < current.Length; i++) current[i] = Vector3.zero;
            ready = true;
        }

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < target.Length; i++) target[i] = Vector3.zero;
            hipsOffsetTarget = 0f;
            RestArms();

            // Without a controller (NPCs) the character simply idles.
            float speed = controller ? controller.PlanarVelocity.magnitude : 0f;
            switch (controller ? controller.State : MoveState.Grounded)
            {
                case MoveState.Grounded: Locomotion(speed, dt); break;
                case MoveState.Airborne: Air(); break;
                case MoveState.Gliding: Glide(); break;
                case MoveState.Climbing: Climb(dt); break;
                case MoveState.Swimming: Swim(speed, dt); break;
            }
            float blend = controller && Combat() ? 26f : blendSpeed;

            float k = 1f - Mathf.Exp(-blend * dt);
            for (int i = 0; i < bones.Length; i++)
            {
                if (!bones[i]) continue;
                current[i] = Vector3.Lerp(current[i], target[i], k);
                Quaternion p = parentBindWorld[i];
                bones[i].localRotation = Quaternion.Inverse(p) * Quaternion.Euler(current[i]) * p * bindLocal[i];
            }
            hipsOffset = Mathf.Lerp(hipsOffset, hipsOffsetTarget, k);
            bones[0].localPosition = hipsBind + (bones[0].parent ? bones[0].parent.InverseTransformVector(rig.transform.up) : Vector3.up) * hipsOffset;
        }

        void Set(Bone b, float x, float y, float z) => target[(int)b] = new Vector3(x, y, z);
        void Add(Bone b, float x, float y, float z) => target[(int)b] += new Vector3(x, y, z);

        void RestArms()
        {
            Set(Bone.LUpperArm, -4f, 0f, -72f);
            Set(Bone.RUpperArm, -4f, 0f, 72f);
            Set(Bone.LLowerArm, 0f, -12f, 0f);
            Set(Bone.RLowerArm, 0f, 12f, 0f);
        }

        // ---------------------------------------------------------------- Locomotion

        void Locomotion(float speed, float dt)
        {
            float t = Time.time;
            float move = Mathf.Clamp01(speed / 1.2f);
            float run = Mathf.InverseLerp(2.5f, 8.5f, speed);
            float stride = Mathf.Lerp(0.75f, 1.45f, run);
            phase += speed * dt / stride * Mathf.PI;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);

            float idle = 1f - move;
            float breath = Mathf.Sin(t * 1.7f);
            Add(Bone.Chest, breath * 1.5f * idle, 0f, 0f);
            Add(Bone.Spine, 0f, 0f, Mathf.Sin(t * 0.5f) * 1.5f * idle);
            Add(Bone.Head, Mathf.Sin(t * 0.37f) * 2f * idle, Mathf.Sin(t * 0.23f) * 6f * idle, 0f);
            Add(Bone.LUpperArm, 0f, 0f, breath * 1.2f * idle);
            Add(Bone.RUpperArm, 0f, 0f, -breath * 1.2f * idle);

            float thighAmp = Mathf.Lerp(28f, 52f, run) * move;
            float kneeAmp = Mathf.Lerp(40f, 100f, run) * move;
            float bias = -run * 12f * move;
            float thighL = -s * thighAmp + bias;
            float thighR = s * thighAmp + bias;
            float kneeL = 4f + kneeAmp * Mathf.Pow(Mathf.Max(0f, c), 1.5f) + run * 10f * move;
            float kneeR = 4f + kneeAmp * Mathf.Pow(Mathf.Max(0f, -c), 1.5f) + run * 10f * move;
            Set(Bone.LUpperLeg, thighL, 0f, 0f);
            Set(Bone.RUpperLeg, thighR, 0f, 0f);
            Set(Bone.LLowerLeg, kneeL, 0f, 0f);
            Set(Bone.RLowerLeg, kneeR, 0f, 0f);
            Set(Bone.LFoot, -(thighL + kneeL) * 0.75f, 0f, 0f);
            Set(Bone.RFoot, -(thighR + kneeR) * 0.75f, 0f, 0f);

            float armAmp = Mathf.Lerp(22f, 60f, run) * move;
            float elbow = Mathf.Lerp(15f, 90f, run) * move + 12f * (1f - move);
            Set(Bone.LUpperArm, s * armAmp - 4f, 0f, Mathf.Lerp(-72f, -66f, run * move));
            Set(Bone.RUpperArm, -s * armAmp - 4f, 0f, Mathf.Lerp(72f, 66f, run * move));
            Set(Bone.LLowerArm, 0f, -elbow - Mathf.Max(0f, -s) * 20f * run, 0f);
            Set(Bone.RLowerArm, 0f, elbow + Mathf.Max(0f, s) * 20f * run, 0f);

            float lean = (4f + run * 14f) * move;
            Add(Bone.Spine, lean * 0.6f, s * 5f * move, 0f);
            Add(Bone.Chest, lean * 0.4f, -s * 9f * move, 0f);
            Add(Bone.Head, -lean * 0.7f, s * 3f * move, 0f);
            Add(Bone.Hips, 0f, -s * 7f * move, c * 2f * move);

            float bob = Mathf.Lerp(0.025f, 0.06f, run) * move;
            hipsOffsetTarget = bob * (0.5f - Mathf.Abs(s)) - run * 0.05f * move;
        }

        void Air()
        {
            float vy = controller ? controller.VerticalSpeed : 0f;
            float fall = Mathf.InverseLerp(2f, -12f, vy);
            Set(Bone.LUpperLeg, Mathf.Lerp(-45f, -15f, fall), 0f, -4f);
            Set(Bone.RUpperLeg, Mathf.Lerp(-10f, 5f, fall), 0f, 4f);
            Set(Bone.LLowerLeg, Mathf.Lerp(70f, 25f, fall), 0f, 0f);
            Set(Bone.RLowerLeg, Mathf.Lerp(40f, 30f, fall), 0f, 0f);
            Set(Bone.LUpperArm, -10f, 0f, Mathf.Lerp(-45f, -15f, fall));
            Set(Bone.RUpperArm, -10f, 0f, Mathf.Lerp(45f, 15f, fall));
            Set(Bone.LLowerArm, 0f, -25f, 0f);
            Set(Bone.RLowerArm, 0f, 25f, 0f);
            Set(Bone.Spine, Mathf.Lerp(8f, -4f, fall), 0f, 0f);
            Set(Bone.Head, Mathf.Lerp(-6f, 10f, fall), 0f, 0f);
        }

        void Glide()
        {
            float t = Time.time;
            Set(Bone.LUpperArm, -15f, 0f, 68f);
            Set(Bone.RUpperArm, -15f, 0f, -68f);
            Set(Bone.LLowerArm, 0f, -18f, 0f);
            Set(Bone.RLowerArm, 0f, 18f, 0f);
            Set(Bone.Head, -40f, 0f, 0f);
            Set(Bone.Spine, -6f, 0f, 0f);
            Set(Bone.LUpperLeg, 6f + Mathf.Sin(t * 3f) * 4f, 0f, -2f);
            Set(Bone.RUpperLeg, 6f - Mathf.Sin(t * 3f) * 4f, 0f, 2f);
            Set(Bone.LLowerLeg, 12f, 0f, 0f);
            Set(Bone.RLowerLeg, 18f, 0f, 0f);
            Set(Bone.LFoot, 30f, 0f, 0f);
            Set(Bone.RFoot, 30f, 0f, 0f);
        }

        void Climb(float dt)
        {
            climbPhase += controller.MoveInput.magnitude * dt * 4.5f;
            float s = Mathf.Sin(climbPhase);
            Set(Bone.LUpperArm, -30f, 0f, 55f + s * 28f);
            Set(Bone.RUpperArm, -30f, 0f, -55f + s * 28f);
            Set(Bone.LLowerArm, 0f, -45f - s * 25f, 0f);
            Set(Bone.RLowerArm, 0f, 45f - s * 25f, 0f);
            Set(Bone.LUpperLeg, -45f - s * 25f, 0f, -8f);
            Set(Bone.RUpperLeg, -45f + s * 25f, 0f, 8f);
            Set(Bone.LLowerLeg, 75f + s * 20f, 0f, 0f);
            Set(Bone.RLowerLeg, 75f - s * 20f, 0f, 0f);
            Set(Bone.LFoot, -20f, 0f, 0f);
            Set(Bone.RFoot, -20f, 0f, 0f);
            Set(Bone.Spine, -6f, s * 4f, 0f);
            Set(Bone.Head, -20f, 0f, 0f);
            hipsOffsetTarget = -0.05f;
        }

        void Swim(float speed, float dt)
        {
            float t = Time.time;
            swimPhase += dt * (1.2f + speed * 0.7f) * 2.2f;
            if (speed > 0.4f)
            {
                float a = swimPhase;
                Set(Bone.LUpperArm, -90f - 80f * Mathf.Cos(a), 0f, -78f);
                Set(Bone.RUpperArm, -90f - 80f * Mathf.Cos(a + Mathf.PI), 0f, 78f);
                Set(Bone.LLowerArm, 0f, -20f, 0f);
                Set(Bone.RLowerArm, 0f, 20f, 0f);
                Set(Bone.LUpperLeg, Mathf.Sin(a * 2f) * 16f, 0f, 0f);
                Set(Bone.RUpperLeg, -Mathf.Sin(a * 2f) * 16f, 0f, 0f);
                Set(Bone.LLowerLeg, 12f, 0f, 0f);
                Set(Bone.RLowerLeg, 12f, 0f, 0f);
                Set(Bone.LFoot, 35f, 0f, 0f);
                Set(Bone.RFoot, 35f, 0f, 0f);
                Set(Bone.Head, -55f, Mathf.Sin(a) * 15f, 0f);
                Set(Bone.Chest, 0f, Mathf.Sin(a) * 12f, 0f);
            }
            else
            {
                Set(Bone.LUpperArm, Mathf.Sin(t * 3f) * 18f, 0f, -35f);
                Set(Bone.RUpperArm, -Mathf.Sin(t * 3f) * 18f, 0f, 35f);
                Set(Bone.LLowerArm, 0f, -35f, 0f);
                Set(Bone.RLowerArm, 0f, 35f, 0f);
                Set(Bone.LUpperLeg, -30f + Mathf.Sin(t * 2.5f) * 22f, 0f, 0f);
                Set(Bone.RUpperLeg, -30f - Mathf.Sin(t * 2.5f) * 22f, 0f, 0f);
                Set(Bone.LLowerLeg, 50f, 0f, 0f);
                Set(Bone.RLowerLeg, 50f, 0f, 0f);
                Set(Bone.Head, -8f, 0f, 0f);
            }
        }

        // ---------------------------------------------------------------- Combat overlays

        bool Combat()
        {
            var pc = PlayerCombat.Instance;
            if (!pc) return false;

            if (pc.HurtTime < 0.35f)
            {
                float w = 1f - pc.HurtTime / 0.35f;
                Add(Bone.Spine, -18f * w, 0f, 0f);
                Add(Bone.Head, -15f * w, 0f, 0f);
                Add(Bone.LUpperArm, -20f * w, 0f, 20f * w);
                Add(Bone.RUpperArm, -20f * w, 0f, -20f * w);
                return true;
            }

            if (pc.DodgeTime < 0.3f)
            {
                float w = Mathf.Sin(pc.DodgeTime / 0.3f * Mathf.PI);
                Add(Bone.Spine, 25f * w, 0f, 0f);
                Add(Bone.LUpperLeg, -40f * w, 0f, 0f);
                Add(Bone.RUpperLeg, -10f * w, 0f, 0f);
                Add(Bone.LLowerLeg, 60f * w, 0f, 0f);
                Add(Bone.RLowerLeg, 50f * w, 0f, 0f);
                hipsOffsetTarget -= 0.18f * w;
                return true;
            }

            if (pc.CastTime < 0.6f)
            {
                float k = pc.CastTime / 0.6f;
                float w = Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI);
                if (pc.LastCastWasBurst)
                {
                    Set(Bone.LUpperArm, -40f * w, 0f, Mathf.Lerp(-72f, 70f, w));
                    Set(Bone.RUpperArm, -40f * w, 0f, Mathf.Lerp(72f, -70f, w));
                    Add(Bone.Spine, -12f * w, 0f, 0f);
                    Add(Bone.Head, -20f * w, 0f, 0f);
                }
                else
                {
                    Set(Bone.LUpperArm, -75f * w, 0f, Mathf.Lerp(-72f, -10f, w));
                    Set(Bone.RUpperArm, -60f * w, 0f, Mathf.Lerp(72f, 20f, w));
                    Set(Bone.LLowerArm, 0f, -10f, 0f);
                    Add(Bone.Chest, 0f, -25f * w, 0f);
                    Add(Bone.Spine, 10f * w, 0f, 0f);
                }
                Add(Bone.LUpperLeg, -25f * w, 0f, 0f);
                Add(Bone.RUpperLeg, 15f * w, 0f, 0f);
                Add(Bone.LLowerLeg, 30f * w, 0f, 0f);
                hipsOffsetTarget -= 0.08f * w;
                return true;
            }

            if (pc.SpinTime < 0.55f)
            {
                float k = pc.SpinTime / 0.55f;
                Set(Bone.LUpperArm, -10f, 0f, -10f);
                Set(Bone.LLowerArm, 0f, -5f, 0f);
                Add(Bone.Chest, 0f, Mathf.Lerp(60f, -60f, k), 0f);
                Add(Bone.Spine, 12f, Mathf.Lerp(30f, -30f, k), 0f);
                hipsOffsetTarget -= 0.12f;
                return true;
            }

            int step = pc.ComboStep;
            if (step < 0 || pc.StepTime > pc.stepDuration * 1.15f) return false;
            float p = Mathf.Clamp01(pc.StepTime / pc.stepDuration);
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, p));
            bool backhand = step % 2 == 1;
            bool finisher = step == pc.comboMultipliers.Length - 1;

            if (finisher)
            {
                // Overhead jump-slam
                Set(Bone.LUpperArm, Mathf.Lerp(-30f, -95f, swing), 0f, Mathf.Lerp(80f, -20f, swing));
                Set(Bone.RUpperArm, Mathf.Lerp(-30f, -70f, swing), 0f, Mathf.Lerp(-80f, 30f, swing));
                Set(Bone.LLowerArm, 0f, -15f, 0f);
                Add(Bone.Spine, Mathf.Lerp(-15f, 30f, swing), 0f, 0f);
                Add(Bone.LUpperLeg, Mathf.Lerp(0f, -45f, swing), 0f, 0f);
                Add(Bone.LLowerLeg, Mathf.Lerp(10f, 60f, swing), 0f, 0f);
                Add(Bone.RUpperLeg, Mathf.Lerp(0f, 20f, swing), 0f, 0f);
                hipsOffsetTarget -= 0.15f * swing;
            }
            else if (!backhand)
            {
                // Forehand: from high outside to low across the body
                Set(Bone.LUpperArm, Mathf.Lerp(40f, -70f, swing), Mathf.Lerp(-20f, 30f, swing), Mathf.Lerp(20f, -45f, swing));
                Set(Bone.LLowerArm, 0f, Mathf.Lerp(-60f, -10f, swing), 0f);
                Set(Bone.RUpperArm, -20f, 0f, 55f);
                Add(Bone.Chest, 0f, Mathf.Lerp(30f, -35f, swing), 0f);
                Add(Bone.Spine, 8f, Mathf.Lerp(15f, -15f, swing), 0f);
            }
            else
            {
                // Backhand: from across the body outward
                Set(Bone.LUpperArm, Mathf.Lerp(-60f, -20f, swing), Mathf.Lerp(40f, -30f, swing), Mathf.Lerp(-50f, 10f, swing));
                Set(Bone.LLowerArm, 0f, Mathf.Lerp(-90f, -15f, swing), 0f);
                Set(Bone.RUpperArm, -10f, 0f, 60f);
                Add(Bone.Chest, 0f, Mathf.Lerp(-35f, 30f, swing), 0f);
                Add(Bone.Spine, 8f, Mathf.Lerp(-15f, 15f, swing), 0f);
            }
            Add(Bone.LUpperLeg, -20f, 0f, 0f);
            Add(Bone.RUpperLeg, 12f, 0f, 0f);
            Add(Bone.LLowerLeg, 25f, 0f, 0f);
            Add(Bone.RLowerLeg, 10f, 0f, 0f);
            hipsOffsetTarget -= 0.06f;
            return true;
        }
    }
}
