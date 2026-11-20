using BattleHorizons.Characters;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Locks the pure math behind the motion rig: exponential damping never
    /// overshoots, procedural lean signs/clamps are stable, and the IK weight
    /// curves behave (foot IK fades out while running, look/hand weights ramp
    /// hip→aim). These are the numbers that decide whether the character feels
    /// "blended" or "video-like", so they are a contract, not a vibe.
    /// </summary>
    public class CharacterMotionTests
    {
        [Test]
        public void DampConvergesWithoutOvershoot()
        {
            var value = 0f;
            for (var i = 0; i < 240; i++)
            {
                var previous = value;
                value = CharacterMotionMath.DampTowards(value, 7.2f, 0.14f, 1f / 60f);
                Assert.GreaterOrEqual(value, previous, "monotonic approach");
                Assert.LessOrEqual(value, 7.2f + 0.0001f, "never overshoots");
            }

            Assert.AreEqual(7.2f, value, 0.01f, "converges to target");
        }

        [Test]
        public void DampIsFrameRateIndependentAndHandlesEdgeCases()
        {
            // 60 fps vs 120 fps over one second land within a hair of each other.
            var a = 0f;
            for (var i = 0; i < 60; i++)
            {
                a = CharacterMotionMath.DampTowards(a, 1f, 0.2f, 1f / 60f);
            }

            var b = 0f;
            for (var i = 0; i < 120; i++)
            {
                b = CharacterMotionMath.DampTowards(b, 1f, 0.2f, 1f / 120f);
            }

            Assert.AreEqual(a, b, 0.01f);
            Assert.AreEqual(5f, CharacterMotionMath.DampTowards(1f, 5f, 0.1f, 0f), "zero dt snaps");
            Assert.AreEqual(5f, CharacterMotionMath.DampTowards(1f, 5f, 0f, 0.1f), "zero smooth time snaps");
        }

        [Test]
        public void LeanSignsFollowStrafeAndTurnDirection()
        {
            // Strafing right (+local x) leans right (+lateral).
            var lean = CharacterMotionMath.ComputeLeanAnglesDeg(2f, 0f, 0f);
            Assert.Greater(lean.x, 0f);

            // Turning right (positive yaw rate) also leans right.
            lean = CharacterMotionMath.ComputeLeanAnglesDeg(0f, 120f, 0f);
            Assert.Greater(lean.x, 0f);

            // Mirrored inputs mirror the lean.
            var left = CharacterMotionMath.ComputeLeanAnglesDeg(-2f, -120f, 0f);
            Assert.Less(left.x, 0f);
            Assert.AreEqual(lean.x, -left.x, 0.001f);
        }

        [Test]
        public void LeanIsClampedAndSprintPitchesForward()
        {
            var lean = CharacterMotionMath.ComputeLeanAnglesDeg(50f, 900f, 0f);
            Assert.AreEqual(8f, lean.x, 0.001f, "lateral lean caps at maxLateralDeg");
            Assert.AreEqual(0f, lean.y, 0.001f, "standing still never pitches");

            lean = CharacterMotionMath.ComputeLeanAnglesDeg(0f, 0f, 7.2f);
            Assert.AreEqual(6f, lean.y, 0.001f, "full sprint pitches maxForwardDeg");
            Assert.Less(CharacterMotionMath.ComputeLeanAnglesDeg(0f, 0f, 3.6f).y, lean.y,
                "forward pitch ramps with speed");
            Assert.AreEqual(0f, CharacterMotionMath.ComputeLeanAnglesDeg(0f, 0f, -4f).y,
                "running backwards must not pitch forward");
        }

        [Test]
        public void FootIkWeightFadesWhileRunningAndAirborne()
        {
            Assert.AreEqual(1f, CharacterMotionMath.FootIkWeight(true, 0f), "standing = full planting");
            Assert.AreEqual(0f, CharacterMotionMath.FootIkWeight(false, 0f), "airborne = no planting");
            Assert.AreEqual(0f, CharacterMotionMath.FootIkWeight(false, 9f));

            var walk = CharacterMotionMath.FootIkWeight(true, 2f);
            var run = CharacterMotionMath.FootIkWeight(true, 6f);
            Assert.Greater(walk, run, "weight fades with speed");
            Assert.AreEqual(0f, CharacterMotionMath.FootIkWeight(true, 12f), "sprint = animation owns the feet");
        }

        [Test]
        public void LookAndHandWeightsRampHipToAim()
        {
            Assert.AreEqual(0.35f, CharacterMotionMath.LookWeight(0f), 0.001f);
            Assert.AreEqual(0.9f, CharacterMotionMath.LookWeight(1f), 0.001f);
            Assert.Greater(CharacterMotionMath.LookWeight(0.5f), CharacterMotionMath.LookWeight(0f));
            Assert.AreEqual(0.9f, CharacterMotionMath.LookWeight(5f), 0.001f, "clamped");

            Assert.AreEqual(0f, CharacterMotionMath.HandIkWeight(false, 1f), "no weapon = no grip");
            Assert.AreEqual(0.5f, CharacterMotionMath.HandIkWeight(true, 0f), 0.001f);
            Assert.AreEqual(0.85f, CharacterMotionMath.HandIkWeight(true, 1f), 0.001f);
        }

        [Test]
        public void UnwrapDegreesKeepsYawRateSaneAcrossTheWrap()
        {
            Assert.AreEqual(10f, CharacterMotionMath.UnwrapDegrees(370f), 0.001f);
            Assert.AreEqual(-10f, CharacterMotionMath.UnwrapDegrees(-370f), 0.001f);
            Assert.AreEqual(180f, Mathf.Abs(CharacterMotionMath.UnwrapDegrees(180f)), 0.001f);
            // 359° → 1° must read as +2°, not −358°.
            Assert.AreEqual(2f, CharacterMotionMath.UnwrapDegrees(1f - 359f), 0.001f);
        }
    }
}
