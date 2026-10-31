# The Last World — project notes

Open-world game (inspired by Genshin Impact / Wuthering Waves), Unity 6.6 (URP, new Input System).
**Version 1.0.0**: the complete story (Prologue + Chapters I to X, three endings) is playable; version details below.
(v0.1.1: Prologue + Chapter I playable start to finish, tested in Play mode
(automated test loop with screenshots, see below). Story: **`STORY.md`** (bible). Credits: `CREDITS.md`.)

## v0.2 → v1.0 — Chapters II to X (see STORY.md §6)
- **Data**: `Scripts/Quests/ChapterQuests.cs` (quests `main2…main10`, chained via `Quest.requires`), `ChapterDialogue.cs` (all the text + waiting dialogues `Idle`), `StoryEvents` (`Play`, `ChapterDone`, `Ending`).
- **QuestManager**: new `StepType.Reach` (distance to a landmark), `QuestStep.lines` (dialogue attached to a *Talk* step), quests locked until the previous one is done, `ChaptersDone`, `CurrentChapter`, `DebugJump`.
- **World**: `Editor/ChapterBuilder.cs` (+ `.Regions.cs`) — sculpts the terrain (the Forge volcano, Ysmère Bay, the Valdrak plateau, a sloped road, the 45 m Seal Tower with its spiral ramp), paints layers, removes trees and grass, places NPCs (Maren, Sael, Orrin, Ilio), camps, bosses, chests, sentinels, `RegionZone` (fog, light, weather) and the celestial Sanctuary (platform 700 m south of the island). **Re-runnable**: the landforms are absolute values, not accumulations.
- **World scripts**: `RegionZone`, `StoryObject` (memory stones, torches, journal, heart of the Seal; `gateQuest/gateStep`), `ChapterGate` (objects visible depending on progress: Ilio at the refuge then at the camp, the white tide in Ch. IX).
- **Enemies**: `EnemyBase` — generic `boss*` tags, `bossTitle`, `rig` (humanoid duelist in mocap: Kael, Aelith's Shadow), platform ground (`GroundY`). `EnemySpawner.spawnOnStart` (story encounters that only appear on command).
- **Cinematics**: `Editor/ChapterCinematics.cs` — CIN_06 to CIN_21 (actors `Actor()`, `Cam()`). New actions: `time:HH`, `respawn:Landmark`, `clearboss:tag`.
- **Companions**: winds awakened in Ch. VII (`PartyMember.Awakened`). Game version: `PlayerSettings.bundleVersion = 1.0.0` (fixed by the builder).
- **Testing**: `Scripts/Core/DevTools.cs` — `Jump(quest, step)`, `Goto(landmark)`, `AutoStep()` (plays the current step: talk, go to the place, defeat the camp or the boss, touch the object, open the chest). During a test, listen to `Application.logMessageReceived` rather than reading the console (the traces are huge).
- ⚠ Wait for compilation to finish before entering Play mode (otherwise non-serialized fields, e.g. `EnemyBase.mpb`, are reset to null) and do not call `Step()` right after entering Play ("PlayerLoop called recursively").

## Performance (v1.0.1)
- **Findings** (1080p camera render, GPU synchronized): the cost comes mostly from rendering the scene (terrain + forest + shadows) and post-processing (~3 ms depth of field, ~3 ms TAA); lights and enemies barely weigh on the GPU. The editor's `Step()` is NOT a good instrument (it includes editor rendering): measure with `Camera.Render()` into a RenderTexture + `ReadPixels` to synchronize.
- `Scripts/Core/PerformanceManager.cs`: 3 quality levels (**Low / Medium / High**, stored in `PlayerPrefs "quality"`, button in Settings), point lights active only near the camera (205 → ~10), depth of field reserved for cinematics, automatic downgrade one level if the average stays < 36 FPS for 5 s. Asset settings (render scale, shadow distance) are restored on stop.
- Editor measurements (High → Medium → Low): top of the Tower 49 → 39 → 20 ms, lagoon 44 → 33 → 22 ms, Forge 27 → 21 → 15 ms.
- Build: stone blocks are flagged *Batching Static*; `EnemyBase` puts enemies to sleep when inactive beyond 110 m; slimmed-down URP (shadows 110 m / 3 cascades / 2048, 4 lights per object); terrain (pixel error 5, trees 1500 m, billboards 450 m, grass 100 m) and LOD bias 1.4. ⚠ Re-running `WorldGenerator` resets those terrain values.

## v0.1.2 — mocap animations (Quaternius UAL1/UAL2, CC0)
- `Editor/AnimationSetup.cs` (called by Build Gameplay Content): extracts the clips from the `Art/Animations/Quaternius` FBX files, generates `Animation/Humanoid.controller` (44 clips, one state per clip) and `Resources/HumanoidClips.asset` (durations).
- `Scripts/Animation`: **CharacterAnimator** (hero: idle/run/jump/attack/dodge from the PlayerController; `PlayAction(state, duration)`), **NpcAnimator** (NPCs: idle state + gestures when `CinematicDirector.CurrentSpeaker` = their name), **AnimationLengths / HumanoidAnim**. `ProceduralAnimator` switches to `overlay` mode (it is now only used for climbing).
- Cinematics: new actions `anim:player|Actor:State[:seconds]` and `npcface:id`. CIN_01 (Aelis gets up, Elara turns) and CIN_02 (gesture on the Altar, Kael taking guard then sword strike) animated. Kael carries an electro sword (`ContentBuilder.Sword`).
- Tested in Play mode (screenshots): CIN_01, CIN_02, player idle / run / attack.
- Kael's sword checked in close-up: properly in hand (grip in the palm, blade in line with the arm); it only looked thin on wide shots.
- CIN_01: shooting split into two shots ("On the ground" then "Getting up", framed on the torso) so Aelis's head stays in frame.
- CIN_04: Aelis reaches out toward the fragment (`Interact`) then `Skill`; Kael casts `Skill` before vanishing. CIN_03 has no humanoid actor; CIN_05: Kael gestures via `NpcAnimator` (`Talk` state during his subtitles).

## v0.1.1 — fixes from in-game testing
- Terrain corrupted by Git (binary file treated as text → player falling into the water): world regenerated (seed 1337) + `.gitattributes` (WorldTerrain.asset as LFS, no end-of-line conversion).
- Cinematics: fade below subtitles, star shots reframed (depth of field blurs beyond 350 m), Aelis's head cut off (landmark at the edge of the step), "the hand" shot reframed, boss title displayed on the overlay, boss body kept for 9 s, Kael absent from the epilogue (a camera shot shared the same name), black screen after the epilogue (fade re-applied when the graph is destroyed), actions at t=0 reliable.
- Gameplay: sword held correctly (blade forward / guard stance), boss HP +35% (5,400 base), audio capped (12 voices, anti-duplicates), Altar pillar in light stone, jars at the right size.
- Public APIs added: `GameUI.AdvanceDialogue()` / `ChooseDialogueOption(i)`, `CinematicDirector.CurrentId` / `SkipCurrent()` / `IsCurrent()`.

## Testing the game automatically (MCP)
- `LastWorld.EditorTools.GameCapture.Capture("Previews/test/x.png")`: captures 3D + UI even without focus.
- In Play: `EditorApplication.isPaused = true` then loops of `EditorApplication.Step()` (50 steps = 1 s). Several `Step()` calls in a single command work.
- Simulated keyboard input never reaches the game (even with Input System settings) → drive it through the public APIs (Attack, TrySkill, Interact, AdvanceDialogue, Teleport…). `Attack()` requires being on the ground: wait ~30 steps after a teleport.
- Do not press the real "New Game" button during a test (it wipes the save): hide "Title" and call `StoryEvents.NewGame()`.
- **Never modify a script while playing**: recompilation resets the singletons.

## Pipeline (Unity "Last World" menus)
1. **Generate World** (`Assets/_Game/Editor/WorldGenerator.cs`, ~2.5 min) — 2 km eroded terrain, Poly Haven textures/trees/grass/rocks, water, clouds, light, player. **Overwrites `Scenes/World.unity`.**
2. **Build Gameplay Content** (`ContentBuilder.cs`) — imports the MPFB characters, equipment, systems, NPCs, camp scenery (Poly Haven props), chests, orbs, enemies, bosses, map, **then the cinematics**. Re-runnable.
- **Build Cinematics** (`CinematicBuilder.cs`) — rebuilds only the 6 cinematics (+ the boss arena clearing).
- **Preview Cinematics (contact sheet)** — renders 4 images per cutscene into `Previews/cinematics_sheet.png`.
- Others: `Import Characters`, `Rebuild Player Character` (obsolete).

## Where things are (`Assets/_Game/`)
- `Scripts/Player`: PlayerController, ProceduralAnimator (poses relative to the T-pose; "L" = +X = the character's right), CapeCloth.
- `Scripts/Combat`: PlayerCombat, PartyManager, Health + Elements, CombatFX, DamageNumbers.
- `Scripts/Enemies`: EnemyBase (AI frozen during cinematics), EnemySpawner, CampLevel.
- `Scripts/Quests`: QuestManager (Chapter I + 2 side quests), **Dialogue** (all the French text, recruitment scenes), **StoryEvents** (chains cutscenes ↔ dialogues ↔ quest), QuestMarkerAnchor.
- `Scripts/Cinematics`: **CinematicDirector** (playback, letterbox, subtitles, fades, "hold Escape" to skip), custom Timeline tracks **SubtitleTrack / FadeTrack / CinematicActionTrack** (actions: `place:`, `show:`, `hide:`, `burst:`, `sound:`, `banner:`, `player:hide|show`), **TimelineMover** (dolly moves), **CinematicTrigger**.
- `Cinematics/*.playable`: the Timelines (editable in the Timeline window; enable the Main Camera's CinemachineBrain to preview).
- `Scripts/World`, `Scripts/UI/GameUI.cs`, `Scripts/Core`: unchanged (version shown on the title screen).
- `Art/PolyHaven` (CC0), `Art/Characters` (MPFB FBX + `Recolor/` = outfit textures re-colored per character).

## Cinematics (see STORY.md §4)
| Id | Trigger |
|---|---|
| CIN_00_Prologue + CIN_01_Reveil | "New Game" → then Elara's automatic dialogue |
| CIN_02_Resonance | touching the Altar (step 1) → dialogue "He had your face" |
| CIN_03_Gardien | approach the arena within 35 m during step 7 |
| CIN_04_PremierVent | death of the Ancient Guardian |
| CIN_05_IlTeRessemblait | end of Chapter I (after Elara's last dialogue) |
CIN_06 to CIN_21 (chapters II to X and the three endings): triggers and content in STORY.md §6, construction in `Editor/ChapterCinematics.cs`.
Kael (antagonist) = the hero's model, dark outfit, silver hair (`Materials/Cinematics/Kael_*`).

## Characters (Blender)
- Sources: `United/blender/*_MPFB.blend` (v0.0 saves in `blender/backup_v0.0/`).
- Re-dress + FBX export script: `United/tools/blender_scripts/fix_outfit.py`
  (`blender -b X_MPFB.blend --python fix_outfit.py -- X <remove,csv> <add,csv> <output.fbx>`), then copy the FBX into `Art/Characters/X/` and re-run Build Gameplay Content.
- **The `donitz_monk_robe_hood*` assets are broken** (hood only): use `donitz_monk_robe`.
- v0.1 outfits: Aelis teal tunic, Kaira ember tunic, Nerys sea-blue gown, Torvan storm-violet tunic, Elara cream dress, Bram ochre tunic (colors in `CharacterImporter.OutfitColors`).

## Technical lessons learned
- A MonoBehaviour / ScriptableObject placed in a scene or an asset must live in a file of the same name.
- `ITimelineClipAsset.clipCaps` is written in lowercase.
- Poly Haven FBX files come with their own LOD0-3; models are real-world scale.
- Unity only extracts textures embedded in an FBX on first import → `ModelImporter.ExtractTextures` (done in CharacterImporter). Re-mapped materials no longer appear in `LoadAllAssetsAtPath`.
- Via MCP: Unity disconnects during recompiles (wait then retry); in Play mode, an unfocused editor does not advance → test with `EditorApplication.isPaused = true` + `EditorApplication.Step()`. Simulated Input System events do not reach the game.
- Blender is driven from the command line (`blender.exe -b`); MPFB: `bl_ext.user_default.mpfb`, data in `extensions/.user/user_default/mpfb/data`.

## Tests done (v1.0) and what remains to validate
- **Automated, green**: full playthrough Chapter I → X via `DevTools.AutoStep` (steps, chaining, cinematics, attached dialogues, camps and bosses spawning, final quest) and final choice → ending 1 (ending 3 was also run) with no errors; all 22 cutscenes played with screenshots; NPC dialogues for each chapter checked at the right steps.
- **To validate by hand (keyboard/mouse)**: feel of boss fights (HP from `CampLevel` levels, Ch. X boss ≈ 2.4 × base HP), walk/jump/glide/climb on the new terrain, skipping cutscenes (hold Escape), readability of camera shots in real time.
- **Known limitations**: no voices or dedicated music; companions are only visible in cutscenes (the player embodies the active character); Kael and Aelith's Shadow share the hero's model (different tints); the stone of the new sites is a desaturated version of `medieval_blocks_02`; region weather (`RegionZone`) is a simple particle system following the camera.
- **Ideas**: music per region, voices, less "golem" boss variants, side quests per region (Orbs and Hunter already exist), new game + (keep the party).
