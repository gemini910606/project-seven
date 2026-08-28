using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Bots
{
    /// <summary>
    /// Switches a bot's brain off on every machine that is not the server.
    ///
    /// Every other bot component is a plain MonoBehaviour with no notion of who
    /// is authoritative, so on a client all of them ran: perception picked its
    /// own targets, the brain steered the client's NavMeshAgent against a
    /// server-authoritative NetworkTransform, and BotWeaponUser called
    /// ShotResolver directly - applying damage to that client's own copies of
    /// Health. Health is not replicated, so every client built a private and
    /// wrong picture of which bots were alive, while the server had a different
    /// one and only the server's counted.
    ///
    /// Position still replicates; only the thinking is server-side. That is what
    /// BotWeaponUser already assumed when it chose to call the resolver directly
    /// instead of sending an RPC the way a player does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotServerAuthority : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (IsServer) return;

            if (TryGetComponent(out BotBrain brain)) brain.enabled = false;
            if (TryGetComponent(out BotPerception perception)) perception.enabled = false;
            if (TryGetComponent(out BotLocomotion locomotion)) locomotion.enabled = false;
            if (TryGetComponent(out BotWeaponUser weaponUser)) weaponUser.enabled = false;

            // Left on, the agent keeps steering the transform against whatever
            // the NetworkTransform is replicating in, and the bot jitters.
            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
        }
    }
}
