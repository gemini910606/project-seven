using System.Collections.Generic;
using UnityEngine;

namespace Game.Missions
{
    /// <summary>
    /// One playable mission, as data.
    ///
    /// <see cref="MissionId"/> is the key the backend groups leaderboards by, so
    /// changing it splits a mission's board in two. Pick it once and leave it.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Mission Definition", fileName = "Mission_")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id sent to the backend. Renaming this splits the leaderboard.")]
        public string MissionId = "dockside-raid";

        public string DisplayName = "Dockside Raid";

        [TextArea(2, 5)]
        public string Briefing = "Get in, take the manifest, get out before the heat catches up.";

        [Header("Objectives")]
        [Tooltip("Completed in order. The last one should be an ExtractObjective.")]
        public List<MissionObjective> Objectives = new();

        [Header("Failure")]
        [Tooltip("Seconds before the run auto-fails. Zero means no limit.")]
        [Min(0f)] public float TimeLimitSeconds;

        [Tooltip("Alert level that triggers an automatic fail. Zero disables it.")]
        [Range(0, 5)] public int FailAtAlertLevel;

        /// <summary>Objectives that must be finished, ignoring optional ones.</summary>
        public int RequiredObjectiveCount
        {
            get
            {
                int count = 0;
                foreach (MissionObjective objective in Objectives)
                {
                    if (objective != null && !objective.Optional) count++;
                }
                return count;
            }
        }

        private void OnValidate()
        {
            // The backend's leaderboard is keyed on this; an empty one silently
            // pools every mission's scores into a single board named "".
            if (string.IsNullOrWhiteSpace(MissionId))
            {
                Debug.LogError($"{name}: MissionId must not be empty.", this);
            }
        }
    }
}
