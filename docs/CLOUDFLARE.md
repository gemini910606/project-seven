# What the domain is actually for

Prices and limits below were checked in **August 2026**. Cloudflare moves; re-check
before you rely on a number.

---

## The short answer

For the next twelve months your domain's job is a **devlog, a press kit, an
email address and a download link**. That is not a small thing — it is how
anyone finds out the game exists — and none of it needs Workers, D1 or Durable
Objects.

The backend in `backend/` exists so that when you *do* want a leaderboard, it is
already written and tested. Ship the game first.

**Realistic all-in cost: about $6/month**, with zero bandwidth charges.

---

## The one genuinely great fit: R2 for downloads

R2 charges **$0.015/GB-month** for storage and **nothing for egress**. Not
"cheap egress" — zero, on the published price sheet.

- A 20 GB build stored: **~$0.15/month** after the 10 GB free tier.
- 10,000 players each downloading 5 GB (50 TB): **$0**.
- The same 50 TB on AWS S3: roughly **$4,000**.

This is the strongest single reason to have this project on Cloudflare, and it
applies from day one.

It also matters for **Unity Addressables**. Addressables officially supports any
third-party CDN as its remote host: enable *Build Remote Catalog*, point
`RemoteLoadPath` at `cdn.<your-domain>`, and you can ship patch content without
a new build, for free.

> **Do not orange-cloud someone else's file host** (Drive, a cheap VPS, MEGA) to
> get free CDN bandwidth for downloads. The old ToS §2.8 is gone, but the
> replacement CDN terms still require large non-HTML files to be hosted *on* a
> Cloudflare service. Put the build in R2 and serve it from an R2 custom domain.
> Getting the zone suspended mid-launch is a self-inflicted wound.

---

## DNS layout

| Subdomain | Points at | Why separate |
|---|---|---|
| apex + `www` | **Pages** — devlog, press kit, mailing list | The thing that actually matters right now |
| `play` | **Pages** — WebGL loader HTML only | See the WebGL section; the big files are not here |
| `api` | **Worker** custom domain — REST | |
| `ws` | **Worker** route — WebSocket / Durable Object upgrades | Rate-limit and WAF rules that suit JSON POSTs break long-lived upgrades |
| `cdn` | **R2** public bucket — Addressables, patch content | Cache aggressively; filenames are hashed and immutable |
| `dl` | **R2** custom domain — installers | Separate cache rules, clean download analytics, revocable URLs |
| `builds` | **private R2** behind **Cloudflare Access** | Playtesters, email OTP, free up to 50 seats |
| `status` | DNS-only CNAME to a third-party status page | A status page hosted on the infrastructure it reports on is useless |

Turn on **Email Routing** first. Thirty minutes, free, and `press@your-domain`
is the single most professional-looking upgrade an indie project can make. Add
SPF and DMARC (`p=reject` if you never send from the domain) so nobody spoofs
your domain in a fake key giveaway.

---

## Costs, itemised

| Service | Free tier | Paid | What you will actually pay |
|---|---|---|---|
| **Workers** | 100k req/day, **10 ms CPU per invocation** | $5/mo min: 10M req, 30M CPU-ms | **$5/mo.** The free tier's 10 ms CPU cap is a real trap for JSON validation plus a few D1 queries. |
| **D1** | 500 MB/db, 5M rows read/day, 100k written/day | 10 GB/db, 25B reads + 50M writes/mo included | **$0.** A hobby leaderboard is nowhere near this. |
| **R2** | 10 GB-month, 1M Class A, 10M Class B | $0.015/GB-mo, **$0 egress** | **~$0.15–0.50/mo** |
| **Durable Objects** | (paid plan) | 1M req/mo, 400k GB-s/mo included | **$0.** SQLite-backed DO storage billing started 2026-01-07; a hobby game stays inside the included tier. |
| **KV** | 100k reads/day but only **1,000 writes/day** | 10M reads/mo; **writes are $5/M — 10× reads** | **$0.** Config and feature flags only. |
| **Pages** | 500 builds/mo, 1 concurrent | | **$0** |
| **Access (Zero Trust)** | 50 users | | **$0** |
| **Email Routing** | unlimited | | **$0** |
| **Turnstile** | 20 widgets, unlimited verifications | | **$0** |
| **Stream** | **none** | $5/1,000 min stored + $1/1,000 delivered | **$0 — use YouTube.** Stream gives you no discovery, and enabling it "to try" starts a subscription. |

Plus the domain, roughly $10/year at Cloudflare Registrar (at cost).

**Total: ~$6/month.**

---

## Realtime multiplayer: the honest version

**Cloudflare cannot host a shooter's authoritative simulation.** Not "it is hard"
— the transport does not exist.

- **Workers have no UDP for user code.** Socket Workers were announced in 2021
  and still have not shipped. The August 2026 gRPC/inbound-TCP beta explicitly
  excludes inbound UDP and QUIC.
- **WebSockets run over TCP**, so one lost packet stalls every message behind
  it. At 60 Hz with 2% loss that is visible, regular hitching, and it is not
  fixable by tuning.
- **Spectrum**, the only Cloudflare product that proxies arbitrary UDP, is
  Enterprise-only.

