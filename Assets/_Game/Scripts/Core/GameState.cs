using System.Collections;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Global game flags and time effects: UI focus (blocks gameplay input and frees the cursor),
    /// hit-stop and slow motion used by combat.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        /// <summary>True while a menu, dialogue or map is open.</summary>
        public static bool UIOpen { get; set; }
        public static bool InputBlocked => UIOpen || Cinematic;
        /// <summary>True while a Timeline cinematic is playing.</summary>
        public static bool Cinematic => Instance && Instance.cinematic;

        public bool cinematic;

        float slowMoUntil;
        float slowMoScale = 1f;
        Coroutine hitStop;

        void Awake()
        {
            Instance = this;
            UIOpen = false;
        }

        void Update()
        {
            if (hitStop != null) return;
            float target = Time.unscaledTime < slowMoUntil ? slowMoScale : 1f;
            Time.timeScale = Mathf.MoveTowards(Time.timeScale, target, Time.unscaledDeltaTime * 4f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        public static void HitStop(float duration)
        {
            if (!Instance || duration <= 0f) return;
            if (Instance.hitStop != null) Instance.StopCoroutine(Instance.hitStop);
            Instance.hitStop = Instance.StartCoroutine(Instance.HitStopRoutine(duration));
        }

        IEnumerator HitStopRoutine(float duration)
        {
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prev;
            hitStop = null;
        }

        public static void SlowMotion(float scale, float duration)
        {
            if (!Instance) return;
            Instance.slowMoScale = scale;
            Instance.slowMoUntil = Time.unscaledTime + duration;
            Time.timeScale = scale;
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
