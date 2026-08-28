using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// <summary>
    /// The only way to actually start a game with someone else.
    ///
    /// SessionLauncher could host and join over Relay from its first commit and
    /// nothing ever called it. NetworkManager's own Start Host button connects
    /// over the transport's configured address - localhost - so it works on one
    /// machine and cannot reach a friend, which makes it easy to believe the
    /// networking is done when the part that crosses the internet has never run.
    /// The feature existed and had no door.
    ///
    /// IMGUI on purpose. A Canvas means prefabs, anchors, a font and six wired
    /// references, all of which have to be rebuilt when the real menu arrives.
    /// This needs none of it and deletes in one file. It is a door, not a design.
    /// </summary>
    [RequireComponent(typeof(SessionLauncher))]
    [DisallowMultipleComponent]
    public sealed class SessionMenu : MonoBehaviour
    {
        private SessionLauncher _launcher;
        private string _code = string.Empty;
        private string _status = "Starting up...";

        private void Awake() => _launcher = GetComponent<SessionLauncher>();

        private void OnEnable()
        {
            _launcher.StatusChanged += OnStatus;
            _launcher.Failed += OnStatus;
        }

        private void OnDisable()
        {
            _launcher.StatusChanged -= OnStatus;
            _launcher.Failed -= OnStatus;
        }

        private void OnStatus(string message) => _status = message;

        private void OnGUI()
        {
            NetworkManager network = NetworkManager.Singleton;

            if (network != null && network.IsListening)
            {
                DrawInMatch();
                return;
            }

            DrawConnectPanel();
        }

        /// <summary>
        /// Once a match is running the player's cursor is locked and this must get
        /// out of the way - so it keeps only the two things worth having: the code
        /// to paste to whoever is still joining, and a way back out.
        /// </summary>
        private void DrawInMatch()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 260f, 70f), GUI.skin.box);

            if (!string.IsNullOrEmpty(_launcher.JoinCode))
            {
                GUILayout.Label($"Join code: {_launcher.JoinCode}");
            }

            if (GUILayout.Button("Leave")) _ = _launcher.LeaveAsync();

            GUILayout.EndArea();
        }

        private void DrawConnectPanel()
        {
            const float width = 320f;
            const float height = 210f;

            Rect panel = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(panel, GUI.skin.box);

            GUILayout.Label("PROJECT SEVEN");
            GUILayout.Space(8f);

            if (GUILayout.Button("Host", GUILayout.Height(32f))) _ = _launcher.HostAsync();

            GUILayout.Space(12f);
            GUILayout.Label("Join code");

            // Codes are short; the cap stops a pasted paragraph from making the
            // field unusable.
            _code = GUILayout.TextField(_code, 12);

            // Disabled with nothing to send, so an empty Join cannot look broken.
            GUI.enabled = !string.IsNullOrWhiteSpace(_code);
            if (GUILayout.Button("Join", GUILayout.Height(28f))) _ = _launcher.JoinAsync(_code);
            GUI.enabled = true;

            GUILayout.Space(10f);
            GUILayout.Label(_status);

            GUILayout.EndArea();
        }
    }
}
