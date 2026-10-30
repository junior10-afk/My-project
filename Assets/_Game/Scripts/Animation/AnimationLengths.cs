using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Clip length of every state of Humanoid.controller (filled by AnimationSetup), so actions can be
    /// stretched to gameplay or cinematic durations. Lives in Resources/HumanoidClips.
    /// </summary>
    public class AnimationLengths : ScriptableObject
    {
        public string[] states = new string[0];
        public float[] lengths = new float[0];

        static Dictionary<string, float> table;

        public static float Of(string state)
        {
            if (table == null)
            {
                table = new Dictionary<string, float>();
                var asset = Resources.Load<AnimationLengths>("HumanoidClips");
                if (asset)
                    for (int i = 0; i < asset.states.Length && i < asset.lengths.Length; i++) table[asset.states[i]] = asset.lengths[i];
            }
            return table.TryGetValue(state, out var l) ? l : 1f;
        }
    }

    /// <summary>Shared helpers to drive Humanoid.controller from code.</summary>
    public static class HumanoidAnim
    {
        static readonly int ActionSpeed = Animator.StringToHash("ActionSpeed");

        /// <summary>Cross-fades to a state; with a duration, the clip is stretched to last exactly that long.</summary>
        public static void Play(Animator animator, string state, float duration = 0f, float fade = 0.12f)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            float len = AnimationLengths.Of(state);
            animator.SetFloat(ActionSpeed, duration > 0f ? len / duration : 1f);
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }
    }
}
