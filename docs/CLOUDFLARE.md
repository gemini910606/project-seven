# What the domain is for now

Short answer: **a page with a download link, and an email address.**

That is a real answer, not a dismissal. When you pivoted to "5v5, only with
friends", almost every reason to have a backend evaporated. This file used to be
four times longer and describe a Worker, a D1 database, a leaderboard and an
anti-cheat scheme. All of it has been deleted from the repo, because it existed
to serve strangers and there are none.

---

## What went away, and why

| Was | Why it is gone |
|---|---|
| Cloudflare Worker API | Nothing to serve. Unity Relay handles the only networking. |
| D1 database | No accounts. Everyone playing is in your Discord. |
| Leaderboard | The scoreboard is in the game and lasts one match. |
| HMAC run signing and anti-cheat | The host runs the simulation and could cheat regardless. Among friends this is a social problem. |
| Turnstile | Guards a signup form that no longer exists. |
| `LobbyRoom` Durable Object | Superseded outright — see below. |

**The Durable Object lobby is the interesting deletion.** It was the right shape
for lobbies and the wrong shape for a shooter: Workers have no UDP for user
code, so everything runs over TCP WebSockets, where one lost packet stalls every
packet behind it. Unity Relay carries genuine UDP, punches NAT, and is free up
to 50 concurrent players. It is simply the better tool, and it is the one the
game now uses.

---

## What the domain still earns

**Email Routing — do this first.** Thirty minutes, free, and you get
`hello@your-domain` forwarding to your existing inbox. Add SPF and DMARC
(`p=reject` if you never send from the domain).

**R2 for the build.** Storage is $0.015/GB-month and **egress is free**. A 2 GB
build costs nothing at all inside the 10 GB free tier, and your friends
re-downloading it every patch costs nothing either. Put it behind a custom
domain at `dl.your-domain` and paste that link in Discord.

> Do not orange-cloud a third-party file host to get free CDN bandwidth. The
> CDN terms require large non-HTML files to be hosted *on* a Cloudflare service.
> Put the build in R2.

**Pages for one page.** `web/` holds a download link and a screenshot. Free.

**Cloudflare Access, if you want it.** Free up to 50 users. Put it in front of
`dl.your-domain` and your build is only downloadable by email addresses on a
list you control. Two hours of setup, replaces an entire auth system, and means
you can share the link without it being *public*.

---

## Cost

**$0/month**, plus roughly $10/year for the domain at Cloudflare Registrar.

Everything above sits inside free tiers with room to spare. There is no Workers
Paid plan to buy any more, because there is no Worker.

---

## If you ever change your mind

Should this stop being friends-only, the parts you would need back — accounts,
a persistent leaderboard, server-side plausibility checks — are in this repo's
git history, tested, at the commit before the pivot. `git log -- backend/`.

They were deleted rather than kept "just in case" on purpose: unused code is a
liability, and a backend nobody calls rots faster than one that is exercised.
Bringing it back is a `git checkout` away and would be a better version anyway,
written against what the game turned out to need.
