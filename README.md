# Project Seven

A compact open-world extraction shooter in Unity, with a Cloudflare-hosted
backend. One district, one rifle, and an alert level that never forgets what
you did.

> Not affiliated with, endorsed by, or connected to Rockstar Games or the Grand
> Theft Auto series.

---

## Read this before you trust any of the C#

**None of the 4,850 lines of C# in `unity/` has ever been compiled by Unity.**

There are no `.meta` files anywhere under `unity/Assets`, which is proof that no
editor has ever imported this project. The code was written without an editor
present, structurally checked, and committed. It is a *starting point that is
probably close*, not working code.

`backend/` is different — it genuinely typechecks and its 36 tests genuinely
pass, because that toolchain runs without Unity.

**So Day 0 is a compile gate, and it is allowed to fail:**

1. Open the project (see [`docs/SETUP.md`](docs/SETUP.md) — use the **URP
   template**, not "add from disk", or everything renders magenta).
2. Read the Console. Fix what does not compile.
3. **Delete anything you do not understand or do not need yet.** Inheriting
   4,850 lines of never-run code you did not write can easily be slower than
   writing 800 lines you do understand. `WorldStreamer`, `SaveSystem` and
   `LobbyRoom` are all fair game — nothing in v1 needs them.

## Start here

**[`SCOPE.md`](SCOPE.md)** — what v1 is, and the arithmetic on why it is not GTA.
**[`docs/ROADMAP.md`](docs/ROADMAP.md)** — sixteen weeks, with a day-by-day first week.

This repository contains a lot of systems and **zero playable scenes**. After the
compile gate, the next thing to do is make a capsule move on a floor — not write
more code.

> **On the repo name.** This repository is public and called `GTA7`. "GTA" is a
> Take-Two trademark and they enforce it aggressively and indiscriminately. The
> genre is completely free — copyright does not protect mechanics — but the name
> is not. Rename the repo, the folder, the domain and the build identifier to
> **Project Seven** before this gets any attention.

---

## Layout

```
unity/          the game        Unity 6.3 LTS (6000.3), URP
backend/        the API         Cloudflare Workers, TypeScript, D1
web/            the site        static, Cloudflare Pages
docs/           the reasoning
```

The game runs with the backend switched off (`BackendConfig.Enabled = false`).
A leaderboard should never be able to stop someone playing.

## Docs

| File | What it answers |
|---|---|
| [`SCOPE.md`](SCOPE.md) | What is in v1 and what is not |
| [`LATER.md`](LATER.md) | Where every good idea goes until v1 ships |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Milestones, exit criteria, week 1 day by day |
| [`docs/SETUP.md`](docs/SETUP.md) | Getting both halves running |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | How the code is organised and why |
| [`docs/ASSETS.md`](docs/ASSETS.md) | What art you have, what you need, and the licence rules |
| [`docs/CLOUDFLARE.md`](docs/CLOUDFLARE.md) | What the domain is for, with real costs |

## Quick start

```bash
# Backend — 36 tests, no Unity required
cd backend && npm install && npm test

# Game
# Unity Hub -> Add project from disk -> unity/
# then: Game -> Bootstrap Project
```

## Tests

| Suite | Count | Run with |
|---|---|---|
| Backend | 36 | `cd backend && npm test` |
| Unity EditMode | 37 | Window → General → Test Runner |

`RunSignerTests.cs` and `backend/test/crypto.test.ts` pin the **same** example
signature string from opposite sides of the wire. If either drifts, a test goes
red instead of every player silently getting a 401 on run submission.

Likewise `ScoreCalculatorTests.cs` and `backend/test/antiCheat.test.ts` assert
that the client's maximum possible score stays under the server's cheat-detection
ceiling. Getting that wrong is invisible in the worst way: perfect runs simply
stop appearing on the leaderboard.

## Assets

**Store-bought art is never committed here.** Unity Asset Store and Fab licences
are per-seat and forbid redistribution. Everything imported goes in
`unity/Assets/ThirdParty/`, which is gitignored.
[`docs/ASSETS.md`](docs/ASSETS.md) is the manifest of what to import and what to
do to it afterwards.
