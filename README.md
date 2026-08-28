# Project Seven

A compact open-world extraction shooter in Unity, with a Cloudflare-hosted
backend. One district, one rifle, and an alert level that never forgets what
you did.

> Not affiliated with, endorsed by, or connected to Rockstar Games or the Grand
> Theft Auto series.

---

## Start here

**[`SCOPE.md`](SCOPE.md)** — what v1 is, and the arithmetic on why it is not GTA.
**[`docs/ROADMAP.md`](docs/ROADMAP.md)** — sixteen weeks, with a day-by-day first week.

If you read nothing else, read the "uncomfortable note" at the bottom of
`SCOPE.md`. This repository currently contains a lot of well-built systems and
**zero playable scenes**. The next thing to do is open Unity and make a capsule
move on a floor — not write more code.

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
