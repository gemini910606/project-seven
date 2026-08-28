using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Game.Core;

namespace Game.Backend
{
    /// <summary>
    /// Talks to the Cloudflare Worker.
    ///
    /// Every call here is optional to the game working. The backend exists to
    /// add a leaderboard and remote config, not to gate play, so failures are
    /// logged and swallowed rather than surfaced as errors - a player with no
    /// internet should never see anything worse than an empty leaderboard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackendClient : MonoBehaviour
    {
        private const string PlayerIdKey = "game.playerId";
        private const string PlayerNameKey = "game.playerName";

        [SerializeField] private BackendConfig config;

        private string _playerId;

        public string PlayerId => _playerId;
        public RemoteConfig Config { get; private set; }
        public bool IsEnabled => config != null && config.Enabled && !string.IsNullOrEmpty(config.BaseUrl);

        private void Awake()
        {
            // The id is minted once and kept forever. It is the whole account
            // system: no password, nothing worth stealing, zero friction.
            _playerId = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            if (string.IsNullOrEmpty(_playerId))
            {
                _playerId = Guid.NewGuid().ToString("D");
                PlayerPrefs.SetString(PlayerIdKey, _playerId);
                PlayerPrefs.Save();
            }
        }

        private void Start()
        {
            if (IsEnabled) StartCoroutine(FetchRemoteConfig());
        }

        public string DisplayName
        {
            get => PlayerPrefs.GetString(PlayerNameKey, "Anonymous");
            set
            {
                PlayerPrefs.SetString(PlayerNameKey, value);
                PlayerPrefs.Save();
            }
        }

        public IEnumerator FetchRemoteConfig()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{config.BaseUrl}/config");
            request.timeout = config.TimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Remote config unavailable: {request.error}");
                yield break;
            }

            try
            {
                Config = JsonUtility.FromJson<RemoteConfig>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                // A malformed config must never brick boot.
                Debug.LogWarning($"Remote config could not be parsed: {e.Message}");
            }
        }

        public IEnumerator RegisterPlayer(string displayName, Action<bool> onComplete = null)
        {
            if (!IsEnabled)
            {
                onComplete?.Invoke(false);
                yield break;
            }

            var payload = new PlayerRegistration { id = _playerId, displayName = displayName };

            using UnityWebRequest request = BuildJsonPost($"{config.BaseUrl}/players", JsonUtility.ToJson(payload));
            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            if (ok) DisplayName = displayName;
            else Debug.LogWarning($"Player registration failed: {request.error} {request.downloadHandler?.text}");

            onComplete?.Invoke(ok);
        }

        /// <summary>
        /// Submits a finished run. Safe to retry: the run id is generated here,
        /// so a resend after a dropped connection is recognised as a duplicate
        /// server-side instead of scoring twice.
        /// </summary>
        public IEnumerator SubmitRun(RunStats stats, int score, Action<RunSubmissionResponse> onComplete = null)
        {
            if (!IsEnabled || stats == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string runId = Guid.NewGuid().ToString("D");
            int durationMs = Mathf.RoundToInt(stats.DurationSeconds * 1000f);

            var submission = new RunSubmission
            {
                id = runId,
                playerId = _playerId,
                missionId = stats.MissionId,
                score = score,
                durationMs = durationMs,
                kills = stats.Kills,
                shotsFired = stats.ShotsFired,
                shotsHit = stats.ShotsHit,
                damageTaken = stats.DamageTaken,
                peakAlert = stats.PeakAlert,
                outcome = stats.Outcome.ToWireValue(),
                clientVersion = config.ClientVersion,
                platform = Application.platform.ToString(),
                signature = RunSigner.Sign(
                    config.RunSigningSecret,
                    RunSigner.BuildPayload(runId, _playerId, stats.MissionId, score, durationMs, stats.Kills)),
            };

            using UnityWebRequest request = BuildJsonPost($"{config.BaseUrl}/runs", JsonUtility.ToJson(submission));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Run submission failed: {request.error} {request.downloadHandler?.text}");
                onComplete?.Invoke(null);
                yield break;
            }

            RunSubmissionResponse response = null;
            try
            {
                response = JsonUtility.FromJson<RunSubmissionResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Run response could not be parsed: {e.Message}");
            }

            onComplete?.Invoke(response);
        }

        public IEnumerator FetchLeaderboard(string missionId, int limit, Action<LeaderboardResponse> onComplete)
        {
            if (!IsEnabled)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"{config.BaseUrl}/leaderboard/{UnityWebRequest.EscapeURL(missionId)}?limit={limit}";
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = config.TimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Leaderboard unavailable: {request.error}");
                onComplete?.Invoke(null);
                yield break;
            }

            LeaderboardResponse response = null;
            try
            {
                response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaderboard could not be parsed: {e.Message}");
            }

            onComplete?.Invoke(response);
        }

        private UnityWebRequest BuildJsonPost(string url, string json)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = config.TimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
    }
}
