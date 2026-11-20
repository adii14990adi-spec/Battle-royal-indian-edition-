using UnityEngine;

namespace BattleHorizons.Characters
{
    /// <summary>
    /// Pure math behind the character motion rig (anti-slide speed damping,
    /// procedural lean, IK weight curves). No MonoBehaviour state, no
    /// UnityEngine.Random — everything is deterministic and EditMode-testable,
    /// matching the house rules for gameplay code.
    /// </summary>
    public static class CharacterMotionMath
    {
        /// <summary>
        /// Exponential smoothing toward a target: never overshoots, frame-rate
        /// independent, and identical to Unity's SmoothDamp feel for the
        /// critically damped case. smoothTime ≈ time to cover ~63 % of the gap.
        /// </summary>
        public static float DampTowards(float current, float target, float smoothTime, float deltaTime)
        {
            if (deltaTime <= 0f || smoothTime <= 0f)
            {
                return target;
            }

            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / smoothTime));
        }

        /// <summary>
        /// Procedural lean angles in degrees: x = lateral roll (positive leans
        /// RIGHT, driven by local strafing velocity plus turn rate), y = forward
        /// pitch (positive leans FORWARD, ramps with sprint speed).
        /// Gains are tuned for the motor's 4.0 walk / 7.2 sprint speeds.
        /// </summary>
        public static Vector2 ComputeLeanAnglesDeg(
            float localLateralSpeed,
            float yawRateDegPerSec,
            float forwardSpeed,
            float maxLateralDeg = 8f,
            float maxForwardDeg = 6f)
        {
            var lateral = Mathf.Clamp(
                localLateralSpeed * 3.5f + yawRateDegPerSec * 0.035f,
                -maxLateralDeg, maxLateralDeg);
            var forward = Mathf.Clamp01(Mathf.Max(0f, forwardSpeed) / 7.2f) * maxForwardDeg;
            return new Vector2(lateral, forward);
        }

        /// <summary>
        /// Foot-IK weight: full when standing/slow, faded out by 3.5 m/s so
        /// running feet are never dragged by the raycast, zero when airborne.
        /// </summary>
        public static float FootIkWeight(bool grounded, float planarSpeed)
        {
            if (!grounded)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - (planarSpeed - 0.5f) / 3f);
        }

        /// <summary>Head look-at weight: subtle (0.35) at hip, strong (0.9) when aiming.</summary>
        public static float LookWeight(float aimBlend01) =>
            0.35f + 0.55f * Mathf.Clamp01(aimBlend01);

        /// <summary>Hand-on-foregrip weight: relaxed carry (0.5) → firm aim grip (0.85).</summary>
        public static float HandIkWeight(bool hasWeapon, float aimBlend01) =>
            hasWeapon ? 0.5f + 0.35f * Mathf.Clamp01(aimBlend01) : 0f;

        /// <summary>Unwraps a degree delta into [-180, 180] (for yaw-rate tracking).</summary>
        public static float UnwrapDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped > 180f)
            {
                wrapped -= 360f;
            }
            else if (wrapped < -180f)
            {
                wrapped += 360f;
            }

            return wrapped;
        }
    }
}
