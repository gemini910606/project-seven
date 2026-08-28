using System;

namespace Game.Backend
{
    /// <summary>
    /// Wire format for POST /v1/runs. Field names are lowerCamelCase because
    /// that is what the Worker validates; JsonUtility serialises field names
    /// verbatim, so renaming a field here silently breaks the endpoint.
    /// </summary>
    [Serializable]
    public sealed class RunSubmission
    {
        public string id;
        public string playerId;
        public string missionId;
        public int score;
        public int durationMs;
        public int kills;
        public int shotsFired;
        public int shotsHit;
        public int damageTaken;
        public int peakAlert;
        public string outcome;
        public string clientVersion;
        public string platform;
        public string signature;
    }

    [Serializable]
    public sealed class RunSubmissionResponse
    {
        public string id;
        public bool accepted;
        public bool duplicate;
        public string[] flags;
    }

    [Serializable]
    public sealed class PlayerRegistration
    {
        public string id;
        public string displayName;
    }

    [Serializable]
    public sealed class LeaderboardEntry
    {
        public int rank;
        public string playerName;
        public int score;
        public int durationMs;
        public int kills;
        public long submittedAt;
    }

    [Serializable]
    public sealed class LeaderboardResponse
    {
        public string missionId;
        public LeaderboardEntry[] entries;
    }

    [Serializable]
    public sealed class RemoteConfig
    {
        public string motd;
        public bool leaderboardEnabled;
        public bool telemetryEnabled;
        public string minClientVersion;
    }
}
