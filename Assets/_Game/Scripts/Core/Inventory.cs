using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    public class ItemDef
    {
        public string id, name, description;
        public Color color;
        public bool usable;
        public Action use;
    }

    /// <summary>Items, Mora and their definitions (names, descriptions, use effects).</summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }
        public static event Action<string, int> ItemAdded;
        public static event Action Changed;

        readonly Dictionary<string, int> counts = new();
        public static readonly Dictionary<string, ItemDef> Defs = new();

        void Awake()
        {
            Instance = this;
            Define("mora", "Mora", "La monnaie commune des terres d'Aerendal.", new Color(1f, 0.82f, 0.35f));
            Define("star_crystal", "Cristal d'étoile", "Un éclat de ciel solidifié. On dit qu'il exauce les vœux.", new Color(0.75f, 0.6f, 1f));
            Define("slime_gel", "Gelée de slime", "Visqueuse et légèrement tiède. Utile aux alchimistes.", new Color(0.6f, 0.9f, 0.8f));
            Define("golem_core", "Noyau de golem", "Une pierre qui pulse encore d'une faible énergie.", new Color(0.9f, 0.7f, 0.4f));
            Define("seal_fragment", "Fragment de sceau", "Un morceau du Sceau Céleste, gravé de runes anciennes.", new Color(0.5f, 1f, 0.85f));
            Define("ancient_key", "Clé ancienne", "Ouvre les portes de la Tour des Vents.", new Color(0.95f, 0.8f, 0.4f));
            Define("apple", "Pomme", "Croquante et sucrée. Restaure 20 % des PV du personnage actif.", new Color(0.95f, 0.3f, 0.25f), true,
                () => PartyManager.Instance?.HealAll(0.2f));
            Define("sweet_bread", "Pain aux baies", "Restaure 40 % des PV de toute l'équipe.", new Color(0.95f, 0.7f, 0.45f), true,
                () => PartyManager.Instance?.HealAll(0.4f));
            Define("xp_book", "Leçon d'aventurier", "Donne 1 000 points d'expérience à l'équipe.", new Color(0.5f, 0.75f, 1f), true,
                () => PartyManager.Instance?.GrantXP(1000));
            Define("wind_orb", "Orbe des Vents", "Fragment de l'âme du vent. Offre-les à l'Autel des Vents.", new Color(0.45f, 1f, 0.8f));
        }

        static void Define(string id, string name, string desc, Color color, bool usable = false, Action use = null) =>
            Defs[id] = new ItemDef { id = id, name = name, description = desc, color = color, usable = usable, use = use };

        public static int Count(string id) => Instance && Instance.counts.TryGetValue(id, out int c) ? c : 0;

        public static void Add(string id, int amount)
        {
            if (!Instance || amount == 0) return;
            Instance.counts[id] = Count(id) + amount;
            if (Instance.counts[id] <= 0) Instance.counts.Remove(id);
            if (amount > 0) ItemAdded?.Invoke(id, amount);
            Changed?.Invoke();
            QuestManager.ReportItem(id);
        }

        public static bool Remove(string id, int amount)
        {
            if (Count(id) < amount) return false;
            Add(id, -amount);
            return true;
        }

        public static bool Use(string id)
        {
            if (!Defs.TryGetValue(id, out var def) || !def.usable || Count(id) <= 0) return false;
            def.use?.Invoke();
            Remove(id, 1);
            AudioManager.Play("pickup");
            return true;
        }

        public static string Name(string id) => Defs.TryGetValue(id, out var d) ? d.name : id;

        public static IEnumerable<KeyValuePair<string, int>> All() => Instance ? Instance.counts : new Dictionary<string, int>();

        public static Dictionary<string, int> Snapshot() => Instance ? new Dictionary<string, int>(Instance.counts) : new();

        public static void Restore(Dictionary<string, int> data)
        {
            if (!Instance) return;
            Instance.counts.Clear();
            foreach (var kv in data) Instance.counts[kv.Key] = kv.Value;
            Changed?.Invoke();
        }
    }
}
