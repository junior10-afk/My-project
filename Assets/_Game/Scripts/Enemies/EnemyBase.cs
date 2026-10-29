using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastWorld
{
    public enum EnemyKind { Slime, Golem, Boss }

    /// <summary>
    /// Enemy brain and procedural animation: wander → aggro → chase → telegraphed attack
    /// (heavy attacks flash yellow and can be parried) → recover; reacts to stagger, freeze,
    /// pull and knockback; drops XP / Mora / items and reports kills to quests.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyBase : MonoBehaviour
    {
        public static readonly List<EnemyBase> All = new();

        public EnemyKind kind = EnemyKind.Slime;
        public Element attackElement = Element.Physical;
        public float moveSpeed = 3f;
        public float attackRange = 2f;
        public float attackDamage = 90f;
        public float aggroRange = 15f;
        public float leashRange = 40f;
        public float attackCooldown = 2.2f;
        public float windup = 0.8f;
        [Range(0f, 1f)] public float heavyChance = 0.35f;
        public int xpReward = 60;
        public int moraReward = 40;
        public string dropItem = "slime_gel";
        public string killTag = "slime";
        public float respawnTime = 150f;

        [Header("Visual rig")]
        public Transform body;
        public Transform head;
        public Transform armL;
        public Transform armR;
        public Renderer glow;

        public Health Health { get; private set; }
        public bool ParryWindowOpen => state == State.Windup && heavy && stateTime > windup - 0.4f;

        enum State { Idle, Chase, Windup, Recover, Dead }

        State state;
        float stateTime;
        float nextAttack;
        bool heavy;
        float landTime;
        int bossPattern;
        Vector3 home;
        Vector3 wanderTarget;
        Vector3 external;
        float animPhase;
        Vector3 bodyBase;
        Vector3 bodyScale;
        Quaternion armLBase, armRBase;
        MaterialPropertyBlock mpb;
        Transform player;
        bool summoned;

        Canvas barCanvas;
        Text label;
        Image barFill;

        public void RefreshLabel()
        {
            if (label) label.text = $"Niv. {Health.level}  {Health.displayName}";
        }
        Image auraIcon;
        float showBarUntil;

        void Awake()
        {
            Health = GetComponent<Health>();
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            home = transform.position;
            wanderTarget = home;
            mpb = new MaterialPropertyBlock();
            if (body)
            {
                bodyBase = body.localPosition;
                bodyScale = body.localScale;
            }
            if (armL) armLBase = armL.localRotation;
            if (armR) armRBase = armR.localRotation;
            BuildHealthBar();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static List<EnemyBase> ByDistance(Vector3 pos, float range)
        {
            var list = new List<EnemyBase>();
            foreach (var e in All)
                if (!e.Health.Dead && Vector3.Distance(e.transform.position, pos) <= range) list.Add(e);
            list.Sort((a, b) => Vector3.Distance(a.transform.position, pos).CompareTo(Vector3.Distance(b.transform.position, pos)));
            return list;
        }

        public static bool AnyAttackLandingSoon(Vector3 pos, float window)
        {
            foreach (var e in All)
            {
                if (e.state != State.Windup) continue;
                float dt = e.landTime - Time.time;
                if (dt < window && dt > -0.05f && Vector3.Distance(e.transform.position, pos) < e.attackRange + 2.5f) return true;
            }
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            if (!player && PlayerController.Instance) player = PlayerController.Instance.transform;

            if (state == State.Dead)
            {
                transform.position += Vector3.down * (dt * 0.6f);
                if (body) body.localScale = Vector3.Lerp(body.localScale, Vector3.zero, dt * 2f);
                return;
            }

            if (external.sqrMagnitude > 0.01f)
            {
                transform.position += external * dt;
                external = Vector3.Lerp(external, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
            }

            bool stunned = Health.Frozen || Health.Staggered;
            if (stunned)
            {
                if (state == State.Windup) state = State.Recover;
                stateTime = 0f;
            }
            else if (!GameState.Cinematic) Think(dt);

            SnapToGround();
            Animate(dt, stunned);
            UpdateBar();
        }

        void Think(float dt)
        {
            if (!player) return;
            float dist = Vector3.Distance(transform.position, player.position);
            float fromHome = Vector3.Distance(transform.position, home);

            switch (state)
            {
                case State.Idle:
                    if (Vector3.Distance(transform.position, wanderTarget) < 0.8f || stateTime > 8f)
                    {
                        wanderTarget = home + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                        stateTime = 0f;
                    }
                    MoveTowards(wanderTarget, moveSpeed * 0.35f, dt);
                    if (dist < aggroRange && !PartyManager.Instance.Active.Down)
                    {
                        state = State.Chase;
                        stateTime = 0f;
                        DamageNumbers.ShowText("!", transform.position + Health.barOffset + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.3f), 40);
                    }
                    break;

                case State.Chase:
                    if (fromHome > leashRange || dist > aggroRange * 2.2f)
                    {
                        state = State.Idle;
                        wanderTarget = home;
                        Health.Heal(Health.maxHealth);
                        break;
                    }
                    if (kind == EnemyKind.Boss && !summoned && Health.Normalized < 0.5f) SummonAdds();
                    if (dist > attackRange * 0.85f) MoveTowards(player.position, moveSpeed, dt);
                    else Face(player.position, dt);
                    if (dist < attackRange && Time.time >= nextAttack)
                    {
                        state = State.Windup;
                        stateTime = 0f;
                        heavy = Random.value < heavyChance;
                        landTime = Time.time + windup;
                        if (heavy) AudioManager.Play("telegraph");
                    }
                    break;

                case State.Windup:
                    Face(player.position, dt * 0.5f);
                    if (stateTime >= windup) Strike();
                    break;

                case State.Recover:
                    if (stateTime > 0.7f)
                    {
                        state = State.Chase;
                        stateTime = 0f;
                        nextAttack = Time.time + attackCooldown * Random.Range(0.8f, 1.25f);
                    }
                    break;
            }
        }

        void Strike()
        {
            state = State.Recover;
            stateTime = 0f;
            float mult = heavy ? 1.6f : 1f;
            Color col = Elements.Color(attackElement);
            Vector3 fwd = transform.forward;

            switch (kind)
            {
                case EnemyKind.Slime:
                    external += fwd * 9f;
                    HitPlayer(transform.position + fwd * 1.2f, 1.6f, mult);
                    CombatFX.Burst(transform.position + fwd + Vector3.up * 0.5f, col, 1f, 15);
                    break;
                case EnemyKind.Golem:
                {
                    Vector3 c = transform.position + fwd * 2.2f;
                    HitPlayer(c, 2.6f, mult);
                    CombatFX.Ring(c, 3f, new Color(0.8f, 0.7f, 0.5f));
                    CombatFX.Burst(c + Vector3.up * 0.3f, new Color(0.7f, 0.6f, 0.45f), 2f, 30);
                    ThirdPersonCamera.ShakeAt(c, 0.25f, 0.2f);
                    AudioManager.Play("slam");
                    break;
                }
                case EnemyKind.Boss:
                {
                    bossPattern++;
                    if (bossPattern % 3 == 0)
                    {
                        // Wide shockwave: must be dodged
                        HitPlayer(transform.position, 9f, 1.3f);
                        CombatFX.Ring(transform.position, 9f, col, 0.6f);
                        CombatFX.Burst(transform.position + Vector3.up, col, 4f, 80);
                    }
                    else
                    {
                        Vector3 c = transform.position + fwd * 3.5f;
                        HitPlayer(c, 3.6f, mult * 1.2f);
                        CombatFX.Ring(c, 4f, col);
                        CombatFX.Burst(c + Vector3.up * 0.3f, col, 2.5f, 50);
                    }
                    ThirdPersonCamera.ShakeAt(transform.position, 0.45f, 0.3f);
                    AudioManager.Play("slam");
                    break;
                }
            }
        }

        void HitPlayer(Vector3 center, float radius, float mult)
        {
            if (!player) return;
            Vector3 d = player.position - center;
            d.y = 0f;
            if (d.magnitude <= radius && Mathf.Abs(player.position.y - center.y) < 3f)
                PartyManager.Instance.DamageActive(attackDamage * mult * Random.Range(0.9f, 1.1f), attackElement, transform.position);
        }

        void SummonAdds()
        {
            summoned = true;
            GameUI.Banner("Le Gardien appelle des renforts !", new Color(1f, 0.7f, 0.4f));
            var spawner = GetComponentInParent<EnemySpawner>();
            if (spawner) spawner.SpawnExtra(3);
        }

        public void OnParried()
        {
            state = State.Recover;
            stateTime = -1.5f;
            Health.Stagger(2.5f);
        }

        public void Pull(Vector3 center, float strength)
        {
            Vector3 d = center - transform.position;
            d.y = 0f;
            if (d.magnitude > 0.5f) external += d.normalized * strength * (kind == EnemyKind.Boss ? 0.1f : 1f);
        }

        public void Knock(Vector3 v)
        {
            float resist = kind == EnemyKind.Slime ? 1f : kind == EnemyKind.Golem ? 0.3f : 0.05f;
            external += v * resist;
        }

        void MoveTowards(Vector3 target, float speed, float dt)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.magnitude < 0.05f) return;
            Face(target, dt);
            transform.position += d.normalized * (speed * dt);
            animPhase += dt * speed * 2.2f;
        }

        void Face(Vector3 target, float dt)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-8f * dt));
        }

        void SnapToGround()
        {
            var t = Terrain.activeTerrain;
            if (!t) return;
            var p = transform.position;
            float ground = t.SampleHeight(p) + t.transform.position.y;
            p.y = Mathf.Lerp(p.y, ground, 0.5f);
            transform.position = p;
        }

        void Animate(float dt, bool stunned)
        {
            float t = Time.time;
            float windupK = state == State.Windup ? Mathf.Clamp01(stateTime / windup) : 0f;

            if (glow)
            {
                Color baseCol = Elements.Color(kind == EnemyKind.Slime ? Health.innateAura : attackElement);
                Color c = baseCol * (1.2f + windupK * 3f);
                if (heavy && ParryWindowOpen) c = new Color(4f, 3.2f, 0.6f);
                if (Health.Frozen) c = new Color(0.8f, 1.6f, 2f);
                glow.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", c);
                glow.SetPropertyBlock(mpb);
            }

            if (!body) return;
            if (kind == EnemyKind.Slime)
            {
                float hop = Mathf.Abs(Mathf.Sin(animPhase));
                float squash = Mathf.Cos(animPhase * 2f) * 0.12f - windupK * 0.3f;
                if (stunned) squash = Mathf.Sin(t * 20f) * 0.03f;
                body.localPosition = bodyBase + Vector3.up * (hop * 0.35f * (state == State.Chase || state == State.Idle ? 1f : 0.2f));
                body.localScale = Vector3.Scale(bodyScale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f));
            }
            else
            {
                float walk = Mathf.Sin(animPhase);
                body.localPosition = bodyBase + Vector3.up * (Mathf.Abs(walk) * 0.08f) + Vector3.down * windupK * 0.15f;
                body.localRotation = Quaternion.Euler(windupK * -15f + (state == State.Recover ? 20f * Mathf.Max(0f, 1f - stateTime * 2f) : 0f), 0f, walk * 3f);
                if (armL && armR)
                {
                    float raise = windupK * 150f;
                    float slam = state == State.Recover ? Mathf.Lerp(-40f, 0f, Mathf.Clamp01(stateTime * 2f)) : 0f;
                    armL.localRotation = armLBase * Quaternion.Euler(-walk * 25f - raise - slam, 0f, 0f);
                    armR.localRotation = armRBase * Quaternion.Euler(walk * 25f - raise - slam, 0f, 0f);
                }
                if (head) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.7f) * 10f, 0f);
            }
        }

        void OnDamaged(DamageInfo info)
        {
            showBarUntil = Time.time + 6f;
            if (state == State.Idle)
            {
                state = State.Chase;
                stateTime = 0f;
            }
        }

        void OnDied(Health h)
        {
            state = State.Dead;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (barCanvas) barCanvas.gameObject.SetActive(false);
            CombatFX.Burst(transform.position + Vector3.up, Elements.Color(attackElement), 2f, 50);
            AudioManager.Play("death");
            PartyManager.Instance?.GrantXP(xpReward);
            PartyManager.Instance?.AddEnergy(4f);
            Inventory.Add("mora", moraReward + Random.Range(0, moraReward / 2));
            if (!string.IsNullOrEmpty(dropItem) && Random.value < 0.6f) Inventory.Add(dropItem, 1);
            QuestManager.ReportKill(killTag);
            var spawner = GetComponentInParent<EnemySpawner>();
            if (spawner) spawner.OnEnemyDied(this);
            Destroy(gameObject, 3f);
        }

        // ---------------------------------------------------------------- World-space health bar

        void BuildHealthBar()
        {
            var go = new GameObject("Barre de vie", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Health.barOffset;
            barCanvas = go.GetComponent<Canvas>();
            barCanvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(160f, 34f);
            rt.localScale = Vector3.one * (kind == EnemyKind.Boss ? 0.02f : 0.01f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label = new GameObject("Nom", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(go.transform, false);
            label.font = font;
            label.fontSize = 14;
            label.alignment = TextAnchor.LowerCenter;
            label.color = Color.white;
            label.text = $"Niv. {Health.level}  {Health.displayName}";
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.5f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            var bg = new GameObject("Fond", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bg.transform.SetParent(go.transform, false);
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            var brt = bg.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.38f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;

            barFill = new GameObject("Vie", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            barFill.transform.SetParent(bg.transform, false);
            barFill.color = new Color(0.9f, 0.25f, 0.2f);
            var frt = barFill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            frt.pivot = new Vector2(0f, 0.5f);

            auraIcon = new GameObject("Aura", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            auraIcon.transform.SetParent(go.transform, false);
            var art = auraIcon.rectTransform;
            art.sizeDelta = new Vector2(14f, 14f);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 1f);
            art.anchoredPosition = new Vector2(0f, 10f);
            barCanvas.gameObject.SetActive(false);
        }

        void UpdateBar()
        {
            var cam = Camera.main;
            if (!cam || !barCanvas) return;
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            bool show = (state == State.Chase || state == State.Windup || state == State.Recover || Time.time < showBarUntil || kind == EnemyKind.Boss && state != State.Idle)
                        && dist < (kind == EnemyKind.Boss ? 60f : 35f);
            if (barCanvas.gameObject.activeSelf != show) barCanvas.gameObject.SetActive(show);
            if (!show) return;
            barCanvas.transform.rotation = cam.transform.rotation;
            barFill.rectTransform.localScale = new Vector3(Health.Normalized, 1f, 1f);
            bool aura = Health.Aura != Element.Physical;
            auraIcon.enabled = aura;
            if (aura) auraIcon.color = Elements.Color(Health.Aura);
        }
    }
}
