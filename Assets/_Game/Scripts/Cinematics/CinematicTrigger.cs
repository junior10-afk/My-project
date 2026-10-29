using UnityEngine;

namespace LastWorld
{
    /// <summary>Plays a cinematic once when the player comes within range during a given quest step.</summary>
    public class CinematicTrigger : MonoBehaviour
    {
        public string cinematicId;
        public string questId = "main1";
        public int step;
        public float radius = 30f;

        bool fired;

        void Update()
        {
            if (fired || CinematicDirector.Playing || GameState.UIOpen) return;
            var p = PlayerController.Instance;
            var qm = QuestManager.Instance;
            if (!p || !qm || !qm.IsAtStep(questId, step)) return;
            if (Vector3.Distance(p.transform.position, transform.position) > radius) return;
            fired = true;
            CinematicDirector.Play(cinematicId);
        }
    }
}
