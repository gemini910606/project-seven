using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The tally for one run. Plain C# so it can be unit-tested without entering
    /// play mode, and so the same object can be handed straight to the backend
    /// client at the end of a run.
    /// </summary>
    [Serializable]
    public sealed class RunStats
    {
        public string MissionId = string.Empty;
        public int Kills;
        public int ShotsFired;
        public int ShotsHit;
        public int DamageTaken;
        public int PeakAlert;
        public int ObjectivesCompleted;
        public int ObjectivesTotal;
        public float DurationSeconds;
        public RunOutcome Outcome = RunOutcome.Aborted;

        public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;

        public void RecordShot() => ShotsFired++;

        public void RecordHit() => ShotsHit++;

        public void RecordKill() => Kills++;

        public void RecordDamageTaken(float amount) =>
            DamageTaken += Mathf.Max(0, Mathf.RoundToInt(amount));

        public void RecordAlert(int level) => PeakAlert = Mathf.Max(PeakAlert, level);

        public void Reset()
        {
            Kills = 0;
            ShotsFired = 0;
            ShotsHit = 0;
            DamageTaken = 0;
            PeakAlert = 0;
            ObjectivesCompleted = 0;
            DurationSeconds = 0f;
            Outcome = RunOutcome.Aborted;
        }
    }
}
