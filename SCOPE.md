# SCOPE — Project Seven: "Yard 7"

**This file is the contract.** If a thing is not on this page, it is not in v1.
When you want to add something, it goes in `LATER.md`, not here.

---

## The honest framing

GTA V cost an estimated **US$265 million**, took **1,000+ people about 5 years**,
and that is roughly **5,000 person-years**. A solo developer at 12 hours a week
produces about **0.3 person-years** in a year.

The gap is about **10,000 to 1**. "Can I build GTA solo" is not a hard question;
it is an arithmetically settled one. So the question changes to: **which 0.01%
of GTA do I build?**

The answer is the heist-gone-loud moment. Sneak in, take the thing, shoot your
way out while the heat climbs. That single loop is the emotional core of GTA,
and it is reachable in about four months.

A finished 15-minute game that 500 people love beats an unfinished open world
that nobody plays. (For calibration: ~19,000 games launched on Steam in 2025 and
the *median* one grossed about $249. Shipping something small and complete is
the ambitious choice, not the modest one.)

---

## V1 — what is in

**One map.** A compact industrial district, roughly **200m × 200m**. Three named
areas — perimeter yard, warehouse interior, admin block — plus one extraction
gate. Greyboxed with ProBuilder first, then kitbashed with Unity Asset Store
package 86679 over the top.

**One player.** Third-person, camera-relative movement, ADS, sprint, crouch.
No cover system, no vaulting, no melee.

**One weapon.** The Fab rifle, hip-fire and aimed, with reload. `WeaponDefinition`
already supports fire modes and shotguns — that is for later, not now.

**Three enemy types**, all the same prefab with different tuning:
| Type | Difference |
|---|---|
| Guard | Patrols. Long reaction delay. Dies fast. |
| Rifleman | Uses cover points. Standard reaction. |
| Heavy | Slower, more health, shorter burst pauses. |

**One heat meter.** 0–5, `AlertSystem.cs`. Drives the reinforcement spawner:
higher heat means a bigger spawn budget, faster spawns, and above level 3
reinforcements arrive already knowing roughly where you are.

**Three missions**, as `MissionDefinition` assets on the same map. Different
objectives, different start and extraction points.

**One run loop.** Insert → objectives → extract or die → score screen → repeat.
Target run length **8–15 minutes**.

**One leaderboard.** Cloudflare, week 14, one hour of work.

**One platform: Windows.** See below.

---

## Explicitly not in v1

The full list is in [`LATER.md`](LATER.md). The headlines:

- **No vehicles.** Enter/exit flow, camera handoff, wheel physics and traffic AI
  are weeks of work each.
- **No multiplayer.** `backend/src/do/LobbyRoom.ts` exists and is **frozen**.
  Do not open it again until v1 has shipped.
- **No browser build.** Package 86679 alone is 203.9 MB of source art; a WebGL
  build realistically needs to land in the tens of megabytes, and that is before
  characters, audio and code. The landing page's "Play in browser" button is
  aspirational — treat it as such or remove it.
- **No open-world streaming.** `WorldStreamer.cs` exists for when there is a
  second district. There is not.
- **No civilians, day/night cycle, weather, weapon shop, second weapon, story,
  cutscenes, or dialogue.**

---

## The uncomfortable note about this repository

At the time of writing, `unity/Assets/Game/Scripts` contains roughly **1,100
lines of C# and there is not one `.unity` scene, `.prefab` or `.asset` file in
the repo.** There is a Durable Object lobby, a D1 schema, an HMAC anti-cheat
scheme and a landing page — all serving a game that cannot currently be played.

That is textbook **systems-before-game-loop**, and it is worth naming rather
than glossing over. The code is a genuine head start and the design in it is
sound, but it is not progress in the only sense that counts.

**The next thing to do is not to write more code.** It is to open Unity and make
a capsule move on a ProBuilder floor.

Track one number weekly: **how many minutes of the game can a stranger play?**

| Week | Target |
|---|---|
| 4 | 3 minutes |
| 10 | 8 minutes |
| 16 | 35 minutes |

If that number does not move for two weeks, something has gone wrong regardless
of how many commits landed.
