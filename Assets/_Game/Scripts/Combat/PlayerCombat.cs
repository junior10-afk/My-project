using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    /// <summary>
    /// Hybrid Genshin / Wuthering Waves combat for the active party member:
    /// 5-hit combo with input buffering and auto-targeting, charged spin (hold), elemental skill (E),
    /// burst (Q) when energy is full, dodge with i-frames (Shift / right click), perfect dodge
    /// (slow motion + counter bonus) and parry (attack during an enemy's yellow flash).
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        public static PlayerCombat Instance { get; private set; }
        public static bool Invulnerable => Instance && Time.time < Instance.iframeUntil;

        public float[] comboMultipliers = { 0.85f, 0.95f, 1.1f, 1.3f, 1.85f };
        public float stepDuration = 0.42f;
        public float comboWindow = 1.0f;
        public float meleeRadius = 1.7f;
        public float dodgeStamina = 18f;
        public float chargedStamina = 22f;
        [Tooltip("Weapon objects per party member (shown while fighting).")]
        public GameObject[] weapons = new GameObject[0];

        public int ComboStep { get; private set; } = -1;
        public float StepTime { get; private set; } = 99f;
        public float CastTime { get; private set; } = 99f;
        public bool LastCastWasBurst { get; private set; }
        public float HurtTime { get; private set; } = 99f;
        public float DodgeTime { get; private set; } = 99f;
        public float SpinTime { get; private set; } = 99f;
        public bool InCombat => Time.time - lastCombatTime < 5f;

        PlayerController pc;
        PartyManager party;
        float iframeUntil;
        float counterUntil;
        bool hitApplied;
        bool buffered;
        float attackHeldSince = -1f;
        bool spinDone;
        float lastCombatTime = -99f;
        static readonly Collider[] buffer = new Collider[48];

        void Awake()
        {
            Instance = this;
            pc = GetComponent<PlayerController>();
            party = GetComponent<PartyManager>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            StepTime += dt;
            CastTime += dt;
            HurtTime += dt;
            DodgeTime += dt;
            SpinTime += dt;

            if (ComboStep >= 0 && !hitApplied && StepTime >= stepDuration * 0.45f)
            {
                hitApplied = true;
                float mult = comboMultipliers[ComboStep];
                bool finisher = ComboStep == comboMultipliers.Length - 1;
                MeleeHit(mult, finisher ? 35f : 15f, finisher ? 200f : 150f, finisher ? 1.25f : 1f);
                CombatFX.Slash(transform.position + Vector3.up * 1.1f, transform.rotation, 1.6f,
                    Color.Lerp(Color.white, Elements.Color(party.Active.element), 0.35f), finisher ? 220f : 150f, ComboStep % 2 == 0 ? 20f : -25f);
            }
            if (ComboStep >= 0 && StepTime > comboWindow) ComboStep = -1;
            if (buffered && ComboStep >= 0 && StepTime >= stepDuration * 0.65f)
            {
                buffered = false;
                NextStep();
            }

            UpdateWeapons();
            if (GameState.InputBlocked || party.Active.Down) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var gp = Gamepad.current;
            bool attackDown = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (gp != null && gp.buttonWest.wasPressedThisFrame);
            bool attackHeld = (mouse != null && mouse.leftButton.isPressed) || (gp != null && gp.buttonWest.isPressed);
            bool skill = (kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.rightShoulder.wasPressedThisFrame);
            bool burst = (kb != null && kb.qKey.wasPressedThisFrame) || (gp != null && gp.leftShoulder.wasPressedThisFrame && gp.rightShoulder.isPressed);
            bool dodge = (kb != null && kb.leftShiftKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);

            if (attackDown)
            {
                attackHeldSince = Time.time;
                spinDone = false;
                if (!TryParry()) Attack();
            }
            if (!attackHeld) attackHeldSince = -1f;
            else if (attackHeldSince > 0f && !spinDone && Time.time - attackHeldSince > 0.45f)
            {
                spinDone = true;
                ChargedAttack();
            }
            if (skill) TrySkill();
            if (burst) TryBurst();
            if (dodge) TryDodge();
        }

        // ---------------------------------------------------------------- Normal / charged attacks

        bool CanAttack => pc.State == MoveState.Grounded && CastTime > 0.55f && HurtTime > 0.35f;

        public void Attack()
        {
            if (!CanAttack) return;
            if (ComboStep >= 0 && StepTime < stepDuration * 0.65f)
            {
                buffered = true;
                return;
            }
            NextStep();
        }

        void NextStep()
        {
            ComboStep = ComboStep >= 0 && StepTime <= comboWindow ? (ComboStep + 1) % comboMultipliers.Length : 0;
            StepTime = 0f;
            hitApplied = false;
            lastCombatTime = Time.time;
            LungeTowardTarget(ComboStep == comboMultipliers.Length - 1 ? 3.5f : 2.5f);
            pc.LockMovement(stepDuration * 0.85f);
            AudioManager.Play("swing", 0.9f + ComboStep * 0.05f);
        }

        public void ChargedAttack()
        {
            if (!CanAttack || !pc.SpendStamina(chargedStamina)) return;
            ComboStep = -1;
            SpinTime = 0f;
            lastCombatTime = Time.time;
            pc.LockMovement(0.55f);
            var center = transform.position + Vector3.up;
            foreach (var h in Targets(center, 3.2f)) Deal(h, 2.2f, Element.Physical, 40f, (h.transform.position - transform.position).normalized * 5f);
            CombatFX.Slash(center, transform.rotation, 2.6f, Color.white, 360f);
            CombatFX.Ring(transform.position, 3.2f, Color.white);
            AudioManager.Play("swing", 0.7f);
            ThirdPersonCamera.Shake(0.2f, 0.15f);
        }

        void MeleeHit(float multiplier, float poise, float arc, float radiusScale)
        {
            Vector3 center = transform.position + transform.forward * 1.1f + Vector3.up;
            bool any = false;
            foreach (var h in Targets(center, meleeRadius * radiusScale))
            {
                Vector3 to = h.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > 1f && Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;
                Deal(h, multiplier, Element.Physical, poise, to.normalized * 2.5f);
                any = true;
            }
            if (any)
            {
                party.AddEnergy(1.5f);
                GameState.HitStop(0.045f);
                ThirdPersonCamera.Shake(0.12f, 0.08f);
                AudioManager.Play("hit");
            }
        }

        // ---------------------------------------------------------------- Skill & burst

        public void TrySkill()
        {
            var m = party.Active;
            if (m.SkillCooldownLeft > 0f || HurtTime < 0.3f) return;
            m.skillReadyTime = Time.time + m.skillCooldown;
            CastTime = 0f;
            LastCastWasBurst = false;
            ComboStep = -1;
            lastCombatTime = Time.time;
            pc.LockMovement(0.45f);
            var target = FindTarget(12f);
            if (target) pc.FaceTowards(target.transform.position - transform.position);
            Color col = Elements.Color(m.element);
            Vector3 fwd = transform.forward;

            switch (m.skill)
            {
                case SkillKind.AnemoVortex:
                {
                    Vector3 c = transform.position + fwd * 3f;
                    foreach (var e in EnemyBase.All)
                        if (!e.Health.Dead && Vector3.Distance(e.transform.position, c) < 7f) e.Pull(c, 9f);
                    foreach (var h in Targets(c + Vector3.up, 4f)) Deal(h, 2.2f, Element.Anemo, 30f, Vector3.zero);
                    CombatFX.Burst(c + Vector3.up, col, 3f, 60);
                    CombatFX.Ring(c, 6f, col);
                    break;
                }
                case SkillKind.PyroSlash:
                {
                    Vector3 c = transform.position + Vector3.up;
                    foreach (var h in Targets(c + fwd * 2.5f, 4f))
                    {
                        Vector3 to = h.transform.position - transform.position;
                        to.y = 0f;
                        if (Vector3.Angle(fwd, to) < 75f || to.magnitude < 1.5f) Deal(h, 3f, Element.Pyro, 45f, to.normalized * 6f);
                    }
                    CombatFX.Slash(c, transform.rotation, 3.5f, col, 150f, 10f);
                    CombatFX.Burst(c + fwd * 2.5f, col, 2.5f, 70);
                    break;
                }
                case SkillKind.HydroWave:
                {
                    foreach (var h in Targets(transform.position + Vector3.up, 5f))
                        Deal(h, 1.8f, Element.Hydro, 20f, (h.transform.position - transform.position).normalized * 7f);
                    party.HealAll(0.08f);
                    CombatFX.Ring(transform.position, 5f, col, 0.5f);
                    CombatFX.Burst(transform.position + Vector3.up, col, 2.5f, 60);
                    break;
                }
                case SkillKind.ElectroStrike:
                {
                    int strikes = 0;
                    foreach (var e in EnemyBase.ByDistance(transform.position, 13f))
                    {
                        CombatFX.Lightning(e.transform.position + Vector3.up * 12f, e.transform.position + Vector3.up, col);
                        Deal(e.Health, 1.7f, Element.Electro, 25f, Vector3.zero);
                        if (++strikes >= 3) break;
                    }
                    if (strikes == 0) CombatFX.Lightning(transform.position + fwd * 4f + Vector3.up * 12f, transform.position + fwd * 4f, col);
                    break;
                }
            }
            AudioManager.Play("skill");
            ThirdPersonCamera.Shake(0.2f, 0.15f);
        }

        public void TryBurst()
        {
            var m = party.Active;
            if (!m.BurstReady || HurtTime < 0.3f) return;
            m.energy = 0f;
            CastTime = 0f;
            LastCastWasBurst = true;
            ComboStep = -1;
            lastCombatTime = Time.time;
            pc.LockMovement(0.9f);
            iframeUntil = Time.time + 1f;
            GameState.SlowMotion(0.4f, 0.5f);
            ThirdPersonCamera.Shake(0.45f, 0.35f);
            GameUI.Banner(m.burstName, Elements.Color(m.element));
            AudioManager.Play("burst");
            Color col = Elements.Color(m.element);

            var zoneGO = new GameObject($"Burst {m.burst}");
            var zone = zoneGO.AddComponent<BurstZone>();
            zone.owner = this;
            zone.element = m.element;
            switch (m.burst)
            {
                case BurstKind.Tornado:
                    zoneGO.transform.position = transform.position + transform.forward * 2f;
                    zone.Setup(4f, 0.4f, 3f, 1.1f, col);
                    zone.velocity = transform.forward * 2.5f;
                    zone.pull = 6f;
                    break;
                case BurstKind.Inferno:
                    zoneGO.transform.position = transform.position;
                    foreach (var h in Targets(transform.position + Vector3.up, 8f))
                        Deal(h, 6f, Element.Pyro, 80f, (h.transform.position - transform.position).normalized * 10f);
                    CombatFX.Burst(transform.position + Vector3.up, col, 7f, 150);
                    CombatFX.Ring(transform.position, 8f, col, 0.6f);
                    zone.Setup(5f, 1f, 5f, 0.7f, col);
                    break;
                case BurstKind.Rain:
                    zoneGO.transform.position = transform.position;
                    zone.Setup(10f, 1f, 9f, 0.6f, col);
                    zone.follow = transform;
                    zone.healPerTick = 0.03f;
                    break;
                case BurstKind.Thunderstorm:
                    zoneGO.transform.position = transform.position;
                    zone.Setup(8f, 0.55f, 12f, 1.4f, col);
                    zone.follow = transform;
                    zone.strikes = true;
                    break;
            }
            party.NotifyChanged();
        }

        // ---------------------------------------------------------------- Defence

        public void TryDodge()
        {
            if (DodgeTime < 0.35f || pc.State != MoveState.Grounded) return;
            if (!pc.SpendStamina(dodgeStamina)) return;
            Vector3 dir = pc.DesiredDirection.sqrMagnitude > 0.01f ? pc.DesiredDirection.normalized : -transform.forward;
            bool perfect = EnemyBase.AnyAttackLandingSoon(transform.position, 0.28f);
            pc.Dash(dir * 13f, 0.22f);
            if (dir == -transform.forward) pc.FaceTowards(-dir);
            iframeUntil = Time.time + 0.32f;
            DodgeTime = 0f;
            ComboStep = -1;
            AudioManager.Play("dodge");
            if (perfect)
            {
                counterUntil = Time.time + 2.5f;
                iframeUntil = Time.time + 0.8f;
                GameState.SlowMotion(0.3f, 1.2f);
                GameUI.Banner("Esquive parfaite !", new Color(0.7f, 0.9f, 1f));
                CombatFX.Ring(transform.position, 4f, new Color(0.7f, 0.9f, 1f));
                party.AddEnergy(5f);
            }
        }

        bool TryParry()
        {
            if (pc.State != MoveState.Grounded) return false;
            EnemyBase best = null;
            float bestDist = 4.8f;
            foreach (var e in EnemyBase.All)
            {
                if (!e.ParryWindowOpen) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            if (!best) return false;

            pc.FaceTowards(best.transform.position - transform.position);
            best.OnParried();
            ComboStep = -1;
            StepTime = 0f;
            lastCombatTime = Time.time;
            iframeUntil = Time.time + 0.6f;
            counterUntil = Time.time + 2f;
            Deal(best.Health, 2.5f, Element.Physical, 120f, (best.transform.position - transform.position).normalized * 4f);
            GameState.HitStop(0.12f);
            GameState.SlowMotion(0.35f, 0.6f);
            GameUI.Banner("Parade !", new Color(1f, 0.85f, 0.35f));
            CombatFX.Burst(best.transform.position + Vector3.up, new Color(1f, 0.85f, 0.35f), 2f, 50);
            AudioManager.Play("parry");
            party.AddEnergy(6f);
            return true;
        }

        public void OnHurt(Vector3 from)
        {
            HurtTime = 0f;
            ComboStep = -1;
            Vector3 away = transform.position - from;
            away.y = 0f;
            pc.Dash(away.normalized * 4f, 0.15f);
            AudioManager.Play("hurt");
        }

        // ---------------------------------------------------------------- Helpers

        public float Deal(Health h, float multiplier, Element element, float poise, Vector3 knockback)
        {
            if (!h || h.Dead) return 0f;
            var m = party.Active;
            bool crit = Random.value < m.critRate;
            float amount = m.ATK * multiplier * (crit ? 1f + m.critDamage : 1f);
            if (Time.time < counterUntil)
            {
                amount *= 1.8f;
                counterUntil = 0f;
            }
            var info = new DamageInfo
            {
                amount = amount,
                element = element,
                crit = crit,
                poise = poise,
                point = h.transform.position + h.barOffset * 0.6f,
                source = gameObject,
                knockback = knockback,
            };
            float dealt = h.TakeDamage(info);
            var enemy = h.GetComponent<EnemyBase>();
            if (enemy) enemy.Knock(knockback);
            CombatFX.Burst(info.point, Elements.Color(element), 0.8f, crit ? 22 : 12);
            lastCombatTime = Time.time;
            return dealt;
        }

        System.Collections.Generic.List<Health> Targets(Vector3 center, float radius)
        {
            var list = new System.Collections.Generic.List<Health>();
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i].GetComponentInParent<Health>();
                if (h && !h.Dead && !list.Contains(h) && !h.transform.IsChildOf(transform)) list.Add(h);
            }
            return list;
        }

        EnemyBase FindTarget(float range)
        {
            EnemyBase best = null;
            float bestScore = float.MaxValue;
            Vector3 camFwd = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized : transform.forward;
            foreach (var e in EnemyBase.All)
            {
                if (e.Health.Dead) continue;
                Vector3 to = e.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d > range) continue;
                float score = d - Vector3.Dot(camFwd, to.normalized) * 3f;
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        void LungeTowardTarget(float maxLunge)
        {
            var target = FindTarget(7f);
            if (target)
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                pc.FaceTowards(to);
                float dist = to.magnitude - 1.4f;
                if (dist > 0.2f) pc.Dash(to.normalized * Mathf.Min(dist, maxLunge) / 0.14f, 0.14f);
            }
            else
            {
                if (pc.DesiredDirection.sqrMagnitude > 0.01f) pc.FaceTowards(pc.DesiredDirection);
                pc.Dash(transform.forward * 4f, 0.08f);
            }
        }

        void UpdateWeapons()
        {
            bool show = InCombat || (ComboStep >= 0);
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i]) weapons[i].SetActive(show && i == party.ActiveIndex);
        }
    }

    /// <summary>Lingering burst area: periodic elemental damage, optional pull, heal, lightning strikes, movement.</summary>
    public class BurstZone : MonoBehaviour
    {
        public PlayerCombat owner;
        public Element element;
        public Transform follow;
        public Vector3 velocity;
        public float pull;
        public float healPerTick;
        public bool strikes;

        float duration, interval, radius, multiplier, t, nextTick;
        Color color;
        GameObject fx;

        public void Setup(float duration, float interval, float radius, float multiplier, Color color)
        {
            this.duration = duration;
            this.interval = interval;
            this.radius = radius;
            this.multiplier = multiplier;
            this.color = color;
            fx = CombatFX.Zone(transform.position, strikes ? radius * 0.4f : radius * 0.6f, color, 60f);
            fx.transform.SetParent(transform, true);
        }

        void Update()
        {
            t += Time.deltaTime;
            if (follow) transform.position = follow.position;
            transform.position += velocity * Time.deltaTime;
            if (Terrain.activeTerrain)
            {
                var p = transform.position;
                p.y = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y;
                transform.position = p;
            }

            if (t >= nextTick)
            {
                nextTick = t + interval;
                if (strikes)
                {
                    var targets = EnemyBase.ByDistance(transform.position, radius);
                    if (targets.Count > 0)
                    {
                        var e = targets[Random.Range(0, targets.Count)];
                        CombatFX.Lightning(e.transform.position + Vector3.up * 14f + Random.insideUnitSphere * 2f, e.transform.position + Vector3.up, color);
                        owner.Deal(e.Health, multiplier, element, 20f, Vector3.zero);
                    }
                }
                else
                {
                    foreach (var e in EnemyBase.ByDistance(transform.position, radius))
                    {
                        if (pull > 0f) e.Pull(transform.position, pull);
                        owner.Deal(e.Health, multiplier, element, 10f, Vector3.zero);
                    }
                }
                if (healPerTick > 0f) PartyManager.Instance.HealAll(healPerTick);
            }
            if (t >= duration) Destroy(gameObject);
        }
    }
}

