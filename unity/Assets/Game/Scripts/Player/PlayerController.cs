using System.Collections;
using UnityEngine;
using Game.Core;
using Game.Weapons;

namespace Game.Player
{
    /// <summary>
    /// Glue. Reads input, drives the motor, the aim controller and the weapon,
    /// and republishes the events the rest of the game cares about.
    ///
    /// Deliberately thin: every rule lives in the component that owns it. When
    /// this file starts growing gameplay logic, that logic is in the wrong place.
    /// </summary>
    [RequireComponent(typeof(ThirdPersonMotor))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerAimController))]
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private WeaponHolder weaponHolder;

        [Tooltip("Where hip-fire traces originate. Usually the weapon muzzle.")]
        [SerializeField] private Transform fireOrigin;

        private ThirdPersonMotor _motor;
        private PlayerInputReader _input;
        private PlayerAimController _aim;
        private Health _health;
        private Coroutine _reloadRoutine;

        public Health Health => _health;
        public ThirdPersonMotor Motor => _motor;
        public PlayerAimController Aim => _aim;
        public WeaponHolder Weapons => weaponHolder;

        private void Awake()
        {
            _motor = GetComponent<ThirdPersonMotor>();
            _input = GetComponent<PlayerInputReader>();
            _aim = GetComponent<PlayerAimController>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _input.JumpPressed += OnJump;
            _input.ReloadPressed += OnReload;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _input.JumpPressed -= OnJump;
            _input.ReloadPressed -= OnReload;
            _health.Died -= OnDied;
        }

        private void Start()
        {
            if (_aim.CameraTransform != null) _motor.SetCameraTransform(_aim.CameraTransform);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Weapon weapon = weaponHolder != null ? weaponHolder.Current : null;
            Vector2 recoil = weapon != null ? weapon.RecoilOffset : Vector2.zero;

            _aim.Tick(_input.Look, _input.AimHeld, recoil);

            _motor.Aiming = _input.AimHeld;
            _motor.SetSprint(_input.SprintHeld);
            _motor.SetCrouch(_input.CrouchHeld);
            _motor.Tick(_input.Move);

            if (weapon == null) return;

            Vector3 origin = fireOrigin != null ? fireOrigin.position : transform.position + Vector3.up * 1.5f;
            weapon.Tick(_input.FireHeld, _input.AimHeld, _motor.PlanarSpeed, origin, _aim.DirectionFrom(origin));

            // Running dry mid-fight should not require noticing the counter.
            if (weapon.IsMagazineEmpty && weapon.CanReload && _reloadRoutine == null) OnReload();
        }

        private void OnJump() => _motor.RequestJump();

        private void OnReload()
        {
            Weapon weapon = weaponHolder != null ? weaponHolder.Current : null;
            if (weapon == null || !weapon.CanReload || _reloadRoutine != null) return;

            _reloadRoutine = StartCoroutine(ReloadRoutine(weapon));
        }

        private IEnumerator ReloadRoutine(Weapon weapon)
        {
            float seconds = weapon.BeginReload();
            if (seconds <= 0f)
            {
                _reloadRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(seconds);

            // The weapon may have been swapped or the player killed mid-reload.
            if (weapon != null && weapon.IsReloading) weapon.FinishReload();
            _reloadRoutine = null;
        }

        private void OnDied(DamageInfo info)
        {
            enabled = false;
            _input.ClearHeldState();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
