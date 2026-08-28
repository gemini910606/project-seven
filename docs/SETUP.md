# Setup

One deployable: the Unity game. There is no backend any more — see
[`CLOUDFLARE.md`](CLOUDFLARE.md) for what was deleted and why.

---

## 1. The Unity project

### Requirements

- **Unity 6.3 LTS**, installed through Unity Hub. `ProjectVersion.txt` pins
  **6000.3.21f1**; if you have a different 6.3 patch, edit that file to match
  what you actually installed rather than installing a second editor. The
  package pins in `Packages/manifest.json` follow whatever that editor bundles.
  6.3 is supported until December 2027; 6.0 LTS runs out in October 2026, so a
  project started now should not begin there.
- Module: **Windows Build Support (IL2CPP)**. No WebGL - a networked 5v5
  shooter is not a browser target, and nobody is playing this in a tab.
- **.NET SDK 8** if you want to run the match-rule tests outside Unity, which
  you do.
- **Git LFS** (`git lfs install`) — the repo's `.gitattributes` routes binary art
  through it.

### First open

The repo does not carry a complete Unity project. `unity/ProjectSettings/` holds
only `ProjectVersion.txt`, so there is no `GraphicsSettings.asset` and URP is in
the manifest without being the active pipeline. Open `unity/` directly and every
material renders magenta.

So the first job is to marry this repo's code to a real, working URP project.

**1. Create the URP project.** Unity Hub -> New project -> **Universal 3D** ->
Unity 6000.3.x. Put it anywhere; it is temporary scaffolding.

**2. Delete `Assets/TutorialInfo/` from it.** That folder is the template's
welcome screen and its only script, `Readme.cs`, sits in the global namespace. It
is the single most common source of `CS0101: already contains a definition for
'Readme'` later on. Nothing depends on it.

**3. Clone this repo** somewhere separate:

```
git clone -b <branch> <repo-url> seven
```

**4. Move the working project into `seven/unity/`.** This direction matters: the
git repo becomes the Unity project, so your scene and prefab work is version
controlled from the first day rather than sitting in an untracked folder.

```
robocopy "<template-project>" "seven\unity" /E /XD Library Temp Logs /XF *.csproj *.sln *.slnx
cd seven
git checkout -- unity/Packages/manifest.json
```

`robocopy` exits with code 1 on success, which looks like a failure and is not.

The `git checkout` at the end is the step people miss: the copy overwrites this
repo's `Packages/manifest.json` with the template's, which does not list Netcode
for GameObjects, Multiplayer Services or ProBuilder. Restoring it puts them back.

**5. Add `seven/unity` to Unity Hub** and open it. Let it import; the first one
takes a while.

**6. Read the Console.** See the compile gate below.

**7. Run `Game > Bootstrap Project`** from the menu bar once it compiles. It
creates the tags, layers and collision-matrix entries the scripts expect, is safe
to run repeatedly, and tells you what it changed.

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

**Unity will offer you Safe Mode. Take it.** The dialog that says "The project
you are opening contains compilation errors" is this gate, arriving on schedule.
Safe Mode skips asset import and gives you the Console and a script editor, so a
fix-and-recompile cycle is seconds instead of minutes; Unity leaves it by itself
once the error count reaches zero. Choosing *Ignore* imports every asset on top
of a broken compile state and buries the real errors under cascading ones.

Work the errors in this order:

1. **Anything under `Library/PackageCache` first.** A package that fails to
   compile takes its whole assembly with it, so one bad package version can
   produce a hundred unrelated-looking errors that vanish together.
2. Then your own scripts, filtering the Console down to errors only.

Expect errors on first import, and treat that as normal rather than as a
disaster. The likely categories:

