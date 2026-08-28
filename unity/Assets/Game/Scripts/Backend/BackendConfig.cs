using UnityEngine;

namespace Game.Backend
{
    /// <summary>
    /// Where the backend lives and how the client talks to it.
    ///
    /// The signing secret sits in this asset, which means it ends up in the
    /// build. That is unavoidable for a client-signed scheme and is why the
    /// server treats a valid signature as "probably our client" rather than
    /// "these numbers are true". Do not reuse this value for anything that
    /// matters, and never put a Cloudflare API token here.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Backend Config", fileName = "BackendConfig")]
    public sealed class BackendConfig : ScriptableObject
    {
        [Tooltip("Base URL including the version segment, e.g. https://api.example.com/v1")]
        public string BaseUrl = "https://api.example.com/v1";

        [Tooltip("Must match RUN_HMAC_SECRET on the Worker. See the warning above.")]
        public string RunSigningSecret = "dev-only-not-a-real-secret";

        [Tooltip("Sent with every run and compared against MIN_CLIENT_VERSION server-side.")]
        public string ClientVersion = "0.1.0";

        [Tooltip("Seconds before a request is abandoned. Keep short: no request here should ever block play.")]
        [Range(1, 30)] public int TimeoutSeconds = 8;

        [Tooltip("Turn the whole backend off for offline builds and local testing.")]
        public bool Enabled = true;
    }
}
