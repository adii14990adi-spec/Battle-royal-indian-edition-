using BattleHorizons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using UnityEngine.UI;

namespace BattleHorizons.Tests.PlayMode
{
    /// <summary>
    /// The in-match touch HUD must be fully code-built art: neon glass orbs and
    /// pictograms painted at runtime (no shipped images), wired to the real
    /// input router, and laid out so the thumb cluster never overlaps.
    /// </summary>
    public class TouchHudArtTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        private static Bounds WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = Vector3.Min(Vector3.Min(corners[0], corners[1]), Vector3.Min(corners[2], corners[3]));
            var max = Vector3.Max(Vector3.Max(corners[0], corners[1]), Vector3.Max(corners[2], corners[3]));
            return new Bounds((min + max) * 0.5f, max - min);
        }

        [UnityTest]
        public IEnumerator Hud_UsesCodePaintedOrbAndIconSprites()
        {
            _host = new GameObject("HudHost");
            var rig = MobileControlsBuilder.Build(_host.transform, null, null);
            yield return null;

            Assert.IsNotNull(rig.Canvas, "HUD canvas must build");

            var fire = rig.FireUiButton.GetComponent<Image>();
            Assert.IsNotNull(fire.sprite, "fire button must carry a painted orb sprite");
            StringAssert.StartsWith("BH_HUD_", fire.sprite.name, "orb sprites are painted at runtime");

            var fireIcon = rig.FireUiButton.transform.Find("Icon");
            Assert.IsNotNull(fireIcon, "fire button must carry a painted pictogram");
            StringAssert.StartsWith("BH_HUD_", fireIcon.GetComponent<Image>().sprite.name);

            foreach (var button in new[] { rig.CrouchUiButton, rig.SprintUiButton, rig.AimUiButton, rig.JumpButton })
            {
                Assert.IsNotNull(button.GetComponent<Image>().sprite, button.name + " needs an orb sprite");
                Assert.IsNotNull(button.transform.Find("Icon"), button.name + " needs a pictogram");
            }

            var knob = rig.MoveStick.transform.Find("Knob");
            Assert.IsNotNull(knob.GetComponent<Image>().sprite, "move stick knob must be the painted neon knob");
            Assert.IsNotNull(rig.MoveStick.GetComponent<Image>().sprite, "move stick base must be the painted glass disc");
        }

        [UnityTest]
        public IEnumerator Hud_ThumbClusterDoesNotOverlap()
        {
            _host = new GameObject("HudHost");
            var rig = MobileControlsBuilder.Build(_host.transform, null, null);
            yield return null;

            var fire = WorldBounds((RectTransform)rig.FireUiButton.transform);
            var jump = WorldBounds((RectTransform)rig.JumpButton.transform);
            var crouch = WorldBounds((RectTransform)rig.CrouchUiButton.transform);
            var sprint = WorldBounds((RectTransform)rig.SprintUiButton.transform);
            var aim = WorldBounds((RectTransform)rig.AimUiButton.transform);

            Assert.IsFalse(fire.Intersects(jump), "fire and jump must not overlap");
            Assert.IsFalse(fire.Intersects(sprint), "fire and sprint must not overlap");
            Assert.IsFalse(crouch.Intersects(sprint), "crouch and sprint must not overlap");
            Assert.IsFalse(sprint.Intersects(aim), "sprint and aim must not overlap");
            Assert.IsFalse(jump.Intersects(aim), "jump and aim must not overlap");
        }
    }
}
