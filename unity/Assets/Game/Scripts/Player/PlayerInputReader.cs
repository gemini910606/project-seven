using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// The one place that talks to the Input System.
    ///
    /// Actions are built in code rather than loaded from a .inputactions asset.
    /// That is a deliberate trade: a generated asset is a binary-ish file that
    /// merges badly and cannot be reviewed in a pull request, whereas this file
    /// is diffable and the bindings are readable. If the project later grows a
    /// rebinding UI, swap this for an InputActionAsset - everything downstream
    /// only sees the properties below, so nothing else has to change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Look sensitivity")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0.01f)] private float gamepadSensitivity = 2.2f;

        [Tooltip("Look sensitivity multiplier while aiming down sights.")]
        [SerializeField, Range(0.1f, 1f)] private float aimSensitivityScale = 0.55f;

        private InputAction _move;
        private InputAction _look;
        private InputAction _fire;
        private InputAction _aim;
        private InputAction _walk;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _reload;
        private InputAction _interact;
        private InputAction _pause;

        private bool _lookIsGamepad;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool FireHeld { get; private set; }
        public bool AimHeld { get; private set; }
        /// <summary>Held to move slowly and silently. In a tactical shooter shift SLOWS you down.</summary>
        public bool WalkHeld { get; private set; }
        public bool CrouchHeld { get; private set; }

        /// <summary>Held, not tapped: planting and defusing take seconds.</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>
        /// Polled rather than delivered as an event, so a jump can never be lost
        /// to callback ordering between the Input System and Update.
        /// </summary>
        public bool JumpPressedThisFrame { get; private set; }

        public event Action FirePressed;
        public event Action ReloadPressed;
        public event Action PausePressed;

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _look = new InputAction("Look", InputActionType.Value);
            _look.AddBinding("<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick");

            _fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            _fire.AddBinding("<Gamepad>/rightTrigger");

            _aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            _aim.AddBinding("<Gamepad>/leftTrigger");

            _walk = new InputAction("Walk", InputActionType.Button, "<Keyboard>/leftShift");
            _walk.AddBinding("<Gamepad>/leftStickPress");

            _crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            _crouch.AddBinding("<Gamepad>/buttonEast");

            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            _reload.AddBinding("<Gamepad>/buttonWest");

            _interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _interact.AddBinding("<Gamepad>/buttonNorth");

            _pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            _pause.AddBinding("<Gamepad>/start");

            _fire.performed += _ => FirePressed?.Invoke();
            _reload.performed += _ => ReloadPressed?.Invoke();
            _pause.performed += _ => PausePressed?.Invoke();

            // Mouse delta is per-frame pixels; stick input is a per-frame axis.
            // They need different scaling, so remember which device last moved.
            _look.performed += ctx => _lookIsGamepad = ctx.control?.device is Gamepad;
        }

        private void OnEnable()
        {
            _move.Enable(); _look.Enable(); _fire.Enable(); _aim.Enable();
            _walk.Enable(); _crouch.Enable(); _jump.Enable(); _reload.Enable();
            _interact.Enable(); _pause.Enable();
        }

        private void OnDisable()
        {
            _move.Disable(); _look.Disable(); _fire.Disable(); _aim.Disable();
            _walk.Disable(); _crouch.Disable(); _jump.Disable(); _reload.Disable();
            _interact.Disable(); _pause.Disable();
        }

        private void OnDestroy()
        {
            _move.Dispose(); _look.Dispose(); _fire.Dispose(); _aim.Dispose();
            _walk.Dispose(); _crouch.Dispose(); _jump.Dispose(); _reload.Dispose();
            _interact.Dispose(); _pause.Dispose();
        }

        private void Update()
        {
            Move = _move.ReadValue<Vector2>();
            FireHeld = _fire.IsPressed();
            AimHeld = _aim.IsPressed();
            WalkHeld = _walk.IsPressed();
            CrouchHeld = _crouch.IsPressed();
            InteractHeld = _interact.IsPressed();
            JumpPressedThisFrame = _jump.WasPressedThisFrame();

            Vector2 raw = _look.ReadValue<Vector2>();
            float sensitivity = _lookIsGamepad ? gamepadSensitivity : mouseSensitivity;

            // Stick input is a rate (units per second); mouse delta is already a
            // per-frame displacement and must NOT be multiplied by deltaTime, or
            // the aim speed changes with framerate.
            if (_lookIsGamepad) raw *= Time.deltaTime * 60f;

            Look = raw * (sensitivity * (AimHeld ? aimSensitivityScale : 1f));
        }

        /// <summary>Zeroes held state. Call when opening a menu or when the round freezes players.</summary>
        public void ClearHeldState()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            FireHeld = AimHeld = WalkHeld = CrouchHeld = false;
            InteractHeld = false;
            JumpPressedThisFrame = false;
        }
    }
}
