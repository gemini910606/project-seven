using UnityEngine;

namespace Game.Missions
{
    /// <summary>Stand inside a trigger volume to complete.</summary>
    [CreateAssetMenu(menuName = "Game/Objectives/Reach Zone", fileName = "Obj_Reach_")]
    public sealed class ReachZoneObjective : MissionObjective
    {
        [Tooltip("Matched against ObjectiveZone.zoneId in the scene.")]
        public string ZoneId = "zone";
    }

    /// <summary>Kill a number of enemies.</summary>
    [CreateAssetMenu(menuName = "Game/Objectives/Eliminate", fileName = "Obj_Eliminate_")]
    public sealed class EliminateObjective : MissionObjective
    {
        [Min(1)] public int Count = 5;

        [Tooltip("Only count enemies whose EnemyTag matches. Empty counts every kill.")]
        public string EnemyTag = string.Empty;

        public override ObjectiveProgress CreateProgress() => new CountedProgress(this, Count);
    }

    /// <summary>Pick up a specific item.</summary>
    [CreateAssetMenu(menuName = "Game/Objectives/Collect", fileName = "Obj_Collect_")]
    public sealed class CollectObjective : MissionObjective
    {
        [Tooltip("Matched against CollectibleItem.itemId in the scene.")]
        public string ItemId = "intel";

        [Min(1)] public int Count = 1;

        public override ObjectiveProgress CreateProgress() => new CountedProgress(this, Count);
    }

    /// <summary>
    /// Reach the extraction zone. Always the last objective; completing it ends
    /// the run as a success.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Objectives/Extract", fileName = "Obj_Extract_")]
    public sealed class ExtractObjective : MissionObjective
    {
        public string ZoneId = "extraction";

        [Tooltip("Seconds the player must stay inside the zone. Non-zero makes extraction a decision rather than a touch.")]
        [Min(0f)] public float HoldSeconds = 4f;
    }

    /// <summary>Progress that needs more than one tick to finish.</summary>
    public sealed class CountedProgress : ObjectiveProgress
    {
        public CountedProgress(MissionObjective definition, int required) : base(definition)
        {
            Required = Mathf.Max(1, required);
        }
    }
}
