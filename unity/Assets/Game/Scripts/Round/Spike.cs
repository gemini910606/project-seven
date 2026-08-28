using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Round.Rules;

namespace Game.Round
{
    /// <summary>
    /// The bomb: carried by attackers, planted on a site, defused by defenders.
    ///
    /// Progress is accumulated on the server only. A client that walks away, is
    /// killed, or is knocked off the spike loses its progress entirely - partial
    /// credit would let two defenders trade a defuse, which changes the whole
    /// shape of a retake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Spike : NetworkBehaviour
    {
        [Header("Timings")]
        [SerializeField, Min(0.5f)] private float plantSeconds = 4f;
        [SerializeField, Min(0.5f)] private float defuseSeconds = 7f;

        [Header("Interaction")]
        [Tooltip("How close a player must be to plant or defuse, in metres.")]
        [SerializeField, Min(0.5f)] private float interactRadius = 2.5f;

        [Tooltip("Sites the spike may be planted on.")]
        [SerializeField] private BombSite[] sites;

        private readonly NetworkVariable<bool> _planted = new();
        private readonly NetworkVariable<float> _progress = new();

        private float _serverProgress;
        private GameObject _currentActor;
        private bool _hadActorThisTick;

        /// <summary>
        /// The one spike in the scene.
        ///
        /// This exists because <see cref="Player.NetworkPlayer"/> lives on a prefab
        /// asset, and a prefab cannot hold a reference to a scene object - Unity
        /// silently drops it. So its serialized `spike` field is unassignable in
        /// practice, and without a fallback every plant and defuse fails silently
        /// forever. <see cref="Weapons.ShotResolver"/> already had the same problem
        /// and the same answer.
        /// </summary>
        public static Spike Instance { get; private set; }

        public bool IsPlanted => _planted.Value;

        /// <summary>0..1 progress on the current plant or defuse, for the HUD.</summary>
        public float Progress => _progress.Value;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            // Guarded: a second spike loading before this one is destroyed would
            // otherwise null out the live reference on its way out.
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called every frame by a character that is holding the interact key
        /// while in range. The server decides whether that means anything.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void InteractRpc(RpcParams rpcParams = default)
        {
            if (RoundDirector.Instance == null) return;

            AdvanceInteraction(FindCharacter(rpcParams.Receive.SenderClientId));
        }

        /// <summary>Server-side entry point for bots, which have no client id.</summary>
        public void BotInteract(GameObject bot) => AdvanceInteraction(bot);

        private void AdvanceInteraction(GameObject actor)
        {
            if (!IsServer || actor == null) return;

            RoundDirector director = RoundDirector.Instance;
            if (director == null || director.PlayersFrozen) return;

            if (!actor.TryGetComponent(out TeamMember member)) return;

            // A character with no team is on neither side. Asking the director
            // throws, and this runs from an RPC every frame someone holds the key.
            if (member.Team == MatchTeam.None) return;

            Side side = director.SideOf(member.Team);

            // Only attackers plant, only defenders defuse, and each only in the
            // phase where it means anything.
            bool planting = !_planted.Value && side == Side.Attackers && director.Phase == RoundPhase.Live;
            bool defusing = _planted.Value && side == Side.Defenders && director.Phase == RoundPhase.PostPlant;

            if (!planting && !defusing) return;

            // Distance to the spike object only matters once it is on the ground.
            //
            // Before the plant it is notionally carried - CompletePlant moves it to
            // wherever the planter stood - so requiring proximity as well as being
            // on a site made planting impossible: the spike starts away from both
            // sites, and nothing in this project picks it up or moves it there.
            //
            // The simplification is that any attacker standing on a site can plant,
            // rather than one nominated carrier. That is in LATER.md.
            if (_planted.Value
                && Vector3.Distance(actor.transform.position, transform.position) > interactRadius)
            {
                return;
            }

            if (planting && !IsOnASite(actor.transform.position)) return;

            // A different character taking over restarts the bar.
            //
            // Identity is the GameObject, not a client id. Every bot went through
            // BotInteract with the same sentinel id, so one bot inherited another's
            // progress - and partial credit between two defusers is precisely what
            // the timings at the top of this file exist to prevent. Client id 0 is
            // also the host, which collided with the "nobody" sentinel.
            if (actor != _currentActor)
            {
                _currentActor = actor;
                _serverProgress = 0f;
            }

            _hadActorThisTick = true;
            _serverProgress += Time.deltaTime / (planting ? plantSeconds : defuseSeconds);
            _progress.Value = Mathf.Clamp01(_serverProgress);

            if (_serverProgress < 1f) return;

            _serverProgress = 0f;
            _progress.Value = 0f;

            if (planting) CompletePlant(actor.transform.position);
            else director.NotifySpikeDefused();
        }

        private void LateUpdate()
        {
            if (!IsServer) return;

            // Nobody touched it this frame, so whatever was in progress is lost.
            if (!_hadActorThisTick && _serverProgress > 0f)
            {
                _serverProgress = 0f;
                _progress.Value = 0f;
                _currentActor = null;
            }

            _hadActorThisTick = false;
        }

        private void CompletePlant(Vector3 position)
        {
            _planted.Value = true;
            transform.position = position;
            RoundDirector.Instance.NotifySpikePlanted();
        }

        /// <summary>Clears the spike for a new round. Server-side.</summary>
        public void ResetForRound()
        {
            if (!IsServer) return;

            _planted.Value = false;
            _progress.Value = 0f;
            _serverProgress = 0f;
            _currentActor = null;
        }

        private bool IsOnASite(Vector3 position)
        {
            if (sites == null || sites.Length == 0) return true;

            foreach (BombSite site in sites)
            {
                if (site != null && site.Contains(position)) return true;
            }
            return false;
        }

        private GameObject FindCharacter(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject != null ? client.PlayerObject.gameObject : null;
        }
    }
}
