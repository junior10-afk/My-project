using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    /// <summary>An enemy camp: spawns its group, reports when cleared and respawns it later out of sight.</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public string campId = "camp";
        public GameObject[] prefabs;
        public GameObject[] extraPrefabs;
        public int count = 3;
        public float radius = 6f;
        public bool respawn = true;
        public float respawnDelay = 180f;

        readonly List<EnemyBase> alive = new();
        float respawnAt = -1f;

        public bool Cleared => alive.Count == 0;

        void Start() => SpawnAll();

        void SpawnAll()
        {
            if (prefabs == null || prefabs.Length == 0) return;
            for (int i = 0; i < count; i++) Spawn(prefabs[i % prefabs.Length]);
        }

        public void SpawnExtra(int n)
        {
            var set = extraPrefabs != null && extraPrefabs.Length > 0 ? extraPrefabs : prefabs;
            for (int i = 0; i < n; i++)
            {
                var e = Spawn(set[i % set.Length]);
                CombatFX.Burst(e.transform.position + Vector3.up, Color.white, 1.5f, 25);
            }
        }

        EnemyBase Spawn(GameObject prefab)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            var pos = transform.position + new Vector3(c.x, 0f, c.y);
            if (Terrain.activeTerrain) pos.y = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            go.SetActive(true);
            var e = go.GetComponent<EnemyBase>();
            var lvl = GetComponent<CampLevel>();
            if (lvl) lvl.Apply(e);
            e.RefreshLabel();
            alive.Add(e);
            return e;
        }

        /// <summary>Respawns the group immediately if it was already cleared (used when a quest needs it).</summary>
        public void EnsureSpawned()
        {
            if (alive.Count > 0) return;
            respawnAt = -1f;
            SpawnAll();
        }

        public static EnemySpawner Find(string id)
        {
            foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Exclude))
                if (s.campId == id) return s;
            return null;
        }

        public void OnEnemyDied(EnemyBase e)
        {
            alive.Remove(e);
            if (alive.Count > 0) return;
            QuestManager.ReportCampCleared(campId);
            if (respawn) respawnAt = Time.time + respawnDelay;
        }

        void Update()
        {
            if (respawnAt < 0f || Time.time < respawnAt) return;
            var p = PlayerController.Instance;
            if (p && Vector3.Distance(p.transform.position, transform.position) < 60f) return;
            respawnAt = -1f;
            SpawnAll();
        }
    }
}
