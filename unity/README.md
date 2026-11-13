# Dead Haul for Windows (Unity)

This folder is the Unity version of Dead Haul. You open it in Unity on your PC and press one menu item to get `DeadHaul.exe`.

Everything (map, characters, guns, UI, sounds) is built from code, so the project has no art files to import. Multiplayer uses the same Cloudflare server as the browser game.

## One-time setup (about 20 minutes, mostly downloading)

1. **Install Unity Hub** from https://unity.com/download and sign in (a free Unity Personal licence is fine).
2. In Unity Hub, go to **Installs > Install Editor** and pick the latest **Unity 6 LTS** (6000.x). Keep **Windows Build Support** ticked (it is on by default on Windows).
3. In Unity Hub, click **New project**, choose the **Universal 3D** template, name it `DeadHaul`, and create it.
4. Get the code: on GitHub open `joshreece70-code/dead-haul`, then **Code > Download ZIP**, and unzip it.
5. Copy the folder `unity/Assets/DeadHaul` from the zip into your new project's `Assets` folder. You can drag it into Unity's **Project** window.
6. Wait for Unity to finish compiling. A **Dead Haul** menu appears in the top bar. Click **Dead Haul > Set Up Project**.

## Play and build

- **Test:** press the **Play** button at the top of the Unity window. The game takes over whatever scene is open.
- **Build the .exe:** click **Dead Haul > Build Windows .exe**. When it finishes, Explorer opens `Builds/DeadHaul/` inside your project.
- **Share it:** zip the whole `Builds/DeadHaul` folder, not just the .exe. The .exe needs the `DeadHaul_Data` folder and the DLLs next to it. Windows may show a SmartScreen warning for an unsigned game: click **More info > Run anyway**.

## Controls

| Key | Action |
| --- | --- |
| WASD | Move |
| Mouse | Look and aim |
| Left click | Shoot or swing |
| Right click | Aim toggle (sniper scopes in) |
| Shift | Sprint |
| C | Crouch |
| Space | Jump (stands up from crouch) |
| Z | Dolphin dive with bullet time, then you land prone |
| 1 2 3, mouse wheel | Primary, secondary, melee |
| R | Reload |
| H | Use a medkit |
| E | Search and loot |
| Tab | Bag |
| V | Far camera |
| Esc | Pause menu (graphics, sensitivity, leave raid) |

## Multiplayer

- Everyone who leaves the lobby code empty is in the public Unity lobby. Type the same code (for example `JUICY`) and press **Join** to get a private lobby.
- Players in the same lobby who start a raid within the same half hour share the same map. The first player already in a raid sets the map for everyone who joins after.
- PvP can be switched off in the menu. A hit only counts when both players have PvP on.
- Unity players and browser players use separate lobbies because the two maps are different.

## Graphics presets

Pick one in the menu or the pause menu:

| Preset | What it does | For |
| --- | --- | --- |
| Low | 75% render scale, no anti-aliasing, short shadows, 60 fps cap | Older laptops |
| Medium | 2x MSAA, 2 shadow cascades | |
| High | 4x MSAA, 4 cascades | |
| Ultra (default) | 4x MSAA, long soft shadows, 165 fps cap | PCs that run Fortnite well |

All presets keep ACES tone mapping, bloom, colour grading, vignette and film grain. URP's ambient occlusion comes from the template's PC renderer.

## What's in this first version

**In:**
- Procedural town: 25 blocks of buildings, parks and parking lots, with cars, street lights and burning barrels.
- Infected (walkers and runners) and hunters that shoot in bursts.
- Containers, car trunks and bodies to loot, with rarity colours. Guns can be found.
- Three green-flare extraction points, and a 15-minute raid timer.
- Stash with selling, a trader, and a loadout of primary, secondary and melee.
- Power-ups: 15-second minigun, field medic and adrenaline.
- Online play through Cloudflare, with name tags and PvP.

**Not in yet (the browser version has these):**
- Enterable buildings and window vaulting
- Driving
- The grapple gun
- Backpack sizes and the grid inventory
- The slot machine

These will be added in later updates.

## If something goes wrong

- **Pink objects:** the project wasn't made from the Universal 3D template. Make a new project with that template and copy the folder in again.
- **Red errors in the Console:** copy the first one and send it to Claude.
- **Reset progress:** use **Dead Haul > Reset Save Data**. The save file lives in `%USERPROFILE%\AppData\LocalLow\<company>\Dead Haul\`.
