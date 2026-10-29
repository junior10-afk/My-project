using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LastWorld
{
    /// <summary>Timeline clip showing one subtitle line (speaker + text) for its duration.</summary>
    [Serializable]
    public class SubtitleClip : PlayableAsset, ITimelineClipAsset
    {
        public string speaker;
        [TextArea(2, 4)] public string text;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SubtitleBehaviour>.Create(graph);
            var b = playable.GetBehaviour();
            b.speaker = speaker;
            b.text = text;
            return playable;
        }
    }

    public class SubtitleBehaviour : PlayableBehaviour
    {
        public string speaker, text;

        public override void OnBehaviourPlay(Playable playable, FrameData info) => CinematicDirector.SetSubtitle(speaker, text);
        public override void OnBehaviourPause(Playable playable, FrameData info) => CinematicDirector.ClearSubtitle(text);
    }
}
