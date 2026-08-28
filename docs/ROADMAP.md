# Roadmap

Twelve weeks at **12–15 hours a week** (~160 hours). Halve the calendar full-time.

Read [`SCOPE.md`](../SCOPE.md) first. Every milestone has an **exit criterion**;
if you cannot honestly tick it, do not start the next one.

The ordering has one rule behind it: **get two machines talking to each other in
week one.** Multiplayer is not a feature you add at the end. Everything built
before the network exists gets rewritten once it does.

---

## M-1 — The compile gate

**Day 0 · 2–4 hours, and it is allowed to be ugly**

None of the Unity C# here has ever been compiled — there are no `.meta` files,
which is the proof. The round rules *have* been (see `tools/RulesTests`), but
they are the only part.

1. Create the project from Unity's **URP template** and move the code in, per
   [`SETUP.md`](SETUP.md). Do not open `unity/` directly.
2. Install Netcode for GameObjects and the Multiplayer Services SDK.
3. Fix whatever does not compile.
4. Delete anything you do not understand. 800 lines you wrote beat 4,000 you
   inherited.

> **Exit:** clean Console, and the EditMode tests run.

## M0 — Two windows, one world

**Week 1 · ~12 hours**

A grey box room. A capsule with `FirstPersonMotor`, `PlayerLook`,
`PlayerInputReader`, `NetworkPlayer`. A `NetworkManager` with the Unity
Transport. Build once, run the build alongside the editor.

Do **not** add a gun yet.

> **Exit:** two clients, and moving in one window moves a capsule in the other.
> This is the single most important milestone in the project; everything after
> it is comparatively easy.

## M1 — Shooting that works over the wire

**Week 2 · ~14 hours**

Rifle in hand. `Weapon` raises `PelletFired`, `NetworkPlayer` ships it to the
server, `ShotResolver` traces it and applies damage. Health, death, and a body
that stops moving on every machine.

Then three hours on feel and nothing else: hitmarker, hit sound, impact
particles, muzzle flash.

> **Exit — the real gate on the project:** you and one friend shoot each other
> across the internet and it is *satisfying*. If it is not fun here, no amount
> of round structure will save it.

## M2 — The round

**Weeks 3–4 · ~22 hours**

Wire `RoundDirector` into the scene: prep freeze, round timer, spawns per side,
one life per round, respawn at round boundaries. Then the spike: `Spike`,
`BombSite`, plant and defuse bars.

`MatchCore` already implements every rule and every edge case, tested. This
milestone is scene wiring, not logic.

> **Exit:** a full round resolves four ways — elimination, detonation, defuse,
> timeout — and the score is right on both machines.

## M3 — Bots

**Weeks 5–6 · ~22 hours**

`BotDirector` fills both teams to five. Tune `BotWeaponUser` — reaction delay,
burst rhythm, aim error — until a bot is beatable but not free.

This is the milestone that makes the game testable. After it, you never again
need to find four other people to check whether something works.

> **Exit:** you play a complete match alone against nine bots and it holds up.

## M4 — The map

**Weeks 7–9 · ~35 hours**

**Only now** import the industrial kit. Dress the greybox you have already
played hundreds of rounds on. Two sites, three lanes, deliberate sightlines.
One URP lighting pass.

Doing this after M3 rather than before is the most important ordering decision
in this roadmap. A map you have not played is a map you will have to rebuild.

> **Exit:** a screenshot you would show someone, and the layout still plays the
> way the greybox did.

## M5 — The bits that make it a game

**Weeks 10–11 · ~22 hours**

HUD: health, ammo, round score, timer, spike state, a scoreboard on Tab. A menu
with **Host** and **Join by code**. Audio: footsteps by surface, gunfire and its
tail, plant and defuse beeps, round-start and round-win stingers.

> **Exit:** a friend downloads a build, pastes a code, and plays without you
> talking them through anything.

## M6 — Ship it to five people

**Week 12 · ~12 hours**

Windows build. Put it somewhere they can download it. Play three matches
together. Fix the top five complaints and nothing else.

> **Exit:** five friends have played a full match and asked when the next one is.

---

## What each horizon buys you

| Horizon | Milestones | What you have |
|---|---|---|
| **2 weeks** | M0–M1 | Two people shooting each other over the internet. Ugly. Real. |
| **1 month** | M0–M2 | Complete rounds with a winner. Playable with one friend. |
| **6 weeks** | M0–M3 | A full 5v5 match with bots, testable alone. |
| **3 months** | M0–M6 | The thing you set out to make. |

---

## Do this week

| Day | ~Hours | Goal |
|---|---|---|
| **0** | 2–4 | Compile gate. URP template, move the code in, install NGO + Multiplayer Services, fix the Console. |
| **1** | 2–3 | ProBuilder room. Capsule with `FirstPersonMotor` + `PlayerLook` + `PlayerInputReader`. **Mouse looks, WASD moves.** Commit. |
| **2** | 2–3 | `NetworkManager`, Unity Transport, a player prefab with `NetworkObject`. Host and client in the editor via **Start Host / Start Client**. |
| **3** | 2–3 | **Two capsules, two windows, both moving.** This is the day the project becomes real. |
| **4** | 2–3 | Read every script that survived. Add your own comments where you had to stop and think. None of it was written by you. |
| **5** | 2–3 | Rifle in hand. `PelletFired` → `FireRpc` → `ShotResolver`. Damage lands on both machines. |
| **6** | 2–3 | `SessionLauncher`: host, get a code, join from a build on another machine. |
| **7** | 2–3 | Three hours on feel. Then get one friend to play it. |

---

## Learning, in the order you will need it

- **M0/M2** — [Netcode for GameObjects docs](https://docs-multiplayer.unity3d.com/netcode/current/about/),
  specifically the "Golden Path" walkthrough. Do this one properly; guessing at
  netcode wastes more time than any other kind of guessing.
- **M0** — Unity's [Boss Room sample](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop),
  a complete small co-op game with NGO. Read it, do not copy it.
- **M1** — [Multiplayer Services SDK](https://docs.unity.com/en-us/mps-sdk) for
  the Relay session flow that `SessionLauncher` wraps.
- **M4** — Unity's [e-book for level designers](https://unity.com/blog/games/e-book-for-level-designers),
  which teaches greybox-first for exactly the reasons this roadmap is ordered
  the way it is.

**Two hours of tutorial maximum per feature** — except netcode, where the budget
is however long it takes to actually understand ownership, authority and RPCs.
