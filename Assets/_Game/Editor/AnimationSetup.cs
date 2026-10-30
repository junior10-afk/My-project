using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Sets up the Quaternius Universal Animation Libraries (CC0, humanoid) and builds the shared
    /// Humanoid.controller used by the player, NPCs and cinematic actors. The controller is a flat list of
    /// states (locomotion blend trees, actions, gestures) with no transitions: CharacterAnimator and
    /// NpcAnimator cross-fade between them from code. Action states read the "ActionSpeed" parameter so
    /// clips can be stretched to the gameplay timing.
    /// </summary>
    public static class AnimationSetup
    {
        public const string ControllerPath = "Assets/_Game/Animation/Humanoid.controller";
        const string Ual1 = "Assets/_Game/Art/Animations/Quaternius/UAL1_Standard.fbx";
        const string Ual2 = "Assets/_Game/Art/Animations/Quaternius/UAL2_Standard.fbx";

        /// <summary>State name → (library, clip). Gameplay code refers to these state names.</summary>
        static readonly (string state, string fbx, string clip)[] Actions =
        {
            ("Attack0", Ual2, "Sword_Regular_A"), ("Attack1", Ual2, "Sword_Regular_B"), ("Attack2", Ual2, "Sword_Regular_C"),
            ("Attack3", Ual2, "Sword_Dash_RM"), ("Attack4", Ual1, "Sword_Attack"), ("Charged", Ual2, "Sword_Regular_Combo"),
            ("Skill", Ual1, "Spell_Simple_Shoot"), ("Burst", Ual2, "OverhandThrow"), ("Dodge", Ual1, "Roll"),
            ("Parry", Ual2, "Sword_Block"), ("Hurt", Ual1, "Hit_Chest"), ("HurtHeavy", Ual2, "Hit_Knockback"),
            ("Death", Ual1, "Death01"), ("JumpStart", Ual1, "Jump_Start"), ("Land", Ual1, "Jump_Land"),
            ("Interact", Ual1, "Interact"), ("ChestOpen", Ual2, "Chest_Open"), ("PickUp", Ual1, "PickUp_Table"),
            ("LayToIdle", Ual2, "LayToIdle"), ("Yes", Ual2, "Yes"), ("Consume", Ual2, "Consume"),
            ("SpellEnter", Ual1, "Spell_Simple_Enter"), ("Punch", Ual1, "Punch_Cross"),
        };

        static readonly (string state, string fbx, string clip)[] Loops =
        {
            ("Idle", Ual1, "Idle_Loop"), ("Talk", Ual1, "Idle_Talking_Loop"), ("FoldArms", Ual2, "Idle_FoldArms_Loop"),
            ("Lantern", Ual2, "Idle_Lantern_Loop"), ("Sit", Ual1, "Sitting_Idle_Loop"), ("No", Ual2, "Idle_No_Loop"),
            ("SwordIdle", Ual1, "Sword_Idle"), ("Air", Ual1, "Jump_Loop"), ("Glide", Ual2, "NinjaJump_Idle_Loop"),
            ("Crouch", Ual1, "Crouch_Idle_Loop"), ("Dance", Ual1, "Dance_Loop"),
        };

        [MenuItem("Last World/Build Animations")]
        public static void Build()
        {
            ConfigureClips(Ual1);
            ConfigureClips(Ual2);
            BuildController();
            BuildLengthTable();
            AssetDatabase.SaveAssets();
            Debug.Log("[AnimationSetup] Humanoid.controller built.");
        }

        /// <summary>Clean names, loop flags, root rotation/height baked into the pose (gameplay moves the character).</summary>
        static void ConfigureClips(string fbx)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (!imp) { Debug.LogError($"[AnimationSetup] Missing {fbx}"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = true;
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                string name = c.name.Contains('|') ? c.name[(c.name.IndexOf('|') + 1)..] : c.name;
                c.name = name;
                c.loopTime = c.loopPose = name.EndsWith("_Loop") || name == "Sword_Idle";
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                // Horizontal travel stays on the root (discarded: applyRootMotion is off) so dash attacks
                // don't slide the body away from the character controller.
                c.lockRootPositionXZ = false;
                c.keepOriginalOrientation = false; // "body orientation": the original root faces backwards (180°)
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;
                c.keepOriginalPositionXZ = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        static AnimationClip Clip(string fbx, string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            if (!clip) Debug.LogWarning($"[AnimationSetup] Clip not found: {name}");
            return clip;
        }

        static void BuildController()
        {
            ArtLibrary.EnsureFolder("Assets/_Game/Animation");
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!ctrl) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Reset parameters and the base state machine in place (keeps the asset GUID referenced by prefabs).
            foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("ActionSpeed", AnimatorControllerParameterType.Float);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>().ToArray())
                AssetDatabase.RemoveObjectFromAsset(o);

            var pos = new Vector3(300f, 0f, 0f);
            AnimatorState Add(string name, Motion motion)
            {
                var st = sm.AddState(name, pos);
                pos += new Vector3(0f, 55f, 0f);
                st.motion = motion;
                st.writeDefaultValues = true;
                return st;
            }

            BlendTree Locomotion(string name, Motion idle)
            {
                var tree = new BlendTree { name = name, blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(tree, ctrl);
                tree.AddChild(idle, 0f);
                tree.AddChild(Clip(Ual1, "Walk_Loop"), 2.2f);   // PlayerController.walkSpeed
                tree.AddChild(Clip(Ual1, "Jog_Fwd_Loop"), 5.2f); // runSpeed
                tree.AddChild(Clip(Ual1, "Sprint_Loop"), 8.5f);  // sprintSpeed
                return tree;
            }

            sm.defaultState = Add("Locomotion", Locomotion("Locomotion", Clip(Ual1, "Idle_Loop")));
            Add("CombatLocomotion", Locomotion("CombatLocomotion", Clip(Ual1, "Sword_Idle")));
            var swim = new BlendTree { name = "Swim", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(swim, ctrl);
            swim.AddChild(Clip(Ual1, "Swim_Idle_Loop"), 0f);
            swim.AddChild(Clip(Ual1, "Swim_Fwd_Loop"), 2.8f);
            Add("Swim", swim);
            foreach (var (state, fbx, clip) in Loops) Add(state, Clip(fbx, clip));
            foreach (var (state, fbx, clip) in Actions)
            {
                var st = Add(state, Clip(fbx, clip));
                st.speedParameter = "ActionSpeed";
                st.speedParameterActive = true;
            }
            EditorUtility.SetDirty(ctrl);
        }

        static void BuildLengthTable()
        {
            const string path = "Assets/_Game/Resources/HumanoidClips.asset";
            ArtLibrary.EnsureFolder("Assets/_Game/Resources");
            var table = AssetDatabase.LoadAssetAtPath<AnimationLengths>(path);
            if (!table)
            {
                table = ScriptableObject.CreateInstance<AnimationLengths>();
                AssetDatabase.CreateAsset(table, path);
            }
            var states = new List<string>();
            var lengths = new List<float>();
            foreach (var (state, fbx, clip) in Actions.Concat(Loops))
            {
                var c = Clip(fbx, clip);
                if (!c) continue;
                states.Add(state);
                lengths.Add(c.length);
            }
            table.states = states.ToArray();
            table.lengths = lengths.ToArray();
            EditorUtility.SetDirty(table);
        }
    }
}
