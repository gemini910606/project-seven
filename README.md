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
| **Match rules** (`Round/Rules/`) | **Compiled and tested.** 25 tests under plain dotnet. Mutation-checked: injecting the classic post-plant bug turns 2 of them red. |
| **Every runtime script** | **Type-checked on CI** against hand-written Unity stubs. |
| **`Game.Runtime` + `Game.Editor`** | **Compiled by a real editor** — Unity 6000.3.21f1 imported the project and the `Game` menu appeared. |
| **All behaviour** | **Never run.** Nobody has played this. |

Two gates, worth different things:

```bash
dotnet test  tools/RulesTests      # 25 tests, ~40ms  - the rules are CORRECT
dotnet build tools/SemanticCheck   # ~3s              - everything else is CONNECTED
```

The first builds `Round/Rules/` outside Unity — those files have no
`UnityEngine` dependency at all, so `dotnet test` runs the *same source*. Round
logic is where a shooter's most embarrassing bugs live, so it is the part worth
proving.

The second compiles every runtime script against stand-ins for Unity, Netcode
and the Input System. It catches a method called by a name it does not have, or
an argument list of the wrong shape — which is this project's entire bug
pattern. It **cannot** prove the code compiles in Unity; the stubs are written
from memory and are wrong in places. `tools/SemanticCheck/UnityStubs.cs` says so
at the top.

Nineteen bugs have been found by reading this code after it compiled, and not
one of them produced an error in the Console. **Compiling is not working** — see
the pull request for the list, grouped by the shapes they take.

---

## Start here

**[`SCOPE.md`](SCOPE.md)** — what v1 is, and why round-based is a far better
project than the open world this started as.
**[`docs/ROADMAP.md`](docs/ROADMAP.md)** — twelve weeks, day-by-day first week.

The roadmap's first real milestone is *two windows, one world*. Get two machines
talking in week one; multiplayer is not something you add at the end.

Two menu items build the project — `Game > Bootstrap Project`, then
`Game > Build Playable Scene` — and the Host / Join panel appears when you press
Play. [`docs/SETUP.md`](docs/SETUP.md) has the whole path.

> **Cloning this?** The branch name still says `gta5-style-open-world-shooter`
> from before the pivot. The game is not that any more; the branch was left
> alone because renaming it would mean reopening the pull request.

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
tools/RulesTests/     dotnet test project compiling the rules outside Unity
tools/SemanticCheck/  type-checks every script against Unity stubs, no editor
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
