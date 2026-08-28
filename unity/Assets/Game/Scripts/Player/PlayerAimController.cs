using UnityEngine;
using Unity.Cinemachine;

namespace Game.Player
{
    /// <summary>
    /// Owns the look angles and the shoulder camera.
    ///
    /// Two Cinemachine cameras with different priorities, rather than one camera
    /// that lerps its own settings: swapping priority lets Cinemachine blend
    /// between them with its own easing, which is both less code and smoother
    /// than hand-animating an FOV.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAimController : MonoBehaviour
    {
        [Header("Cinemachine")]
        [Tooltip("Follow camera used while moving around.")]
        [SerializeField] private CinemachineCamera hipCamera;

        [Tooltip("Tighter over-the-shoulder camera used while aiming.")]
        [SerializeField] private CinemachineCamera aimCamera;

        [Tooltip("Pivot the cameras follow. Yaw is applied to the player body, pitch to this.")]
        [SerializeField] private Transform cameraPivot;

        [Header("Look limits")]
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Aim")]
        [Tooltip("Layers an aim ray can hit when resolving what the reticle is over.")]
        [SerializeField] private LayerMask aimMask = ~0;

        [Tooltip("Distance used for the aim point when the ray hits nothing.")]
        [SerializeField, Min(1f)] private float aimFallbackDistance = 300f;

        private float _yaw;
        private float _pitch;
        private Camera _mainCamera;

        public bool IsAiming { get; private set; }

        /// <summary>World point the reticle is currently over. Weapons fire towards this.</summary>
        public Vector3 AimPoint { get; private set; }

        public Transform CameraTransform => _mainCamera != null ? _mainCamera.transform : null;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _yaw = transform.eulerAngles.y;

            if (cameraPivot == null)
            {
                Debug.LogError($"{name}: PlayerAimController needs a cameraPivot.", this);
                enabled = false;
            }
        }

        /// <param name="lookDelta">Already sensitivity-scaled, from PlayerInputReader.</param>
        /// <param name="recoilOffset">Pitch/yaw kick from the equipped weapon.</param>
        public void Tick(Vector2 lookDelta, bool aiming, Vector2 recoilOffset)
        {
            IsAiming = aiming;

            _yaw += lookDelta.x;
            _pitch = Mathf.Clamp(_pitch - lookDelta.y, minPitch, maxPitch);

            // Recoil is added on top of the player's own angles rather than being
            // folded into them, so releasing the trigger returns the view to where
            // the player was actually aiming instead of where recoil left it.
            cameraPivot.rotation = Quaternion.Euler(_pitch + recoilOffset.x, _yaw + recoilOffset.y, 0f);

            UpdateCameraPriorities();
            UpdateAimPoint();
        }

        private void UpdateCameraPriorities()
        {
            if (hipCamera == null || aimCamera == null) return;

            hipCamera.Priority = IsAiming ? 10 : 20;
            aimCamera.Priority = IsAiming ? 20 : 10;
        }

        private void UpdateAimPoint()
        {
            if (_mainCamera == null)
            {
                AimPoint = cameraPivot.position + cameraPivot.forward * aimFallbackDistance;
                return;
            }

            // Trace from the screen centre, not the muzzle: the player aims with
            // the reticle, and a muzzle-origin ray drifts off it whenever the gun
            // is offset from the camera - which is always, in third person.
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            AimPoint = Physics.Raycast(ray, out RaycastHit hit, aimFallbackDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.origin + ray.direction * aimFallbackDistance;
        }

        /// <summary>
        /// Direction from a muzzle to the aim point. Converging the muzzle ray on
        /// the reticle target is what stops shots going wide of the crosshair at
        /// close range.
        /// </summary>
        public Vector3 DirectionFrom(Vector3 origin)
        {
            Vector3 delta = AimPoint - origin;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
        }
    }
}
