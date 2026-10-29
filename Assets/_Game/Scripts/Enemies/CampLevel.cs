using UnityEngine;

namespace LastWorld
{
    /// <summary>Level of an enemy camp; spawned enemies get their stats scaled to it.</summary>
    public class CampLevel : MonoBehaviour
    {
        public int level = 1;

        public void Apply(EnemyBase enemy)
        {
            float k = 1f + (level - 1) * 0.15f;
            enemy.Health.ApplyLevel(level, k);
            enemy.attackDamage *= 1f + (level - 1) * 0.08f;
            enemy.xpReward = Mathf.RoundToInt(enemy.xpReward * (1f + (level - 1) * 0.1f));
            enemy.moraReward = Mathf.RoundToInt(enemy.moraReward * (1f + (level - 1) * 0.1f));
        }
    }
}
