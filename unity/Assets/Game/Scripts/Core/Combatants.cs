using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Everyone who can shoot or be shot: players and bots alike.
    ///
    /// Bots need to find enemies, and a registry of at most ten characters is
    /// far cheaper and far more predictable than a Physics.OverlapSphere over a
    /// level full of colliders. It also means a bot's target selection reads as
    /// "nearest living enemy" rather than as a physics query.
    /// </summary>
    public static class Combatants
    {
        private static readonly List<TeamMember> All = new();

        public static IReadOnlyList<TeamMember> Everyone => All;

        public static void Register(TeamMember member)
        {
            if (member != null && !All.Contains(member)) All.Add(member);
        }

        public static void Unregister(TeamMember member) => All.Remove(member);

        /// <summary>
        /// Clears the registry. Domain reloads are disabled in many projects for
        /// faster iteration, which leaves static state alive between play
        /// sessions; without this the second run starts with ghosts in the list.
        /// </summary>
        public static void Clear() => All.Clear();

        /// <summary>
        /// Nearest living enemy of <paramref name="self"/>, or null.
        /// </summary>
        public static TeamMember NearestHostile(TeamMember self, Vector3 from, float maxDistance = float.MaxValue)
        {
            if (self == null) return null;

            TeamMember best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = All.Count - 1; i >= 0; i--)
            {
                TeamMember other = All[i];
                if (other == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                if (!self.IsHostileTo(other)) continue;
                if (other.TryGetComponent(out Health health) && !health.IsAlive) continue;

                float sqr = (other.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = other;
            }

            return best;
        }
    }

    /// <summary>
    /// Keeps <see cref="Combatants"/> in step with the scene. Put it on every
    /// player and bot prefab next to <see cref="TeamMember"/>.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    [DisallowMultipleComponent]
    public sealed class CombatantRegistration : MonoBehaviour
    {
        private TeamMember _member;

        private void Awake() => _member = GetComponent<TeamMember>();

        private void OnEnable() => Combatants.Register(_member);

        private void OnDisable() => Combatants.Unregister(_member);
    }
}
