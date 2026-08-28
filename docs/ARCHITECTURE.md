# Architecture

One deployable: the Unity game. `web/` is a download page. There is no backend.

```
unity/Assets/Game/Scripts/
  Round/Rules/   engine-free match rules      <- compiled and tested outside Unity
  Round/         director, spike, sites, spawns
  Player/        motor, look, input, network glue
  Weapons/       weapon data, fire control, server-side hit resolution
  Bots/          perception, locomotion, brain, director
  Core/          health, damage, teams, combatant registry
  Net/           Relay session hosting and joining
tools/RulesTests/  dotnet project that builds the rules without Unity
```

---

## The one idea worth copying from this repo

**`Round/Rules/` has no `UnityEngine` dependency, and its asmdef sets
`noEngineReferences: true` so it can never gain one by accident.**

That single constraint means `tools/RulesTests` compiles the *same source files*
with plain `dotnet test`. 25 tests, 40 milliseconds, no editor, no licence.

Round logic is where a shooter's most embarrassing bugs live — "we won but the
spike detonated", "the score went to the wrong team after halftime" — and they
are all reachable by a unit test *if the rules are not tangled up in
MonoBehaviours*. Once they are tangled, testing them needs a play-mode test,
which needs a scene, which nobody writes.

The Unity layer feeds `MatchCore` facts and reacts to its events. It never asks
Unity anything.

### Two rules that the tests exist to protect

**Side is not team.** `MatchTeam.A`/`B` persist for the match; `Side.Attackers`/
`Defenders` swap at halftime. Conflating them is why scoreboards break after the
swap. `SideOf(team)` and `TeamOn(side)` are the only legal way to move between
them.

**A planted spike changes what can end a round.** Wiping the attackers before a
plant wins the round for defenders. Wiping them *after* a plant wins nothing —
the spike is still ticking, and someone has to defuse it. The round clock stops
mattering entirely once the spike is down. `WipingTheAttackersAfterAPlantDoesNotEndTheRound`
is the test; injecting that bug turns it red.

---

## Networking

**Host-authoritative over Unity Relay.** One friend's machine is both a player
and the server. Relay punches NAT, so nobody port-forwards, nobody needs a
static IP, and nobody's home address is visible to the others. Free to 50
concurrent players; this game needs ten.

### Who decides what

| Thing | Where it happens | Why |
|---|---|---|
| Movement | Owning client, replicated | Server-authoritative movement needs prediction and reconciliation — weeks of work whose only payoff is stopping a cheating friend. |
| Fire rate, ammo, recoil, spread | Owning client | These must feel instant. Predicting them locally is the whole reason shooting feels responsive. |
| **Whether a bullet hit anyone** | **Server, always** | A kill that exists on one machine and not the others is not a bug you can live with. |
| Round state, score, spike | Server, replicated | Two machines disagreeing about who won is worse than any latency. |
| Bots | Server only | They are ordinary `NetworkObject`s; a client cannot tell a bot from a laggy human. |

`Weapon` deliberately does **not** resolve hits. It decides a shot happened and
where each pellet went, then raises `PelletFired(origin, direction)`.
`NetworkPlayer` ships that to the server over an RPC; `BotWeaponUser` — already
on the server — calls the resolver directly. Both land in the same
`ShotResolver`, so a bot's rifle and a player's rifle do identical damage under
identical rules.

### What this costs, stated plainly

**No lag compensation.** The server traces against where characters are *now*,
not where the shooter saw them. A client on 60ms has to lead a running target
very slightly, and the host — who has zero latency — wins close peeks slightly
more often. Doing this properly means rewinding every hitbox to the shooter's
timestamp. It is a large piece of work. Rotate who hosts before writing any of
it, and measure the ping before believing it is the problem.

**The host can cheat.** They run the simulation. There is no defence and there
does not need to be one.

---

## Gameplay code

**One owner per concept.** `MatchCore` is the only thing that knows what a round
is. `ShotResolver` is the only thing that changes health from a bullet.
`Combatants` is the only registry of who can shoot. Systems raise events; they
do not reach for each other.

**Tuning is data, logic is code.** `WeaponDefinition` is a ScriptableObject.
Balancing is opening an asset, not editing a file and waiting for a domain
reload.

**Bots share the players' code.** They use the same `Weapon` component, the same
`ShotResolver`, the same damage numbers. Difficulty lives entirely in
`BotWeaponUser` — reaction delay, burst rhythm, deliberate aim error — never in
giving bots better numbers. That is what makes them feel like opponents rather
than like a different game.

### The bots, specifically

`BotBrain` is a six-state switch: Idle, Advance, Engage, Search, Interact, Dead.
Not a behaviour tree; at this size a tree is more machinery than behaviour.

It replaced an open-world guard AI, and the change of job is larger than it
looks. The old brain patrolled, investigated noises and escalated a wanted
level — behaviours for a world that persists. A round-based bot has one job that
resets every round: attackers advance and plant, defenders hold and defuse, and
both fight whatever they meet.

**Search is still the state that matters.** A bot that loses you and then hunts
reads as a person; one that instantly forgets or instantly knows reads as a
turret. It is time-boxed here, because a round has a clock and a bot that hunts
forever never reaches the site.

Perception scans on an interval (0.15s), not per frame. Target selection asks
`Combatants.NearestHostile` — a list of at most ten — rather than an
`OverlapSphere` through a level full of colliders.

---

## What is deliberately missing

- **Abilities.** The largest content cost in a game like this. Each is its own
  VFX, audio, netcode and balance problem.
- **A buy phase.** Needs at least four weapons before it is a decision.
- **Lag compensation, anti-cheat, accounts, matchmaking.** All exist to serve
  strangers. There are none.
- **An object pooler.** Hitscan allocates nothing per shot. Add pooling when the
  profiler says to.
