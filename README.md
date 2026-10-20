# DOOM — Unity Edition

A remake of the original DOOM's gameplay in Unity 6 (6000.3.5f2, URP), targeting Windows in
landscape, playable with keyboard + mouse or any gamepad. All art and audio are **procedural
placeholders** designed to be swapped out file-by-file.

## Running

- **Editor:** open the project, open `Assets/Scenes/Main.unity`, press Play. (The game bootstraps
  itself from code, so Play works from any scene.)
- **Build:** menu **Doom → Build Windows (x64)** → `Builds/Windows/DOOM.exe`.
  Headless: `unity run . -- -executeMethod Doom.EditorTools.DoomProjectSetup.BuildWindows`
- **Tests:** `unity test . --mode PlayMode` (play-through of every level, doors/keys, monster AI,
  simulated keyboard and gamepad input, save/load round trip, lifts, teleporters).

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move / strafe | WASD, ↑/↓, `,` `.` | Left stick (analog speed) |
| Turn | Mouse, ←/→ | Right stick |
| Fire | Left mouse, Ctrl | Right trigger |
| Use / open | E, Space | A / Cross |
| Run | Shift (inverts *Always run*) | Push stick fully |
| Weapons | 1–7, mouse wheel, Q, `[` `]` | LB / RB, Y / Triangle |
| Automap | Tab | View / Select |
| Menu | Esc | Start |
| Quicksave / quickload | F6 / F9 | (via menu) |

Menus are navigated with arrows/WASD + Enter/Esc, or D-pad/stick + A/B. Vertical aiming is off by
default like the original (autoaim handles height). Turn on **Freelook** in Options for mouse/stick
pitch. Options also cover sensitivity, invert Y, render resolution (classic 200p / 400p / native),
status bar vs fullscreen HUD, crosshair, rumble, volume and fullscreen.

## Saving

**Save Game** (in-game menu) and **Load Game** (title or pause menu) offer 6 slots, plus F6
quicksave / F9 quickload. A save captures the whole level as it stands: player position, health,
armor, weapons, ammo and keys; every monster (position, health, awake or not, corpses); remaining
and dropped pickups; barrels; door states; secrets found; automap; and level stats. Saves are JSON
files in `%USERPROFILE%\AppData\LocalLow\DoomUnity\DOOM\saves`. Projectiles in flight are not
saved, and you can't save while dead or while the exit is triggering.

Cheats: `iddqd`, `idkfa`, `idfa`, `idclip`, `iddt`.

## What's implemented

- **Player:** momentum movement, view bob, step smoothing, Doom armor rules (green 1/3, blue 1/2),
  nukage damage, pistol start on death, inventory carried between levels.
- **Weapons:** fist, chainsaw, pistol, shotgun, chaingun, rocket launcher (splash), plasma gun,
  BFG9000 (ball + 40-tracer spray). Timings/damage converted from the original tic tables,
  first-shot accuracy, autoaim, muzzle-flash lighting, ammo auto-switch, backpack.
- **Monsters:** zombieman, shotgun guy, imp, demon, cacodemon, lost soul, baron of hell. They wake on
  sight or noise, chase (flow-field pathing around walls), open unlocked doors, attack with
  distance-weighted odds, flinch on pain chance, infight, drop items, and respawn on Nightmare.
- **World:** doors, colored key doors, secret doors, secret areas, exit switches, lifts (use them or
  step on from above; they carry players and monsters), teleporters (monsters use them too, and
  arriving on someone telefrags them), stairs/raised floors, outdoor sky areas, damaging nukage, explosive barrels, Doom-style diminishing light.
- **Game flow:** title, menus, 5 skill levels, 3 levels (E1M1–E1M3, original layouts), automap,
  intermission tally (kills/items/secrets/time/par), finale, status bar with face.

## Replacing placeholder art & audio

Every asset is looked up by name in `Resources` first; the placeholder is only generated if no
file exists. Drop a file with the right name into:

| Folder | Contents |
|---|---|
| `Assets/Resources/Art/Textures/` | walls, floors, ceilings, `sky`, `stbar`, `stbar_side` |
| `Assets/Resources/Art/Sprites/` | monsters, items, weapons, projectiles, effects, HUD |
| `Assets/Resources/Audio/` | sound effects |
| `Assets/Resources/Music/` | `title`, `e1m1`, `e1m2`, `e1m3`, `intermission`, `finale` (optional) |

To get every expected filename, run **Doom → Export Placeholder Assets (staging folder)**. It writes
all placeholders as PNG/WAV into `Assets/PlaceholderExport/` so you can paint over them and move
them into the `Resources` folders above. Images under any `/Art/` folder are imported with point
filtering, no mipmaps and no compression automatically.

Naming conventions:

- Monsters: `<id>_<frame>` with ids `zombieman shotgunguy imp demon cacodemon lostsoul baron` and
  frames `walk0 walk1 attack0 attack1 pain death0 death1 death2` (death2 = corpse).
- Weapons (first person): `weapon_<id>_<idle|fire0|fire1>`, ids `fist chainsaw pistol shotgun chaingun rocketlauncher plasmagun bfg`.
- Items: `item_<name>` (e.g. `item_medikit`, `item_bluekey`, `item_shotgun`).
- Projectiles: `proj_<id>_<fly0|fly1|hit0|hit1|hit2>`. Effects: `fx_<puff|blood|explosion|bfgspray>_<0-2>`.
- HUD: `hud_face_<0-4>_<l|c|r>`, `hud_face_<ouch|dead|god|grin>`, `hud_skull0/1`, and optional
  `hud_titlepic` / `hud_interpic` full-screen backgrounds.
- Sounds: a monster-specific sound (`imp_sight`, `baron_death`, …) overrides the generic
  `monster_sight/active/pain/death/attack` for that monster.

Sprites are scaled at **32 pixels per meter** (one original map unit per pixel), with the pivot at
bottom-center, so original-sized sprites appear at the right size.

## Level format

Levels live in `Assets/Scripts/World/LevelData.cs` as ASCII grids (one character = one 2×2 m cell);
the legend is documented at the top of that file. An optional `Heights` grid raises floors in
0.25 m steps for stairs and platforms, and sets each lift's raised height. Lifts are `L` cells;
teleporter pads `Q`/`W`/`E` lead to destinations `q`/`w`/`e`. Adding a level means adding a `LevelDef` to `Levels.All`.

## Project layout

```
Assets/Scripts/Core     GameManager, input, settings, audio, asset lookup, placeholder generators
Assets/Scripts/World    level data, geometry builder, flow-field pathing, doors, exit switch, sky
Assets/Scripts/Actors   monsters, projectiles, combat helpers, pickups, barrels, billboards
Assets/Scripts/Player   controller, health/inventory, weapons
Assets/Scripts/UI       HUD/automap/intermission (IMGUI), menus
Assets/Scripts/Editor   setup + build menu, placeholder exporter, art import settings
Assets/Tests/PlayMode   gameplay tests
```

## Not implemented (yet)

8-angle monster rotations (one facing per frame for now), crushers, the remaining monsters (spectre, pain elemental, cyberdemon, etc.), powerups beyond the
soulsphere (berserk, invulnerability, radiation suit, light amp), and multiplayer.
