using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Round;
using Game.Weapons;

namespace Game.Player
{
    /// <summary>
    /// Glue for one player. Reads input on the owning client, drives movement,
    /// aiming and the weapon, and keeps the local rig switched off for everyone
    /// else's copy.
    ///
    /// Deliberately thin. Every actual rule lives in the component that owns it;
    /// when this file starts growing gameplay logic, that logic is in the wrong
    /// place.
    ///
    /// Movement here is client-authoritative - the owner moves its own character
    /// and the position is replicated. That is the wrong answer for a competitive
    /// game and the right one for this project: server-authoritative movement
    /// with prediction and reconciliation is weeks of work, and the thing it buys
    /// you is protection from cheating friends.
    /// </summary>
    [RequireComponent(typeof(FirstPersonMotor))]
    [RequireComponent(typeof(PlayerLook))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(TeamMember))]
    [DisallowMultipleComponent]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        [Header("Local-only objects")]
        [Tooltip("Camera, HUD and view model. Enabled only on the owning client.")]
        [SerializeField] private GameObject[] localOnly;

        [Tooltip("Body mesh shown to everyone else and hidden from the owner.")]
        [SerializeField] private GameObject[] remoteOnly;

        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private WeaponHolder weapons;
        [SerializeField] private Spike spike;

        [Tooltip("Server-side hit resolution. One in the scene; found automatically if left empty.")]
        [SerializeField] private ShotResolver shotResolver;

        [Tooltip("How close the player must be to the spike to plant or defuse.")]
        [SerializeField, Min(0.5f)] private float interactRange = 2.5f;

        private FirstPersonMotor _motor;
        private PlayerLook _look;
        private Health _health;
        private Weapon _subscribedWeapon;

        private void Awake()
        {
            _motor = GetComponent<FirstPersonMotor>();
            _look = GetComponent<PlayerLook>();
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            bool owner = IsOwner;

            foreach (GameObject go in localOnly) if (go != null) go.SetActive(owner);
            foreach (GameObject go in remoteOnly) if (go != null) go.SetActive(!owner);

            // Input and the motor only run on the machine that owns this player.
            if (input != null) input.enabled = owner;
            _motor.enabled = owner;
            _look.enabled = owner;

            if (owner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (IsServer)
            {
                RoundDirector.Instance?.Register(_health);
                if (shotResolver == null) shotResolver = FindFirstObjectByType<ShotResolver>();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) RoundDirector.Instance?.Unregister(_health);
            Unsubscribe();
        }

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// Keeps the pellet subscription pointed at whatever is currently held.
        /// Missing a weapon swap here means the new gun silently does no damage,
        /// which is a miserable bug to chase.
        /// </summary>
        private void TrackEquippedWeapon(Weapon weapon)
        {
            if (weapon == _subscribedWeapon) return;

            Unsubscribe();
            _subscribedWeapon = weapon;
            if (_subscribedWeapon != null) _subscribedWeapon.PelletFired += OnPelletFired;
        }

        private void Unsubscribe()
        {
            if (_subscribedWeapon == null) return;
            _subscribedWeapon.PelletFired -= OnPelletFired;
            _subscribedWeapon = null;
        }

        /// <summary>
        /// The owning client fired. Ship the ray to the server, which is the only
        /// thing allowed to decide whether it hit anyone.
        /// </summary>
        private void OnPelletFired(Vector3 origin, Vector3 direction) =>
            FireRpc(origin, direction);

        [Rpc(SendTo.Server)]
        private void FireRpc(Vector3 origin, Vector3 direction)
        {
            if (shotResolver == null || !_health.IsAlive) return;

            Weapon weapon = weapons != null ? weapons.Current : null;
            if (weapon == null) return;

            shotResolver.Resolve(gameObject, weapon.Definition, origin, direction);
        }

        private void Update()
        {
            if (!IsOwner || input == null) return;

            RoundDirector director = RoundDirector.Instance;
            bool frozen = director != null && director.PlayersFrozen;

            _motor.Frozen = frozen;

            Weapon weapon = weapons != null ? weapons.Current : null;
            TrackEquippedWeapon(weapon);
            Vector2 recoil = weapon != null ? weapon.RecoilOffset : Vector2.zero;

            _look.Tick(input.Look, recoil, _motor.IsCrouching);

            _motor.SetWalk(input.WalkHeld);
            _motor.SetCrouch(input.CrouchHeld);
            if (input.JumpPressedThisFrame) _motor.RequestJump();
            _motor.Tick(input.Move, _look.Yaw);

            if (frozen || !_health.IsAlive) return;

            TickWeapon(weapon);
            TickSpike();
        }

        private void TickWeapon(Weapon weapon)
        {
            if (weapon == null) return;

            Vector3 origin = _look.EyePosition;
            weapon.Tick(input.FireHeld, input.AimHeld, _motor.PlanarSpeed, origin, _look.AimDirection);
        }

        private void TickSpike()
        {
            // The serialized field is a convenience for a hand-built scene. It is
            // normally empty and has to be: this component lives on a prefab
            // asset, and a prefab cannot reference a scene object - Unity drops
            // the link without saying so. Falling back to the scene's spike is
            // what actually makes planting work.
            if (spike == null) spike = Spike.Instance;

            if (spike == null || !input.InteractHeld) return;
            if (Vector3.Distance(transform.position, spike.transform.position) > interactRange) return;

            // The server decides whether this means anything - whether the player
            // is on the right side, in the right phase, and standing on a site.
            spike.InteractRpc();
        }
    }
}
