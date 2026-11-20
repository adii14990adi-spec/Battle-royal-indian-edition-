using UnityEngine;
using BattleHorizons.Core;

namespace BattleHorizons.Gameplay.Combat
{
    /// <summary>
    /// Runtime-built greybox combat FX so shooting is playable (and testable in
    /// CI) with zero committed art/audio assets:
    ///   * a muzzle-flash particle burst (cone, 60 ms, no texture),
    ///   * a procedurally synthesised gunshot AudioClip — a deterministic
    ///     noise crack + low thump, identical on every device,
    ///   * a one-line tracer that self-destroys after 80 ms.
    /// Replace each with real assets by assigning the WeaponController's
    /// muzzle/audio fields; the factories are only used when they are null.
    /// </summary>
    public static class CombatPlaceholderFx
    {
        public const int GunshotSampleRate = 44100;
        public const float GunshotDurationSeconds = 0.3f;
        private const int GunshotSeed = 90210;

        private static AudioClip _gunshotClip;

        /// <summary>
        /// Synthesises gunshot PCM: exponential-decay noise crack over a 85 Hz
        /// body thump. Pure function of (sampleRate, duration) — EditMode-tested.
        /// </summary>
        public static float[] GenerateGunshotSamples(int sampleRate, float durationSeconds)
        {
            sampleRate = Mathf.Clamp(sampleRate, 8000, 96000);
            durationSeconds = Mathf.Clamp(durationSeconds, 0.05f, 2f);
            var count = Mathf.CeilToInt(sampleRate * durationSeconds);
            var samples = new float[count];
            var rng = new RandomStream(GunshotSeed);

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var crack = rng.Range(-1f, 1f) * Mathf.Exp(-t * 26f);
                var spike = rng.Range(-1f, 1f) * Mathf.Exp(-t * 420f);
                var thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 34f) * 0.55f;
                samples[i] = Mathf.Clamp(crack * 0.6f + spike * 0.5f + thump, -1f, 1f);
            }

            return samples;
        }

        /// <summary>The shared gunshot clip, built once on first use.</summary>
        public static AudioClip GunshotClip()
        {
            if (_gunshotClip != null)
            {
                return _gunshotClip;
            }

            var samples = GenerateGunshotSamples(GunshotSampleRate, GunshotDurationSeconds);
            _gunshotClip = AudioClip.Create(
                "gunshot_placeholder", samples.Length, 1, GunshotSampleRate, false);
            _gunshotClip.SetData(samples, 0);
            return _gunshotClip;
        }

        /// <summary>
        /// Builds a stopped, world-space muzzle-flash ParticleSystem parented to
        /// <paramref name="parent"/>; call Play() per shot.
        /// </summary>
        public static ParticleSystem BuildMuzzleFlash(Transform parent)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // cone fires +Z of the mount

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.06f;
            main.startSpeed = 2.5f;
            main.startSize = 0.1f;
            main.startColor = new Color(1f, 0.78f, 0.3f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.05f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        /// <summary>Thin tracer line from muzzle to impact, gone after 80 ms.</summary>
        public static void SpawnTracer(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.02f;
            line.endWidth = 0.02f;
            line.useWorldSpace = true;
            line.sharedMaterial = null; // default line material
            line.startColor = new Color(1f, 0.9f, 0.5f, 0.9f);
            line.endColor = new Color(1f, 0.9f, 0.5f, 0f);
            Object.Destroy(go, 0.08f);
        }
    }
}
