# LATER

Everything that is not in [`SCOPE.md`](SCOPE.md).

Scope creep never arrives as "let's add an MMO". It arrives as twenty
individually reasonable decisions: *just* a second weapon, *just* a smoke
grenade, *just* one more map. Each is defensible. Each costs a week.

**The rule: every new idea comes here immediately, with one line on why it is
not v1. Nothing moves to `SCOPE.md` until v1 has shipped.**

---

## Banned from v1

| Idea | Why not now |
|---|---|
| Agent abilities (smoke, flash, walls) | The single largest content cost in a game like this. Each one is its own VFX, audio, netcode and balance problem, and balance only emerges from hundreds of matches. |
| A buy phase and economy | Needs at least four weapons before it is a decision rather than a menu. |
| A second weapon | `WeaponDefinition` already supports it. The cost is balance, animation, UI and pickup design. |
| A second map | Finish the first one. You will rebuild half of it after M3 anyway. |
| Lag compensation | Rewinding every hitbox to the shooter's timestamp is weeks of work. Rotate the host first and see if anyone actually notices. |
| Dedicated servers | Relay plus a listen server is free and does the job. Edgegap exists when it does not. |
| Anti-cheat | The host runs the simulation. There is no defence, and among friends no need. |
| Accounts, ranked, matchmaking | You know everyone playing. |
| Spectator mode | Genuinely nice, genuinely not a v1 feature. |
| Replays / demos | Requires deterministic simulation or a full state log. Large. |
| Voice chat | Discord already does this better than you will. |
| Custom keybinding UI | `PlayerInputReader` builds actions in code precisely so this can be swapped for an `InputActionAsset` later. |
| Console or mobile | Devkits, certification, and a control scheme this game was not designed for. |
| Steam release | $100 Steam Direct, and it turns friends-only into strangers-included, which brings back anti-cheat and accounts. |
| Bringing back the Cloudflare backend | It is in git history at the commit before the pivot. Restore it if the game stops being friends-only, and write it against what the game turned out to need. |

---

## Ideas parked here as they come up

<!-- Date, one line on the idea, one line on why it is not v1. -->

- _(nothing yet)_
