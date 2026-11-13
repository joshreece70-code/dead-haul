# Dead Haul

A raid-and-extract looter that runs in the browser. This repository holds the game, its multiplayer server for Cloudflare, and a Blender export script for custom weapon models.

| Path | What it is |
| --- | --- |
| `public/index.html` | The whole game |
| `public/netroom.js` | Browser side of multiplayer: one WebSocket per lobby code |
| `src/worker.js` | The server: a Cloudflare Worker that serves `public/` and relays players in each lobby |
| `wrangler.toml` | Cloudflare configuration |
| `public/models/` | The weapon models the game loads |
| `tools/blender_build_weapons.py` | Builds the weapons inside Blender, or writes the model files without it |
| `tools/blender_export.py` | Exports weapon models from Blender into `public/models/` |

## 1. Put it on GitHub

Create an empty repository on GitHub (no README), then from this folder:

```
git init -b main
git add .
git commit -m "Dead Haul"
git remote add origin https://github.com/YOUR-NAME/dead-haul.git
git push -u origin main
```

## 2. Host it on Cloudflare

You need a free Cloudflare account. Pick one of these.

**From the Cloudflare dashboard (no tools to install).** Go to Workers & Pages, create a Worker, choose to import a Git repository, and pick this one. Cloudflare reads `wrangler.toml` and deploys on every push.

**From your computer.** Install Node.js, then:

```
npm install
npx wrangler login
npx wrangler deploy
```

Either way you get an address like `https://dead-haul.YOUR-SUBDOMAIN.workers.dev`. Send that link to friends. Nobody needs an account to play.

To try it locally first, run `npx wrangler dev` and open the address it prints.

## 3. How multiplayer works

- Everyone on the page with the same lobby code is in the same squad. A blank code is the open lobby.
- The server keeps one small room per lobby code. It stores nothing: it holds each connected player's latest position and relays it to the others.
- A room holds up to 16 players.
- Players share the map and can see, shoot and loot each other. Infected, scavs, containers and cars are simulated separately for each player.
- Lobby codes are not passwords. Anyone with your link who knows or guesses a code can join it.

Cloudflare's free plan includes Workers and Durable Objects with daily limits. A few friends playing will stay well inside them, but check your account's usage page if the game gets popular.

## 4. Weapon models and Blender

All eleven weapons (pistol, SMG, shotgun, SPAS-12, Uzi, rifle, AK-47, sniper, minigun, katana, lightsaber hilt) are model files in `public/models/`, listed in `public/models/manifest.json`. The game loads them in place of its built-in guns. If a file is missing or fails to load, that weapon falls back to the built-in model.

**Open them in Blender.** In Blender's Scripting tab, open `tools/blender_build_weapons.py` and run it. It builds all eleven weapons as editable mesh objects, laid out side by side in a `DeadHaulWeapons` collection, with their materials.

**Edit and export.**

1. Save the `.blend` file inside this repository, for example `blender/weapons.blend`.
2. Edit the weapons, or replace them with your own. Keep these rules:
   - One object per weapon, named `pistol`, `smg`, `shotgun`, `spas`, `uzis`, `rifle`, `ak`, `sniper`, `minigun`, `katana` or `saber`. For `saber`, model the hilt only; the game draws the glowing blade.
   - Barrel along +Y, top of the gun along +Z, origin at the grip, real size in metres.
   - Several materials are fine. Each part shows its material's base colour. For a textured weapon, use a single mesh with a single material.
   - Keep the sights at about the same height, or the gun will sit too high or low when aiming in first person.
3. Open `tools/blender_export.py` in the Scripting tab and run it.
4. Commit the changed files in `public/models/` and push.

**Without Blender.** `python3 tools/blender_build_weapons.py` rewrites the model files straight from the designs in that script.

Character models are not replaceable yet.

## Not yet verified

The server and the browser networking were tested together in a simulation, not on Cloudflare itself. The weapon model files were checked for structure and proportions, but the game's model loader and the two Blender scripts have not been run for real. Expect to adjust small things on first deploy.
