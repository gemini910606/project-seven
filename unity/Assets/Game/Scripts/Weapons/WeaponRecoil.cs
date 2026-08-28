using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Camera kick. Kept separate from spread because they solve different
    /// problems: spread is what the bullet does, recoil is what the player has to
    /// fight. A weapon can have one without the other.
    /// </summary>
    public struct WeaponRecoil
    {
        private Vector2 _current;
        private int _shotParity;

        /// <summary>Pitch (x) and yaw (y) offset in degrees to add to the look angles.</summary>
        public readonly Vector2 Current => _current;

        public void RegisterShot(WeaponDefinition def)
        {
            // Alternating horizontal kick gives a readable zig-zag pattern the
            // player can learn to counter, instead of unlearnable noise.
            _shotParity++;
            float sign = (_shotParity & 1) == 0 ? 1f : -1f;
            float horizontal = def.RecoilHorizontal * sign * Random.Range(0.6f, 1f);

            _current.x -= def.RecoilVertical;
            _current.y += horizontal;
        }

        public void Recover(WeaponDefinition def, float deltaTime)
        {
            _current = Vector2.Lerp(
                _current, Vector2.zero, 1f - Mathf.Exp(-def.RecoilRecoveryPerSecond * deltaTime));
        }

        public void Reset()
        {
            _current = Vector2.zero;
            _shotParity = 0;
        }
    }
}
