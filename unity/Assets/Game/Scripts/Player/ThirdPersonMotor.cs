using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Camera-relative third-person locomotion on a CharacterController.
    ///
    /// CharacterController rather than a Rigidbody: this game needs precise,
    /// non-negotiable movement (a shooter where the player slides on a corpse is
    /// a bad shooter), and CharacterController gives that for free. The cost is
    /// that physics does not push the player around, which is exactly the trade
    /// GTA-likes make for on-foot movement anyway.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class ThirdPersonMotor : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.2f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7.4f;
        [SerializeField, Min(0f)] private float crouchSpeed = 2.0f;
        [SerializeField, Min(0f)] private float aimSpeed = 2.8f;

        [Header("Acceleration")]
        [Tooltip("Seconds to reach target speed. Small values feel arcade, large feel heavy.")]
        [SerializeField, Min(0.01f)] private float accelerationTime = 0.12f;
        [SerializeField, Min(0.01f)] private float decelerationTime = 0.09f;

        [Header("Turning")]
        [Tooltip("Seconds to face the movement direction when not aiming.")]
        [SerializeField, Min(0.01f)] private float turnSmoothTime = 0.09f;

        [Header("Jump and gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -21f;

        [Tooltip("Grace period after walking off a ledge during which a jump still works. Players read this as responsiveness, not as cheating.")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

        [Tooltip("A jump pressed this long before landing still fires on touchdown.")]
        [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;

        [Header("Crouching")]
        [SerializeField, Min(0.5f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.5f)] private float crouchingHeight = 1.15f;
        [SerializeField, Min(0.01f)] private float crouchTransitionTime = 0.15f;

        [Tooltip("Layers that block standing back up. Leave out the player's own layer.")]
        [SerializeField] private LayerMask ceilingMask = ~0;

        [Header("Ground")]
        [Tooltip("Downward force applied while grounded so the controller hugs slopes and stairs instead of bouncing down them.")]
        [SerializeField] private float groundedStick = -3f;

        private CharacterController _controller;
        private Transform _cameraTransform;

        private Vector3 _horizontalVelocity;
        private Vector3 _velocitySmoothing;
        private float _verticalVelocity;
        private float _turnSmoothVelocity;

        private float _lastGroundedTime = float.NegativeInfinity;
        private float _jumpPressedTime = float.NegativeInfinity;
        private float _heightVelocity;

        private bool _crouchRequested;
        private bool _sprintRequested;
        private bool _aiming;

        /// <summary>Planar speed in m/s. Drives the animator and the footstep noise emitter.</summary>
        public float PlanarSpeed => _horizontalVelocity.magnitude;

        /// <summary>Planar speed as a fraction of sprint speed, for animator blend trees.</summary>
        public float NormalizedSpeed => sprintSpeed > 0f ? PlanarSpeed / sprintSpeed : 0f;

        public bool IsGrounded => _controller.isGrounded;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }

        /// <summary>Set by PlayerAimController; changes speed and turning behaviour.</summary>
        public bool Aiming
        {
            get => _aiming;
            set => _aiming = value;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = standingHeight;

            if (Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        /// <summary>
        /// Lets the camera rig hand over its transform explicitly. Camera.main is
        /// a scene search and is wrong the moment there is more than one camera.
        /// </summary>
        public void SetCameraTransform(Transform cameraTransform) => _cameraTransform = cameraTransform;

        public void RequestJump() => _jumpPressedTime = Time.time;

        public void SetCrouch(bool crouching) => _crouchRequested = crouching;

        public void SetSprint(bool sprinting) => _sprintRequested = sprinting;

        /// <param name="moveInput">Raw stick/WASD input, unnormalised.</param>
        public void Tick(Vector2 moveInput)
        {
            UpdateStance();
            Vector3 desired = DesiredVelocity(moveInput);
            ApplyTurning(desired);
            ApplyGravityAndJump();

            _horizontalVelocity = Vector3.SmoothDamp(
                _horizontalVelocity,
                desired,
                ref _velocitySmoothing,
                desired.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? accelerationTime : decelerationTime);

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private Vector3 DesiredVelocity(Vector2 moveInput)
        {
            // Clamp rather than normalise: a half-tilted stick should walk.
            Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
            if (input.sqrMagnitude < 0.0001f)
            {
                IsSprinting = false;
                return Vector3.zero;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_cameraTransform != null)
            {
                // Flatten the camera basis, or looking down would slow the player.
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            }

            Vector3 direction = (forward * input.y + right * input.x);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            // Sprinting is refused while aiming, crouching, or backpedalling -
            // sprinting backwards looks wrong and reads as a bug.
            IsSprinting = _sprintRequested && !_aiming && !IsCrouching && input.y > 0.4f;

            float speed = IsCrouching ? crouchSpeed
                : _aiming ? aimSpeed
                : IsSprinting ? sprintSpeed
                : walkSpeed;

            return direction * speed;
        }

        private void ApplyTurning(Vector3 desiredVelocity)
        {
            if (_cameraTransform == null) return;

            float targetYaw;

            if (_aiming)
            {
                // Aiming locks the body to the camera so the weapon points where
                // the reticle does. Anything else makes shooting feel dishonest.
                targetYaw = _cameraTransform.eulerAngles.y;
            }
            else
            {
                if (desiredVelocity.sqrMagnitude < 0.01f) return;
                targetYaw = Mathf.Atan2(desiredVelocity.x, desiredVelocity.z) * Mathf.Rad2Deg;
            }

            float yaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetYaw, ref _turnSmoothVelocity,
                _aiming ? turnSmoothTime * 0.4f : turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void ApplyGravityAndJump()
        {
            if (_controller.isGrounded)
            {
                _lastGroundedTime = Time.time;
                if (_verticalVelocity < 0f) _verticalVelocity = groundedStick;
            }

            bool withinCoyote = Time.time - _lastGroundedTime <= coyoteTime;
            bool jumpBuffered = Time.time - _jumpPressedTime <= jumpBufferTime;

            if (jumpBuffered && withinCoyote && !IsCrouching)
            {
                // v = sqrt(2 * g * h), so tuning jumpHeight in metres actually
                // produces that height regardless of the gravity value.
                _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                _jumpPressedTime = float.NegativeInfinity;
                _lastGroundedTime = float.NegativeInfinity;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }
        }

        private void UpdateStance()
        {
            bool wantsCrouch = _crouchRequested;

            // Refuse to stand up under a low ceiling, or the controller's capsule
            // grows into geometry and the player is ejected through it.
            if (IsCrouching && !wantsCrouch && BlockedAbove()) wantsCrouch = true;

            IsCrouching = wantsCrouch;

            float targetHeight = IsCrouching ? crouchingHeight : standingHeight;
            float height = Mathf.SmoothDamp(
                _controller.height, targetHeight, ref _heightVelocity, crouchTransitionTime);

            _controller.height = height;
            // Keep the capsule's feet planted while its height changes.
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool BlockedAbove()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 origin = transform.position + Vector3.up * (_controller.height - radius);
            float distance = standingHeight - _controller.height + 0.05f;

            return Physics.SphereCast(
                origin, radius, Vector3.up, out _, distance, ceilingMask, QueryTriggerInteraction.Ignore);
        }
    }
}
