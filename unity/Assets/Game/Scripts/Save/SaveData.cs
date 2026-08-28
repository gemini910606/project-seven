using System;
using System.Collections.Generic;

namespace Game.Save
{
    /// <summary>
    /// The whole save file.
    ///
    /// <see cref="Version"/> exists so that a save written by an older build can
    /// be migrated rather than discarded. Bump it whenever a field's meaning
    /// changes, and handle the old value in SaveSystem.Migrate. Silently
    /// deleting a player's progress because a field moved is unforgivable and
    /// entirely avoidable.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string PlayerId = string.Empty;
        public string DisplayName = "Anonymous";

        public int TotalRuns;
        public int TotalKills;
        public int BestScore;
        public float BestTimeSeconds;

        public List<MissionRecord> Missions = new();

        public SettingsData Settings = new();

        public MissionRecord GetOrCreate(string missionId)
        {
            foreach (MissionRecord record in Missions)
            {
                if (record.MissionId == missionId) return record;
            }

            var created = new MissionRecord { MissionId = missionId };
            Missions.Add(created);
            return created;
        }
    }

    [Serializable]
    public sealed class MissionRecord
    {
        public string MissionId = string.Empty;
        public bool Completed;
        public int Attempts;
        public int BestScore;
        public float BestTimeSeconds;
    }

    [Serializable]
    public sealed class SettingsData
    {
        public float MasterVolume = 0.8f;
        public float MouseSensitivity = 0.12f;
        public bool InvertY;
        public int QualityLevel = -1;
    }
}
