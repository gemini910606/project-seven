using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// Hosts or joins a match through Unity Relay, using a short join code.
    ///
    /// Relay is what makes "just play with friends" actually work: it punches
    /// through NAT, so nobody port-forwards, nobody needs a static IP, and
    /// nobody exposes their home address to the others. It carries real UDP, so
    /// unlike a WebSocket relay a dropped packet does not stall the ones behind
    /// it. Free up to 50 concurrent players, which is ten times more than this
    /// game will ever need.
    ///
    /// The topology is host-authoritative: one friend's machine IS the server
    /// and also plays on it. Be honest about the two consequences:
    ///
    ///  1. The host has no network latency, so they win close peeks slightly
    ///     more often. There is no lag compensation here, and adding it properly
    ///     is a large piece of work. Rotate who hosts if it starts to matter.
    ///  2. The host can trivially cheat, because they run the simulation. Among
    ///     friends that is a social problem, not an engineering one, and it is
    ///     the reason this project has no anti-cheat and needs none.
    ///
    /// API surface to re-verify against the docs when you first build this, as
    /// the Multiplayer Services SDK is young and moves:
    /// https://docs.unity.com/en-us/mps-sdk/create-session
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionLauncher : MonoBehaviour
    {
        [Tooltip("Total players in a session, both teams. Bots fill the rest locally on the host.")]
        [SerializeField, Min(2)] private int maxPlayers = 10;

        private ISession _session;

        /// <summary>The code to paste into Discord. Empty until hosting starts.</summary>
        public string JoinCode { get; private set; } = string.Empty;

        public bool IsInSession => _session != null;

        /// <summary>Raised with a human-readable status for the menu to display.</summary>
        public event Action<string> StatusChanged;

        /// <summary>Raised when hosting or joining fails, with a message worth showing.</summary>
        public event Action<string> Failed;

        private async void Start() => await EnsureSignedIn();

        /// <summary>
        /// Anonymous sign-in: no accounts, no passwords, no personal data. Unity
        /// mints an id and caches it on the device. That is all this game needs.
        /// </summary>
        private async Task<bool> EnsureSignedIn()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Report("Connecting to Unity services...");
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Report("Ready.");
                return true;
            }
            catch (Exception e)
            {
                Fail($"Could not reach Unity services: {e.Message}");
                return false;
            }
        }

        public async Task<string> HostAsync()
        {
            if (!await EnsureSignedIn()) return null;

            try
            {
                Report("Creating session...");

                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);

                JoinCode = _session.Code;
                Report($"Hosting. Join code: {JoinCode}");
                return JoinCode;
            }
            catch (Exception e)
            {
                Fail($"Could not host: {e.Message}");
                return null;
            }
        }

        public async Task<bool> JoinAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Fail("Enter a join code first.");
                return false;
            }

            if (!await EnsureSignedIn()) return false;

            try
            {
                Report("Joining...");

                // Codes are case-insensitive but the service wants them uppercase,
                // and people paste them out of Discord with stray whitespace.
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                    joinCode.Trim().ToUpperInvariant());

                JoinCode = _session.Code;
                Report("Joined.");
                return true;
            }
            catch (Exception e)
            {
                Fail($"Could not join: {e.Message}");
                return false;
            }
        }

        public async Task LeaveAsync()
        {
            if (_session == null) return;

            try
            {
                await _session.LeaveAsync();
            }
            catch (Exception e)
            {
                // Leaving is best-effort: the session may already be gone because
                // the host quit, which is not worth surfacing to the player.
                Debug.LogWarning($"Leaving the session failed: {e.Message}");
            }
            finally
            {
                _session = null;
                JoinCode = string.Empty;

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }

                Report("Left the session.");
            }
        }

        private void Report(string message)
        {
            Debug.Log($"[Session] {message}");
            StatusChanged?.Invoke(message);
        }

        private void Fail(string message)
        {
            Debug.LogError($"[Session] {message}");
            Failed?.Invoke(message);
        }
    }
}
