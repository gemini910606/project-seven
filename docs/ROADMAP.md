# Roadmap

Sixteen weeks at **12–15 hours a week** (~200 hours). Halve the calendar if you
work on it full-time.

Read [`SCOPE.md`](../SCOPE.md) first. This file is how to build what that file
describes.

Every milestone has an **exit criterion**. If you cannot honestly tick it, do
not start the next one.

---

## M0 — It moves and it shoots

**Days 1–3 · ~9 hours**

Create `Assets/Game/Scenes/Sandbox.unity`. A ProBuilder floor 40×40m and a dozen
boxes for cover. A capsule with `CharacterController`, `ThirdPersonMotor` and
`PlayerInputReader`. Cinemachine 3 follow camera plus an over-shoulder aim
camera swapped on right mouse. A crosshair `Image` on a Canvas. Hook up a
`Weapon` with a `WeaponDefinition` asset and raycast at a wall.

> **Exit:** you can walk around a grey box room and put holes in a wall.

## M1 — One enemy that fights back

**Weeks 1–2 · ~20 hours**

Bake a NavMesh. Build the enemy prefab: `NavMeshAgent`, `EnemyLocomotion`,
`EnemyPerception`, `EnemyBrain`, `Health`, `EnemyWeaponUser`, its own `Weapon`.

Then spend **three full hours on feel and nothing else**: hitmarker, hit sound,
impact particles, muzzle flash, camera shake, a death ragdoll or a
fall-over animation.

> **Exit — and this is the real gate on the whole project:** a friend plays for
> five minutes without being asked to, and shooting one enemy is *satisfying*.
> If it is not fun here, it will not become fun by adding a city. Fix it now or
> stop.

## M2 — The loop closes

**Weeks 3–4 · ~20 hours**

Objective trigger volumes (`ObjectiveZone`), an extraction zone, a run timer, a
fail state, and a score screen driven by `MissionDirector`, `RunStats` and
`ScoreCalculator`.

The map is still ugly greybox. That is correct and intentional.

> **Exit:** a run has a beginning, a middle, an end, and a number at the end
> that you want to beat.

## M3 — The district

**Weeks 5–7 · ~35 hours**

**Only now** import package 86679 into `Assets/ThirdParty/`. Kitbash the
200×200m industrial district over the greybox you have already playtested.
Re-bake the NavMesh. One URP lighting pass: baked lightmaps, a skybox, fog, one
colour-grading volume.

Doing this *after* M2 rather than before is the single most important ordering
decision in this roadmap. Decorating a layout you have not played is how
projects end up with a beautiful map that is no fun to fight in.

> **Exit:** a screenshot you would post publicly.

## M4 — Heat and three enemy types

**Weeks 8–10 · ~35 hours**

Wire `AlertSystem` to `SpawnDirector`: heat drives spawn budget, rate and
distance. Add Rifleman and Heavy as tuning variants of the M1 enemy. Add cover
point usage. HUD heat meter using `AlertSystem.ProgressToNextLevel`.

> **Exit:** getting spotted at level 1 feels different from getting spotted at
> level 4, and you can describe the difference in one sentence.

## M5 — Three missions, HUD, audio

**Weeks 11–13 · ~35 hours**

Missions as `MissionDefinition` assets in `Assets/Game/Data/`. Full HUD: health,
ammo, heat, objective marker. Audio pass — footsteps by surface, weapon fire and
tail, enemy barks, ambience, and a two-state music system that switches on heat
level. Main menu, pause menu, settings.

Free audio: [freesound.org](https://freesound.org) and the annual
[Sonniss GDC bundles](https://sonniss.com/gameaudiogdc).

> **Exit:** you can launch the game from an executable and play three different
> missions without touching the editor.

## M6 — Ship it

**Weeks 14–16 · ~30 hours**

Hook the client to the backend (`BackendClient`, run submission, leaderboard
display). Build for Windows. Make an itch.io page with six screenshots and a
45-second in-engine trailer. Get **ten external playtesters**. Fix the top ten
complaints and nothing else.

The Cloudflare work is **one hour, here** — not now. See
[`docs/CLOUDFLARE.md`](CLOUDFLARE.md).

> **Exit:** a stranger on the internet has finished a run and their score is on
> the leaderboard.

---

## What each horizon actually buys you

At 12–15 hours a week:

| Horizon | Milestones | What you have |
|---|---|---|
| **1 month** (~55h) | M0–M2 | A greybox loop. Move, aim, shoot, one enemy, one objective, extract, score. Ugly. Complete. Playable by a friend. |
| **3 months** (~170h) | M0–M4 | The district built and lit, three enemy types, heat escalating, one full mission. A demo you can post. |
| **6 months** | M0–M6 + polish | A shippable 30-minute game with a leaderboard. |
| **1 year** | v1 shipped, plus one item from `LATER.md` | A game with an audience and one big new system. Vehicles, if you still want them. |

---

## Do this week

| Day | ~Hours | Goal |
|---|---|---|
| **1** | 2–3 | Open the project in Unity 6000.3.12f1. Install ProBuilder. Create `Sandbox.unity`, a 40×40m floor and a dozen cover boxes. Drop in a capsule with `CharacterController` + `ThirdPersonMotor` + `PlayerInputReader`. **WASD moves a capsule.** Commit. |
| **2** | 2–3 | Cinemachine 3: a follow camera and an aim camera, swapped on right mouse. A crosshair on a Canvas. Commit. |
| **3** | 2–3 | The rifle mesh in the hand socket, a `WeaponDefinition` asset, `Weapon` wired to `PlayerController`. **Shooting a wall leaves a decal.** Commit. |
| **4** | 2–3 | Read all thirteen existing scripts end to end. Add your own comments where you had to stop and think. This is not busywork — code you cannot explain costs 5× to debug later. |
| **5** | 2–3 | Bake a NavMesh. One enemy prefab that walks a `PatrolRoute`. |
| **6** | 2–3 | The enemy sees, chases and shoots you. You can kill it. |
| **7** | 2–3 | Three hours on feel only: hitmarker, hit sound, impact particles, camera shake. Then show it to one person. |

---

## Learning, in the order you will need it

- **Foundation** — [Unity Learn Pathways](https://learn.unity.com/pathways).
  Do *Unity Essentials* only if the editor still feels unfamiliar, then
  *Junior Programmer* **alongside** the project, never before it.
- **M0/M1** — Code Monkey's third-person shooter controller videos, built on
  Unity's Starter Assets. Covers the aim camera, crosshair and hit resolution.
- **M1** — Unity's own [AI Navigation docs](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html)
  for NavMesh baking and agents.
- **M3** — Unity's [e-book for level designers](https://unity.com/blog/games/e-book-for-level-designers),
  which teaches greybox-first for exactly the reasons this roadmap is ordered
  the way it is.

**Two hours of tutorial maximum per feature.** Then implement it badly and move
on. Refactor only when a change you actually need is hard.
