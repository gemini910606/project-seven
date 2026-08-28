using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.Round.Rules;

namespace Game.Round
{
    /// <summary>
    /// Spawn points for each side. Because sides swap at halftime, these are
    /// keyed by SIDE, not by team - the attacker spawn stays the attacker spawn
    /// and the teams move between them.
    /// </summary>
    public sealed class TeamSpawns : MonoBehaviour
    {
        [SerializeField] private List<Transform> attackerSpawns = new();
        [SerializeField] private List<Transform> defenderSpawns = new();

        private int _attackerCursor;
        private int _defenderCursor;

        /// <summary>
        /// Moves a character to the next free spawn for its side. Round-robins
        /// rather than picking at random so five teammates never stack inside
        /// each other on the same point.
        /// </summary>
        public void PlaceAtSpawn(Transform character, Side side)
        {
            List<Transform> points = side == Side.Attackers ? attackerSpawns : defenderSpawns;
            if (points.Count == 0)
            {
                Debug.LogError($"{name}: no spawn points configured for {side}.", this);
                return;
            }

            int cursor = side == Side.Attackers ? _attackerCursor++ : _defenderCursor++;
            Transform point = points[cursor % points.Count];
            if (point == null) return;

            // A CharacterController overwrites transform writes on its next
            // internal update, so it has to be disabled across a teleport.
            //
            // A NavMeshAgent does exactly the same thing, and bots have one of
            // those instead - so respawned bots used to be dragged back toward
            // where they died. Re-enabling the agent also re-projects it onto the
            // navmesh at its new position, which is what makes it path from the
            // spawn rather than from wherever it thought it was.
            var controller = character.GetComponent<CharacterController>();
            var agent = character.GetComponent<NavMeshAgent>();

            bool hadController = controller != null && controller.enabled;
            bool hadAgent = agent != null && agent.enabled;

            if (hadController) controller.enabled = false;
            if (hadAgent) agent.enabled = false;

            character.SetPositionAndRotation(point.position, point.rotation);

            if (hadController) controller.enabled = true;
            if (hadAgent) agent.enabled = true;
        }

        /// <summary>Called at the start of each round so the round-robin restarts.</summary>
        public void ResetCursors()
        {
            _attackerCursor = 0;
            _defenderCursor = 0;
        }

        private void OnDrawGizmos()
        {
            DrawSpawnGizmos(attackerSpawns, new Color(1f, 0.4f, 0.2f, 0.85f));
            DrawSpawnGizmos(defenderSpawns, new Color(0.2f, 0.7f, 1f, 0.85f));
        }

        private static void DrawSpawnGizmos(List<Transform> points, Color color)
        {
            Gizmos.color = color;
            foreach (Transform point in points)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.position + Vector3.up, 0.5f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.5f);
            }
        }
    }
}
