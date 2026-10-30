# Le Dernier Monde — notes de reprise

Jeu open world (inspiré de Genshin Impact / Wuthering Waves), Unity 6.6 (URP, nouvel Input System).
**Version 0.1.1** (30/10/2026) : Prologue + Chapitre I jouables de bout en bout, testés en Play
(boucle de test automatisée avec captures, voir plus bas). Histoire : **`STORY.md`** (bible). Crédits : `CREDITS.md`.

## v0.1.2 — animations mocap (Quaternius UAL1/UAL2, CC0)
- `Editor/AnimationSetup.cs` (appelé par Build Gameplay Content) : extrait les clips des FBX `Art/Animations/Quaternius`, génère `Animation/Humanoid.controller` (44 clips, un état par clip) et `Resources/HumanoidClips.asset` (durées).
- `Scripts/Animation` : **CharacterAnimator** (héros : idle/course/saut/attaque/esquive depuis le PlayerController ; `PlayAction(state, durée)`), **NpcAnimator** (PNJ : état d'idle + gestes quand `CinematicDirector.CurrentSpeaker` = leur nom), **AnimationLengths / HumanoidAnim**. `ProceduralAnimator` passe en mode `overlay` (il ne sert plus que pour l'escalade).
- Cinématiques : nouvelles actions `anim:player|Acteur:État[:secondes]` et `npcface:id`. CIN_01 (Aelis se relève, Elara se tourne) et CIN_02 (geste sur l'Autel, Kael en garde puis coup d'épée) animées. Kael porte une épée électro (`ContentBuilder.Sword`).
- Testé en Play (captures) : CIN_01, CIN_02, idle / course / attaque du joueur.
- À régler : épée de Kael fine et mal calée dans la main (plans larges), cadrage « Au sol » de CIN_01 qui coupe la tête d'Aelis, CIN_03–05 pas encore animées.

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

## Non testé / à faire ensuite
- Tout le Chapitre I a été joué en test automatisé (v0.1.1) ; reste à valider au vrai clavier/souris (saut des cinématiques en maintenant Échap, ressenti du combat).
- Kaïra/Nerys/Torvan parlent lors de leur recrutement sans apparaître physiquement ; si un compagnon est actif, c'est lui qui apparaît dans les cinématiques à la place d'Aelis.
- Animations mocap en place (v0.1.2) mais seulement sur CIN_01/02 ; animer CIN_03 (Gardien), CIN_04, CIN_05 avec l'action `anim:`. Pas de lip-sync.
- Pas de voix (sous-titres seulement), pas de musique dédiée aux cinématiques.
- Défauts connus : orientation de l'épée ; boss probablement trop facile ; l'heure du jour n'est pas forcée dans les cinématiques (CIN_05 devrait être de nuit).
- Feuille de route : v0.2 = Chapitre II « Les Cendres de Cendrelune » (voir STORY.md §3).
