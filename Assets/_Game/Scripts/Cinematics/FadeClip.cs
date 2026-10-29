using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LastWorld
{
    /// <summary>Timeline clip fading the screen overlay from one opacity to another (black, white…).</summary>
    [Serializable]
    public class FadeClip : PlayableAsset, ITimelineClipAsset
    {
        public Color color = Color.black;
        [Range(0f, 1f)] public float from = 1f;
        [Range(0f, 1f)] public float to;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FadeBehaviour>.Create(graph);
            var b = playable.GetBehaviour();
            b.color = color;
            b.from = from;
            b.to = to;
            return playable;
        }
    }

    public class FadeBehaviour : PlayableBehaviour
    {
        public Color color;
        public float from, to;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            double d = playable.GetDuration();
            float t = d > 0 ? Mathf.SmoothStep(0f, 1f, (float)(playable.GetTime() / d)) : 1f;
            var c = color;
            c.a = Mathf.Lerp(from, to, t);
            CinematicDirector.SetFade(c);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // Hold the final value once the clip has fully played.
            if (playable.GetTime() >= playable.GetDuration() - 0.05)
            {
                var c = color;
                c.a = to;
                CinematicDirector.SetFade(c);
            }
        }
    }
}
