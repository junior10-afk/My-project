using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    public class DialogueLine
    {
        public string speaker;
        public string text;
        public List<(string label, Action action)> choices;

        public DialogueLine(string speaker, string text, List<(string, Action)> choices = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.choices = choices;
        }
    }

    /// <summary>All story dialogue, chosen according to quest progress (see STORY.md).</summary>
    public static class DialogueDB
    {
        const string Elara = "Elara";
        const string Bram = "Bram";
        const string Kaira = "Kaïra";
        const string Nerys = "Nerys";
        const string Torvan = "Torvan";
        const string Me = "Aelis";

        public static List<DialogueLine> For(string npc)
        {
            var qm = QuestManager.Instance;
            switch (npc)
            {
                case "elara":
                    if (qm.IsAtStep("main1", 0)) return new()
                    {
                        new(Elara, "Doucement… Tu es resté inconscient toute la nuit. Je t'ai trouvé au pied de l'Autel, au milieu d'un tourbillon de vent."),
                        new(Me, "Je… je ne me souviens de rien. Ni de mon nom, ni d'où je viens."),
                        new(Elara, "Tu as murmuré un nom en dormant : « Aelis ». Gardons-le, en attendant mieux."),
                        new(Elara, "Tu es aux Prairies de Vent-Clair, sur Aerendal. Je suis Elara, Gardienne de l'Autel des Vents."),
                        new(Me, "Aerendal…?"),
                        new(Elara, "Le dernier monde. Tous les autres se sont éteints, effacés par la Pâleur. Seul le Sceau Céleste nous protégeait encore."),
                        new(Elara, "Il s'est brisé il y a un mois. Depuis, les quatre vents se sont tus, et l'horizon commence à pâlir."),
                        new(Elara, "Mais toi… le vent tourne autour de toi comme s'il te reconnaissait. Pose ta main sur l'Autel. Je veux savoir ce qu'il a à te dire."),
                    };
                    if (qm.IsAtStep("main1", 1)) return new() { new(Elara, "L'Autel est juste derrière moi. N'aie pas peur : il ne mord pas. Du moins, plus depuis longtemps.") };
                    if (qm.IsAtStep("main1", 2)) return new()
                    {
                        new(Elara, "Des slimes se sont regroupés à l'est du camp. Sans le vent pour les disperser, ils prolifèrent."),
                        new(Elara, "Le vent t'a donné sa force : clic gauche pour frapper, E pour ta compétence, Q quand ta jauge élémentaire est pleine."),
                    };
                    if (qm.IsAtStep("main1", 3)) return new()
                    {
                        new(Elara, "Les Sentinelles sont ces obélisques aux orbes turquoise, disséminés dans la région. Elles dorment depuis la chute du Sceau."),
                        new(Elara, "Si elles réagissent à ta présence, tu pourras voyager de l'une à l'autre depuis la carte (M)."),
                    };
                    if (qm.IsAtStep("main1", 4)) return new()
                    {
                        new(Elara, "La vision t'a montré un éclat tombant vers les ruines anciennes, au nord. Un vieux soldat les garde : Torvan."),
                        new(Elara, "Il est bourru, mais honnête. S'il te menace… ne le prends pas personnellement."),
                    };
                    if (qm.IsAtStep("main1", 5)) return new()
                    {
                        new(Elara, "C'est… c'est bien un fragment du Sceau Céleste. Je n'en avais vu qu'en gravure, dans les livres de ma mère."),
                        new(Elara, "Il est froid. Comme si le vent qui l'habitait était endormi, enfermé quelque part."),
                        new(Elara, "Les runes parlent d'une clé, gardée au sommet de la Tour des Vents, sur le plus haut pic de l'île."),
                        new(Elara, "Au pied de la tour veille le Gardien Ancien. Esquive au tout dernier moment, ou pare ses coups quand il brille d'or."),
                        new(Me, "Tu as l'air d'en savoir beaucoup sur lui."),
                        new(Elara, "Quand j'étais petite, on le voyait marcher autour de la tour, au crépuscule. Il saluait les enfants. Il ne salue plus personne."),
                    };
                    if (qm.IsAtStep("main1", 6)) return new() { new(Elara, "La Tour des Vents se dresse sur le plus haut sommet. Grimpe, plane, et garde de l'endurance pour la fin.") };
                    if (qm.IsAtStep("main1", 7)) return new() { new(Elara, "Le Gardien Ancien t'attend dans l'arène de pierre. Délivre-le… il a assez veillé.") };
                    if (qm.IsAtStep("main1", 8)) return new()
                    {
                        new(Elara, "Tu sens ça ? La brise… Elle est revenue. Les herbes chantent à nouveau. Le Vent-Clair est libre !"),
                        new(Me, "Le Gardien m'a appelé « Aelith » avant de s'effondrer. Ce nom te dit quelque chose ?"),
                        new(Elara, "…Aelith était la Gardienne Première du Vent-Clair. Celle qui a donné son âme pour forger le Sceau, il y a cinq siècles."),
                        new(Elara, "Et le voyageur de ta vision… Je l'ai vu, le soir où le Sceau s'est brisé. Il avait ton visage, Aelis. Trait pour trait."),
                        new(Me, "Il reste trois fragments, n'est-ce pas ?"),
                        new(Elara, "Trois. Dispersés aux quatre coins d'Aerendal. Et quelque chose me dit qu'il les cherche, lui aussi."),
                        new(Elara, "Repose-toi cette nuit. Le chemin ne fait que commencer."),
                    };
                    return new()
                    {
                        new(Elara, "Que le vent guide tes pas, Aelis."),
                        new(Elara, "Si tu trouves des Orbes des Vents, rapporte-les : l'Autel renforcera ton souffle."),
                    };

                case "bram":
                    return new()
                    {
                        new(Bram, "Bienvenue au comptoir de Bram ! Les prix sont honnêtes, la Pâleur, elle, ne fait pas de rabais.", new()
                        {
                            ("Acheter une Pomme (60 Mora)", () => Buy("apple", 60)),
                            ("Acheter un Pain aux baies (180 Mora)", () => Buy("sweet_bread", 180)),
                            ("Acheter une Leçon d'aventurier (900 Mora)", () => Buy("xp_book", 900)),
                            ("Ton bras… que t'est-il arrivé ?", () => DialogueSystem.Show(new()
                            {
                                new(Bram, "Ça ? Un souvenir de la côte ouest. J'ai tendu la main vers la Pâleur pour sauver une cargaison."),
                                new(Bram, "Je ne sens plus rien de ce côté. Ni le chaud, ni le froid. Comme si mon bras avait oublié d'exister."),
                                new(Bram, "Alors un conseil d'ami, gamin : quand le ciel devient blanc… tu cours. Et tu ne te retournes pas."),
                            }, null)),
                            ("Au revoir", null),
                        }),
                    };

                case "kaira":
                    return new() { new(Kaira, "Allez ! Ces slimes n'ont qu'à bien se tenir. Mon épée chauffe déjà.") };
                case "nerys":
                    return new() { new(Nerys, "Les Sentinelles sont reliées par des courants d'eau souterrains… Si on cartographiait leur débit, on pourrait… pardon, je m'égare.") };
                case "torvan":
                    return new() { new(Torvan, "Hmpf. Trente ans que je garde ces ruines. Je peux bien t'accompagner un peu, jeune vent.") };
            }
            return new() { new("???", "…") };
        }

        // ================================================================ Story scenes

        /// <summary>Played after the altar vision (CIN_02).</summary>
        public static List<DialogueLine> AfterVision() => new()
        {
            new(Me, "J'ai vu… un homme, dans la tempête. Il a brisé quelque chose d'un seul coup d'épée."),
            new(Elara, "Tu l'as vu, toi aussi… Le soir où le Sceau s'est brisé, j'étais ici. Je n'ai rien pu faire."),
            new(Elara, "Il avait ton visage."),
            new(Me, "Mon visage ? Mais alors… qui suis-je ?"),
            new(Elara, "Je l'ignore. Mais l'Autel t'a répondu, et il ne répond à personne depuis cinq cents ans. Ça doit vouloir dire quelque chose."),
            new(Elara, "D'abord, aide-nous : des slimes menacent le camp, à l'est."),
        };

        public static List<DialogueLine> Recruit(string member) => member switch
        {
            "Kaïra" => new()
            {
                new(Kaira, "Pas mal, pour quelqu'un qui tombe du ciel ! Tu comptais me laisser tout le travail ?"),
                new(Me, "Tu étais là depuis le début ?"),
                new(Kaira, "Depuis le troisième slime. Je suis Kaïra, épéiste, des Hauts de Cendrelune. Enfin… de ce qu'il en reste."),
                new(Kaira, "La Pâleur a avalé mon village. Mon petit frère, Ilio, était dedans. Je cherche un moyen de le ramener."),
                new(Kaira, "Si toi, tu peux réveiller les vents, alors je reste avec toi. Allez, on y va !"),
            },
            "Nerys" => new()
            {
                new(Nerys, "Incroyable… Trois Sentinelles réveillées en une journée ! Elles résonnent avec toi, tu le sens ?"),
                new(Nerys, "Oh, pardon. Nerys, archiviste d'Ysmère. J'étudie les Sentinelles depuis… disons, depuis trop longtemps."),
                new(Nerys, "Les textes disent qu'elles ne s'éveillent qu'au contact d'un des Gardiens Premiers. Ce qui voudrait dire que tu…"),
                new(Nerys, "Non, c'est absurde. Enfin, sûrement. Il faut que je t'accompagne. Pour la science !"),
            },
            "Torvan" => new()
            {
                new(Torvan, "Pas un pas de plus. Tu as du culot de revenir ici, après ce que tu as fait au Sceau."),
                new(Me, "Je ne suis pas celui que vous croyez. Je me suis réveillé hier, sans souvenirs."),
                new(Torvan, "…Hmpf. Tes yeux. Les siens étaient fatigués, vieux comme les montagnes. Les tiens sont neufs."),
                new(Torvan, "Torvan, dernier soldat de l'Ordre de la Veille. Ce fragment est tombé dans les ruines la nuit du drame. Prends-le."),
                new(Torvan, "Et je viens avec toi. Pas pour te protéger : pour garder un œil sur toi."),
            },
            _ => null,
        };

        static void Buy(string item, int price)
        {
            if (Inventory.Remove("mora", price))
            {
                Inventory.Add(item, 1);
                GameUI.Toast($"Acheté : {Inventory.Name(item)}");
            }
            else GameUI.Toast("Pas assez de Mora.");
        }
    }
}
