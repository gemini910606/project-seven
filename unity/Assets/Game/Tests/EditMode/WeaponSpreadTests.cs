using NUnit.Framework;
using UnityEngine;
using Game.Weapons;

namespace Game.Tests
{
    public sealed class WeaponSpreadTests
    {
        private WeaponDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _definition.BaseSpreadDegrees = 0.5f;
            _definition.HipFireSpreadDegrees = 3f;
            _definition.SpreadPerMoveSpeed = 0.25f;
            _definition.SpreadPerShot = 0.4f;
            _definition.MaxSpreadDegrees = 5f;
            _definition.SpreadRecoveryPerSecond = 8f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_definition);

        [Test]
        public void AimingIsTighterThanHipFiring()
        {
            var spread = new WeaponSpread();

            float aimed = spread.CurrentDegrees(_definition, aiming: true, moveSpeed: 0f);
            float hip = spread.CurrentDegrees(_definition, aiming: false, moveSpeed: 0f);

            Assert.Less(aimed, hip);
            Assert.AreEqual(_definition.HipFireSpreadDegrees, hip - aimed, 0.001f);
        }

        [Test]
        public void MovingWidensTheCone()
        {
            var spread = new WeaponSpread();

            float still = spread.CurrentDegrees(_definition, aiming: true, moveSpeed: 0f);
            float running = spread.CurrentDegrees(_definition, aiming: true, moveSpeed: 6f);

            Assert.Greater(running, still);
        }

        [Test]
        public void SustainedFireAccumulatesSpread()
        {
            var spread = new WeaponSpread();
            float before = spread.CurrentDegrees(_definition, aiming: true, moveSpeed: 0f);

            spread.RegisterShot(_definition);
            spread.RegisterShot(_definition);

            Assert.Greater(spread.CurrentDegrees(_definition, aiming: true, moveSpeed: 0f), before);
        }

        [Test]
        public void AccumulatedSpreadIsCapped()
        {
            var spread = new WeaponSpread();
            for (int i = 0; i < 200; i++) spread.RegisterShot(_definition);

            Assert.LessOrEqual(spread.Accumulated, _definition.MaxSpreadDegrees + 0.001f);
        }

        [Test]
        public void RecoveryReducesAccumulatedSpread()
        {
            var spread = new WeaponSpread();
            for (int i = 0; i < 5; i++) spread.RegisterShot(_definition);

            float peak = spread.Accumulated;
            spread.Recover(_definition, 0.1f);

            Assert.Less(spread.Accumulated, peak);
        }

        [Test]
        public void RecoveryNeverGoesNegative()
        {
            var spread = new WeaponSpread();
            spread.RegisterShot(_definition);
            spread.Recover(_definition, 100f);

            Assert.AreEqual(0f, spread.Accumulated, 0.0001f);
        }

        [Test]
        public void ResetClearsAccumulation()
        {
            var spread = new WeaponSpread();
            spread.RegisterShot(_definition);
            spread.Reset();

            Assert.AreEqual(0f, spread.Accumulated, 0.0001f);
        }

        [Test]
        public void ZeroConeLeavesTheDirectionUntouched()
        {
            Vector3 forward = Vector3.forward;
            Assert.AreEqual(forward, WeaponSpread.Apply(forward, 0f));
        }

        [Test]
        public void PerturbedDirectionStaysInsideTheCone()
        {
            const float cone = 5f;

            for (int i = 0; i < 500; i++)
            {
                Vector3 result = WeaponSpread.Apply(Vector3.forward, cone);

                Assert.AreEqual(1f, result.magnitude, 0.001f, "Result must be normalised.");
                Assert.LessOrEqual(
                    Vector3.Angle(Vector3.forward, result), cone + 0.01f,
                    "A pellet left the cone the weapon promised.");
            }
        }

        [Test]
        public void StraightUpDoesNotProduceNaN()
        {
            // Vector3.Cross(up, up) is zero, so the naive basis construction
            // degenerates here. Shooting at the sky must not corrupt the ray.
            for (int i = 0; i < 100; i++)
            {
                Vector3 result = WeaponSpread.Apply(Vector3.up, 5f);

                Assert.IsFalse(float.IsNaN(result.x) || float.IsNaN(result.y) || float.IsNaN(result.z));
                Assert.AreEqual(1f, result.magnitude, 0.001f);
            }
        }
    }
}
