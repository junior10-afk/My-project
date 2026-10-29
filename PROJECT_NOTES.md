# Le Dernier Monde — notes de reprise

Jeu open world (inspiré de Genshin Impact / Wuthering Waves), Unity 6.6 (URP, nouvel Input System).
**Version 0.1.0** (29/10/2026) : Prologue + Chapitre I jouables avec histoire complète, narration
réécrite et 6 cinématiques Timeline/Cinemachine. Histoire : **`STORY.md`** (bible). Crédits : `CREDITS.md`.

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
- Cinématiques vérifiées par planche contact, **pas encore jouées en conditions réelles** (vraies entrées, timing, enchaînements dialogue ↔ cinématique). Le saut (maintenir Échap) n'est pas testé au clavier.
- Les acteurs n'ont que l'animation procédurale (idle) : pas de gestes ni de lip-sync. Étape suivante : animations mocap (avatars Humanoid prêts) + AnimationTrack dans les Timelines.
- Pas de voix (sous-titres seulement), pas de musique dédiée aux cinématiques.
- Défauts connus : orientation de l'épée ; boss probablement trop facile ; l'heure du jour n'est pas forcée dans les cinématiques (CIN_05 devrait être de nuit).
- Feuille de route : v0.2 = Chapitre II « Les Cendres de Cendrelune » (voir STORY.md §3).