- **A package that is newer than your editor.** This one is worth recognising
  on sight, because the error points at Unity's own package source and looks
  like Unity is broken:

  ```
  Library\PackageCache\com.unity.inputsystem@<hash>\...\InputSystemPluginControl.cs(47,25):
  error CS0117: 'BuildTarget' does not contain a definition for 'ReservedCFE'
  ```

  Nothing is wrong with your project. `BuildTarget.ReservedCFE` is a value Unity
  added to that enum in a later editor patch; the package was built against an
  editor that has it and yours does not. **Any `CS0117`/`CS0246` pointing inside
  `Library/PackageCache` means the same thing.**

  **The rule that actually prevents this: make `ProjectVersion.txt` match the
  editor you have installed, and use the package versions that editor bundles.**
  A pin that is right for one 6.3 patch can be wrong for another - Input System
  1.20.0 became the bundled version in 6000.3.21f1, and is too new for the
  patches before it.

  Two fixes, either is fine:
  - **Lower the package** in `Packages/manifest.json` by one minor version, then
    delete `Library/PackageCache` and reopen.
  - **Raise the editor** to the newest 6.3 patch in Unity Hub.

  The versions in `Packages/manifest.json` were chosen from Unity's docs, not by
  opening the editor, so treat them as a starting point. If Package Manager
  offers a different version, it knows better than the manifest does.

- **`Cannot connect to 'download.packages.unity.com' (ECONNRESET)`.** Not a
  project problem at all — Unity could not reach its package CDN. Retry first;
  it is often transient. If it persists, check a VPN or corporate firewall, and
  set `HTTP_PROXY`/`HTTPS_PROXY` if you are behind a proxy. If the package that
  failed is `com.unity.ide.rider` and you do not use Rider, just delete that
  line from `Packages/manifest.json` — the IDE integration packages are
  convenience, not requirements, and nothing here needs them to compile.

- **Missing packages** — anything referenced in `Game.Runtime.asmdef` that did
  not install will fail the whole assembly, not just one file. So one failed
  download can look like a hundred unrelated errors.

- **Package API drift** — the Multiplayer Services SDK is young and moves.
  `SessionOptions`, `WithRelayNetwork()` and `JoinSessionByCodeAsync` are the
  calls to check first, against
  https://docs.unity.com/en-us/mps-sdk/create-session .

- **Duplicate class definitions from the template.** `CS0101 already contains a
  definition` plus `CS0579 Duplicate attribute` on the same file means that file
  exists twice. After the template merge above, the usual culprit is
  `Assets/TutorialInfo/Scripts/Readme.cs`. Find every copy with
  `dir /s /b Assets\*Readme.cs` (or `find Assets -name 'Readme.cs'`) and delete
  the whole `TutorialInfo` folder wherever it appears.

- **Ordinary mistakes** in code nobody has run.

And one thing that looks alarming and is not:

> `The following asset(s) located in immutable packages were unexpectedly
> altered` naming ProBuilder's `.mat` files is a **warning, not an error**.
> ProBuilder ships Built-in Render Pipeline materials; this project is URP, so
> Unity's material upgrader rewrote them in place. Unity did that, not you.
> Ignore it. The only consequence is that reinstalling or updating ProBuilder
> reverts them and its geometry renders magenta until you re-run
> `Edit > Rendering > Materials > Convert All Built-in Materials to URP`.

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

**Nobody can move, forever.** `RoundDirector.PlayersFrozen` is true during Prep
and Over, and `RoundPhase.Prep` is the zero value — so a director that never ran
`OnNetworkSpawn` on the server reads as a permanent prep phase and freezes
everyone. Check the `RoundDirector` GameObject has a `NetworkObject` and is
actually spawned.

**Shooting does nothing to other players.** Hits resolve on the server only
(`ShotResolver`), so look at the *host's* Console, not the client's. Two usual
causes: there is no `ShotResolver` in the scene, or `TeamMember` is missing from
one of the characters — `ShotResolver` skips friendly fire via
`TeamMember.AreHostile`, and that returns false when either side has no team, so
every shot is silently treated as friendly.

**A client connects but sees nobody.** The player prefab has no
`NetworkTransform`, or it is not assigned on the `NetworkManager`.

**Everyone spawns stacked inside each other.** `TeamSpawns` round-robins, but
only if `ResetCursors()` runs at round start — `RoundDirector` calls it, so this
means the director does not have the `TeamSpawns` reference wired.

**Bots stand still.** No NavMesh under them, or no objective assigned on
`BotDirector`. `BotLocomotion.MoveTo` deliberately logs nothing when
`NavMesh.SamplePosition` fails, because near a level's edges that happens
constantly and the log would be useless noise.

**Two builds refuse to connect.** Version mismatch. NGO checks that both ends
run the same Netcode config; rebuild both sides from the same commit.
