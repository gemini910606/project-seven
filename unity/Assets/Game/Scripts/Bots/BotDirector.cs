using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Round;
using Game.Round.Rules;

namespace Game.Bots
{
    /// <summary>
    /// Fills both teams up to full with bots.
    ///
    /// This is what makes five-versus-five work when three friends are online,
    /// and it is why the AI survived the pivot away from an open world at all.
    /// It also means you can test a full match alone, which is worth more to a
    /// hobby project than almost any other single feature.
    ///
    /// Server-only. Bots are ordinary NetworkObjects, so clients see them exactly
    /// as they see each other.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotDirector : NetworkBehaviour
    {
        [SerializeField] private GameObject botPrefab;

        [Tooltip("Objective each side plays for. Attackers walk to it; defenders hold near it.")]
        [SerializeField] private Transform objective;

        [SerializeField] private TeamSpawns spawns;

        [Tooltip("Seconds between checks for empty slots. Humans join between rounds, not mid-frame.")]
        [SerializeField, Min(0.5f)] private float refillInterval = 2f;

        private readonly List<GameObject> _bots = new();
        private float _nextRefillTime;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            if (botPrefab == null)
            {
                Debug.LogError($"{name}: BotDirector has no bot prefab.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!IsServer || Time.time < _nextRefillTime) return;
            _nextRefillTime = Time.time + refillInterval;

            RoundDirector director = RoundDirector.Instance;
            if (director == null) return;

            // Only rebalance between rounds. Spawning a bot mid-round would drop
            // a fresh enemy into a fight that has already been fought.
            if (director.Phase != RoundPhase.Prep) return;

            Refill(MatchTeam.A);
            Refill(MatchTeam.B);
        }

        private void Refill(MatchTeam team)
        {
            int wanted = TeamSizeTarget();
            int present = CountOnTeam(team);

            for (int i = present; i < wanted; i++) SpawnBot(team);

            // If humans joined since last round, retire the surplus bots rather
            // than fielding six against five.
            for (int i = present; i > wanted; i--) RemoveOneBot(team);
        }

        private int TeamSizeTarget()
        {
            RoundDirector director = RoundDirector.Instance;
            return director != null ? director.TeamSize : 5;
        }

        private static int CountOnTeam(MatchTeam team)
        {
            int count = 0;
            foreach (TeamMember member in Combatants.Everyone)
            {
                if (member != null && member.Team == team) count++;
            }
            return count;
        }

        private void SpawnBot(MatchTeam team)
        {
            GameObject bot = Instantiate(botPrefab);

            if (bot.TryGetComponent(out TeamMember member)) member.Assign(team, bot: true);
            if (bot.TryGetComponent(out BotBrain brain)) brain.SetObjective(objective);

            if (bot.TryGetComponent(out NetworkObject netObject)) netObject.Spawn();

            if (spawns != null && RoundDirector.Instance != null)
            {
                spawns.PlaceAtSpawn(bot.transform, RoundDirector.Instance.SideOf(team));
            }

            if (bot.TryGetComponent(out Health health)) RoundDirector.Instance?.Register(health);

            _bots.Add(bot);
        }

        private void RemoveOneBot(MatchTeam team)
        {
            for (int i = _bots.Count - 1; i >= 0; i--)
            {
                GameObject bot = _bots[i];
                if (bot == null)
                {
                    _bots.RemoveAt(i);
                    continue;
                }

                if (!bot.TryGetComponent(out TeamMember member) || member.Team != team) continue;

                if (bot.TryGetComponent(out Health health)) RoundDirector.Instance?.Unregister(health);
                if (bot.TryGetComponent(out NetworkObject netObject) && netObject.IsSpawned) netObject.Despawn();
                else Destroy(bot);

                _bots.RemoveAt(i);
                return;
            }
        }
    }
}
