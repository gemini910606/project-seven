using UnityEngine;
using Game.Bots;

namespace Game.Player
{
    /// <summary>
    /// First-person movement tuned for a tactical shooter, which is a different
    /// animal from the third-person open-world motor this replaced.
    ///
    /// The differences that matter:
    ///  - Almost no air control. You commit to a jump; you cannot steer out of it.
    ///  - Walking is a real tactical choice: silent, and it tightens the weapon
    ///    cone. Running is loud and inaccurate.
    ///  - Movement is deliberate. Fast acceleration would let players peek and
    ///    stop instantly, which removes the entire skill of holding an angle.
    ///
    /// Footsteps feed the same NoiseSystem the bots listen to, so sprinting past
    /// a bot gets you heard exactly like it would a human.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField, Min(0f)] private float runSpeed = 5.4f;
        [SerializeField, Min(0f)] private float walkSpeed = 2.6f;
        [SerializeField, Min(0f)] private float crouchSpeed = 1.9f;

        [Header("Acceleration")]
        [Tooltip("Seconds to reach full speed. Deliberately slow: instant stops would kill the skill of holding an angle.")]
        [SerializeField, Min(0.01f)] private float groundAcceleration = 0.11f;
        [SerializeField, Min(0.01f)] private float groundDeceleration = 0.08f;

        [Tooltip("Fraction of ground acceleration available in the air. Near zero on purpose.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.08f;

        [Header("Jump and gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 0.95f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStick = -3f;

        [Header("Stance")]
        [SerializeField, Min(0.5f)] private float standingHeight = 1.8f;
        [SerializeField, Min(0.5f)] private float crouchingHeight = 1.2f;
        [SerializeField, Min(0.01f)] private float crouchTransitionTime = 0.12f;
        [SerializeField] private LayerMask ceilingMask = ~0;

        [Header("Footsteps")]
        [Tooltip("Metres between footstep noises while running.")]
        [SerializeField, Min(0.1f)] private float strideLength = 2.2f;

        [Tooltip("How far a running footstep carries, in metres. Walking is silent.")]
        [SerializeField, Min(0f)] private float footstepNoiseRadius = 22f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _heightVelocity;
        private float _distanceSinceStep;

        private bool _walkRequested;
        private bool _crouchRequested;
        private bool _jumpRequested;

        public float PlanarSpeed => _horizontalVelocity.magnitude;
        public bool IsGrounded => _controller.isGrounded;
        public bool IsCrouching { get; private set; }
        public bool IsWalking { get; private set; }

        /// <summary>Set by the round director; a frozen player cannot leave spawn.</summary>
        public bool Frozen { get; set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _controller.height = standingHeight;
        }

        public void SetWalk(bool walking) => _walkRequested = walking;

        public void SetCrouch(bool crouching) => _crouchRequested = crouching;

        public void RequestJump() => _jumpRequested = true;

        /// <param name="moveInput">Raw WASD/stick input.</param>
        /// <param name="yaw">Current look yaw in degrees; movement is relative to it.</param>
        public void Tick(Vector2 moveInput, float yaw)
        {
            if (Frozen)
            {
                moveInput = Vector2.zero;
                _jumpRequested = false;
            }

            UpdateStance();

            Vector3 desired = DesiredVelocity(moveInput, yaw);
            bool grounded = _controller.isGrounded;

            float smoothing = desired.sqrMagnitude > _horizontalVelocity.sqrMagnitude
                ? groundAcceleration
                : groundDeceleration;

            // In the air the target is barely reachable, so a jump commits you to
            // roughly the direction you left the ground in.
            if (!grounded) smoothing /= Mathf.Max(0.001f, airControl);

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, desired, (1f / smoothing) * Time.deltaTime);

            ApplyGravityAndJump(grounded);
            EmitFootsteps(grounded);

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private Vector3 DesiredVelocity(Vector2 moveInput, float yaw)
        {
            Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
            if (input.sqrMagnitude < 0.0001f)
            {
                IsWalking = false;
                return Vector3.zero;
            }

            IsWalking = _walkRequested && !IsCrouching;

            float speed = IsCrouching ? crouchSpeed : IsWalking ? walkSpeed : runSpeed;

            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 direction = facing * new Vector3(input.x, 0f, input.y);

            return direction.normalized * (speed * input.magnitude);
        }

        private void ApplyGravityAndJump(bool grounded)
        {
            if (grounded && _verticalVelocity < 0f) _verticalVelocity = groundedStick;

            if (_jumpRequested && grounded && !IsCrouching)
            {
                // v = sqrt(2gh), so jumpHeight is honestly in metres.
                _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            }
            else if (!grounded)
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            _jumpRequested = false;
        }

        private void EmitFootsteps(bool grounded)
        {
            // Walking and crouching are silent. That is the whole point of them.
            if (!grounded || IsWalking || IsCrouching || PlanarSpeed < 0.1f)
            {
                _distanceSinceStep = 0f;
                return;
            }

            _distanceSinceStep += PlanarSpeed * Time.deltaTime;
            if (_distanceSinceStep < strideLength) return;

            _distanceSinceStep = 0f;
            NoiseSystem.EmitSafe(transform.position, footstepNoiseRadius);
        }

        private void UpdateStance()
        {
            bool wantsCrouch = _crouchRequested;
            if (IsCrouching && !wantsCrouch && BlockedAbove()) wantsCrouch = true;

            IsCrouching = wantsCrouch;

            float target = IsCrouching ? crouchingHeight : standingHeight;
            float height = Mathf.SmoothDamp(_controller.height, target, ref _heightVelocity, crouchTransitionTime);

            _controller.height = height;
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
