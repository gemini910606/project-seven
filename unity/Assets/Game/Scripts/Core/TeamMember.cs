using UnityEngine;
using Game.Round.Rules;

namespace Game.Core
{
    /// <summary>
    /// Marks which team a character belongs to. On every player and every bot.
    ///
    /// Team is persistent for the match; which SIDE that team is playing changes
    /// at halftime and is owned by <see cref="Game.Round.Rules.MatchCore"/>. Ask
    /// the round director for the side rather than storing it here, or you will
    /// have two sources of truth that disagree for exactly one round.
    ///
    /// KNOWN LIMIT: this value is NOT replicated. The server assigns it and the
    /// server is the only thing that reads it - hit resolution, the round counts
    /// and bot targeting are all server-side, so today that is enough. The first
    /// client-side thing that needs to know a team (a scoreboard, nameplates, a
    /// "you are attacking" banner) has to replicate it first, because a client's
    /// copy still says whatever the prefab said.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamMember : MonoBehaviour
    {
        [SerializeField] private MatchTeam team = MatchTeam.A;

        [Tooltip("True for bots. Used by the bot director to decide which slots it may recycle.")]
        [SerializeField] private bool isBot;

        public MatchTeam Team => team;
        public bool IsBot => isBot;

        public void Assign(MatchTeam value, bool bot)
        {
            team = value;
            isBot = bot;
        }

        /// <summary>True when the other character is on the opposing team.</summary>
        public bool IsHostileTo(TeamMember other) =>
            other != null && other.team != MatchTeam.None && team != MatchTeam.None && other.team != team;

        /// <summary>
        /// Convenience for weapon and perception code, which usually starts from
        /// a collider rather than a component.
        /// </summary>
        public static bool AreHostile(GameObject a, GameObject b)
        {
            if (a == null || b == null) return false;

            TeamMember ta = a.GetComponentInParent<TeamMember>();
            TeamMember tb = b.GetComponentInParent<TeamMember>();
            return ta != null && tb != null && ta.IsHostileTo(tb);
        }
    }
}
