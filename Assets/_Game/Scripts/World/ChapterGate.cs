using UnityEngine;

namespace LastWorld
{
    /// <summary>Shows its targets only while the number of finished chapters is within [showFrom, hideFrom).</summary>
    public class ChapterGate : MonoBehaviour
    {
        public int showFrom;
        public int hideFrom = 99;
        public GameObject[] targets;

        float timer;
        int last = -1;

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            var qm = QuestManager.Instance;
            if (!qm) return;
            int done = qm.ChaptersDone;
            if (done == last) return;
            last = done;
            bool on = done >= showFrom && done < hideFrom;
            foreach (var t in targets)
                if (t) t.SetActive(on);
        }
    }
}
