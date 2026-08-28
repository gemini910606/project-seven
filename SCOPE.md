# SCOPE — Project Seven

**A 5v5 round-based tactical shooter, for you and your friends.** No store page,
no players you have not met, no anti-cheat.

This file is the contract. If a thing is not on this page, it is not in v1.
New ideas go in [`LATER.md`](LATER.md), immediately.

---

## Why this is a far better project than the open world was

The previous plan was a GTA-like open world. The arithmetic on that was brutal:
GTA V took over 1,000 people about five years, roughly 5,000 person-years, and a
solo developer at 12 hours a week produces about 0.3 of one per year.

**Round-based deletes almost all of that cost.** There is no city, no streaming,
no traffic, no pedestrians, no missions, no save system, no open world at all —
one map, and the same map every round. What is left is the part that was always
going to decide whether the game was fun: the shooting.

**And "only with friends" deletes the rest.** No accounts, no matchmaking, no
leaderboard, no anti-cheat, no dedicated servers, no store page, no trademark
exposure. One friend's machine is the server. A join code goes in Discord.

The honest trade is that you swap a content problem for an engineering one:
multiplayer. Among friends that engineering problem is small — see below.

---

## V1 — what is in

**One map.** Small and deliberate. Two bomb sites, three lanes between spawns.
Roughly the footprint of a single Valorant site complex, not a district.
Greyboxed in ProBuilder first; dressed with the industrial kit afterwards.

**One weapon.** The free Unity Asset Store rifle. Hip-fire and aimed, with
reload. Movement wrecks your accuracy; standing still and walking do not.

**5v5, bots fill the gaps.** This is the feature that makes the whole thing
workable: three friends online means seven bots, and you can test a complete
match alone at 3am. `BotDirector` tops both teams up between rounds.

**Bomb-defusal rounds.** Attackers plant the spike, defenders defuse it. One
life per round. First to 7 round wins; sides swap after 6.

**No abilities.** Pure gunplay, the CS lineage rather than the Valorant one.
Abilities are the single largest content cost in a game like this — each one is
its own VFX, audio, netcode and balance problem. The architecture leaves room;
v1 does not spend it.

**Host-authoritative networking** over Unity Relay. Free, no port forwarding, no
static IP, nobody's home address exposed to anyone else.

---

## Explicitly not in v1

Full list in [`LATER.md`](LATER.md). Headlines:

- **No abilities.** See above.
- **No buy phase or economy.** Everyone gets the same rifle. An economy needs at
  least four weapons to be a decision at all.
- **No ranked, matchmaking, or accounts.** You know everyone playing.
- **No anti-cheat.** The host runs the simulation and could trivially cheat.
  Among friends that is a social problem, not an engineering one.
- **No second map.** Finish the first one.
- **No lag compensation.** The server traces against where players are *now*, so
  the host wins close peeks slightly more often. Rotate the host if it bothers
  anyone. Doing it properly means rewinding every hitbox, which is weeks.
- **No dedicated servers.** Relay plus a listen server is free and sufficient.

---

## What the pivot cost, honestly

The repo previously held 4,850 lines of C# for an open-world extraction shooter.
About 2,800 of those are now deleted: the mission system, world streaming, the
alert/wanted system, the save system, the score submission, the whole Cloudflare
backend, and the third-person motor.

What survived and changed jobs:

| Was | Is now |
|---|---|
| `EnemyBrain` and friends — open-world guards | `BotBrain` — bots that play the round objective |
| `NoiseSystem` — AI hearing gunshots | Unchanged, and now also hears footsteps |
| `WeaponDefinition` / `Spread` / `Recoil` | Unchanged; the spread model already suited a tactical shooter |
| `Health`, `DamageInfo`, `DamageResolver` | Unchanged |
| `ThirdPersonMotor` | Replaced by `FirstPersonMotor` — different game, different feel |

The AI surviving is not luck. "Enemies that see you, hunt you and shoot back"
is the same problem whether they guard a warehouse or hold a bomb site.

---

## The thing to keep watching

The old scope doc had to warn that the project was all systems and no game.
That is still true, and the pivot did not fix it: **there is still not one
`.unity` scene in this repository.**

What did change is that the round rules are now genuinely verified —
`tools/RulesTests` compiles and runs `MatchCore` under plain dotnet, and 25
tests pass. That is real. The Unity half still has never been compiled.

Track one number weekly: **how many minutes can five people actually play?**

| Week | Target |
|---|---|
| 2 | Two clients connect, both see each other move |
| 4 | A full round resolves: plant, defuse, someone wins |
| 8 | A whole match with bots filling both teams |
| 12 | Five friends play three matches and want a fourth |
