using NUnit.Framework;
using UnityEngine;
using Game.AI;

namespace Game.Tests
{
    public sealed class AlertSystemTests
    {
        private GameObject _host;
        private AlertSystem _alert;

        /// <summary>
        /// Mirrors the default levelThresholds on AlertSystem. Duplicated
        /// deliberately: if someone retunes the component, these tests should
        /// fail and make them think, not quietly follow along.
        /// </summary>
        private static readonly float[] Thresholds = { 20f, 55f, 110f, 200f, 320f };

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AlertSystemHost");
            _alert = _host.AddComponent<AlertSystem>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void StartsClear()
        {
            Assert.AreEqual(0, _alert.Level);
            Assert.AreEqual(0f, _alert.Heat, 0.001f);
        }

        [Test]
        public void HeatBelowTheFirstThresholdDoesNotRaiseTheLevel()
        {
            _alert.AddHeat(Thresholds[0] - 1f);
            Assert.AreEqual(0, _alert.Level);
        }

        [Test]
        public void ReachingAThresholdRaisesTheLevel()
        {
            _alert.AddHeat(Thresholds[0]);
            Assert.AreEqual(1, _alert.Level);
        }

        [Test]
        public void EachThresholdRaisesExactlyOneLevel()
        {
            for (int i = 0; i < Thresholds.Length; i++)
            {
                _alert.ClearHeat();
                _alert.AddHeat(Thresholds[i]);
                Assert.AreEqual(i + 1, _alert.Level, $"Threshold index {i} produced the wrong level.");
            }
        }

        [Test]
        public void LevelIsCappedAtMax()
        {
            _alert.AddHeat(100000f);
            Assert.AreEqual(AlertSystem.MaxLevel, _alert.Level);
        }

        [Test]
        public void HeatIsClampedSoALongFirefightCannotBankEscalation()
        {
            _alert.AddHeat(100000f);
            float first = _alert.Heat;

            _alert.AddHeat(100000f);
            Assert.AreEqual(first, _alert.Heat, 0.001f, "Heat must saturate at maxHeat.");
        }

        [Test]
        public void LevelChangedFiresWithPreviousAndCurrent()
        {
            int previous = -1;
            int current = -1;
            _alert.LevelChanged += (from, to) => { previous = from; current = to; };

            _alert.AddHeat(Thresholds[0]);

            Assert.AreEqual(0, previous);
            Assert.AreEqual(1, current);
        }

        [Test]
        public void LevelChangedDoesNotFireWhenTheLevelIsUnchanged()
        {
            _alert.AddHeat(Thresholds[0]);

            int calls = 0;
            _alert.LevelChanged += (_, _) => calls++;

            // Still comfortably inside level 1.
            _alert.AddHeat(1f);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void ClearHeatDropsToZero()
        {
            _alert.AddHeat(Thresholds[4]);
            _alert.ClearHeat();

            Assert.AreEqual(0, _alert.Level);
            Assert.AreEqual(0f, _alert.Heat, 0.001f);
        }

        [Test]
        public void ProgressToNextLevelSpansTheBandBetweenThresholds()
        {
            _alert.AddHeat(Thresholds[0]);
            Assert.AreEqual(0f, _alert.ProgressToNextLevel, 0.01f, "Just into a level means no progress yet.");

            _alert.AddHeat((Thresholds[1] - Thresholds[0]) * 0.5f);
            Assert.AreEqual(0.5f, _alert.ProgressToNextLevel, 0.02f);
        }

        [Test]
        public void ProgressIsFullAtMaxLevel()
        {
            _alert.AddHeat(100000f);
            Assert.AreEqual(1f, _alert.ProgressToNextLevel, 0.001f);
        }

        [Test]
        public void NonPositiveHeatIsIgnored()
        {
            _alert.AddHeat(Thresholds[0]);
            float before = _alert.Heat;

            _alert.AddHeat(0f);
            _alert.AddHeat(-50f);

            Assert.AreEqual(before, _alert.Heat, 0.001f);
        }

        [Test]
        public void ReportedEventsAccumulateTowardsEscalation()
        {
            for (int i = 0; i < 6; i++) _alert.ReportGunshotHeard();

            Assert.Greater(_alert.Level, 0, "Six gunshots should be enough to be noticed.");
        }
    }
}
