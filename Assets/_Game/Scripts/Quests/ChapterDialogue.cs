using System.Collections.Generic;

namespace LastWorld
{
    /// <summary>
    /// All the dialogue of Chapters II to X (see STORY.md §6): briefings by Elara, scenes of the regions,
    /// memory stones, vigils and the three endings. Chapter I lives in <see cref="DialogueDB"/>.
    /// </summary>
    public static class ChapterDialogue
    {
        const string Elara = "Elara", Bram = "Bram", Kaira = "Kaïra", Nerys = "Nerys", Torvan = "Torvan", Me = "Aelis";
        const string Kael = "Kael", Maren = "Maren", Sael = "Sael", Orrin = "Orrin", Ilio = "Ilio", Narr = "";
        const string Aelith = "Aelith", Cendrel = "Cendrel", Ysme = "Ysmé", Valdrak = "Valdrak", Voice = "Voix lointaine";

        static DialogueLine L(string who, string text) => new(who, text);

        // ================================================================ Idle talk (after the current story step is done)

        public static List<DialogueLine> Idle(string npc)
        {
            int done = QuestManager.Instance ? QuestManager.Instance.ChaptersDone : 0;
            switch (npc)
            {
                case "elara":
                    if (done >= 10) return new() { L(Elara, "Le vent chante sur toute l'île, Aelis. Même moi, je n'ai plus rien à prier. Alors je me contente d'écouter.") };
                    if (done >= 9) return new() { L(Elara, "Les vents t'attendent à la Tour du Sceau. Quoi que tu choisisses… je serai fière de toi.") };
                    if (done >= 7) return new() { L(Elara, "Vous avez tous changé. Je le vois dans vos pas. Allez, la Tour du Sceau vous attend au centre de l'île.") };
                    if (done >= 5) return new() { L(Elara, "Ilio dort enfin dans une vraie couverture. Merci d'avoir ramené cet enfant à Kaïra.") };
                    if (done >= 3) return new() { L(Elara, "Trois vents libres. Je sens l'île respirer… mais la Pâleur gagne chaque jour du terrain.") };
                    if (done >= 2) return new() { L(Elara, "Cendrelune respire de nouveau, d'après les voyageurs. Il en reste d'autres à délivrer.") };
                    return null;
                case "bram":
                    return null;
                case "ilio_camp":
                    return new()
                    {
                        L(Ilio, "Grande sœur dit que tu fais tomber les gardiens avec le vent. C'est vrai ?"),
                        L(Ilio, "Moi, quand je serai grand, je ferai marchand. Comme Bram. Lui, il dit que ça rapporte plus que les épées."),
                    };
                case "maren":
                    return new()
                    {
                        L(Maren, done >= 2 ? "Les cheminées fument de nouveau. C'est drôle : j'avais oublié l'odeur du pain qui brûle un peu." : "Les braises ne mentent jamais. Va, petit. Je les surveille."),
                    };
                case "sael":
                    return new()
                    {
                        L(Sael, done >= 3 ? "La lagune est redevenue claire. Les poissons reviennent. Comme si le monde pardonnait." : "Les eaux gardent tout. Même ce qu'on voudrait oublier."),
                    };
                case "orrin":
                    return new()
                    {
                        L(Orrin, done >= 4 ? "Le tonnerre chante dans les murs. Trente ans que je l'attendais. Va, Torvan. La Veille est finie." : "On ne passe pas dans l'ombre. Allume les flambeaux."),
                    };
                case "ilio":
                    return new()
                    {
                        L(Ilio, "Je tiens la lanterne. Le monsieur triste m'a dit de ne pas la lâcher. Alors je ne la lâche pas."),
                    };
            }
            return null;
        }

        // ================================================================ Scenes

