# Le Dernier Monde — notes de reprise

Jeu open world (inspiré de Genshin Impact / Wuthering Waves), Unity 6.6 (URP, nouvel Input System).
**Version 1.0.0** : l'histoire complète (Prologue + Chapitres I à X, trois fins) est jouable ; détail des versions plus bas.
(v0.1.1 : Prologue + Chapitre I jouables de bout en bout, testés en Play
(boucle de test automatisée avec captures, voir plus bas). Histoire : **`STORY.md`** (bible). Crédits : `CREDITS.md`.

## v0.2 → v1.0 — Chapitres II à X (voir STORY.md §6)
- **Données** : `Scripts/Quests/ChapterQuests.cs` (quêtes `main2…main10`, enchaînées par `Quest.requires`), `ChapterDialogue.cs` (tout le texte + dialogues d'attente `Idle`), `StoryEvents` (`Play`, `ChapterDone`, `Ending`).
- **QuestManager** : nouveau `StepType.Reach` (distance à un repère), `QuestStep.lines` (dialogue attaché à une étape *Talk*), quêtes verrouillées tant que la précédente n'est pas finie, `ChaptersDone`, `CurrentChapter`, `DebugJump`.
- **Monde** : `Editor/ChapterBuilder.cs` (+ `.Regions.cs`) — sculpte le terrain (volcan de la Forge, baie d'Ysmère, plateau de Valdrak, route en pente, Tour du Sceau de 45 m avec rampe en spirale), peint les couches, retire arbres et herbe, place PNJ (Maren, Sael, Orrin, Ilio), camps, boss, coffres, sentinelles, `RegionZone` (brouillard, lumière, météo) et le Sanctuaire céleste (plateforme à 700 m au sud de l'île). **Relançable** : les reliefs sont des valeurs absolues, pas des cumuls.
- **Scripts de monde** : `RegionZone`, `StoryObject` (pierres de mémoire, flambeaux, journal, cœur du Sceau ; `gateQuest/gateStep`), `ChapterGate` (objets visibles selon l'avancée : Ilio au refuge puis au camp, marée blanche au Ch. IX).
- **Ennemis** : `EnemyBase` — tags `boss*` génériques, `bossTitle`, `rig` (duelliste humanoïde en mocap : Kael, l'Ombre d'Aelith), sol des plateformes (`GroundY`). `EnemySpawner.spawnOnStart` (rencontres d'histoire qui n'apparaissent que sur ordre).
- **Cinématiques** : `Editor/ChapterCinematics.cs` — CIN_06 à CIN_21 (acteurs `Actor()`, `Cam()`). Nouvelles actions : `time:HH`, `respawn:Repère`, `clearboss:tag`.
- **Compagnons** : vents éveillés au Ch. VII (`PartyMember.Awakened`). Version du jeu : `PlayerSettings.bundleVersion = 1.0.0` (fixée par le builder).
- **Tests** : `Scripts/Core/DevTools.cs` — `Jump(quête, étape)`, `Goto(repère)`, `AutoStep()` (joue l'étape courante : parle, va au lieu, vainc le camp ou le boss, touche l'objet, ouvre le coffre). Pendant un test, écouter `Application.logMessageReceived` plutôt que lire la console (les traces sont énormes).
- ⚠ Attendre la fin de la compilation avant d'entrer en Play (sinon les champs non sérialisés, ex. `EnemyBase.mpb`, sont remis à nul) et ne pas appeler `Step()` juste après l'entrée en Play (« PlayerLoop appelé récursivement »).

## Performances (v1.0.1)
- **Constat** (rendu caméra 1080p, GPU synchronisé) : le coût vient surtout du rendu de la scène (terrain + forêt + ombres) et du post-traitement (~3 ms de profondeur de champ, ~3 ms de TAA) ; les lumières et ennemis pèsent peu sur le GPU. Le `Step()` de l'éditeur n'est PAS un bon instrument (il inclut le rendu de l'éditeur) : mesurer avec `Camera.Render()` vers une RenderTexture + `ReadPixels` pour synchroniser.
- `Scripts/Core/PerformanceManager.cs` : 3 niveaux de qualité (**Faible / Moyenne / Haute**, mémorisés dans `PlayerPrefs "quality"`, bouton dans Paramètres), lumières ponctuelles actives seulement près de la caméra (205 → ~10), profondeur de champ réservée aux cinématiques, baisse automatique d'un niveau si la moyenne reste < 36 FPS pendant 5 s. Les réglages d'asset (échelle de rendu, distance d'ombres) sont restaurés à l'arrêt.
- Mesures éditeur (Haute → Moyenne → Faible) : sommet de la Tour 49 → 39 → 20 ms, lagune 44 → 33 → 22 ms, Forge 27 → 21 → 15 ms.
- Build : les blocs de pierre sont marqués *Batching Static* ; `EnemyBase` met en sommeil les ennemis inactifs à plus de 110 m ; URP allégé (ombres 110 m / 3 cascades / 2048, 4 lumières par objet) ; terrain (erreur de pixel 5, arbres 1500 m, billboards 450 m, herbe 100 m) et LOD bias 1,4. ⚠ Relancer `WorldGenerator` réinitialise ces valeurs de terrain.

## v0.1.2 — animations mocap (Quaternius UAL1/UAL2, CC0)
- `Editor/AnimationSetup.cs` (appelé par Build Gameplay Content) : extrait les clips des FBX `Art/Animations/Quaternius`, génère `Animation/Humanoid.controller` (44 clips, un état par clip) et `Resources/HumanoidClips.asset` (durées).
- `Scripts/Animation` : **CharacterAnimator** (héros : idle/course/saut/attaque/esquive depuis le PlayerController ; `PlayAction(state, durée)`), **NpcAnimator** (PNJ : état d'idle + gestes quand `CinematicDirector.CurrentSpeaker` = leur nom), **AnimationLengths / HumanoidAnim**. `ProceduralAnimator` passe en mode `overlay` (il ne sert plus que pour l'escalade).
- Cinématiques : nouvelles actions `anim:player|Acteur:État[:secondes]` et `npcface:id`. CIN_01 (Aelis se relève, Elara se tourne) et CIN_02 (geste sur l'Autel, Kael en garde puis coup d'épée) animées. Kael porte une épée électro (`ContentBuilder.Sword`).
- Testé en Play (captures) : CIN_01, CIN_02, idle / course / attaque du joueur.
- Épée de Kael vérifiée en gros plan : bien en main (poignée dans la paume, lame dans le prolongement) ; elle paraissait fine seulement sur les plans larges.
- CIN_01 : cadrage en deux plans (« Au sol » puis « Se relève », visé sur le torse) pour que la tête d'Aelis reste dans le champ.
- CIN_04 : Aelis tend la main vers le fragment (`Interact`) puis `Skill` ; Kael lance `Skill` avant de disparaître. CIN_03 n'a pas d'acteur humanoïde ; CIN_05 : Kael gesticule via `NpcAnimator` (état `Talk` pendant ses sous-titres).

## v0.1.1 — corrections issues des tests en jeu
- Terrain corrompu par Git (fichier binaire traité comme texte → joueur qui tombe dans l'eau) : monde régénéré (seed 1337) + `.gitattributes` (WorldTerrain.asset en LFS, sans conversion de fins de ligne).
- Cinématiques : fondu sous les sous-titres, plans de l'étoile recadrés (la profondeur de champ floute au-delà de 350 m), tête d'Aelis coupée (repère au bord de la marche), plan « la main » recadré, titre du boss affiché sur l'overlay, corps du boss conservé 9 s, Kael absent de l'épilogue (un plan caméra portait le même nom), écran noir après l'épilogue (fondu réappliqué à la destruction du graphe), actions à t=0 fiables.
- Jeu : épée tenue correctement (lame vers l'avant/en garde), PV du boss +35 % (5 400 de base), audio plafonné (12 voix, anti-doublons), pilier de l'Autel en pierre claire, jarres à la bonne taille.
- API publiques ajoutées : `GameUI.AdvanceDialogue()` / `ChooseDialogueOption(i)`, `CinematicDirector.CurrentId` / `SkipCurrent()` / `IsCurrent()`.

## Tester le jeu automatiquement (MCP)
- `LastWorld.EditorTools.GameCapture.Capture("Previews/test/x.png")` : capture 3D + UI même sans focus.
- En Play : `EditorApplication.isPaused = true` puis boucles de `EditorApplication.Step()` (50 pas = 1 s). Plusieurs `Step()` dans une même commande fonctionnent.
- Les entrées clavier simulées n'atteignent pas le jeu (même avec les réglages Input System) → piloter par les API publiques (Attack, TrySkill, Interact, AdvanceDialogue, Teleport…). `Attack()` exige d'être au sol : attendre ~30 pas après une téléportation.
- Ne pas appuyer sur le vrai « Nouvelle partie » en test (efface la sauvegarde) : masquer « Titre » et appeler `StoryEvents.NewGame()`.
- **Ne jamais modifier un script pendant le Play** : la recompilation réinitialise les singletons.

## Pipeline (menus Unity « Last World »)
1. **Generate World** (`Assets/_Game/Editor/WorldGenerator.cs`, ~2,5 min) — terrain 2 km érodé, textures/arbres/herbe/rochers Poly Haven, eau, nuages, lumière, joueur. **Écrase `Scenes/World.unity`.**
2. **Build Gameplay Content** (`ContentBuilder.cs`) — importe les persos MPFB, équipe, systèmes, PNJ, décor du camp (props Poly Haven), coffres, orbes, ennemis, boss, carte, **puis les cinématiques**. Relançable.
- **Build Cinematics** (`CinematicBuilder.cs`) — reconstruit seulement les 6 cinématiques (+ clairière de l'arène du boss).
- **Preview Cinematics (contact sheet)** — rend 4 images par cinématique dans `Previews/cinematics_sheet.png`.
- Autres : `Import Characters`, `Rebuild Player Character` (obsolète).

## Où est quoi (`Assets/_Game/`)
- `Scripts/Player` : PlayerController, ProceduralAnimator (poses relatives à la T-pose ; « L » = +X = droite du perso), CapeCloth.
- `Scripts/Combat` : PlayerCombat, PartyManager, Health + Elements, CombatFX, DamageNumbers.
- `Scripts/Enemies` : EnemyBase (IA gelée pendant les cinématiques), EnemySpawner, CampLevel.
- `Scripts/Quests` : QuestManager (Chapitre I + 2 quêtes secondaires), **Dialogue** (tout le texte FR, scènes de recrutement), **StoryEvents** (enchaîne cinématiques ↔ dialogues ↔ quête), QuestMarkerAnchor.
- `Scripts/Cinematics` : **CinematicDirector** (lecture, letterbox, sous-titres, fondus, « maintenir Échap » pour passer), pistes Timeline maison **SubtitleTrack / FadeTrack / CinematicActionTrack** (actions : `place:`, `show:`, `hide:`, `burst:`, `sound:`, `banner:`, `player:hide|show`), **TimelineMover** (travellings), **CinematicTrigger**.
- `Cinematics/*.playable` : les Timelines (éditables dans la fenêtre Timeline ; activer le CinemachineBrain de la Main Camera pour prévisualiser).
- `Scripts/World`, `Scripts/UI/GameUI.cs`, `Scripts/Core` : inchangés (version affichée sur l'écran titre).
- `Art/PolyHaven` (CC0), `Art/Characters` (FBX MPFB + `Recolor/` = textures de tenue recolorées par perso).

## Cinématiques (voir STORY.md §4)
| Id | Déclencheur |
|---|---|
| CIN_00_Prologue + CIN_01_Reveil | « Nouvelle partie » → puis dialogue d'Elara automatique |
| CIN_02_Resonance | toucher l'Autel (étape 1) → dialogue « Il avait ton visage » |
| CIN_03_Gardien | approcher l'arène à < 35 m pendant l'étape 7 |
| CIN_04_PremierVent | mort du Gardien Ancien |
| CIN_05_IlTeRessemblait | fin du Chapitre I (après le dernier dialogue d'Elara) |
CIN_06 à CIN_21 (chapitres II à X et les trois fins) : déclencheurs et contenu dans STORY.md §6, construction dans `Editor/ChapterCinematics.cs`.
Kael (antagoniste) = modèle du héros, tenue sombre, cheveux argentés (matériaux `Materials/Cinematics/Kael_*`).

## Personnages (Blender)
- Sources : `United/blender/*_MPFB.blend` (sauvegardes v0.0 dans `blender/backup_v0.0/`).
- Script de rhabillage + export FBX : `United/tools/blender_scripts/fix_outfit.py`
  (`blender -b X_MPFB.blend --python fix_outfit.py -- X <retirer,csv> <ajouter,csv> <sortie.fbx>`), puis copier le FBX dans `Art/Characters/X/` et relancer Build Gameplay Content.
- **Les assets `donitz_monk_robe_hood*` sont défectueux** (capuche seule) : utiliser `donitz_monk_robe`.
- Tenues v0.1 : Aelis tunique sarcelle, Kaïra tunique braise, Nerys robe bleu marée, Torvan tunique violet orage, Elara robe crème, Bram tunique ocre (couleurs dans `CharacterImporter.OutfitColors`).

## Règles techniques apprises
- Un MonoBehaviour / ScriptableObject posé dans une scène ou un asset doit être dans un fichier du même nom.
- `ITimelineClipAsset.clipCaps` s'écrit en minuscule.
- Les FBX Poly Haven ont leurs propres LOD0-3 ; modèles à l'échelle réelle.
- Unity n'extrait les textures intégrées d'un FBX qu'au premier import → `ModelImporter.ExtractTextures` (fait dans CharacterImporter). Les matériaux remappés n'apparaissent plus dans `LoadAllAssetsAtPath`.
- Via MCP : Unity se déconnecte pendant les recompilations (attendre puis réessayer) ; en Play, l'éditeur sans focus n'avance pas → tester avec `EditorApplication.isPaused = true` + `EditorApplication.Step()`. Les évènements Input System simulés n'arrivent pas au jeu.
- Blender se pilote en ligne de commande (`blender.exe -b`) ; MPFB : `bl_ext.user_default.mpfb`, données dans `extensions/.user/user_default/mpfb/data`.

## Tests réalisés (v1.0) et reste à valider
- **Automatisé, vert** : parcours complet Chapitre I → X par `DevTools.AutoStep` (étapes, enchaînements, cinématiques, dialogues attachés, apparition des camps et boss, quête finale) et choix final → fin 1 (la fin 3 a aussi été lancée) sans aucune erreur ; les 22 cinématiques jouées avec captures ; dialogues des PNJ de chaque chapitre vérifiés aux bonnes étapes.
- **À valider à la main (clavier/souris)** : ressenti des combats de boss (PV de niveau pris dans `CampLevel`, boss du Ch. X ≈ 2,4 × les PV de base), marche/saut/planeur/escalade dans les nouveaux reliefs, saut des cinématiques (Échap maintenu), lisibilité des plans de caméra en vrai temps réel.
- **Limites connues** : pas de voix ni de musique dédiée ; les compagnons ne sont visibles que dans les cinématiques (le joueur incarne le personnage actif) ; Kael et l'Ombre d'Aelith partagent le modèle du héros (teintes différentes) ; la pierre des nouveaux sites est une version désaturée de `medieval_blocks_02` ; la météo des régions (`RegionZone`) est un système de particules simple suivant la caméra.
- **Pistes** : musique par région, voix, variantes de boss moins « golem », quêtes secondaires par région (Orbes et Chasseur existent déjà), nouvelle partie + (garder l'équipe).
