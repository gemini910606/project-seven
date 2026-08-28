# Setup

One deployable: the Unity game. There is no backend any more — see
[`CLOUDFLARE.md`](CLOUDFLARE.md) for what was deleted and why.

---

## 1. The Unity project

### Requirements

- **Unity 6.3 LTS (`6000.3.x`)**, installed through Unity Hub.
  6.3 is supported until December 2027; 6.0 LTS runs out in October 2026, so a
  project started now should not begin there.
- Module: **Windows Build Support (IL2CPP)**. No WebGL - a networked 5v5
  shooter is not a browser target, and nobody is playing this in a tab.
- **.NET SDK 8** if you want to run the match-rule tests outside Unity, which
  you do.
- **Git LFS** (`git lfs install`) — the repo's `.gitattributes` routes binary art
  through it.

### First open — do NOT use "Add project from disk"

`unity/ProjectSettings/` contains exactly one file, `ProjectVersion.txt`. There
is no `GraphicsSettings.asset`, which means **URP is in the package manifest but
is not the active render pipeline**. Opening this folder directly gives you a
project where every material renders magenta, and you will spend an hour finding
out why.

Create the project from Unity's own URP template instead, then move this repo's
code into it:

1. Unity Hub → **New project** → **Universal 3D** template → Unity **6000.3.x**.
   Name it `unity`, create it somewhere temporary.
2. From that new project, copy into this repo's `unity/` folder:
   - the whole `ProjectSettings/` folder (this is the part that matters — it
     carries `GraphicsSettings.asset` with URP wired up, plus quality settings),
   - `Assets/Settings/` (the URP asset and renderer),
   - `Packages/packages-lock.json`.
3. Merge this repo's `Packages/manifest.json` into the template's — or add the
   packages through Package Manager: **Netcode for GameObjects**, **Multiplayer
   Services**, **Authentication**, **Input System**, **AI Navigation**,
   **ProBuilder**. The first three are what make it a multiplayer game; the last
   one you need on day one for greyboxing.
4. Open `unity/`. Let it import. The first import takes a while.
5. **Read the Console.** See the compile gate below.
6. Run **Game → Bootstrap Project** from the menu bar. This creates the tags,
   layers and collision-matrix entries the scripts expect. It is safe to run
   repeatedly and it tells you what it changed.

> **Input System note:** when Unity asks whether to enable the new Input System
> backend, say yes and let it restart. `PlayerInputReader` builds its actions in
> code and will not work under the old backend.

### The compile gate

**Almost none of this C# has ever been compiled by Unity.** There are no `.meta`
files under `unity/Assets`, which is the proof.

The exception is `Round/Rules/`, which is genuinely compiled and genuinely
tested - `dotnet test tools/RulesTests` builds those same files and runs 25
tests against them. Everything else was written without an editor available,
structurally checked, and committed.

Expect errors on first import, and treat that as normal rather than as a
disaster. The likely categories:

- **Package API drift** — the Multiplayer Services SDK is young and moves.
  `SessionOptions`, `WithRelayNetwork()` and `JoinSessionByCodeAsync` are the
  calls to check first, against
  https://docs.unity.com/en-us/mps-sdk/create-session .
- **Missing packages** — anything referenced in `Game.Runtime.asmdef` that did
  not install will fail the whole assembly, not just one file.
- **Ordinary mistakes** in code nobody has run.

Then do the deletion pass. **Delete what you do not understand.** Code you
cannot explain costs about five times as much to debug later, and 800 lines you
wrote beat 4,000 you inherited.

### Wire up a playable scene

Nothing here ships a scene - `.unity` files are YAML that is painful to review
and impossible to merge. Build one:

1. **NetworkManager**: one GameObject with `NetworkManager` + `UnityTransport`.
   Assign the player prefab.
2. **Player prefab**: `CharacterController`, `NetworkObject`,
   `NetworkTransform`, `FirstPersonMotor`, `PlayerLook`, `PlayerInputReader`,
   `NetworkPlayer`, `Health`, `TeamMember`, `CombatantRegistration`,
   `WeaponHolder`. A `CameraPivot` child at eye height holds the camera; put it
   in `NetworkPlayer.localOnly` so only the owner sees through it, and the body
   mesh in `remoteOnly`.
3. **Weapon**: the rifle under the hand socket, with `Weapon` pointing at a
   `WeaponDefinition` asset. One `ShotResolver` in the scene.
4. **Round**: one GameObject with `NetworkObject`, `RoundDirector`,
   `TeamSpawns`, `BotDirector` and `NoiseSystem`. A `Spike` with a
   `NetworkObject`, and a `BombSite` per site.
5. **Spawns**: empty transforms parented under `TeamSpawns`, five per side.
   These are keyed by SIDE, not team - the teams swap between them at halftime.
6. **Bot prefab**: like the player prefab, minus input and camera, plus
   `NavMeshAgent`, `BotLocomotion`, `BotPerception`, `BotBrain`,
   `BotWeaponUser`. Bake a NavMesh.

[`ARCHITECTURE.md`](ARCHITECTURE.md) explains why the pieces split this way.

### Playing it

**In the editor**, use NetworkManager's *Start Host*, then run a build alongside
and *Start Client* - or install Unity's Multiplayer Play Mode package to get two
players in one editor.

**Over the internet**, `SessionLauncher.HostAsync()` returns a join code.
Whoever hosts pastes it in Discord; everyone else calls `JoinAsync(code)`. Relay
handles NAT traversal, so nobody forwards a port.

### Tests

**Window > General > Test Runner > EditMode > Run All.**

`MatchCoreTests` is the one that matters. It also runs outside Unity:

```bash
dotnet test tools/RulesTests     # 25 tests, no editor needed
```

Both run the same file. The dotnet one is the one that is currently known to
pass.

---

## 2. The domain

There is no backend to deploy. See [`CLOUDFLARE.md`](CLOUDFLARE.md) - the short
version is Email Routing, and R2 behind `dl.your-domain` for the build.

---

## Common first-run problems

**Everything is magenta.** Imported materials are Built-in, the project is URP.
`Edit → Rendering → Materials → Convert All Built-in Materials to URP`.

**The enemy stands still and does nothing.** No NavMesh under it. Bake one, and
check `EnemyLocomotion.MoveTo` is not being handed an off-mesh point — it logs
nothing when `SamplePosition` fails, by design, because it happens constantly at
world edges.

**Shots come out of the gun but miss the crosshair.** `fireOrigin` is set to the
muzzle but `PlayerAimController.AimPoint` is traced from the screen centre. That
convergence is deliberate; if it looks wrong, the muzzle transform is probably
pointing down the wrong axis.



**Two builds refuse to connect.** Version mismatch. NGO checks that both ends
run the same Netcode config; rebuild both sides from the same commit.
