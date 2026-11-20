using System.Collections.Generic;
using BattleHorizons.Core;
using BattleHorizons.Gameplay.Combat;
using BattleHorizons.Gameplay.Loot;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Combat-contract tests: the loadout covers every loot weapon, the pure
    /// WeaponRuntime honours fire mode / magazine / reload timing, the seeded
    /// RecoilModel is deterministic and always climbs-then-recovers, and the
    /// procedural gunshot is reproducible sample-for-sample.
    /// </summary>
    public class CombatSystemsTests
    {
        // --- Loadout ↔ loot contract ---------------------------------------

        [Test]
        public void EveryLootWeaponHasCombatStats()
        {
            var items = DefaultLootContent.CreateItems();
            var weapons = DefaultWeaponContent.CreateAll();

            foreach (var pair in items)
            {
                if (pair.Value.category != ItemCategory.Weapon)
                {
                    continue;
                }

                Assert.IsTrue(weapons.ContainsKey(pair.Key),
                    $"loot weapon '{pair.Key}' has no WeaponData — picking it up would do nothing");
                Assert.AreEqual(pair.Value.tier, weapons[pair.Key].tier,
                    $"tier mismatch for '{pair.Key}' between loot and combat");
            }

            Assert.GreaterOrEqual(weapons.Count, 4);
        }

        [Test]
        public void FireModesAndAdsFovsMatchTheDesign()
        {
            var weapons = DefaultWeaponContent.CreateAll();

            Assert.AreEqual(FireMode.Semi, weapons["wpn_pistol"].fireMode);
            Assert.AreEqual(FireMode.Auto, weapons["wpn_smg"].fireMode);
            Assert.AreEqual(FireMode.Auto, weapons["wpn_assault_rifle"].fireMode);
            Assert.AreEqual(FireMode.Semi, weapons["wpn_sniper"].fireMode);

            // ADS pulls the 60° hip FOV in: rifle 45°, sniper the tightest.
            Assert.AreEqual(45f, weapons["wpn_assault_rifle"].adsFovDeg, 0.01f);
            Assert.Less(weapons["wpn_sniper"].adsFovDeg, weapons["wpn_assault_rifle"].adsFovDeg);
            Assert.Greater(weapons["wpn_sniper"].damage, weapons["wpn_assault_rifle"].damage);

            foreach (var data in weapons.Values)
            {
                Assert.Less(data.adsFovDeg, 60f, "ADS must zoom in, never out");
                Assert.Less(data.adsSpreadDeg, data.hipSpreadDeg, "ADS must tighten spread");
                Assert.Greater(data.recoilPitchDeg, 0f);
                Assert.Greater(data.SecondsPerShot, 0f);
            }
        }

        // --- WeaponRuntime: fire rate, semi edge, reload --------------------

        [Test]
        public void AutoWeaponHonoursRoundsPerMinute()
        {
            var rifle = DefaultWeaponContent.CreateAll()["wpn_assault_rifle"]; // 600 rpm → 0.1 s
            var runtime = new WeaponRuntime(rifle);

            var shots = 0;
            for (var frame = 0; frame < 60; frame++)
            {
                shots += runtime.Tick(frame / 60f, true);
            }

            // One second of held trigger at 600 rpm ≈ 10 shots (float drift ±1).
            Assert.GreaterOrEqual(shots, 9);
            Assert.LessOrEqual(shots, 11);
            Assert.AreEqual(rifle.magazineSize - shots, runtime.MagAmmo);
        }

        [Test]
        public void SemiWeaponNeedsATriggerRelease()
        {
            var pistol = DefaultWeaponContent.CreateAll()["wpn_pistol"];
            var runtime = new WeaponRuntime(pistol);

            var shots = 0;
            for (var frame = 0; frame < 60; frame++)
            {
                shots += runtime.Tick(frame / 60f, true);
            }

            Assert.AreEqual(1, shots, "holding the trigger on semi must fire exactly once");

            runtime.Tick(1.0f, false);           // release
            shots += runtime.Tick(1.1f, true);   // press again past the 0.2 s cadence
            Assert.AreEqual(2, shots);
        }

        [Test]
        public void EmptyMagazineAutoReloadsAndReservePaysForIt()
        {
            var pistol = DefaultWeaponContent.CreateAll()["wpn_pistol"]; // mag 12, reserve 48, 1.5 s
            var runtime = new WeaponRuntime(pistol);

            var time = 0f;
            var shots = 0;
            var frame = 0;
            while (shots < 12 && frame < 600)
            {
                // Semi-auto: pump the trigger (4 frames held, 4 released).
                shots += runtime.Tick(frame / 60f, frame % 8 < 4);
                frame++;
            }

            time = frame / 60f;
            Assert.AreEqual(12, shots);
            Assert.AreEqual(0, runtime.MagAmmo);

            // Next trigger pull on an empty mag starts the reload.
            runtime.Tick(time, false);
            runtime.Tick(time + 1f / 60f, true);
            Assert.IsTrue(runtime.IsReloading);
            Assert.AreEqual(0, runtime.MagAmmo, "reload must not refill before it finishes");

            runtime.Tick(time + pistol.reloadSeconds + 0.1f, false);
            Assert.IsFalse(runtime.IsReloading);
            Assert.AreEqual(12, runtime.MagAmmo);
            Assert.AreEqual(48 - 12, runtime.ReserveAmmo);
        }

        [Test]
        public void ReloadAndReserveGuards()
        {
            var smg = DefaultWeaponContent.CreateAll()["wpn_smg"];
            var runtime = new WeaponRuntime(smg);

            Assert.IsFalse(runtime.StartReload(0f), "a full mag must not reload");

            runtime.Tick(0f, true); // one shot out
            Assert.IsTrue(runtime.StartReload(0.1f));
            Assert.IsFalse(runtime.StartReload(0.2f), "no double reload");

            var full = new WeaponRuntime(smg);
            full.AddReserve(10000);
            Assert.AreEqual(smg.maxReserve, full.ReserveAmmo, "reserve must clamp at maxReserve");
        }

        // --- RecoilModel ----------------------------------------------------

        [Test]
        public void RecoilIsDeterministicAndAlwaysClimbs()
        {
            var rifle = DefaultWeaponContent.CreateAll()["wpn_assault_rifle"];
            var a = new RecoilModel(new RandomStream(4242));
            var b = new RecoilModel(new RandomStream(4242));

            for (var i = 0; i < 20; i++)
            {
                var ka = a.ApplyShot(rifle, false);
                var kb = b.ApplyShot(rifle, false);
                Assert.AreEqual(ka, kb, $"recoil desync at shot {i}");
                Assert.Greater(ka.y, 0f, "pitch kick must climb");
                Assert.LessOrEqual(Mathf.Abs(ka.x), rifle.recoilYawJitterDeg + 0.001f);
            }

            Assert.Greater(a.AccumulatedPitchDeg, 0f);
            Assert.LessOrEqual(a.AccumulatedPitchDeg, RecoilModel.MaxAccumulatedPitchDeg);
        }

        [Test]
        public void AdsTightensRecoilAndRecoveryBleedsItBack()
        {
            var rifle = DefaultWeaponContent.CreateAll()["wpn_assault_rifle"];
            var hip = new RecoilModel(new RandomStream(7));
            var ads = new RecoilModel(new RandomStream(7));

            var hipKick = hip.ApplyShot(rifle, false).y;
            var adsKick = ads.ApplyShot(rifle, true).y;
            Assert.Less(adsKick, hipKick, "ADS must scale recoil down");

            // Full recovery returns ratio × accumulated and empties the bank.
            var recovered = Vector2.zero;
            for (var i = 0; i < 600; i++)
            {
                recovered += hip.Recover(rifle, 1f / 60f);
            }

            Assert.AreEqual(0f, hip.AccumulatedPitchDeg, 0.001f);
            Assert.AreEqual(hipKick * rifle.recoveryReturnRatio, recovered.y, 0.01f);
        }

        // --- Procedural gunshot ---------------------------------------------

        [Test]
        public void GunshotSamplesAreDeterministicAndDecay()
        {
            var a = CombatPlaceholderFx.GenerateGunshotSamples(44100, 0.3f);
            var b = CombatPlaceholderFx.GenerateGunshotSamples(44100, 0.3f);

            Assert.AreEqual(Mathf.CeilToInt(44100 * 0.3f), a.Length);
            Assert.AreEqual(a, b, "gunshot synthesis must be reproducible");

            var head = 0f;
            var tail = 0f;
            foreach (var sample in a)
            {
                Assert.LessOrEqual(Mathf.Abs(sample), 1f);
            }

            for (var i = 0; i < 200; i++)
            {
                head = Mathf.Max(head, Mathf.Abs(a[i]));
                tail = Mathf.Max(tail, Mathf.Abs(a[a.Length - 1 - i]));
            }

            Assert.Greater(head, tail * 2f, "envelope must decay — the tail is the bang's rumble");
        }
    }
}
