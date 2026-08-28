# Architecture

Two deployables that know almost nothing about each other:

```
unity/     the game          (Unity 6.3 LTS, URP, C#)
backend/   the live service  (Cloudflare Workers, TypeScript, D1)
web/       the site          (static, Cloudflare Pages)
```

The game is playable with the backend switched off. That is a design rule, not
an accident: a leaderboard should never be able to stop someone playing.

---

## The one contract between them

`POST /v1/runs` carries an HMAC signature over a pipe-joined string:

```
runId|playerId|missionId|score|durationMs|kills
```

Built by `RunSigner.BuildPayload` (C#) and `runSignaturePayload` (TypeScript).
Both sides have a test pinning the same literal example, so a change to one
turns a test red instead of turning every player's submission into a 401.

**The signing secret ships inside the game build.** Anyone willing to open the
binary will find it. This is not a security boundary; it raises forgery from
"paste a URL into a terminal" to "reverse the client", and nothing more. The
actual defence is `backend/src/lib/antiCheat.ts`, which trusts none of the
submitted numbers and checks them for internal consistency instead — more hits
than shots fired, more kills than hits, a fire rate no weapon can produce.

The scoring rules exist on both sides and must agree. `ScoreCalculator.cs`
computes the score; `LIMITS` in `antiCheat.ts` bounds what a legitimate score
can be. Tests on both sides assert the relationship, because getting it wrong is
invisible: perfect runs simply stop appearing on the leaderboard.

---

## Game code

### The shape

```
Scripts/
  Core/       damage, health, run stats, scoring
  Player/     input, locomotion, aiming, glue
  Weapons/    weapon data, firing, spread, recoil
  AI/         perception, state machine, alert level, spawning
  Missions/   objectives, mission runtime, zones
  World/      scene streaming
  Backend/    HTTP client, run signing
  UI/         HUD
```

Three assemblies (`Game.Runtime`, `Game.Editor`, `Game.Tests.EditMode`). The
split keeps iteration compiles small and, more importantly, makes it impossible
for game code to accidentally reference `UnityEditor` and break the build.

### Rules the code follows

**One owner per concept.** `MissionDirector` is the only thing that knows what a
"run" is. `AlertSystem` is the only thing that knows what heat is. Weapons know
about bullets and nothing about scores. When a system needs to tell others
something happened, it raises an event; it does not reach for them.

That is what lets `Weapon` be used unmodified by both the player and the AI. An
enemy rifle and a player rifle have identical damage, falloff and fire rate, so
the player can reason about what is shooting at them, and one balance change
applies to both. Difficulty lives in `EnemyWeaponUser` — reaction delay, burst
rhythm, deliberate aim error — never in different numbers.

**Tuning is data, logic is code.** `WeaponDefinition`, `MissionDefinition` and
the objective types are ScriptableObjects. Balancing is opening an asset, not
editing a file and waiting for a domain reload.

The corollary, which is the classic trap: **ScriptableObjects hold no runtime
state**. `MissionObjective` is the definition; `ObjectiveProgress` is the
per-run companion. State on the asset survives between play sessions in the
editor and produces objectives that start already complete.

**Pure logic is extracted so it can be tested.** `ScoreCalculator`,
`WeaponSpread`, `WeaponRecoil` and `RunSigner` are plain C# with no
`MonoBehaviour` in sight. The EditMode suite runs them in milliseconds with no
scene, which is why there are 37 tests and not 4.

### The AI, specifically

`EnemyBrain` is a six-state `switch`: Idle, Patrol, Investigate, Combat, Search,
Dead. Not a behaviour tree — at this size a tree is more machinery than
behaviour, and six states with explicit transitions fit on one screen. Reach for
a tree somewhere north of fifteen states.

**Search is the state that matters.** An enemy that loses you and then *hunts*
reads as a person; one that instantly forgets or instantly knows reads as a
turret. Everything else is scaffolding around making that state possible:
`EnemyPerception` keeps a last-known position and a memory window,
`EnemyLocomotion.TryFindPointNear` gives it somewhere to look.

Perception scans on a fixed interval (default 0.15s), not per frame. Line-of-
sight raycasting is the single largest AI cost and the player cannot tell the
difference.

### Alert level

`AlertSystem` models heat as a continuous float with thresholds, not an integer
star count. The integer is derived. This is because the interesting design space
is the decay — sitting in a stairwell watching the meter fall, deciding whether
there is time for one more objective — and an integer cannot express it.

Enemies raise heat by *seeing* you (`EnemyBrain` on entering Combat), gunshots
raise it by being heard, kills raise it. Heat holds at the current level's floor
while anyone can still see you, so a level never flickers mid-firefight.

`SpawnDirector` turns the number into pressure. Above level 3 reinforcements
arrive already alerted, which is what stops a high alert level feeling like a
low one with more bodies.

### World streaming

`WorldStreamer` loads additive scenes by distance, with a hysteresis gap between
the load and unload radii. Without that gap a player standing on a chunk
boundary loads and unloads the same scene every few frames.

Additive scenes rather than Addressables *for the map*: scenes let you author a
district by opening it, and they keep lighting data per chunk. Addressables are
the right tool for content you want to patch remotely without a new build —
see `docs/CLOUDFLARE.md`.

---

## Backend

Hono on Workers, D1 for storage, one Durable Object.

**The leaderboard is a query, not a table.** It is derived from `runs` every
time, filtered on `flags = ''` and `banned = 0`. Banning a player, or
re-running the anti-cheat rules over history, corrects the board with no
migration and no backfill. It costs one indexed query per request, cached at
the edge for 30 seconds.

**Run submission is idempotent.** The client mints the run id before it sends,
so a retry after a dropped connection collides on the primary key and is
answered as a duplicate rather than scored twice.

**There are no passwords.** The client generates a UUID on first launch and
treats it as a bearer token. For a single-player game with a vanity leaderboard
that is the right trade: nothing valuable is behind it, and the player gets zero
friction. If real accounts are ever needed, put OAuth in front and keep this id
as the internal key.

### `LobbyRoom`, and why multiplayer does not live here

The Durable Object handles presence, chat and readiness. It is a good fit:
a single-threaded consistency point with a stable address is exactly what
matchmaking wants.

It is a **bad** fit for a shooter's authoritative simulation, and the file says
so at length:

- Workers cannot open UDP sockets. Everything is WebSocket over TCP, so one lost
  packet stalls every packet behind it. A twitch shooter needs to *drop* stale
  state, not queue it.
- All traffic relays through whichever colo the object lives in. Two players in
  the same city can pay a transcontinental round trip.

So: lobby, chat, presence, readiness and a low-rate co-op relay here; the match
itself on a host that speaks UDP (Edgegap, Hathora, a plain VM) when that day
comes. Which is not soon — see `docs/ROADMAP.md`.

---

## What is deliberately missing

- **Vehicles.** Weeks of work for the enter/exit flow, camera handoff, wheel
  physics and traffic AI. Cut from v1 on purpose.
- **A dialogue or cutscene system.** Nothing in the vertical slice needs one.
- **Multiplayer.** Starting a solo project with netcode is the most reliable way
  to never ship one.
- **An object pooler.** Hitscan weapons allocate nothing per shot. Add pooling
  when the profiler says to, not before.
