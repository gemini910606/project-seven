using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// First-person camera aiming.
    ///
    /// The body yaws and the camera pitches, which is the whole rig - no
    /// Cinemachine, no follow damping, no smoothing. A tactical shooter's aim
    /// must be one-to-one with the mouse; any smoothing at all makes precise
    /// flicks feel like the game is arguing with you.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [Tooltip("Camera pivot at eye height, a child of the player.")]
        [SerializeField] private Transform cameraPivot;

        [SerializeField] private float minPitch = -89f;
        [SerializeField] private float maxPitch = 89f;

        [Header("View height")]
        [SerializeField] private float standingEyeHeight = 1.62f;
        [SerializeField] private float crouchingEyeHeight = 1.05f;
        [SerializeField, Min(0.01f)] private float eyeTransitionTime = 0.1f;

        private float _yaw;
        private float _pitch;
        private float _eyeVelocity;
        private float _eyeHeight;

        /// <summary>Body yaw in degrees. The motor moves relative to this.</summary>
        public float Yaw => _yaw;

        public Vector3 EyePosition => cameraPivot != null ? cameraPivot.position : transform.position;

        public Vector3 AimDirection => cameraPivot != null ? cameraPivot.forward : transform.forward;

        private void Awake()
        {
            _yaw = transform.eulerAngles.y;
            _eyeHeight = standingEyeHeight;

            if (cameraPivot == null)
            {
                Debug.LogError($"{name}: PlayerLook needs a cameraPivot.", this);
                enabled = false;
            }
        }

        /// <param name="lookDelta">Already sensitivity-scaled, from PlayerInputReader.</param>
        /// <param name="recoilOffset">Pitch/yaw kick from the weapon, in degrees.</param>
        public void Tick(Vector2 lookDelta, Vector2 recoilOffset, bool crouching)
        {
            _yaw += lookDelta.x;
            _pitch = Mathf.Clamp(_pitch - lookDelta.y, minPitch, maxPitch);

            // Yaw the body, pitch only the camera. Applying recoil on top rather
            // than folding it in means releasing the trigger returns the view to
            // where the player was actually aiming.
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(_pitch + recoilOffset.x, recoilOffset.y, 0f);

            float targetEye = crouching ? crouchingEyeHeight : standingEyeHeight;
            _eyeHeight = Mathf.SmoothDamp(_eyeHeight, targetEye, ref _eyeVelocity, eyeTransitionTime);
            cameraPivot.localPosition = new Vector3(0f, _eyeHeight, 0f);
        }

        /// <summary>Snaps the view, e.g. to face the site at round start.</summary>
        public void SetYaw(float yaw)
        {
            _yaw = yaw;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }
    }
}
