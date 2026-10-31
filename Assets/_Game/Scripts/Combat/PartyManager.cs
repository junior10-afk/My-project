using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public enum SkillKind { AnemoVortex, PyroSlash, HydroWave, ElectroStrike }
    public enum BurstKind { Tornado, Inferno, Rain, Thunderstorm }

    [Serializable]
    public class PartyMember
    {
        public string name = "Voyageur";
        public string title = "";
        public Element element = Element.Anemo;
        public SkillKind skill;
        public BurstKind burst;
        public string skillName = "Compétence";
        public string burstName = "Déchaînement";
        public GameObject model;
        public bool unlocked = true;
        public float baseHP = 1100f;
        public float baseATK = 110f;
        public float critRate = 0.2f;
        public float critDamage = 0.7f;
        public float skillCooldown = 6f;
        public float burstEnergy = 60f;
        public int level = 1;
        public int xp;

        [NonSerialized] public float hp = -1f;
        [NonSerialized] public float energy;
        [NonSerialized] public float skillReadyTime;

        /// <summary>Chapter VII: the companion's wind is awakened (+20 % HP, +25 % ATK).</summary>
        public bool Awakened => ChapterQuests.IsAwakened(name);
        public float MaxHP => baseHP * (1f + (level - 1) * 0.12f) * (Awakened ? 1.2f : 1f);
        public float ATK => baseATK * (1f + (level - 1) * 0.1f) * (Awakened ? 1.25f : 1f);
        public int XPToNext => 400 + level * 250;
        public bool Down => hp <= 0f;
        public float SkillCooldownLeft => Mathf.Max(0f, skillReadyTime - Time.time);
        public bool BurstReady => energy >= burstEnergy;
    }

    /// <summary>
    /// The four-character party (Genshin-style): switching with 1-4, per-member HP/energy/cooldowns,
    /// shields, levelling, and team wipe → respawn at the last activated waypoint.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        public static PartyManager Instance { get; private set; }

        public PartyMember[] members = Array.Empty<PartyMember>();
        public float switchCooldown = 0.8f;

        public int ActiveIndex { get; private set; }
        public PartyMember Active => members[ActiveIndex];
        public float Shield { get; private set; }
        public Element ShieldElement { get; private set; }
        public Vector3 RespawnPoint { get; set; }

        public event Action Changed;
        public event Action<int> Switched;
        public event Action<PartyMember> LeveledUp;

        float switchReady;
        float shieldUntil;
        ProceduralAnimator anim;
        PlayerController controller;

        void Awake()
        {
            Instance = this;
            anim = GetComponent<ProceduralAnimator>();
            controller = GetComponent<PlayerController>();
            foreach (var m in members)
                if (m.hp < 0f) m.hp = m.MaxHP;
            RespawnPoint = transform.position;
            ActiveIndex = Mathf.Max(0, Array.FindIndex(members, m => m.unlocked));
            ApplyModels();
        }

        void Update()
        {
            if (Shield > 0f && Time.time > shieldUntil) { Shield = 0f; Changed?.Invoke(); }
            if (GameState.InputBlocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) TrySwitch(0);
            if (kb.digit2Key.wasPressedThisFrame) TrySwitch(1);
            if (kb.digit3Key.wasPressedThisFrame) TrySwitch(2);
            if (kb.digit4Key.wasPressedThisFrame) TrySwitch(3);
        }

        public bool TrySwitch(int index)
        {
            if (index < 0 || index >= members.Length || index == ActiveIndex) return false;
            var m = members[index];
            if (!m.unlocked || m.Down || Time.time < switchReady) return false;
            ActiveIndex = index;
            switchReady = Time.time + switchCooldown;
            ApplyModels();
            CombatFX.Burst(transform.position + Vector3.up, Elements.Color(m.element), 1.5f, 30);
            AudioManager.Play("switch");
            Switched?.Invoke(index);
            Changed?.Invoke();
            return true;
        }

        void ApplyModels()
        {
            for (int i = 0; i < members.Length; i++)
                if (members[i].model) members[i].model.SetActive(i == ActiveIndex);
            if (!Active.model) return;
            var rig = Active.model.GetComponent<Animator>();
            var mocap = GetComponent<CharacterAnimator>();
            if (mocap) mocap.SetRig(rig);          // mocap clips, procedural only while climbing
            else if (anim) anim.SetRig(rig);
        }

        public void Unlock(string memberName)
        {
            foreach (var m in members)
                if (m.name == memberName && !m.unlocked)
                {
                    m.unlocked = true;
                    m.hp = m.MaxHP;
                    GameUI.Banner($"{m.name} rejoint l'équipe !", Elements.Color(m.element));
                    Changed?.Invoke();
                }
        }

        public void DamageActive(float amount, Element element, Vector3 from)
        {
            if (PlayerCombat.Invulnerable || Active.Down) return;
            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, amount * (element == ShieldElement ? 0.4f : 1f));
                Shield -= absorbed;
                amount -= absorbed / (element == ShieldElement ? 0.4f : 1f);
                amount = Mathf.Max(0f, amount);
            }
            Active.hp -= amount;
            DamageNumbers.ShowText(Mathf.RoundToInt(amount).ToString(), transform.position + Vector3.up * 1.8f, new Color(1f, 0.35f, 0.3f), 30);
            ThirdPersonCamera.Shake(0.25f, 0.2f);
            PlayerCombat.Instance?.OnHurt(from);
            if (Active.hp <= 0f)
            {
                Active.hp = 0f;
                int next = Array.FindIndex(members, m => m.unlocked && !m.Down);
                if (next >= 0)
                {
                    switchReady = 0f;
                    TrySwitch(next);
                }
                else StartCoroutine(TeamWipe());
            }
            Changed?.Invoke();
        }

        IEnumerator TeamWipe()
        {
            GameUI.Banner("Toute l'équipe est à terre…", new Color(1f, 0.4f, 0.35f));
            GameState.UIOpen = true;
            yield return new WaitForSecondsRealtime(2f);
            controller.Teleport(RespawnPoint);
            foreach (var m in members)
                if (m.unlocked) m.hp = m.MaxHP * 0.4f;
            GameState.UIOpen = false;
            ActiveIndex = Array.FindIndex(members, m => m.unlocked);
            ApplyModels();
            Changed?.Invoke();
        }

        public void HealAll(float fraction)
        {
            foreach (var m in members)
                if (m.unlocked && !m.Down) m.hp = Mathf.Min(m.MaxHP, m.hp + m.MaxHP * fraction);
            DamageNumbers.ShowText($"+{Mathf.RoundToInt(Active.MaxHP * fraction)}", transform.position + Vector3.up * 1.9f, new Color(0.5f, 1f, 0.45f), 28);
            Changed?.Invoke();
        }

        public void ReviveAndHealAll()
        {
            foreach (var m in members)
                if (m.unlocked) m.hp = m.MaxHP;
            Changed?.Invoke();
        }

        public void AddEnergy(float amount)
        {
            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                if (!m.unlocked) continue;
                m.energy = Mathf.Min(m.burstEnergy, m.energy + (i == ActiveIndex ? amount : amount * 0.6f));
            }
            Changed?.Invoke();
        }

        public void AddShield(float amount, Element element)
        {
            Shield = Mathf.Max(Shield, amount);
            ShieldElement = element;
            shieldUntil = Time.time + 12f;
            Changed?.Invoke();
        }

        public void GrantXP(int amount)
        {
            foreach (var m in members)
            {
                if (!m.unlocked) continue;
                m.xp += amount;
                while (m.xp >= m.XPToNext && m.level < 90)
                {
                    m.xp -= m.XPToNext;
                    float ratio = m.hp / m.MaxHP;
                    m.level++;
                    m.hp = m.MaxHP * Mathf.Max(ratio, 0.5f);
                    LeveledUp?.Invoke(m);
                    GameUI.Toast($"{m.name} passe au niveau {m.level} !");
                }
            }
            Changed?.Invoke();
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
