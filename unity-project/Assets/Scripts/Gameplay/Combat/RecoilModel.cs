using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// Procedural recoil as pure math: each shot adds a seeded kick (pitch up,
    /// yaw jitter), and recovery bleeds the accumulated kick back at
    /// <c>recoveryDegPerSec</c>, returning <c>recoveryReturnRatio</c> of it to
    /// the camera so the player "pulls down" against the spray naturally.
    ///
    /// The jitter stream is a seeded <see cref="RandomStream"/> (validator rule
    /// 8): two clients with the same seed see the identical spray pattern,
    /// which is what kills recoil-desync exploits before multiplayer lands.
    /// </summary>
    public sealed class RecoilModel
    {
        /// <summary>Cap on stored kick so holding LMB never banks infinite climb.</summary>
        public const float MaxAccumulatedPitchDeg = 24f;
        public const float MaxAccumulatedYawDeg = 12f;

        private readonly RandomStream _rng;

        public RecoilModel(RandomStream rng)
        {
            _rng = rng ?? new RandomStream(1);
        }

        /// <summary>Pitch kick still owed to recovery, degrees (up positive).</summary>
        public float AccumulatedPitchDeg { get; private set; }

        /// <summary>Yaw kick still owed to recovery, degrees.</summary>
        public float AccumulatedYawDeg { get; private set; }

        /// <summary>
        /// Returns this shot's camera kick in degrees: x = yaw (signed),
        /// y = pitch (always ≥ 0, guns climb). ADS multiplies by
        /// <c>adsRecoilScale</c> so aiming genuinely tightens the spray.
        /// </summary>
        public Vector2 ApplyShot(WeaponData data, bool aiming)
        {
            if (data == null)
            {
                return Vector2.zero;
            }

            var scale = aiming ? data.adsRecoilScale : 1f;
            var pitch = data.recoilPitchDeg * scale * _rng.Range(0.85f, 1.15f);
            var yaw = _rng.Range(-1f, 1f) * data.recoilYawJitterDeg * scale;

            AccumulatedPitchDeg = Mathf.Min(AccumulatedPitchDeg + pitch, MaxAccumulatedPitchDeg);
            AccumulatedYawDeg = Mathf.Clamp(AccumulatedYawDeg + yaw, -MaxAccumulatedYawDeg, MaxAccumulatedYawDeg);
            return new Vector2(yaw, pitch);
        }

        /// <summary>
        /// Decays the accumulated kick by <c>recoveryDegPerSec × dt</c> and
        /// returns the degrees to hand back to the camera this frame
        /// (x = yaw to undo, y = pitch to lower). Zero when nothing is left.
        /// </summary>
        public Vector2 Recover(WeaponData data, float deltaTime)
        {
            if (data == null || deltaTime <= 0f)
            {
                return Vector2.zero;
            }

            var budget = data.recoveryDegPerSec * deltaTime;

            var pitchOut = Mathf.Min(AccumulatedPitchDeg, budget);
            AccumulatedPitchDeg -= pitchOut;

            var yawOut = Mathf.Abs(AccumulatedYawDeg) <= budget
                ? AccumulatedYawDeg
                : Mathf.Sign(AccumulatedYawDeg) * budget;
            AccumulatedYawDeg -= yawOut;

            return new Vector2(
                yawOut * data.recoveryReturnRatio,
                pitchOut * data.recoveryReturnRatio);
        }

        /// <summary>Drops all stored kick (weapon switch, death, respawn).</summary>
        public void Reset()
        {
            AccumulatedPitchDeg = 0f;
            AccumulatedYawDeg = 0f;
        }
    }
}
