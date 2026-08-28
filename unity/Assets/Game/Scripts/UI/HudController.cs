using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.AI;
using Game.Core;
using Game.Missions;
using Game.Player;
using Game.Weapons;

namespace Game.UI
{
    /// <summary>
    /// The in-game HUD.
    ///
    /// Everything here is a listener. The HUD never asks the game to do anything
    /// and never owns state - if a value is wrong on screen it is wrong in the
    /// system that published it, which makes HUD bugs quick to place.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private AlertSystem alertSystem;
        [SerializeField] private MissionDirector missionDirector;

        [Header("Health")]
        [SerializeField] private Image healthFill;
        [SerializeField] private CanvasGroup damageVignette;

        [Tooltip("Seconds for the red flash to fade after a hit.")]
        [SerializeField, Min(0.05f)] private float vignetteFadeSeconds = 0.7f;

        [Header("Ammo")]
        [SerializeField] private TMP_Text ammoLabel;
        [SerializeField] private TMP_Text weaponLabel;
        [SerializeField] private GameObject reloadIndicator;

        [Header("Alert")]
        [SerializeField] private Image alertFill;
        [SerializeField] private TMP_Text alertLabel;

        [Header("Objectives")]
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text extractionLabel;

        [Header("Reticle")]
        [SerializeField] private RectTransform reticle;

        [Tooltip("Reticle size in pixels at zero spread and at max spread.")]
        [SerializeField] private Vector2 reticleSizeRange = new(24f, 120f);

        private readonly StringBuilder _objectiveText = new();
        private Weapon _weapon;
        private float _vignetteAlpha;

        private void OnEnable()
        {
            if (player != null) player.Health.Damaged += OnPlayerDamaged;
            if (alertSystem != null) alertSystem.LevelChanged += OnAlertLevelChanged;
            if (missionDirector != null)
            {
                missionDirector.ObjectiveCompleted += OnObjectiveCompleted;
                missionDirector.ExtractionProgress += OnExtractionProgress;
            }
        }

        private void OnDisable()
        {
            if (player != null) player.Health.Damaged -= OnPlayerDamaged;
            if (alertSystem != null) alertSystem.LevelChanged -= OnAlertLevelChanged;
            if (missionDirector != null)
            {
                missionDirector.ObjectiveCompleted -= OnObjectiveCompleted;
                missionDirector.ExtractionProgress -= OnExtractionProgress;
            }
        }

        private void Start()
        {
            RefreshObjectives();
            if (extractionLabel != null) extractionLabel.gameObject.SetActive(false);
        }

        private void Update()
        {
            UpdateHealth();
            UpdateWeapon();
            UpdateAlert();
            UpdateReticle();
            FadeVignette();
        }

        private void UpdateHealth()
        {
            if (player == null || healthFill == null) return;
            healthFill.fillAmount = player.Health.Normalized;
        }

        private void UpdateWeapon()
        {
            Weapon current = player != null && player.Weapons != null ? player.Weapons.Current : null;
            if (current != _weapon)
            {
                _weapon = current;
                if (weaponLabel != null)
                {
                    weaponLabel.text = _weapon != null ? _weapon.Definition.DisplayName : string.Empty;
                }
            }

            if (_weapon == null)
            {
                if (ammoLabel != null) ammoLabel.text = string.Empty;
                return;
            }

            if (ammoLabel != null) ammoLabel.text = $"{_weapon.MagazineAmmo} / {_weapon.ReserveAmmo}";
            if (reloadIndicator != null) reloadIndicator.SetActive(_weapon.IsReloading);
        }

        private void UpdateAlert()
        {
            if (alertSystem == null) return;

            if (alertFill != null) alertFill.fillAmount = alertSystem.ProgressToNextLevel;

            if (alertLabel == null) return;

            // A cooling meter is the player's cue that hiding is working, so it
            // gets its own visible state rather than just a shrinking bar.
            alertLabel.text = alertSystem.Level == 0
                ? "CLEAR"
                : (alertSystem.IsCoolingDown ? $"HEAT {alertSystem.Level} - COOLING" : $"HEAT {alertSystem.Level}");
        }

        private void UpdateReticle()
        {
            if (reticle == null || _weapon == null || player == null) return;

            // Reticle size tracks actual spread, so the HUD never promises
            // accuracy the weapon will not deliver.
            WeaponDefinition def = _weapon.Definition;
            float maxCone = def.MaxSpreadDegrees + def.HipFireSpreadDegrees;
            float cone = def.BaseSpreadDegrees
                         + (player.Aim.IsAiming ? 0f : def.HipFireSpreadDegrees)
                         + player.Motor.PlanarSpeed * def.SpreadPerMoveSpeed;

            float t = maxCone > 0f ? Mathf.Clamp01(cone / maxCone) : 0f;
            float size = Mathf.Lerp(reticleSizeRange.x, reticleSizeRange.y, t);
            reticle.sizeDelta = new Vector2(size, size);
        }

        private void FadeVignette()
        {
            if (damageVignette == null) return;

            _vignetteAlpha = Mathf.Max(0f, _vignetteAlpha - Time.deltaTime / vignetteFadeSeconds);
            damageVignette.alpha = _vignetteAlpha;
        }

        private void OnPlayerDamaged(DamageInfo info, float applied) => _vignetteAlpha = 1f;

        private void OnAlertLevelChanged(int previous, int current)
        {
            // Hook stingers and screen effects here. Escalation needs to be felt,
            // not read off a meter.
        }

        private void OnObjectiveCompleted(ObjectiveProgress progress) => RefreshObjectives();

        private void OnExtractionProgress(float remainingSeconds)
        {
            if (extractionLabel == null) return;

            bool active = remainingSeconds > 0f;
            extractionLabel.gameObject.SetActive(active);
            if (active) extractionLabel.text = $"EXTRACTING  {remainingSeconds:0.0}s";
        }

        private void RefreshObjectives()
        {
            if (objectiveLabel == null || missionDirector == null) return;

            _objectiveText.Clear();

            foreach (ObjectiveProgress progress in missionDirector.Objectives)
            {
                if (progress.IsComplete) continue;

                _objectiveText.Append(progress.Definition.Description);
                if (progress.Required > 1) _objectiveText.Append($"  {progress.Current}/{progress.Required}");
                _objectiveText.AppendLine();

                // Only the current objective and any optional ones are shown.
                // A full checklist spoils the mission's shape.
                if (!progress.Definition.Optional) break;
            }

            objectiveLabel.text = _objectiveText.ToString().TrimEnd();
        }
    }
}
