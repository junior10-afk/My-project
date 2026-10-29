using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LastWorld
{
    /// <summary>
    /// Timeline clip firing a CinematicDirector action when it starts: "place:Mark", "show:Object",
    /// "hide:Object", "burst:Mark:Element", "sound:id", "banner:Text". Essential actions still run
    /// when the cinematic is skipped.
    /// </summary>
    [Serializable]
    public class CinematicActionClip : PlayableAsset, ITimelineClipAsset
    {
        public string action;
        public bool essential;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CinematicActionBehaviour>.Create(graph);
            playable.GetBehaviour().action = action;
            return playable;
        }
    }

    public class CinematicActionBehaviour : PlayableBehaviour
    {
        public string action;
        bool fired;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (fired || !Application.isPlaying) return;
            fired = true;
            CinematicDirector.Fire(action);
        }
    }
}
