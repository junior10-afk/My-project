using UnityEngine;
using UnityEngine.Playables;

namespace LastWorld
{
    /// <summary>
    /// Moves (and optionally turns) an object between two poses following a PlayableDirector's
    /// time — camera dollies, a falling star, an actor walking. Runs before Cinemachine so shot
    /// cameras read the updated pose, and in edit mode so the Timeline preview matches.
    /// </summary>
    [ExecuteAlways, DefaultExecutionOrder(-200)]
    public class TimelineMover : MonoBehaviour
    {
        public PlayableDirector director;
        public float start, end = 1f;
        public Vector3 fromPosition, toPosition;
        public bool rotate;
        public Vector3 fromEuler, toEuler;
        public bool easeIn = true, easeOut = true;

        void LateUpdate()
        {
            if (!director) return;
            float t = Mathf.InverseLerp(start, end, (float)director.time);
            if (easeIn && easeOut) t = t * t * (3f - 2f * t);
            else if (easeIn) t *= t;
            else if (easeOut) t = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.LerpUnclamped(fromPosition, toPosition, t);
            if (rotate) transform.rotation = Quaternion.Slerp(Quaternion.Euler(fromEuler), Quaternion.Euler(toEuler), t);
        }
    }
}
