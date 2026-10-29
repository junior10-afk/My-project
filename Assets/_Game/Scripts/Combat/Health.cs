using System;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Hit points, defence, elemental aura and reaction handling for enemies.
    /// (Party members keep their own HP in PartyManager.)
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 500f;
        public float defense = 0.35f;
        public float maxPoise = 40f;
        public int level = 5;
        public string displayName = "Ennemi";
        public Vector3 barOffset = new(0f, 2f, 0f);
        [Tooltip("Element this unit naturally carries (e.g. a Pyro slime is permanently Pyro-aura'd).")]
        public Element innateAura = Element.Physical;

        public float Current { get; private set; }
        public bool Dead { get; private set; }
        public Element Aura { get; private set; }
        public float FrozenUntil { get; private set; }
        public bool Frozen => Time.time < FrozenUntil;
        public float StaggerUntil { get; private set; }
        public bool Staggered => Time.time < StaggerUntil;
        public float Normalized => Current / maxHealth;

        public event Action<DamageInfo> Damaged;
        public event Action<Health> Died;

        float auraUntil;
        float poise;
        float shredUntil;
        float chargedUntil;
        float nextChargeTick;
        GameObject chargedSource;

        void Awake()
        {
            Current = maxHealth;
            poise = maxPoise;
            Aura = innateAura;
        }

        void Update()
        {
            if (Dead) return;
            if (Aura != innateAura && Time.time > auraUntil) Aura = innateAura;
            poise = Mathf.Min(maxPoise, poise + maxPoise * 0.1f * Time.deltaTime);

            if (Time.time < chargedUntil && Time.time >= nextChargeTick)
            {
                nextChargeTick = Time.time + 1f;
                Apply(new DamageInfo { amount = 40f, element = Element.Electro, point = transform.position + barOffset * 0.5f, source = chargedSource, fromReaction = true, reaction = Reaction.ElectroCharged });
            }
        }

        public void Heal(float amount) => Current = Mathf.Min(maxHealth, Current + amount);

        public void ApplyLevel(int newLevel, float healthMultiplier)
        {
            level = newLevel;
            maxHealth *= healthMultiplier;
            Current = maxHealth;
        }

        /// <summary>Applies damage, resolving elemental reactions. Returns the final damage dealt.</summary>
        public float TakeDamage(DamageInfo info)
        {
            if (Dead) return 0f;

            if (!info.fromReaction && info.element != Element.Physical)
            {
                var reaction = Elements.Resolve(Aura, info.element, out float mult, out bool consume);
                info.reaction = reaction;
                info.amount *= mult;
                Element previous = Aura;
                if (consume && Aura != innateAura) Aura = innateAura;
                else if (reaction == Reaction.None || !consume)
                {
                    if (info.element != Element.Anemo && info.element != Element.Geo && innateAura == Element.Physical)
                    {
                        Aura = info.element;
                        auraUntil = Time.time + 8f;
                    }
                }
                HandleReaction(reaction, previous, info);
            }
            return Apply(info);
        }

        float Apply(DamageInfo info)
        {
            float def = Time.time < shredUntil ? defense * 0.6f : defense;
            float dmg = Mathf.Max(1f, info.amount * (1f - def));
            Current -= dmg;
            poise -= info.poise;
            if (poise <= 0f)
            {
                poise = maxPoise;
                StaggerUntil = Time.time + 1.2f;
            }
            info.amount = dmg;
            Damaged?.Invoke(info);
            DamageNumbers.Show(info);
            if (Current <= 0f)
            {
                Current = 0f;
                Dead = true;
                Died?.Invoke(this);
            }
            return dmg;
        }

        public void Stagger(float duration) => StaggerUntil = Mathf.Max(StaggerUntil, Time.time + duration);

        void HandleReaction(Reaction reaction, Element previousAura, DamageInfo info)
        {
            switch (reaction)
            {
                case Reaction.Overloaded:
                    CombatFX.Burst(transform.position + Vector3.up, Elements.Color(Reaction.Overloaded), 3f, 30);
                    foreach (var h in Nearby(3.5f))
                        if (h != this) h.Apply(new DamageInfo { amount = 120f, element = Element.Pyro, point = h.transform.position + Vector3.up, source = info.source, fromReaction = true, reaction = Reaction.Overloaded, poise = 30f });
                    Stagger(0.8f);
                    break;
                case Reaction.ElectroCharged:
                    chargedUntil = Time.time + 4f;
                    nextChargeTick = Time.time + 0.5f;
                    chargedSource = info.source;
                    break;
                case Reaction.Frozen:
                    FrozenUntil = Time.time + 3.5f;
                    CombatFX.Burst(transform.position + Vector3.up, Elements.Color(Element.Cryo), 1.5f, 20);
                    break;
                case Reaction.Superconduct:
                    foreach (var h in Nearby(3f)) h.shredUntil = Time.time + 10f;
                    CombatFX.Burst(transform.position + Vector3.up, Elements.Color(Reaction.Superconduct), 3f, 25);
                    break;
                case Reaction.Swirl:
                    CombatFX.Burst(transform.position + Vector3.up, Elements.Color(previousAura), 4f, 35);
                    foreach (var h in Nearby(5f))
                        if (h != this)
                            h.TakeDamage(new DamageInfo { amount = 80f, element = previousAura, point = h.transform.position + Vector3.up, source = info.source, poise = 10f });
                    break;
                case Reaction.Crystallize:
                    PartyManager.Instance?.AddShield(150f, previousAura);
                    break;
            }
        }

        static readonly Collider[] buffer = new Collider[32];

        // Returns a fresh list (reactions can recurse into other units' Nearby calls).
        System.Collections.Generic.List<Health> Nearby(float radius)
        {
            var list = new System.Collections.Generic.List<Health>();
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, buffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i].GetComponentInParent<Health>();
                if (h && !h.Dead && !list.Contains(h)) list.Add(h);
            }
            return list;
        }
    }
}
