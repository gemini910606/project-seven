using System.Collections.Generic;
using UnityEngine;
using Game.Weapons;

namespace Game.Player
{
    /// <summary>
    /// Holds the weapons a character carries and decides which one is active.
    ///
    /// Weapon instances are created once and enabled/disabled, not spawned and
    /// destroyed on every switch: a switch happens constantly in a firefight and
    /// instantiating a model mid-fight is a visible hitch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponHolder : MonoBehaviour
    {
        [Tooltip("Bone the weapon model is parented to. Usually the right hand.")]
        [SerializeField] private Transform handSocket;

        [Tooltip("Weapons this character starts with. Index 0 is equipped on spawn.")]
        [SerializeField] private List<Weapon> weapons = new();

        private int _index = -1;

        public Weapon Current => _index >= 0 && _index < weapons.Count ? weapons[_index] : null;
        public int Count => weapons.Count;
        public Transform HandSocket => handSocket;

        private void Start()
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null) weapons[i].gameObject.SetActive(false);
            }

            if (weapons.Count > 0) Equip(0);
        }

        public void Equip(int index)
        {
            if (index < 0 || index >= weapons.Count || index == _index) return;

            Weapon previous = Current;
            if (previous != null)
            {
                // A reload interrupted by a swap must not silently finish later.
                previous.CancelReload();
                previous.gameObject.SetActive(false);
            }

            _index = index;

            Weapon next = Current;
            if (next != null) next.gameObject.SetActive(true);
        }

        public void EquipNext()
        {
            if (weapons.Count == 0) return;
            Equip((_index + 1) % weapons.Count);
        }

        /// <summary>Adds a weapon at runtime, e.g. one picked up off the ground.</summary>
        public void AddWeapon(Weapon weapon, bool equipImmediately = true)
        {
            if (weapon == null || weapons.Contains(weapon)) return;

            weapons.Add(weapon);
            weapon.gameObject.SetActive(false);

            if (equipImmediately) Equip(weapons.Count - 1);
        }
    }
}
