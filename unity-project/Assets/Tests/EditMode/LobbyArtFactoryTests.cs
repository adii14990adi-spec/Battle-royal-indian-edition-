using BattleHorizons.UI;
using NUnit.Framework;
using UnityEngine;

namespace BattleHorizons.Tests.EditMode
{
    /// <summary>
    /// Locks the procedural Home Screen art engine: every sprite is painted in
    /// code, so these tests pin determinism (same pixels every run), opacity
    /// where the UI needs it (panels, icons, gold button), transparent corners
    /// on rounded shapes, and that the vista actually contains sky, sun and
    /// fortress bands instead of an accidental flat fill.
    /// </summary>
    public class LobbyArtFactoryTests
    {
        [Test]
        public void SpritesAreCachedAndDeterministic()
        {
            var first = LobbyArtFactory.Landscape();
            var second = LobbyArtFactory.Landscape();
            Assert.AreSame(first, second, "landscape sprite must be cached");

            var iconA = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Gear);
            var iconB = LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Gear);
            Assert.AreSame(iconA, iconB, "icons must be cached");
            Assert.AreNotSame(iconA, LobbyArtFactory.Icon(LobbyArtFactory.LobbyIcon.Bell));
        }

        [Test]
        public void EveryLobbyIconPaintsOpaquePixels()
        {
            foreach (LobbyArtFactory.LobbyIcon kind in System.Enum.GetValues(typeof(LobbyArtFactory.LobbyIcon)))
            {
                var sprite = LobbyArtFactory.Icon(kind);
                Assert.IsNotNull(sprite, kind + " icon must exist");
                var pixels = sprite.texture.GetPixels32();
                var opaque = 0;
                for (var i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 200)
                    {
                        opaque++;
                    }
                }

                Assert.GreaterOrEqual(opaque, 40, kind + " icon must paint a visible glyph");
            }
        }

        [Test]
        public void PanelHasRoundedTransparentCornersAndOpaqueCentre()
        {
            var pixels = LobbyArtFactory.Panel().texture.GetPixels32();
            const int size = 128;
            Assert.AreEqual(0, pixels[0].a, "bottom-left corner must be transparent");
            Assert.AreEqual(0, pixels[size - 1].a, "bottom-right corner must be transparent");
            Assert.AreEqual(0, pixels[(size - 1) * size].a, "top-left corner must be transparent");
            Assert.Greater(pixels[(size / 2) * size + size / 2].a, 200, "centre must be opaque");
        }

        [Test]
        public void GoldButtonIsOpaqueInsideAndGradedTopToBottom()
        {
            var tex = LobbyArtFactory.GoldButton().texture;
            var pixels = tex.GetPixels32();
            var w = tex.width;
            var h = tex.height;
            var centreTop = pixels[(int)(h * 0.8f) * w + w / 2];
            var centreBottom = pixels[(int)(h * 0.2f) * w + w / 2];
            Assert.Greater(centreTop.a, 200);
            Assert.Greater(centreBottom.a, 200);
            Assert.Greater(centreTop.r + centreTop.g, centreBottom.r + centreBottom.g - 40f / 255f,
                "gold gradient must stay warm across the button");
            Assert.AreEqual(0, pixels[0].a, "rounded corner must be transparent");
        }

        [Test]
        public void VistaContainsSkySunAndGroundBands()
        {
            var tex = LobbyArtFactory.Landscape().texture;
            Assert.AreEqual(1024, tex.width);
            Assert.AreEqual(576, tex.height);
            var pixels = tex.GetPixels32();
            var w = tex.width;
            var h = tex.height;

            Color At(float u, float v) => pixels[(int)(v * (h - 1)) * w + (int)(u * (w - 1))];

            var zenith = At(0.15f, 0.97f);
            var horizon = At(0.15f, 0.52f);
            var ground = At(0.5f, 0.03f);
            Assert.Greater(zenith.b, zenith.r, "zenith must be blue-dominant");
            Assert.Greater(horizon.r, horizon.b, "horizon must be warm");
            Assert.Less(ground.r + ground.g + ground.b, 1.2f, "foreground must stay dark for UI contrast");

            // The sun glow region must be brighter than the sky beside it.
            var sun = At(0.70f, 0.52f);
            var beside = At(0.40f, 0.62f);
            Assert.Greater(sun.r + sun.g + sun.b, beside.r + beside.g + beside.b,
                "sun glow must brighten its neighbourhood");
        }

        [Test]
        public void PainterPrimitivesAreDeterministic()
        {
            var a = new LobbyArtFactory.Painter(64, 64);
            a.Circle(32, 32, 20, Color.white);
            a.Line(4, 4, 60, 60, 3, new Color(1f, 0f, 0f, 0.5f));
            var b = new LobbyArtFactory.Painter(64, 64);
            b.Circle(32, 32, 20, Color.white);
            b.Line(4, 4, 60, 60, 3, new Color(1f, 0f, 0f, 0.5f));
            Assert.AreEqual(a.PixelHash(), b.PixelHash(), "same draw calls must yield identical pixels");

            var c = new LobbyArtFactory.Painter(64, 64);
            c.Circle(30, 32, 20, Color.white);
            Assert.AreNotEqual(a.PixelHash(), c.PixelHash(), "different geometry must differ");
        }

        [Test]
        public void PolygonScanlineFillsInteriorOnly()
        {
            var p = new LobbyArtFactory.Painter(48, 48);
            p.Poly(new[] { new Vector2(8, 8), new Vector2(40, 8), new Vector2(24, 40) }, Color.white);
            var pixels = p.ToTexture("t").GetPixels32();
            Assert.Greater(pixels[20 * 48 + 24].a, 200, "triangle interior must fill");
            Assert.AreEqual(0, pixels[44 * 48 + 24].a, "above the apex must stay empty");
            Assert.AreEqual(0, pixels[4 * 48 + 4].a, "outside the base corners must stay empty");
        }
    }

    /// <summary>
    /// Wallet/callsign persistence behind the Home Screen chips.
    /// </summary>
    public class LobbyProfileTests
    {
        [Test]
        public void DefaultsMatchTheSeason05MockAndFormatWithSeparators()
        {
            PlayerPrefs.DeleteKey("bh.lobby.coins");
            PlayerPrefs.DeleteKey("bh.lobby.gems");
            PlayerPrefs.DeleteKey("bh.lobby.callsign");
            Assert.AreEqual(45820, LobbyProfile.Coins);
            Assert.AreEqual(1240, LobbyProfile.Gems);
            Assert.AreEqual("Commander_K", LobbyProfile.CallSign);
            Assert.AreEqual("45,820", LobbyProfile.FormatCoins);
            Assert.AreEqual("1,240", LobbyProfile.FormatGems);
        }

        [Test]
        public void GrantCoinsAccumulatesAndIgnoresNonPositive()
        {
            PlayerPrefs.SetInt("bh.lobby.coins", 100);
            LobbyProfile.GrantCoins(50);
            Assert.AreEqual(150, LobbyProfile.Coins);
            LobbyProfile.GrantCoins(0);
            LobbyProfile.GrantCoins(-20);
            Assert.AreEqual(150, LobbyProfile.Coins);
            PlayerPrefs.SetInt("bh.lobby.coins", 45820);
        }
    }
}
