# Setup

Two independent halves. The game runs with no backend at all — set
`BackendConfig.Enabled = false` and skip part 2 entirely until you want a
leaderboard.

---

## 1. The Unity project

### Requirements

- **Unity 6.3 LTS (`6000.3.x`)**, installed through Unity Hub.
  6.3 is supported until December 2027; 6.0 LTS runs out in October 2026, so a
  project started now should not begin there.
- Modules: **Windows Build Support (IL2CPP)** and, if you want the browser
  demo, **WebGL Build Support**.
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
3. Merge this repo's `Packages/manifest.json` into the template's — or just add
   the extra packages through Package Manager: **Cinemachine, Input System,
   AI Navigation, Animation Rigging, ProBuilder**. (Skip Addressables; nothing in
   v1 uses it. ProBuilder you need on day one, for greyboxing.)
4. Open `unity/`. Let it import. The first import takes a while.
5. **Read the Console.** See the compile gate below.
6. Run **Game → Bootstrap Project** from the menu bar. This creates the tags,
   layers and collision-matrix entries the scripts expect. It is safe to run
   repeatedly and it tells you what it changed.

> **Input System note:** when Unity asks whether to enable the new Input System
> backend, say yes and let it restart. `PlayerInputReader` builds its actions in
> code and will not work under the old backend.

### The compile gate

**None of the C# in this repo has ever been compiled by Unity.** There are no
`.meta` files under `unity/Assets`, which is the proof. It was written without an
editor available, structurally checked, and committed.

Expect errors on first import, and treat that as normal rather than as a
disaster. The likely categories:

- **Package API drift** — `CinemachineCamera.Priority` and the
  `Unity.Cinemachine` namespace are Cinemachine 3.x; if Package Manager resolved
  a different major version, these move.
- **Missing packages** — anything referenced in `Game.Runtime.asmdef` that did
  not install will fail the whole assembly, not just one file.
- **Ordinary mistakes** in code nobody has run.

Then do the deletion pass. Nothing in v1 needs `WorldStreamer`, `SaveSystem`,
`LobbyRoom`, or `Addressables`. **Delete what you do not understand.** Code you
cannot explain costs about five times as much to debug later, and 800 lines you
wrote beats 4,850 you inherited.

### Wire up a playable scene

Nothing in this repo ships a scene — scenes are `.unity` YAML that is painful to
review and impossible to merge. Build one:

1. **Player**: a capsule with `CharacterController`, `PlayerInputReader`,
   `ThirdPersonMotor`, `PlayerAimController`, `Health`, `PlayerController`,
   `WeaponHolder`. Tag it `Player`, layer `Player`.
2. **Camera**: a `CinemachineBrain` on the Main Camera, plus two
   `CinemachineCamera`s — a hip one and a tighter aim one — both following a
   `CameraPivot` child of the player. Assign them to `PlayerAimController`.
3. **Weapon**: the rifle prefab under the hand socket, with a `Weapon` component
   pointing at a `WeaponDefinition` asset and a `Muzzle` transform.
4. **AI**: bake a NavMesh (`Window → AI → Navigation`). An enemy prefab needs
   `NavMeshAgent`, `EnemyLocomotion`, `EnemyPerception`, `EnemyBrain`, `Health`,
   `EnemyWeaponUser` and its own `Weapon`.
5. **Director**: one GameObject carrying `AlertSystem`, `NoiseSystem`,
   `SpawnDirector`, `MissionDirector` and `BackendClient`.
6. **Mission**: **Create → Game → Mission Definition**, add objectives from
   **Create → Game → Objectives → …**, and drop `ObjectiveZone` triggers in the
   scene whose ids match.

`docs/ARCHITECTURE.md` explains why the pieces are split this way.

### Tests

**Window → General → Test Runner → EditMode → Run All.** 37 tests, no scene
required. `RunSignerTests` is the important one: it pins the exact signature
string the Worker expects.

---

## 2. The Cloudflare backend

```bash
cd backend
npm install
npx wrangler login
```

Then create the resources and paste the returned ids into `wrangler.toml`:

```bash
npx wrangler d1 create gta7-db            # -> database_id
npx wrangler kv namespace create CONFIG   # -> id
npx wrangler r2 bucket create gta7-builds
```

Set the secrets. `RUN_HMAC_SECRET` must match `BackendConfig.RunSigningSecret`
in Unity:

```bash
npx wrangler secret put RUN_HMAC_SECRET
npx wrangler secret put TURNSTILE_SECRET   # optional; blank disables the check
```

Migrate and deploy:

```bash
npm run db:migrate:remote
npm run deploy
```

### Local development

```bash
cp .dev.vars.example .dev.vars
npm run db:migrate:local
npm run dev          # http://localhost:8787
```

Point `BackendConfig.BaseUrl` at `http://localhost:8787/v1` while developing.

### Verify it works

```bash
curl https://api.your-domain.com/v1/health
# {"status":"ok","environment":"production","checks":{"d1":"ok"}}
```

---

## 3. DNS

See `docs/CLOUDFLARE.md` for the full subdomain plan and what each service
costs. The short version:

| Record | Points at |
|---|---|
| `www` / apex | Cloudflare Pages (the `web/` folder) |
| `api` | the Worker (**Workers Routes → Custom Domain**) |
| `cdn` | the R2 bucket's public custom domain |
| `play` | Pages, serving the WebGL build |

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

**Run submissions return 401.** `RUN_HMAC_SECRET` and
`BackendConfig.RunSigningSecret` disagree. Run both test suites — if they pass
and submissions still 401, it is the secret, not the code.

**The WebGL build shows a black canvas.** Almost always the `Content-Encoding`
headers. `web/_headers` has them; check they were deployed with the build.
