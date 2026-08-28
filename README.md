# Project Seven

A **5v5 round-based tactical shooter** in Unity, for playing with friends. One
map, one rifle, bomb-defusal rounds, and bots that fill the empty slots so five
versus five works when three of you are online.

Host over Unity Relay, share a join code, play. No accounts, no store page, no
matchmaking, no anti-cheat.

---

## What is actually verified

| Part | Status |
|---|---|
| **Match rules** (`Round/Rules/`) | **Compiled and tested.** 25 tests pass under plain dotnet. Mutation-checked: injecting the classic post-plant bug turns 2 of them red. |
| Everything else in `unity/` | **Never compiled.** No `.meta` files exist, so no Unity editor has ever imported this project. |

That split is deliberate. The match rules have no `UnityEngine` dependency at
all, so `tools/RulesTests` builds the *same source files* with `dotnet test` —
no editor, no licence, no 20-minute import. Round logic is where a shooter's
most embarrassing bugs live, so it is the part worth proving.

```bash
dotnet test tools/RulesTests     # 25 tests, ~40ms
```

The rest is a starting point that is probably close. **Day 0 is a compile gate**
— see [`docs/ROADMAP.md`](docs/ROADMAP.md). Expect errors, fix them, and delete
anything you do not understand.

---

## Start here

**[`SCOPE.md`](SCOPE.md)** — what v1 is, and why round-based is a far better
project than the open world this started as.
**[`docs/ROADMAP.md`](docs/ROADMAP.md)** — twelve weeks, day-by-day first week.

The roadmap's first real milestone is *two windows, one world*. Get two machines
talking in week one; multiplayer is not something you add at the end.

> **On the repo name.** This is called `GTA7` and it is public. It is not a GTA
> clone any more, and "GTA" is a Take-Two trademark. Rename it.

---

## Layout

```
unity/
  Assets/Game/Scripts/
    Round/Rules/    engine-free match rules  <- the tested part
    Round/          round director, spike, bomb sites, spawns
    Player/         first-person motor, look, input, network glue
    Weapons/        weapon data, fire control, server-side hit resolution
    Bots/           perception, locomotion, brain, bot director
    Core/           health, damage, teams, combatant registry
    Net/            Relay session hosting and joining
tools/RulesTests/   dotnet test project compiling the rules outside Unity
web/                a page with a download link. That is all it needs to be.
docs/
```

## Docs

| File | What it answers |
|---|---|
| [`SCOPE.md`](SCOPE.md) | What is in v1, what the pivot cost |
| [`LATER.md`](LATER.md) | Where every good idea goes until v1 ships |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Milestones and exit criteria |
| [`docs/SETUP.md`](docs/SETUP.md) | Getting it open and connected |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | How it is organised, and the netcode's honest limits |
| [`docs/ASSETS.md`](docs/ASSETS.md) | What art to get and the licence rules |
| [`docs/CLOUDFLARE.md`](docs/CLOUDFLARE.md) | What the domain is for now (much less than before) |

## Assets

**Store-bought art is never committed here.** Unity Asset Store and Fab licences
are per-seat and forbid redistribution. Imports go in
`unity/Assets/ThirdParty/`, which is gitignored.