What Durable Objects *are* good at:

| Use | Rate | Verdict |
|---|---|---|
| Lobby, chat, presence, readiness, matchmaking queue | 1–5 Hz | **Correct tool.** WebSocket-native, one consistent object per room, nearly free while idle thanks to the Hibernation API. |
| Slow co-op PvE relay, 2–8 players | 10–20 Hz | Marginal. Try it, measure it. |
| Competitive authoritative FPS | 60 Hz | **No. Do not try to make this work.** |

> **A Durable Object never moves.** Its datacenter is fixed by the *first*
> `get()` for that id, permanently — there is no move API. For a Taiwan-based
> playerbase, always pass `locationHint: 'apac-ne'`. `backend/src/index.ts` does
> this and also encodes the region into the object name so a room can never be
> created in the wrong place. Forget it once and a lobby first touched from
> Frankfurt costs every Taipei player ~250 ms for its entire life.

### When you do need real netcode

Split it: **lobby and matchmaking on Durable Objects, simulation somewhere that
speaks UDP.**

| Option | Cost | Notes |
|---|---|---|
| **Photon Fusion / Quantum** | **100 CCU free for commercial use**; $95/yr for 200 CCU | Least ops work by a wide margin. Start here. |
| **Unity Gaming Services Relay** | 50 average CCU free, then $0.16/CCU | Integrates natively with Netcode for GameObjects. |
| **Edgegap** | ~$0.069/vCPU-hr + $0.10/GB egress, $1/mo min | Right shape if you want your own authoritative server binary. |
| **Fly.io** | ~$1.94/mo minimum | Real UDP, but needs a dedicated IPv4, port numbers must match, MTU drops to ~1300, and you own scaling and DDoS exposure. |

> **Hathora is dead.** Frozen on acquisition in March 2026, permanently shut down
> 2026-05-05. Every 2024–2025 tutorial recommending it is stale. Providers churn:
> put netcode behind a transport interface (Netcode for GameObjects, FishNet,
> Mirror) so swapping is a config change.

There is one more path worth knowing: **Cloudflare Realtime TURN** does relay
genuine UDP — anycast in 250+ cities, **1,000 GB/month free**, then $0.05/GB.
Paired with `com.unity.webrtc` DataChannels in unreliable+unordered mode it
gives P2P co-op with NAT traversal on a Cloudflare bill. It is a *relay*, not a
server, so the host client can cheat — fine for 2–8 player co-op PvE, wrong for
anything competitive.

---

## WebGL on Pages: two hard limits

1. **25 MiB per file** (and 20,000 files per deployment on Free). Unity emits one
   `Build.data` and one `Build.wasm`, both of which exceed this for anything
   beyond a trivial scene. A naive `pages deploy` of a real build **fails**.
   Keep `index.html` and the loader on Pages; put `Build/*` in R2 behind `cdn.`
   and point the loader config there.
2. **Browsers cap out around 2–4 GB of heap** and there is no streaming install.
   A GTA-scale world is not a realistic WebGL target *at all*. What works is a
   deliberately small slice — a shooting range, one building — as a demo that
   converts into a download.

`web/_headers` carries the `Content-Encoding` rules for `.br`/`.gz`, which
Cloudflare will not infer from the extension. Without them the browser downloads
a Brotli blob, tries to parse it as WebAssembly, and you get a black canvas.

---

## Anti-cheat: what is and is not achievable

**Every secret compiled into a Unity build is public.** IL2CPP is not
encryption. HMAC keys and "obfuscated" constants come out in under an hour with
Il2CppDumper, and Cheat Engine edits memory regardless. Design on the assumption
that a determined player can forge any client-side value.

What is worth doing, in order of value per hour:

1. **Plausibility bounds server-side** (`backend/src/lib/antiCheat.ts`). More
   hits than shots, more kills than hits, a fire rate no weapon produces, a run
   shorter than the extraction walk. Catches essentially all casual cheating.
2. **Derive the leaderboard from raw runs**, so a ban or a rule change corrects
   history with no migration. Already how it works.
3. **Rate-limit per account in D1.** Not per IP, and *not* with the Workers
   rate-limiting binding — that counts **per colo**, so a distributed attacker
   multiplies your limit by the number of datacenters they hit. Free-plan WAF
   rate limiting is worse: one rule, IP only.
4. **Archive raw run telemetry in R2** so you can invalidate retroactively.
5. **HMAC signing** (`RunSigner.cs`). Raises forgery from "paste a URL into a
   terminal" to "reverse the client". Worth the hour it costs; worth nothing
   more than that.

> **Turnstile is a browser widget.** There is no supported way to render it in a
> Unity standalone build without embedding a webview. Use it on the web signup
> and leaderboard pages; have the game client authenticate with a token issued
> after a web login.

---

## One thing that could delete most of this

**If you ship on Steam, Steam already gives you** CDN, patching, accounts,
achievements, cloud saves and leaderboards, free, for the 30% cut you are paying
anyway ($100 Steam Direct fee up front).

That shrinks the Cloudflare backend to a marketing site and a download page.
Decide the storefront *before* building auth, not after.