        public static List<DialogueLine> Scene(string key) => key switch
        {
            // ---------------------------------------------------------------- Chapitre II — Les Cendres de Cendrelune
            "c2_brief" => new()
            {
                L(Elara, "Le second fragment est tombé au nord-ouest, aux Hauts de Cendrelune. Autrefois, le vent de braise y soufflait plus fort que nulle part ailleurs."),
                L(Kaira, "Cendrelune… C'est chez moi. Ce qu'il en reste."),
                L(Elara, "Je sais, Kaïra. Personne ne t'oblige à y retourner."),
                L(Kaira, "Allez ! Si je n'y retourne pas aujourd'hui, je n'y retournerai jamais."),
                L(Me, "Kael y sera aussi, n'est-ce pas ?"),
                L(Elara, "Il cherche les fragments, lui aussi. Ne le laisse pas les atteindre le premier."),
                L(Elara, "Et prenez garde : à Cendrelune, les cendres ne sont pas qu'un souvenir. Elles brûlent encore."),
            },
            "c2_village" => new()
            {
                L(Kaira, "Le puits… la boulangerie… Tout est gris. Même le feu a oublié comment brûler."),
                L(Me, "Kaïra…"),
                L(Kaira, "Ça va. Ça va ! Cherchons quelqu'un qui sache ce qui s'est passé."),
                L(Narr, "Au bout de la rue, une silhouette courbée tisonne les braises d'un foyer éteint."),
            },
            "c2_maren" => new()
            {
                L(Maren, "Un revenant, ou un fou ? Les deux sont les bienvenus. Voilà longtemps que personne ne monte ici."),
                L(Kaira, "Maren ? C'est moi. Kaïra. La fille du forgeron."),
                L(Maren, "Petite braise… Tu as grandi. Tu as les yeux de ton père, et son mauvais caractère."),
                L(Maren, "La Pâleur est venue un soir de printemps. Elle a pris les toits, les voix, les noms. Ilio courait vers la Forge quand elle l'a avalé."),
                L(Kaira, "Il courait… vers la Forge ?"),
                L(Maren, "Le feu de la Forge n'a jamais cessé de brûler. Il protégeait la colline. Jusqu'à ce matin, où un homme au visage fatigué y est monté."),
                L(Me, "Kael."),
                L(Maren, "Les braises ont crié quand il est passé. Allez-y, mais les Brûlés rôdent entre ici et là-haut. Nettoyez le chemin."),
                L(Kaira, "Allez ! Chemin dégagé en dix minutes."),
            },
            "c2_camp_done" => new()
            {
                L(Kaira, "Voilà qui est fait. Mon épée n'a pas chauffé aussi bien depuis des années."),
                L(Me, "La Forge est juste au-dessus de nous."),
                L(Kaira, "…Je sais. Mon père y travaillait. Je n'y suis jamais retournée."),
            },
            "c2_boss_done" => new()
            {
                L(Kaira, "Il ne m'a pas reconnue non plus."),
                L(Me, "C'était le gardien de la Forge. Il ne protégeait plus que le fragment."),
                L(Kaira, "Le coffre est là, sur l'enclume de mon père. Ouvre-le, Aelis. S'il te plaît. Je n'y arrive pas."),
            },
            "c2_end" => new()
            {
                L(Narr, "Le Vent-de-Braise se libère. Sur la colline, les cheminées de Cendrelune se remettent à fumer."),
                L(Kaira, "Ilio… Tu es là, quelque part. Je t'ai entendu."),
                L(Me, "Alors nous le retrouverons."),
            },

            // ---------------------------------------------------------------- Chapitre III — La Mémoire des Marées
            "c3_brief" => new()
            {
                L(Elara, "Les braises ont parlé à Kaïra. Le vent d'eau dort sous la lagune d'Ysmère, à l'ouest."),
                L(Nerys, "Ysmère ! Ma ville… mes archives… Si elles ont survécu, nous aurons enfin la vérité sur les Gardiens."),
                L(Elara, "Quelle vérité ?"),
                L(Nerys, "Oh — des broutilles. Des hypothèses. Rien de… enfin si, c'est capital, mais ce serait trop long, et — il faut que j'y aille !"),
                L(Kaira, "Elle cache quelque chose."),
                L(Nerys, "Moi ? Jamais !"),
                L(Torvan, "Hmpf. Tout le monde cache quelque chose."),
            },
            "c3_lagoon" => new()
            {
                L(Nerys, "La lagune a monté de trois mètres… Les quais, la bibliothèque, tout est sous l'eau. Mais les Archives sont bâties sur un roc : elles tiennent."),
                L(Me, "Quelqu'un vit encore ici ?"),
                L(Nerys, "Sael. Ma maîtresse. Si quelqu'un a survécu, c'est elle. Elle est trop têtue pour se noyer."),
            },
            "c3_sael1" => new()
            {
                L(Sael, "Une voix du dehors. J'avais fini par croire que le monde s'était arrêté à ma porte."),
                L(Nerys, "Maîtresse !"),
                L(Sael, "Nerys, ma petite érudite. Toujours à parler avant de respirer."),
                L(Sael, "Les Archives dorment sous la marée. Seules trois Pierres de Mémoire, autour de la lagune, peuvent les rouvrir. Elles ne s'éveillent qu'au contact du vent."),
                L(Me, "Du vent ?"),
                L(Sael, "Du tien. J'ai senti l'air changer quand tu es entré."),
                L(Sael, "Mais les eaux ont des gardiens, désormais : des créatures qui montent des profondeurs. Écarte-les, puis touche les Pierres."),
            },
            "c3_camp_done" => new()
            {
                L(Nerys, "Les Pierres de Mémoire ! Elles brillent là-bas, sur les rochers. Trois en tout."),
                L(Torvan, "Hmpf. Ne les touche qu'une à la fois. Les vieilles choses n'aiment pas la hâte."),
            },
            "mem1" => new()
            {
                L(Narr, "La pierre murmure. Quatre silhouettes se tiennent la main devant une lumière bleue : Aelith, Cendrel, Ysmé, Valdrak."),
                L(Aelith, "Nous le ferons ensemble. Un monde pour mille ne vaut rien. Un monde pour tous vaut bien une vie."),
                L(Ysme, "Une vie chacun, ce n'est pas pareil."),
                L(Aelith, "Non. Mais c'est plus juste."),
            },
            "mem2" => new()
            {
                L(Narr, "Un jeune homme aux yeux déjà fatigués se tient derrière les Gardiens."),
                L(Kael, "Il doit y avoir un autre moyen, Aelith."),
                L(Aelith, "Il n'y en a pas, Kael."),
                L(Kael, "Alors je le trouverai. Même si cela me prend cent ans."),
                L(Me, "Il l'a appelée Aelith… et elle, elle l'a appelé Kael. Comme s'ils étaient…"),
            },
            "mem3" => new()
            {
                L(Narr, "Aelith sourit, la lumière déjà sur ses mains."),
                L(Aelith, "Cinq cents ans, mon frère… Sois patient. Je serai là."),
                L(Narr, "La lumière l'engloutit. Un cri déchire la mémoire : celui de Kael."),
                L(Nerys, "Son frère. Kael est son frère. Je le soupçonnais depuis le début !"),
                L(Kaira, "Tu aurais pu le dire plus tôt."),
                L(Nerys, "J'allais le dire ! Dans le bon ordre !"),
            },
            "c3_sael2" => new()
            {
                L(Sael, "Tu as vu. Alors tu dois savoir le reste. Le Livre des Marées, sur ce lutrin, garde ce que les Pierres n'osent pas dire."),
                L(Sael, "Pose ta main dessus, enfant. Et ne détourne pas les yeux."),
            },
            "c3_truth" => new()
            {
                L(Me, "Je me souviens. De sa voix. De son visage. Kael… c'est mon frère."),
                L(Nerys, "Aelith. Tu es Aelith. Je le savais ! Enfin, je le soupçonnais. Non, j'aurais dû le dire plus tôt."),
                L(Kaira, "Tu t'appelles Aelis, point. Aelith, c'est une histoire."),
                L(Torvan, "Hmpf. Une histoire de cinq siècles."),
                L(Me, "Mon âme était dans le Sceau. Il l'a brisé pour me libérer."),
                L(Sael, "Et le prix, c'est Aerendal. La Pâleur n'est pas un monstre : c'est l'oubli de l'Arbre des Cieux. Le Sceau ne la combat pas, il la retarde."),
                L(Me, "Alors le restaurer ne la fera pas disparaître."),
                L(Sael, "Il achète du temps, au prix d'une âme. À toi de juger si le prix est juste."),
                L(Sael, "Le dernier veilleur du bassin garde le fragment d'eau. Il ne te laissera pas passer, enfant. Ne lui en veux pas."),
            },
            "c3_boss_done" => new()
            {
                L(Nerys, "Il a pleuré en tombant. Vous avez vu ? De l'eau douce, pas de l'eau de mer."),
                L(Me, "Il attendait quelqu'un."),
                L(Nerys, "Le coffre est au fond du bassin. Vas-y. Moi, je regarde l'eau. C'est… plus fort que moi."),
            },
            "c3_end" => new()
            {
                L(Narr, "Le Vent-des-Marées se libère. La pluie tombe sur la lagune, douce et tiède, et lave la pierre."),
                L(Nerys, "On dirait qu'il pleure de soulagement."),
                L(Sael, "Les Archives sont à vous, petits. Tout ce que nous savons, vous le saurez."),
            },

            // ---------------------------------------------------------------- Chapitre IV — Le Serment d'Orage
            "c4_brief" => new()
            {
                L(Elara, "Il reste le Vent d'Orage, sur les pics de Valdrak, à l'est. L'ancienne forteresse de l'Ordre de la Veille y domine la vallée."),
                L(Torvan, "Hmpf. Je n'y suis pas retourné depuis trente ans."),
                L(Elara, "Torvan… Tu devrais me dire ce que tu sais."),
                L(Torvan, "Plus tard. Certaines choses se disent mieux devant une porte qu'on s'apprête à ouvrir."),
                L(Me, "Il a peur."),
                L(Kaira, "Il a peur ET il y va. C'est ça, être courageux."),
            },
            "c4_valdrak" => new()
            {
                L(Torvan, "Le col de Valdrak. Mon serment est né ici, il y a un peu plus d'un demi-siècle."),
                L(Nerys, "La citadelle ! Intacte, ou presque. Trois flambeaux, d'après mes notes : celui de la Mémoire, celui du Courage, celui du Silence."),
                L(Torvan, "Le vieil Orrin doit encore veiller dans les murs. Si quelqu'un tient encore debout ici, c'est lui."),
            },
            "c4_orrin1" => new()
            {
                L(Orrin, "Ce pas… Torvan ? Ton pas n'a pas changé depuis trente ans."),
                L(Torvan, "Orrin. Tu es toujours vivant, vieux fou."),
                L(Orrin, "Trop têtu pour mourir. Les trois flambeaux de la Veille doivent être allumés avant que la porte s'ouvre. On ne passe pas dans l'ombre."),
                L(Orrin, "Mais écoute, petit : nous gardions le fragment d'Orage, oui. Pas pour le protéger. Pour empêcher qu'on le brise."),
                L(Torvan, "Orrin…"),
                L(Orrin, "Le serment de l'Ordre était simple : que jamais les dieux ne soient libérés sans que le monde soit prêt. Tu as toujours su pourquoi, Torvan."),
                L(Me, "Torvan ?"),
                L(Torvan, "…Je savais. Tout le temps. Que les Gardiens étaient prisonniers du Sceau. J'ai gardé ce secret pour que les gens dorment la nuit."),
                L(Me, "Tu pensais bien faire."),
                L(Torvan, "Je ne pensais pas. J'obéissais. Hmpf. Allumons ces flambeaux."),
            },
            "vigil0" => new()
            {
                L(Narr, "Le premier flambeau s'embrase d'une flamme blanc-bleu. Une voix ancienne murmure : « Je me souviens. »"),
            },
            "vigil1" => new()
            {
                L(Narr, "Le deuxième flambeau s'allume. La voix répond, plus ferme : « Je ne fuis pas. »"),
            },
            "vigil2" => new()
            {
                L(Narr, "Le troisième flambeau s'allume. Dans la pierre, le tonnerre gronde : « Je veille. »"),
                L(Orrin, "La porte s'ouvre. Va, Torvan. Dis-lui, à ce garçon, que la Veille se termine ici."),
            },
            "c4_camp_done" => new()
            {
                L(Torvan, "Hmpf. Les gardiens de pierre tiennent mieux la foudre que la plupart des hommes."),
                L(Me, "La porte de la citadelle est ouverte."),
            },
            "c4_after_serment" => new()
            {
                L(Torvan, "Il aurait pu me tuer. Il ne l'a pas fait."),
                L(Kaira, "Il est fatigué, ton Kael. Fatigué ET dangereux."),
                L(Nerys, "Il a dit « Cœur du Sceau »… Au centre de l'île, il y a une tour. La Tour du Sceau. Il faut que j'ouvre mes notes."),
                L(Me, "D'abord, ce colosse. Il se réveille."),
            },
            "c4_boss_done" => new()
            {
                L(Torvan, "Le fragment d'Orage. Prends-le, jeune vent. C'est à toi de décider ce qu'il deviendra."),
                L(Orrin, "Le coffre de la Veille est derrière l'autel. Il t'a attendu cinq siècles."),
            },
            "c4_end" => new()
            {
                L(Narr, "Les quatre fragments chantent à l'unisson dans la main d'Aelis."),
                L(Me, "Ils pointent tous vers le centre de l'île."),
                L(Nerys, "La Tour du Sceau. Les Gardiens y ont forgé le Sceau. C'est là que tout se décidera."),
                L(Torvan, "Hmpf. Pas avant d'avoir dormi une nuit entière."),
            },

            // ---------------------------------------------------------------- Chapitre V — Les Terres Pâles
            "c5_brief" => new()
            {
                L(Elara, "Regarde au nord. L'horizon a avalé la mer. La Pâleur ne se contente plus des bords : elle avance."),
                L(Bram, "Et mes tonneaux de vin qui sont de l'autre côté. Dix pièces le tonneau, je dis ça comme ça."),
                L(Elara, "Les villageois du rivage nord ont fui. Certains disent avoir entendu des voix d'enfants dans la brume."),
                L(Kaira, "Des voix d'enfants ?"),
                L(Elara, "…Peut-être n'est-ce rien."),
                L(Kaira, "Allez ! On y va. Maintenant."),
            },
            "c5_pale" => new()
            {
                L(Narr, "Ici, la couleur s'efface. Les herbes sont blanches et les ombres n'ont plus de contours."),
                L(Nerys, "Fascinant. La lumière n'est pas absorbée… elle est oubliée."),
                L(Torvan, "Hmpf. Ne respire pas trop fort."),
                L(Kaira, "J'entends quelque chose. Là-bas. Une lueur."),
            },
            "c5_camp_done" => new()
            {
                L(Me, "Ces créatures n'ont ni visage ni nom. Ce sont des échos de ce que la Pâleur a mangé."),
                L(Kaira, "La lueur est derrière la colline. Venez !"),
            },
            "c5_ilio" => new()
            {
                L(Ilio, "Qui est là ? Si c'est encore un rêve, vous êtes très réalistes."),
                L(Kaira, "Ilio. C'est moi. C'est vraiment moi."),
                L(Ilio, "Grande sœur ? Tu as les cheveux plus courts."),
                L(Kaira, "Et toi, tu as grandi d'une tête. Ils ne te nourrissaient donc pas, là-bas ?"),
                L(Ilio, "Je suis resté près de la lanterne. Elle empêche la Pâleur d'entrer. Un monsieur triste l'a laissée, il m'a dit de ne pas la lâcher."),
                L(Me, "Un monsieur aux cheveux argentés ?"),
                L(Ilio, "Oui. Il vient parfois. Il laisse du pain, et il ne dit rien. Il a toujours l'air de chercher quelqu'un."),
                L(Torvan, "Hmpf. Kael nourrit un enfant perdu. Je n'aurais pas parié là-dessus."),
            },
            "kael_journal" => new()
            {
                L(Narr, "Sur un rocher, un carnet aux pages jaunies. L'écriture est fine, pressée."),
                L(Kael, "An 212. J'ai cessé de compter les jours. Les mondes s'éteignent. Elle est toujours dans le Sceau."),
                L(Kael, "An 340. J'ai trouvé un fragment de sa voix dans une pierre de marée. Elle disait mon nom."),
                L(Kael, "An 497. Trois ans. Je briserai le Sceau et ce monde ne me le pardonnera pas. Moi non plus."),
                L(Narr, "La dernière page : « Si quelqu'un lit ceci, qu'il sache que je ne suis pas un monstre. Je suis un frère qui a trop attendu. »"),
                L(Me, "Il n'est pas un monstre. Il est exactement ce que je serais devenu à sa place."),
            },
            "c5_boss_done" => new()
            {
                L(Ilio, "Il s'est éteint. Comme les autres mondes. Doucement."),
                L(Kaira, "On rentre, Ilio. Tu viens avec nous. Tu es en sécurité."),
                L(Ilio, "Et la lanterne ?"),
                L(Kaira, "On l'emporte. Allez, tiens-la bien."),
            },

            // ---------------------------------------------------------------- Chapitre VI — La Mémoire d'Aelith
            "c6_brief" => new()
            {
                L(Elara, "Les quatre fragments chantent ensemble. Ils veulent être posés sur l'Autel. Fais-le, Aelis."),
                L(Me, "Que va-t-il se passer ?"),
                L(Elara, "Je l'ignore. Mon rôle était de prier, pas de comprendre. Mais l'Autel gardait une mémoire que le Sceau avait scellée."),
                L(Kaira, "Si ça tourne mal, on te ramène par les cheveux."),
            },
            "spirit_cendrel" => new()
            {
                L(Cendrel, "Tu es revenue, petite du vent. Ma chaleur t'attendait."),
                L(Cendrel, "Dans le Sceau, nous avons chauffé les nuits les plus longues. Nous avons veillé sur vous cinq siècles. Ce n'était pas une prison, non… mais c'était une longue nuit."),
            },
            "spirit_ysme" => new()
            {
                L(Ysme, "Nous nous sommes trompés, Aelith. Nous avons sauvé un monde en volant la liberté de ses dieux."),
                L(Ysme, "Ne refais pas notre erreur par fidélité à nous. Tu as le droit de vouloir vivre."),
            },
            "spirit_valdrak" => new()
            {
                L(Valdrak, "Il existe une Troisième Voie. Quatre vents, quatre cœurs vivants : celui qui accepte de porter, porte. Pas de prison, pas d'éternité."),
                L(Valdrak, "Tes compagnons doivent éveiller les vents qu'ils portent. Qu'ils affrontent leurs veilles. Et toi, affronte la tienne."),
                L(Narr, "Une ombre se détache du sol, avec ton visage."),
            },
            "c6_shade" => new()
            {
                L(Narr, "L'Ombre d'Aelith te fait face, les yeux vides."),
                L(Aelith, "Pourquoi résister ? Tout s'efface. Les mondes, les noms, les souvenirs. Tu seras oubliée aussi."),
                L(Me, "Je sais. Mais je suis là maintenant. Et je ne veux pas disparaître en silence."),
            },
            "c6_shade_done" => new()
            {
                L(Aelith, "…Alors vis. Pour nous deux."),
                L(Narr, "L'ombre se dissout. La plateforme tremble : le chemin du retour s'ouvre."),
            },
            "c6_core" => new()
            {
                L(Narr, "Les quatre fragments flottent au centre de la plateforme, tournant autour d'un point lumineux."),
                L(Me, "Ce n'est pas un sacrifice. C'est une promesse."),
            },
            "c6_end" => new()
            {
                L(Elara, "Tu as pleuré dans ton sommeil."),
                L(Me, "J'ai vu ce que le Sceau cachait, Elara. Et j'ai vu une autre voie."),
                L(Elara, "Alors il faut l'ouvrir. Vite."),
            },

            // ---------------------------------------------------------------- Chapitre VII — Les Quatre Veilles
            "c7_brief" => new()
            {
                L(Elara, "Les Gardiens ont parlé de trois épreuves : la veille de braise, la veille de marée, la veille d'orage. Chacun de vous doit éveiller le vent qu'il porte."),
                L(Kaira, "Moi d'abord. Mon feu a besoin de se calmer."),
                L(Nerys, "Moi… j'ai besoin de me taire. C'est bien plus dur."),
                L(Torvan, "Hmpf. Moi, de parler."),
                L(Me, "Je vous accompagne. Un par un."),
            },
            "c7_kaira" => new()
            {
                L(Narr, "Au cœur du cercle de braise, Kaïra pose sa lame et s'assoit enfin."),
                L(Kaira, "Je me bats parce que rester immobile me terrifie. Parce que quand je m'arrête, j'entends le village qui s'éteint."),
                L(Kaira, "Mais Ilio est vivant. Il n'y a plus de silence à fuir. Je peux m'arrêter, parfois."),
                L(Narr, "Le Vent-de-Braise s'éveille en elle, calme et brûlant."),
            },
            "c7_nerys" => new()
            {
                L(Narr, "Au bord de l'eau, Nerys ferme son carnet."),
                L(Nerys, "Je parle quand j'ai peur. Je remplis les silences pour qu'on ne remarque pas que je tremble."),
                L(Nerys, "J'ai peur de ce qui va arriver. Et pour la première fois… je n'ai rien à ajouter."),
                L(Narr, "Le Vent-des-Marées s'éveille en elle, profond et paisible."),
            },
            "c7_torvan" => new()
            {
                L(Narr, "Sur la crête, Torvan range sa pipe."),
                L(Torvan, "Trente ans à garder un secret. À obéir à un serment dont je ne voyais plus la raison. Hmpf."),
                L(Torvan, "Aujourd'hui, je choisis ce que je protège. Toi. Eux. Ce monde bancal."),
                L(Narr, "Le Vent-d'Orage s'éveille en lui, grave et fidèle."),
            },
            "c7_end" => new()
            {
                L(Elara, "Quatre vents, quatre cœurs. Les Gardiens avaient raison : il fallait des vivants, pas des dieux."),
                L(Me, "Il ne manque plus que la Tour du Sceau."),
            },

            // ---------------------------------------------------------------- Chapitre VIII — La Tour du Sceau
            "c8_brief" => new()
            {
                L(Elara, "Les quatre vents sont prêts. Il reste la Tour du Sceau, au centre de l'île, là où tout a commencé. Kael t'y attend, j'en suis sûre."),
                L(Me, "Je sais. Je l'ai vu en rêve."),
                L(Elara, "Si tu dois le combattre… ne le hais pas."),
                L(Me, "Je ne le hais pas, Elara. C'est mon frère."),
            },
            "c8_base" => new()
            {
                L(Nerys, "La Tour du Sceau. Quarante mètres de pierre claire. Un escalier en spirale, d'après mes notes."),
                L(Torvan, "Hmpf. Les gardes de Kael rôdent. Pas des hommes. Des échos."),
                L(Kaira, "Allez ! Un étage à la fois."),
            },
            "c8_camp_done" => new()
            {
                L(Me, "Le chemin monte. Je le sens."),
                L(Nerys, "Dernier tour de spirale, puis le sommet. Respirez."),
            },
            "c8_kael_down" => new()
            {
                L(Kael, "…Tu te bats comme elle."),
                L(Me, "Je me bats comme moi."),
                L(Kael, "Oui. C'est bien ce qui me terrifie."),
            },
            "c8_end" => new()
            {
                L(Torvan, "Hmpf. Il s'est volatilisé."),
                L(Kaira, "Regardez l'horizon… Le mur blanc."),
                L(Nerys, "La Pâleur a franchi le dôme. Elle sera sur nous à l'aube."),
                L(Me, "Il nous reste une nuit."),
            },

            // ---------------------------------------------------------------- Chapitre IX — La Veille du Dernier Jour
            "c9_brief" => new()
            {
                L(Elara, "La Pâleur est à nos portes. Elle sera sur nous à l'aube. Il reste une nuit."),
                L(Elara, "Des créatures sans nom approchent du camp. Défendez l'Autel, puis préparez-vous. Chacun a quelque chose à dire."),
            },
            "c9_wave_done" => new()
            {
                L(Kaira, "Ça, c'est la première vague. Et les suivantes ?"),
                L(Torvan, "Hmpf. Toujours plus nombreuses."),
            },
            "veillee_kaira" => new()
            {
                L(Kaira, "Si demain tout s'éteint, je veux que tu saches : tu es la première personne à qui j'ai dit mon vrai nom."),
                L(Me, "Kaïra, c'est pas ton vrai nom ?"),
                L(Kaira, "Mon vrai nom, c'est « la sœur d'Ilio ». Et c'est le plus beau que je connaisse."),
            },
            "veillee_nerys" => new()
            {
                L(Nerys, "J'ai passé ma vie à classer des légendes. Pour la première fois, j'en vis une."),
                L(Nerys, "Si on gagne, j'écrirai tout. Dans l'ordre. Avec des notes de bas de page."),
                L(Me, "Et si on perd ?"),
                L(Nerys, "Alors j'aurai eu le meilleur sujet d'étude de l'histoire. Et de bons amis."),
            },
            "veillee_torvan" => new()
            {
                L(Torvan, "Hmpf. Je ne suis pas doué pour les adieux."),
                L(Torvan, "Mais demain, quoi qu'il arrive, c'est moi qui passe devant. C'est mon dernier serment, et je le choisis."),
            },
            "veillee_elara" => new()
            {
                L(Elara, "Je t'ai trouvé au pied de l'Autel, il y a quelques semaines, et déjà je ne sais plus comment je vivais avant."),
                L(Elara, "Je ne suis pas ta mère. Mais s'il fallait choisir, je la serais."),
            },
            "c9_bram" => new()
            {
                L(Bram, "Écoute, gamin. Ce soir, tout est gratuit. Je dis ça comme ça, hein, c'est pas une habitude."),
                L(Bram, "Tiens, une lanterne. Elle est à moi depuis vingt ans. Elle me protège de l'obscurité. Rends-la-moi après… ou ne la rends pas."),
                L(Me, "Merci, Bram."),
                L(Bram, "Ne dis rien. Je risquerais de pleurer. Et je ne facture pas les larmes, c'est trop cher."),
            },
            "c9_ilio" => new()
            {
                L(Ilio, "Grande sœur dit que tu vas sauver le monde. C'est vrai ?"),
                L(Me, "On va essayer."),
                L(Ilio, "Alors prends ça. C'est un caillou chanceux. Il m'a protégé dans la Pâleur."),
                L(Me, "Merci, Ilio. Je te le rendrai."),
            },

            // ---------------------------------------------------------------- Chapitre X — Le Dernier Monde
            "c10_brief" => new()
            {
                L(Elara, "Tout ce que j'ai à te donner, ce sont des prières. Prends-les."),
                L(Elara, "Quoi que tu choisisses au sommet, je saurai que c'est le bon choix, parce que c'est toi."),
                L(Me, "Merci, Elara. Pour tout."),
                L(Elara, "Va. Le vent est avec toi."),
            },
            "c10_core" => new()
            {
                L(Narr, "Le Cœur du Sceau bat sous tes pieds, lent, fatigué, immense."),
                L(Me, "C'est maintenant."),
            },
            "c10_boss_done" => new()
            {
                L(Narr, "L'Effacement se disperse en poussière blanche. Le Cœur du Sceau s'ouvre comme une fleur."),
                L(Kael, "Voilà. La Pâleur reprend son souffle. Nous avons quelques minutes."),
                L(Kael, "Choisis, petite sœur. Quelle que soit ta décision, je serai à tes côtés. Cette fois."),
            },
            "c10_choice" => new()
            {
                new(Me, "Je sais ce que je dois faire.", new()
                {
                    ("Restaurer le Sceau — se sacrifier", () => StoryEvents.Ending(1)),
                    ("Laisser le monde s'éteindre, avec Kael", () => StoryEvents.Ending(2)),
                    ("La Troisième Voie — les quatre vents", () => StoryEvents.Ending(3)),
                }),
            },
            _ => null,
        };
    }
}
