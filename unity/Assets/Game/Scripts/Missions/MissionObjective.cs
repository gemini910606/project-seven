using System;
using UnityEngine;

namespace Game.Missions
{
    /// <summary>
    /// One objective in a mission, authored as an asset.
    ///
    /// Objectives are ScriptableObjects holding only their definition; the
    /// mutable per-run state lives in <see cref="ObjectiveProgress"/>. Putting
    /// runtime state on the asset is the classic ScriptableObject trap - it
    /// persists between play sessions in the editor and produces objectives that
    /// start already complete.
    /// </summary>
    public abstract class MissionObjective : ScriptableObject
    {
        [Tooltip("Shown in the HUD tracker, e.g. \"Reach the loading dock\".")]
        public string Description = "Do the thing";

        [Tooltip("Optional objectives do not block extraction but still pay out.")]
        public bool Optional;

        [Tooltip("Identifier used by trigger volumes and mission scripting to reference this objective.")]
        public string Id = "objective";

        /// <summary>Creates the mutable per-run companion for this objective.</summary>
        public virtual ObjectiveProgress CreateProgress() => new(this);
    }

    /// <summary>Mutable per-run state for one objective.</summary>
    [Serializable]
    public class ObjectiveProgress
    {
        public readonly MissionObjective Definition;

        public bool IsComplete { get; private set; }
        public int Current { get; private set; }
        public int Required { get; protected set; } = 1;

        public event Action<ObjectiveProgress> Completed;

        public ObjectiveProgress(MissionObjective definition)
        {
            Definition = definition;
        }

        public float Normalized => Required > 0 ? Mathf.Clamp01((float)Current / Required) : 0f;

        public void Advance(int amount = 1)
        {
            if (IsComplete || amount <= 0) return;

            Current = Mathf.Min(Required, Current + amount);
            if (Current < Required) return;

            IsComplete = true;
            Completed?.Invoke(this);
        }

        public void ForceComplete()
        {
            if (IsComplete) return;
            Current = Required;
            IsComplete = true;
            Completed?.Invoke(this);
        }
    }
}
