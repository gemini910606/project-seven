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
to run repeatedly, and pops a dialog saying what it changed.

That the `Game` menu exists at all is the compile gate's pass signal: Unity only
registers a `[MenuItem]` from an assembly that compiled, `Game.Editor` references
`Game.Runtime`, and an assembly whose reference failed to build does not compile.
So the menu appearing means every runtime script compiled and every assembly name
in the asmdef resolved.

It also runs headless, which is the fastest way to re-run it after a code change:

```
"<editor>\Unity.exe" -batchmode -quit -logFile - ^
  -projectPath "<repo>\unity" ^
  -executeMethod Game.EditorTools.ProjectBootstrap.Bootstrap
```

Only one process can hold the project lock, so close the editor first.

**8. Run `Game > Build Playable Scene`.** It writes the scene and both prefabs.
See below for what it makes and the two things it cannot do for you.

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

**Get every error at once instead of one screenshot at a time.** Unity writes
the whole compile log to `Editor.log`, so one command beats scrolling the
Console:

```powershell
# PowerShell
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" |
  Select-String "error CS" |
  ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
```

Deduplicated, in file order, paste-able. On macOS the file is
`~/Library/Logs/Unity/Editor.log`.

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

> **One trap in that pass.** Deleting a package from `Packages/manifest.json`
> while `Game.Runtime.asmdef` still names its assembly kills the *entire*
> assembly — all 29 runtime scripts at once, with an error that names the
> assembly rather than anything you touched. `Game.Runtime.asmdef` therefore
> lists only assemblies some file actually has a `using` for. If you delete a
> package, delete its line from the asmdef in the same commit.
>
> Note that `UnityEngine.AI` — `NavMeshAgent`, `NavMesh.SamplePosition` — is
> core engine (`com.unity.modules.ai`), *not* the `com.unity.ai.navigation`
> package. Keep that package anyway: it provides the `NavMeshSurface` component
> you need in the scene to bake, which is why it is in the manifest without
> being in the asmdef.

### Build the playable scene

**`Game > Build Playable Scene`.** One menu item; it writes:

```
Assets/Game/Scenes/Match.unity
Assets/Game/Prefabs/Player.prefab
Assets/Game/Prefabs/Bot.prefab
Assets/Game/Data/Weapon_Rifle.asset
```

The scene has a NetworkManager with UnityTransport and the player prefab
assigned, a ShotResolver, a NoiseSystem, the round systems on one spawned
NetworkObject, a spike, two bomb sites, five spawns a side, and enough greybox
to have somewhere to stand and something to hide behind.

This used to be a checklist of about sixty inspector drags, and every entry
under *Common first-run problems* below is one missed drag. A checklist that
long is not a setup step, it is a bug generator - so it is code now, in
`Assets/Game/Editor/SceneSetup/SceneBuilder.cs`, where the wiring shows up in a
diff and cannot be half-finished. Run it again any time; it overwrites.

Anything it could not wire automatically is listed in the dialog at the end
rather than left silently wrong.

Two things it cannot do for you:

1. **Bake a NavMesh** — `Window > AI > Navigation`, Bake. Without one the bots
   stand still.
2. **Confirm `Bot.prefab` is a registered network prefab.** Netcode normally
   adds new `NetworkObject` prefabs to `DefaultNetworkPrefabs.asset` by itself,
   but that is a setting, and spawning an unregistered prefab throws.

#### How a character is put together, and why

Worth understanding before you change it, because the layout is not the obvious
one:

```
Player                 layer: Ignore Raycast   CharacterController, Health, TeamMember
├─ Body                layer: Character        CapsuleCollider  <- body hitbox
│  └─ Mesh             layer: Ignore Raycast   visual only, no collider
├─ Head                layer: WeakPoint        SphereCollider   <- head hitbox
└─ CameraPivot         (owner only)            Camera, AudioListener, weapon
```

`CharacterHitboxes` on the root switches both hitboxes off when the character
dies and back on at the start of the next round. Nothing removes a body from the
world - there is no respawn inside a round - so without that, corpses stand
around absorbing bullets: a raycast stops at the first collider it meets, the
shot resolves against a dead Health and does nothing, and the round the bullet
was spent on is over. It finds its own colliders by layer, so there is nothing
to wire and nothing to forget.

**The movement volume and the hit volumes are different objects.** The
CharacterController wraps the whole body, so it is always the outermost surface
- a head collider placed inside it can never be the nearest hit, and headshots
would silently never register. Splitting them costs one layer and fixes it
outright. `Character` and `WeakPoint` are therefore hitbox-only layers that
collide with nothing; `Game > Bootstrap Project` sets that matrix up.

**The Mesh is hidden from the owner, not the Body.** `NetworkPlayer.remoteOnly`
does `SetActive(!owner)`, and on a host the host's own player *is* the owner -
so hiding the object that carries the hitbox would switch the host's hitbox off
on the one machine that resolves every hit, and the host would be bulletproof.

[`ARCHITECTURE.md`](ARCHITECTURE.md) explains why the components split this way.

### Playing it

**In the editor**, use NetworkManager's *Start Host*, then run a build alongside
and *Start Client* - or install Unity's Multiplayer Play Mode package to get two
players in one editor.

**Over the internet**, press Play and use the on-screen panel: **Host** returns a
six-character join code, and everyone else pastes it into **Join**. Relay handles
NAT traversal, so nobody forwards a port and nobody sees anyone else's IP.

That panel (`SessionMenu`) is deliberately IMGUI and deliberately ugly. It is the
only route to Relay - NetworkManager's own *Start Host* button connects over the
transport's configured address, which is localhost, so it works on one machine
and silently cannot reach a friend.

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

Most of these are what a hand-wired scene gets wrong, and `Game > Build Playable
Scene` prevents them. They are kept because you will edit that scene by hand
eventually, and then they come back.

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
