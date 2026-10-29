using UnityEngine;

namespace LastWorld
{
    public enum Element { Physical, Pyro, Hydro, Electro, Cryo, Anemo, Geo }

    public enum Reaction { None, Vaporize, Melt, Overloaded, ElectroCharged, Frozen, Superconduct, Swirl, Crystallize }

    public struct DamageInfo
    {
        public float amount;
        public Element element;
        public bool crit;
        public float poise;
        public Vector3 point;
        public Vector3 knockback;
        public GameObject source;
        public Reaction reaction;
        public bool fromReaction;
    }

    public static class Elements
    {
        public static Color Color(Element e) => e switch
        {
            Element.Pyro => new Color(1f, 0.45f, 0.2f),
            Element.Hydro => new Color(0.25f, 0.65f, 1f),
            Element.Electro => new Color(0.75f, 0.45f, 1f),
            Element.Cryo => new Color(0.6f, 0.95f, 1f),
            Element.Anemo => new Color(0.45f, 1f, 0.8f),
            Element.Geo => new Color(1f, 0.8f, 0.3f),
            _ => new Color(0.95f, 0.95f, 0.95f),
        };

        public static string Name(Element e) => e switch
        {
            Element.Pyro => "Pyro",
            Element.Hydro => "Hydro",
            Element.Electro => "Électro",
            Element.Cryo => "Cryo",
            Element.Anemo => "Anémo",
            Element.Geo => "Géo",
            _ => "Physique",
        };

        public static string Name(Reaction r) => r switch
        {
            Reaction.Vaporize => "Vaporisation",
            Reaction.Melt => "Fonte",
            Reaction.Overloaded => "Surcharge",
            Reaction.ElectroCharged => "Électrocution",
            Reaction.Frozen => "Gel",
            Reaction.Superconduct => "Supraconduction",
            Reaction.Swirl => "Dispersion",
            Reaction.Crystallize => "Cristallisation",
            _ => "",
        };

        public static Color Color(Reaction r) => r switch
        {
            Reaction.Vaporize => new Color(1f, 0.75f, 0.35f),
            Reaction.Melt => new Color(1f, 0.7f, 0.4f),
            Reaction.Overloaded => new Color(1f, 0.45f, 0.6f),
            Reaction.ElectroCharged => new Color(0.8f, 0.55f, 1f),
            Reaction.Frozen => new Color(0.7f, 0.95f, 1f),
            Reaction.Superconduct => new Color(0.7f, 0.7f, 1f),
            Reaction.Swirl => new Color(0.45f, 1f, 0.8f),
            Reaction.Crystallize => new Color(1f, 0.85f, 0.4f),
            _ => UnityEngine.Color.white,
        };

        /// <summary>
        /// Resolves the reaction between an existing aura and an incoming element.
        /// Returns the reaction, its damage multiplier and whether the aura is consumed.
        /// </summary>
        public static Reaction Resolve(Element aura, Element incoming, out float multiplier, out bool consumeAura)
        {
            multiplier = 1f;
            consumeAura = true;
            if (aura == Element.Physical || incoming == Element.Physical || aura == incoming)
            {
                consumeAura = false;
                return Reaction.None;
            }
            if (incoming == Element.Anemo) return aura == Element.Geo ? Reaction.None : Reaction.Swirl;
            if (incoming == Element.Geo) return Reaction.Crystallize;
            if (aura == Element.Anemo || aura == Element.Geo) { consumeAura = false; return Reaction.None; }

            switch (aura, incoming)
            {
                case (Element.Pyro, Element.Hydro): multiplier = 2f; return Reaction.Vaporize;
                case (Element.Hydro, Element.Pyro): multiplier = 1.5f; return Reaction.Vaporize;
                case (Element.Cryo, Element.Pyro): multiplier = 2f; return Reaction.Melt;
                case (Element.Pyro, Element.Cryo): multiplier = 1.5f; return Reaction.Melt;
                case (Element.Pyro, Element.Electro):
                case (Element.Electro, Element.Pyro): multiplier = 1.3f; return Reaction.Overloaded;
                case (Element.Hydro, Element.Electro):
                case (Element.Electro, Element.Hydro): consumeAura = false; return Reaction.ElectroCharged;
                case (Element.Hydro, Element.Cryo):
                case (Element.Cryo, Element.Hydro): return Reaction.Frozen;
                case (Element.Cryo, Element.Electro):
                case (Element.Electro, Element.Cryo): multiplier = 1.1f; return Reaction.Superconduct;
            }
            consumeAura = false;
            return Reaction.None;
        }
    }
}
