# Assets

Store-bought art is **never committed to this repository**. Unity Asset Store and
Fab licences are per-seat and forbid redistribution; a public repo containing
them is a licence breach, and it would bloat the clone to gigabytes besides.

Everything imported lands in `unity/Assets/ThirdParty/`, which `.gitignore`
excludes. This file is the manifest: it says what to buy, where it goes, and what
to do to it after importing.

---

## What you actually have

### 1. Unity Asset Store 86679 — **this is not a map**

**"RPG/FPS Game Assets for PC/Mobile (Industrial Set v2.0)"** by Dmitrii Kutsenko.
Free, 203.9 MB, Built-in/URP/HDRP, Unity 2022.1.18f1+, Extension Asset under the
Standard Unity Asset Store EULA.

It is a **modular industrial kitbash set** — walls, pipes, containers, railings,
props. It is a very good one, and it is roughly 9,000 favourites' worth of
useful. But it contains no city, no streets, no drivable layout and no interiors.

This is the single biggest gap between the plan and the assets, so plan around
it rather than discovering it in week three. Three honest options:

| Option | Effort | Cost | Verdict |
|---|---|---|---|
| **Greybox one district yourself with ProBuilder, then kitbash 86679 over it** | 1–2 weeks | free | **Recommended.** You control the layout, which is the part that decides whether the game is fun. The kit makes it look finished. |
| Buy a city pack (Synty POLYGON City, or similar) | days | ~$40–100 | Fast and coherent, but the layout is someone else's and rarely suits a specific mission design. |
| Procedural / real-world data (CityGen3D, Mapbox, OSM) | weeks | varies | Produces a lot of city and very little *level*. Wrong tool for a vertical slice. |

The vertical slice in `docs/ROADMAP.md` is scoped to a **compact industrial
district**, which is exactly what 86679 is good at. That is not a coincidence —
scope to the art you own.

### 2. Rifle — Fab "FPS Guns 4K - Rifle 1"

`https://www.fab.com/listings/a7e70f32-2091-416c-8da9-1546b4dff1bb`

### 3. Character — Fab "Apocalyptic Survivor / Assassin, Low Poly"

`https://www.fab.com/listings/7b1fe6a8-89ea-491f-b59f-1d2aa48bbe79`

---

## Check these on the Fab listing before you buy

Fab is Epic's marketplace, and a meaningful share of what is on it targets
Unreal only. **Check every one of these on the listing page.** They decide
whether the asset is usable here at all:

1. **Licence.** Fab items carry either a *Standard Licence* (usable in any
   engine) or an **Unreal-Engine-only** licence. Unreal-only content **cannot
   legally be used in Unity** regardless of what file formats it ships with.
   This is the one that ends the conversation, so check it first.
2. **Supported file formats.** You need **FBX** or **glTF/GLB**. A listing that
   ships only `.uasset` is an Unreal project, not a model, and there is no
   supported way to get it into Unity.
3. **Rigged / animated.** For the character: is it rigged, and to what skeleton?
   Fab characters are very often rigged to the **UE5 mannequin**, whose bone
   names and proportions differ from Unity's Humanoid rig.
4. **Poly count and texture resolution.** "4K" in a gun's name means 4096px
   textures. That is fine for one hero weapon and ruinous if you import twenty.

---

## After importing

### The rifle

1. Drop the FBX under `unity/Assets/ThirdParty/Guns/`.
2. Set **Import Settings → Materials → Material Creation Mode: Standard**, then
   remap to URP/Lit materials. Imported materials are almost always Built-in and
   render magenta under URP.
3. Add an empty child transform at the barrel tip named `Muzzle`. `Weapon.cs`
   traces from this and spawns the muzzle flash here.
4. Create a `WeaponDefinition` asset (**Create → Game → Weapon Definition**) and
   point `ViewModelPrefab` at the rifle prefab. All tuning lives in the asset,
   never in the model.

### The character

This is the fiddly one. Budget half a day, not half an hour.

1. Import the FBX to `unity/Assets/ThirdParty/Characters/`.
2. **Rig → Animation Type: Humanoid**, then **Configure…** and check every bone
   mapping. A UE5-mannequin rig will usually map, but check the fingers, the
   twist bones, and the T-pose. A wrong T-pose is the cause of most "why is my
   character doing the Naruto run" bugs.
3. If the rig will not map cleanly, the fastest fix by a wide margin is
   **[Mixamo](https://www.mixamo.com/)**: upload the FBX, let it auto-rig, and
   download it with a few animations. Free, and it produces a clean Humanoid.
4. Animations: Unity's **Starter Assets — Third Person Controller** (free on the
   Asset Store) ships a usable locomotion set that retargets onto any Humanoid.
   Use it as a placeholder so you can play the game this week; replace it when
   the game is worth animating properly.
5. Add a `Muzzle`-style empty on the right hand bone and assign it as
   `WeaponHolder.handSocket`.

---

## Licence rules, stated plainly

- **Do not commit store assets.** Not to a public repo, not to a private one you
  might make public later.
- **Extension Asset** (the Unity Asset Store's default) is a **single-seat**
  licence. One developer, one licence. A second person on the project needs
  their own.
- Assets baked into a **built game** are fine — that is the entire point of the
  licence. Distributing the *source* assets is not.
- Keep receipts. If you ever sell the game, you may be asked to show the chain.

---

## Legal, for a project shaped like this one

- **Do not call it GTA.** Do not use "Grand Theft Auto", Rockstar's marks, its
  logos, its fonts, or its characters. "A GTA-like" is fine to *say*; it is not
  fine to *ship*. The working title in this repo is `PROJECT SEVEN`.
- **Real weapon brands**: modelling a real firearm is legal, but using the
  manufacturer's *name* or trade dress can attract a trademark complaint. Give
  guns invented names.
- **Real car brands**: same, and enforced far more aggressively. Invent them.
- **Music**: no commercial tracks, ever, including in a devlog video that YouTube
  will match against Content ID.
- The landing page in `web/index.html` already carries a disclaimer in the
  footer. Keep it there.
