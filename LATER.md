# LATER

Everything that is not in [`SCOPE.md`](SCOPE.md).

Scope creep never arrives as "let's add an MMO". It arrives as twenty
individually reasonable decisions: *just* a second weapon, *just* one car, *just*
a day/night cycle. Each is defensible. Each costs a week.

**The rule: every new idea comes here immediately, with one line on why it is
not v1. Nothing moves from this file to `SCOPE.md` until v1 has shipped.**

---

## Banned from v1

| Idea | Why not now |
|---|---|
| Driveable vehicles | Enter/exit flow, camera handoff, wheel physics, traffic AI. Weeks each, and none of it makes the shooting better. |
| Pedestrians / civilians | Crowd AI, plus a whole morality system nobody asked for. |
| Day/night cycle | Doubles the lighting work and every screenshot decision. |
| Weather | Same, plus VFX. |
| A second weapon | `WeaponDefinition` already supports it. Adding one costs balancing, animation, UI and pickup design. |
| Weapon shop / inventory | Needs an economy, which needs a progression loop, which needs more content than exists. |
| Story, cutscenes, dialogue | A writing and tooling problem, not a game problem. |
| Multiplayer of any kind | Multiplies every bug by the player count. `LobbyRoom.ts` is frozen. |
| A save system beyond the leaderboard | A 15-minute run does not need mid-run saves. `SaveSystem.cs` covers settings and records; that is enough. |
| Open-world streaming | `WorldStreamer.cs` exists for a second district. There is one district. |
| A second district | Finish the first one. |
| Police helicopters | Flying AI, new pathing, new audio. A great heat-level-5 payoff — for v2. |
| Melee combat | New animation set, new hit detection, new balance. |
| Full cover system | Peek, blind-fire, transitions. The single largest hidden cost in third-person shooters. |
| Console ports | Requires a devkit and a certification process. |
| Browser build | 86679 alone is 203.9 MB. Not viable without a separate low-poly art budget. |
| Behaviour-tree AI (`com.unity.behavior`) | `EnemyBrain` is six states. Revisit past fifteen. |
| Procedural city generation | Produces a lot of city and very little level. |
| Steam release | $100 Steam Direct. Ship on itch.io first and find out whether anyone wants it. |

---

## Ideas parked here as they come up

<!-- Date, one line on the idea, one line on why it is not v1. -->

- _(nothing yet)_
