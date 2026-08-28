using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Game.Bots;
using Game.Core;
using Game.Net;
using Game.Player;
using Game.Round;
using Game.Weapons;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the playable scene, the player prefab and the bot prefab in code.
    ///
    /// This exists for the same reason <see cref="ProjectBootstrap"/> does. A
    /// hand-wired scene is a checklist of about sixty inspector drags, and every
    /// one of the "common first-run problems" in docs/SETUP.md is a single missed
    /// drag: nobody can move (RoundDirector unspawned), shooting does nothing
    /// (no ShotResolver), everyone spawns stacked (TeamSpawns unassigned). A
    /// checklist that long is not a setup step, it is a bug generator.
    ///
    /// Doing it here makes the wiring reviewable in a diff, repeatable after a
    /// mistake, and - the part that matters most - impossible to half-finish.
    ///
    /// Deliberately NOT in the Game.Editor assembly. This file is new and has
    /// never been compiled; keeping it separate means a mistake in here cannot
    /// take Game > Bootstrap Project down with it.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/Game/Scenes";
        private const string PrefabsFolder = "Assets/Game/Prefabs";
        private const string DataFolder = "Assets/Game/Data";

        private const string ScenePath = ScenesFolder + "/Match.unity";
        private const string PlayerPrefabPath = PrefabsFolder + "/Player.prefab";
        private const string BotPrefabPath = PrefabsFolder + "/Bot.prefab";
        private const string RiflePath = DataFolder + "/Weapon_Rifle.asset";

        /// <summary>Anything that could not be wired automatically, reported at the end.</summary>
        private static readonly List<string> Warnings = new();

        [MenuItem("Game/Build Playable Scene", priority = 1)]
        public static void Build()
        {
            if (LayerMask.NameToLayer("Character") < 0)
            {
                EditorUtility.DisplayDialog(
                    "Build Playable Scene",
                    "Run Game > Bootstrap Project first.\n\n" +
                    "The layers this scene needs do not exist yet, and building on top " +
                    "of missing layers produces a scene that looks right and does not work.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Warnings.Clear();

            EnsureFolder("Assets/Game", "Scenes");
            EnsureFolder("Assets/Game", "Prefabs");
            EnsureFolder("Assets/Game", "Data");

            WeaponDefinition rifle = EnsureRifle();
            GameObject playerPrefab = BuildPlayerPrefab(rifle);
            GameObject botPrefab = BuildBotPrefab(rifle);

            BuildScene(playerPrefab, botPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report();
        }

        // ----------------------------------------------------------------
        // Scene
        // ----------------------------------------------------------------

        private static void BuildScene(GameObject playerPrefab, GameObject botPrefab)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCameraAndLighting();
            BuildGreybox();

            GameObject shotResolverGo = new("ShotResolver");
            ShotResolver resolver = shotResolverGo.AddComponent<ShotResolver>();
            // Bullets see hitboxes and the world, and nothing else. The movement
            // capsule lives on Ignore Raycast on purpose - see BuildCharacterBase.
            SetInt(resolver, "hitMask", MaskOf("Character", "WeakPoint", "Environment"));
            SetInt(resolver, "weakPointMask", MaskOf("WeakPoint"));

            new GameObject("NoiseSystem").AddComponent<NoiseSystem>();

            // --- objective geometry ---
            BombSite siteA = BuildSite("Site A", "A", new Vector3(-14f, 0f, 12f));
            BombSite siteB = BuildSite("Site B", "B", new Vector3(14f, 0f, 12f));

            GameObject spikeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spikeGo.name = "Spike";
            spikeGo.transform.position = new Vector3(0f, 0.35f, -14f);
            spikeGo.transform.localScale = new Vector3(0.4f, 0.55f, 0.25f);
            spikeGo.layer = LayerMask.NameToLayer("Interactable");
            Object.DestroyImmediate(spikeGo.GetComponent<BoxCollider>());
            spikeGo.AddComponent<NetworkObject>();
            // CompletePlant moves the spike to wherever it was planted, and without
            // this that move is server-only: clients keep seeing it at its starting
            // position and defenders walk to the wrong place.
            spikeGo.AddComponent<NetworkTransform>();
            Spike spike = spikeGo.AddComponent<Spike>();
            SetRefArray(spike, "sites", new Object[] { siteA, siteB });

            // --- match systems ---
            GameObject matchGo = new("Match");
            matchGo.AddComponent<NetworkObject>();
            TeamSpawns spawns = matchGo.AddComponent<TeamSpawns>();
            RoundDirector director = matchGo.AddComponent<RoundDirector>();
            BotDirector bots = matchGo.AddComponent<BotDirector>();

            List<Object> attackerSpawns = BuildSpawnRow(matchGo.transform, "Attacker Spawn", new Vector3(0f, 0f, -22f));
            List<Object> defenderSpawns = BuildSpawnRow(matchGo.transform, "Defender Spawn", new Vector3(0f, 0f, 20f));

            SetRefArray(spawns, "attackerSpawns", attackerSpawns);
            SetRefArray(spawns, "defenderSpawns", defenderSpawns);

            SetRef(director, "spawns", spawns);
            SetRef(director, "spike", spike);

            SetRef(bots, "botPrefab", botPrefab);
            SetRef(bots, "spawns", spawns);
            SetRef(bots, "objective", siteA.transform);

            // --- networking ---
            GameObject netGo = new("NetworkManager");
            NetworkManager manager = netGo.AddComponent<NetworkManager>();
            UnityTransport transport = netGo.AddComponent<UnityTransport>();
            netGo.AddComponent<SessionLauncher>();

            SetRef(manager, "NetworkConfig.PlayerPrefab", playerPrefab);
            SetRef(manager, "NetworkConfig.NetworkTransport", transport);

            // The bot prefab has to be a registered network prefab or spawning one
            // throws at runtime. Netcode's own auto-registration usually catches it,
            // but it is a setting and settings get turned off.
            Warnings.Add(
                "Confirm Bot.prefab is in the NetworkManager's Network Prefabs list. " +
                "Netcode normally adds new NetworkObject prefabs to DefaultNetworkPrefabs.asset " +
                "by itself; if it did not, drag it in.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
        }

        private static void BuildCameraAndLighting()
        {
            GameObject cameraGo = new("Scene Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 12f, -26f);
            cameraGo.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
            Camera sceneCamera = cameraGo.AddComponent<Camera>();
            // Behind every player camera, so it stops mattering the moment one
            // spawns. Without it the Game view is black until Start Host, which
            // looks like a broken build rather than an empty server.
            sceneCamera.depth = -10f;
            // No AudioListener on purpose: the player's camera carries the only one.

            GameObject sunGo = new("Directional Light");
            sunGo.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.1f;
        }

        /// <summary>
        /// Enough greybox to have somewhere to stand and something to hide behind.
        /// Not a level - a level is a week of ProBuilder work and belongs to whoever
        /// is playing, not to a setup script.
        /// </summary>
        private static void BuildGreybox()
        {
            int environment = LayerMask.NameToLayer("Environment");

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            ground.layer = environment;

            GameObject cover = new("Cover");
            Vector3[] blocks =
            {
                new(-8f, 1f, 0f), new(8f, 1f, 0f), new(0f, 1f, 6f), new(0f, 1f, -6f),
                new(-14f, 1f, 6f), new(14f, 1f, 6f), new(-5f, 1f, -12f), new(5f, 1f, -12f),
            };

            foreach (Vector3 position in blocks)
            {
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Block";
                block.transform.SetParent(cover.transform);
                block.transform.position = position;
                block.transform.localScale = new Vector3(3f, 2f, 1.2f);
                block.layer = environment;
            }
        }

        private static BombSite BuildSite(string name, string siteName, Vector3 position)
        {
            GameObject go = new(name);
            go.transform.position = position;

            // BombSite has [RequireComponent(typeof(BoxCollider))], so this is
            // already there by the time AddComponent returns.
            BombSite site = go.AddComponent<BombSite>();
            SetString(site, "siteName", siteName);

            BoxCollider box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(9f, 4f, 9f);
            box.center = new Vector3(0f, 2f, 0f);

            return site;
        }

        private static List<Object> BuildSpawnRow(Transform parent, string prefix, Vector3 centre)
        {
            List<Object> spawns = new();
            GameObject row = new(prefix + "s");
            row.transform.SetParent(parent);

            for (int i = 0; i < 5; i++)
            {
                GameObject spawn = new($"{prefix} {i + 1}");
                spawn.transform.SetParent(row.transform);
                spawn.transform.position = centre + new Vector3((i - 2) * 2.2f, 0f, 0f);
                // Facing the middle of the map, so nobody starts looking at a wall.
                spawn.transform.rotation = Quaternion.LookRotation(-centre.normalized);
                spawns.Add(spawn.transform);
            }

            return spawns;
        }

        private static void AddToBuildSettings()
        {
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath) return;
            }

            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes)
            {
                new(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ----------------------------------------------------------------
        // Prefabs
        // ----------------------------------------------------------------

        private static GameObject BuildPlayerPrefab(WeaponDefinition rifle)
        {
            GameObject root = BuildCharacterBase("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();
            SetInt(motor, "ceilingMask", MaskOf("Environment"));

            PlayerLook look = root.AddComponent<PlayerLook>();
            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            WeaponHolder holder = root.AddComponent<WeaponHolder>();
            NetworkPlayer player = root.AddComponent<NetworkPlayer>();

            // Eye height matches PlayerLook.standingEyeHeight; the camera is the
            // only thing the owner should see through and the only thing that
            // hears, which is why the AudioListener rides on it.
            GameObject pivot = new("CameraPivot");
            pivot.transform.SetParent(root.transform);
            pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            GameObject cameraGo = new("Camera");
            cameraGo.transform.SetParent(pivot.transform);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 90f;
            camera.nearClipPlane = 0.05f;
            camera.depth = 0f;
            cameraGo.AddComponent<AudioListener>();

            GameObject socket = new("HandSocket");
            socket.transform.SetParent(pivot.transform);
            socket.transform.localPosition = new Vector3(0.24f, -0.2f, 0.32f);

            Weapon weapon = BuildRifle(socket.transform, rifle);

            SetRef(look, "cameraPivot", pivot.transform);
            SetRef(holder, "handSocket", socket.transform);
            SetRefArray(holder, "weapons", new Object[] { weapon });

            SetRef(player, "input", input);
            SetRef(player, "weapons", holder);
            SetRefArray(player, "localOnly", new Object[] { pivot });
            // The MESH, not the Body object that carries the hitbox. remoteOnly is
            // SetActive(!owner), and on a host the host's own player is the owner -
            // so hiding the hitbox here would switch off the host's own hitbox on
            // the exact machine that resolves every hit, making the host bulletproof.
            SetRefArray(player, "remoteOnly", new Object[] { root.transform.Find("Body/Mesh").gameObject });

            // shotResolver and spike are scene objects and stay empty here on
            // purpose - a prefab asset cannot hold a scene reference, Unity drops
            // it without a word. Both components find theirs at spawn.

            SetAuthorityToOwner(root.GetComponent<NetworkTransform>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildBotPrefab(WeaponDefinition rifle)
        {
            GameObject root = BuildCharacterBase("Bot");

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            // BotLocomotion has [RequireComponent(typeof(NavMeshAgent))].
            BotLocomotion locomotion = root.AddComponent<BotLocomotion>();
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = 4.6f;
            agent.angularSpeed = 480f;
            agent.acceleration = 24f;
            // BotLocomotion turns the body itself so the bot faces what it is
            // shooting at rather than where it is walking.
            agent.updateRotation = false;

            BotPerception perception = root.AddComponent<BotPerception>();
            SetInt(perception, "occlusionMask", MaskOf("Environment"));

            GameObject eyes = new("Eyes");
            eyes.transform.SetParent(root.transform);
            eyes.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            SetRef(perception, "eyes", eyes.transform);

            GameObject socket = new("HandSocket");
            socket.transform.SetParent(eyes.transform);
            socket.transform.localPosition = new Vector3(0.24f, -0.2f, 0.32f);
            Weapon weapon = BuildRifle(socket.transform, rifle);

            BotWeaponUser weaponUser = root.AddComponent<BotWeaponUser>();
            SetRef(weaponUser, "weapon", weapon);
            SetRef(weaponUser, "fireOrigin", eyes.transform);

            // BotBrain requires BotPerception, BotLocomotion, Health and TeamMember,
            // so it goes on last.
            BotBrain brain = root.AddComponent<BotBrain>();
            SetRef(brain, "weaponUser", weaponUser);

            // Switches all of the above off on anything that is not the server.
            root.AddComponent<BotServerAuthority>();

            SetAuthorityToServer(root.GetComponent<NetworkTransform>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// The parts a player and a bot share: identity, health, and the hitboxes
        /// bullets actually test against.
        ///
        /// The root sits on Ignore Raycast rather than Character. That is the whole
        /// trick to making headshots possible: the movement capsule wraps the entire
        /// body, so a head collider placed inside it can never be the closest hit and
        /// headshots would silently never register. Separating "the volume that
        /// stops you walking through walls" from "the volumes bullets test against"
        /// costs one layer and fixes it completely.
        /// </summary>
        private static GameObject BuildCharacterBase(string name)
        {
            GameObject root = new(name) { layer = LayerMask.NameToLayer("Ignore Raycast") };

            root.AddComponent<Health>();
            root.AddComponent<TeamMember>();
            root.AddComponent<CombatantRegistration>();
            root.AddComponent<CharacterHitboxes>();

            GameObject body = new("Body") { layer = LayerMask.NameToLayer("Character") };
            body.transform.SetParent(root.transform);
            CapsuleCollider bodyBox = body.AddComponent<CapsuleCollider>();
            bodyBox.radius = 0.3f;
            bodyBox.height = 1.55f;
            bodyBox.center = new Vector3(0f, 0.775f, 0f);

            GameObject head = new("Head") { layer = LayerMask.NameToLayer("WeakPoint") };
            head.transform.SetParent(root.transform);
            SphereCollider headBox = head.AddComponent<SphereCollider>();
            headBox.radius = 0.17f;
            headBox.center = new Vector3(0f, 1.68f, 0f);

            // Something to look at. A capsule is not a character model, but an
            // invisible opponent is impossible to playtest against.
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "Mesh";
            mesh.transform.SetParent(body.transform);
            mesh.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            mesh.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            mesh.layer = LayerMask.NameToLayer("Ignore Raycast");
            Object.DestroyImmediate(mesh.GetComponent<CapsuleCollider>());

            return root;
        }

        private static Weapon BuildRifle(Transform parent, WeaponDefinition definition)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Rifle";
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(0.07f, 0.12f, 0.75f);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());

            GameObject muzzle = new("Muzzle");
            muzzle.transform.SetParent(go.transform);
            // The cube is 0.75 long, so half its local length puts this at the tip.
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            Weapon weapon = go.AddComponent<Weapon>();
            SetRef(weapon, "definition", definition);
            SetRef(weapon, "muzzle", muzzle.transform);
            return weapon;
        }

        private static WeaponDefinition EnsureRifle()
        {
            WeaponDefinition existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RiflePath);
            if (existing != null) return existing;

            WeaponDefinition rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            rifle.DisplayName = "Rifle";
            AssetDatabase.CreateAsset(rifle, RiflePath);
            return rifle;
        }

        // ----------------------------------------------------------------
        // Serialized-field plumbing
        // ----------------------------------------------------------------
        //
        // Every reference below is a private [SerializeField], so it has to be set
        // through SerializedObject. Each setter checks that the property exists and
        // records a warning instead of throwing: a renamed field then costs one
        // manual drag, not a broken menu item and a stack trace.

        private static bool SetRef(Object target, string path, Object value)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(path);
            if (property == null) return Missing(target, path);

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetRefArray(Object target, string path, IList<Object> values)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(path);
            if (property == null) return Missing(target, path);

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetInt(Object target, string path, int value)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(path);
            if (property == null) return Missing(target, path);

            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetString(Object target, string path, string value)
        {
            SerializedObject so = new(target);
            SerializedProperty property = so.FindProperty(path);
            if (property == null) return Missing(target, path);

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Netcode's authority field has moved between versions, so this is
        /// best-effort: if the property is not there, the transform still works,
        /// it is just server-authoritative and the owner's own movement will
        /// rubber-band until it is set by hand.
        /// </summary>
        private static void SetAuthorityToOwner(NetworkTransform transform) =>
            SetAuthority(transform, "Owner");

        private static void SetAuthorityToServer(NetworkTransform transform) =>
            SetAuthority(transform, "Server");

        private static void SetAuthority(NetworkTransform transform, string mode)
        {
            if (transform == null) return;

            SerializedObject so = new(transform);
            SerializedProperty property = so.FindProperty("AuthorityMode");
            if (property == null)
            {
                Warnings.Add(
                    $"Could not set {transform.gameObject.name}'s NetworkTransform authority to " +
                    $"{mode} - this Netcode version names the field something else. Set it in " +
                    "the Inspector.");
                return;
            }

            int index = System.Array.IndexOf(property.enumNames, mode);
            if (index < 0)
            {
                Warnings.Add($"NetworkTransform has no '{mode}' authority mode. Set it in the Inspector.");
                return;
            }

            property.enumValueIndex = index;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Missing(Object target, string path)
        {
            Warnings.Add($"'{target.name}' has no serialized field '{path}' - assign it in the Inspector.");
            return false;
        }

        // ----------------------------------------------------------------
        // Small helpers
        // ----------------------------------------------------------------

        private static int MaskOf(params string[] layers)
        {
            int mask = 0;
            foreach (string layer in layers)
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0) mask |= 1 << index;
                else Warnings.Add($"Layer '{layer}' is missing. Run Game > Bootstrap Project.");
            }
            return mask;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static void Report()
        {
            string body =
                $"Built:\n" +
                $"  {ScenePath}\n" +
                $"  {PlayerPrefabPath}\n" +
                $"  {BotPrefabPath}\n" +
                $"  {RiflePath}\n\n" +
                "Bake a NavMesh (Window > AI > Navigation) or the bots will not move.\n\n" +
                "Then press Play and use NetworkManager's Start Host.";

            if (Warnings.Count > 0)
            {
                body += "\n\nNeeds a look:\n  - " + string.Join("\n  - ", Warnings);
            }

            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Build Playable Scene", body, "OK");
            Debug.Log("Build Playable Scene\n" + body);
        }
    }
}
