# The Last World

A 3D open-world action-adventure game, built with **Unity 6 (URP)** and inspired by
Genshin Impact / Wuthering Waves. The game's dialogue and UI are in French.

> Aerendal is the last living world of the Sky Tree. The Pallor — a nameless force that
> erases colors, sounds and memories — has destroyed all the others. The Celestial Seal
> that kept the Pallor at bay has just shattered into four fragments, and the four winds
> that kept the world alive have fallen silent. Aelis wakes up near an altar, with no
> memories… and with a dormant wind fragment in his hand.

## Features

- **Complete playable story**: Prologue + Chapters I to X, three endings.
- **Open world** of 2 km² generated through a pipeline: eroded terrain, forests, lagoon,
  volcano, storm peaks, a 45 m Seal Tower with a spiral ramp, and a floating Sky Sanctuary.
- **Combat**: attack, dodge, elemental skill, gliding/climbing, enemy camps and
  story bosses.
- **Quest system**: chained main quests per chapter + side quests.
- **22 cutscenes** (Timeline + Cinemachine) with subtitles, letterbox and fades,
  animated with the Universal Animation Library (Quaternius, CC0).
- **Performance model**: 3 quality levels (Low / Medium / High) with automatic
  downgrading if FPS drop.
- **Save system**.

## Controls

| Action | Key |
|---|---|
| Move | ZQSD / WASD (Input System) |
| Jump | Space |
| Dodge | Shift |
| Attack | Left click |
| Skill | Right click / E |
| Interact | F |
| Skip a cutscene | hold Escape |

## How to open the project

1. Clone the repository (the terrain is stored with Git LFS — install
   [Git LFS](https://git-lfs.com) first, then run `git lfs pull`).
2. Open the folder in **Unity Hub** with Unity 6 (URP + Input System) — the world
   lives in `Assets/_Game/Scenes/World.unity`.
3. Play directly, or regenerate the content through the **Last World** menus:
   - *Generate World* (~2.5 min, overwrites the scene);
   - *Build Gameplay Content* (characters, NPCs, quests, bosses, cinematics);
   - *Build Cinematics* / *Preview Cinematics*.

## Project structure (`Assets/_Game/`)

| Folder | Contents |
|---|---|
| `Scripts/Player` | hero controller, procedural animation, cape |
| `Scripts/Combat` | combat, party, health + elements, FX, damage numbers |
| `Scripts/Enemies` | enemy AI, spawners, camp levels |
| `Scripts/Quests` | quests, dialogue, story orchestration |
| `Scripts/Cinematics` | cinematic director and custom Timeline tracks |
| `Cinematics/` | the 22 Timelines (CIN_00 to CIN_21) |
| `Scripts/World` | regions, story objects (memory stones, journal…) |
| `Scripts/UI` / `Scripts/Core` | UI, game state, save system, DevTools, performance |
| `Art/PolyHaven` | CC0 assets (textures, rocks, camp props) |
| `Art/Characters` | character FBX (MPFB) and Quaternius animations |

## Documentation

- **`STORY.md`** — story bible: the world, the characters, every chapter and ending.
- **`PROJECT_NOTES.md`** — technical notes: architecture, pipeline, testing, versions.
- **`CREDITS.md`** — credits for third-party assets (all CC0 or free).

## Credits

Original creation (story, dialogue, code, procedural terrain): the *The Last World*
project. See **`CREDITS.md`** for the full list — **Poly Haven**, **MakeHuman / MPFB**,
**Quaternius** and the Unity packages are all freely licensed (CC0 unless stated).
