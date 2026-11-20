using UnityEngine;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Procedural art for the in-match touch HUD: the neon "glass orb" control
    /// style (dark smoked-glass disc, diagonal gloss sweep, cyan glow rings)
    /// with code-drawn white pictograms - bullet, crouch, jump, sprint, aim -
    /// plus the concentric-glass joystick base and its glowing knob.
    /// Every sprite is painted pixel-by-pixel at runtime; the build ships no
    /// HUD images.
    /// </summary>
    public static class HudArtFactory
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Cache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite FireOrb() => Cached("BH_HUD_FireOrb", () => PaintOrb(256, 1));
        public static Sprite CrouchOrb() => Cached("BH_HUD_CrouchOrb", () => PaintOrb(256, 2));
        public static Sprite JumpOrb() => Cached("BH_HUD_JumpOrb", () => PaintOrb(256, 1));
        public static Sprite SprintOrb() => Cached("BH_HUD_SprintOrb", () => PaintOrb(256, 3));
        public static Sprite AimOrb() => Cached("BH_HUD_AimOrb", () => PaintOrb(256, 1));
        public static Sprite JoystickBase() => Cached("BH_HUD_StickBase", () => PaintStickBase(512));
        public static Sprite JoystickKnob() => Cached("BH_HUD_StickKnob", () => PaintStickKnob(256));

        public static Sprite IconBullet() => Cached("BH_HUD_IconBullet", PaintBullet);
        public static Sprite IconCrouch() => Cached("BH_HUD_IconCrouch", PaintCrouch);
        public static Sprite IconJump() => Cached("BH_HUD_IconJump", PaintJump);
        public static Sprite IconSprint() => Cached("BH_HUD_IconSprint", PaintSprint);
        public static Sprite IconAim() => Cached("BH_HUD_IconAim", PaintAim);

        private static Sprite Cached(string key, System.Func<Sprite> paint)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = paint();
                Cache[key] = sprite;
            }

            return sprite;
        }

        private static readonly Color Neon = new Color(0.32f, 0.95f, 0.95f);
        private static readonly Color GlassDeep = new Color(0.03f, 0.05f, 0.06f);
        private static readonly Color GlassMid = new Color(0.13f, 0.17f, 0.18f);
        private static readonly Color Pictogram = new Color(0.96f, 0.98f, 0.99f);

        /// <summary>
        /// Smoked-glass orb: radial dark glass, inner rim light, one diagonal
        /// gloss sweep and 1-3 concentric neon rings with a soft outer bloom.
        /// </summary>
        private static Sprite PaintOrb(int size, int rings)
        {
            var painter = new LobbyArtFactory.Painter(size, size);
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - c) / c;
                    var dy = (y - c) / c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1.08f)
                    {
                        continue;
                    }

                    // Glass body with a 2 px anti-aliased edge.
                    var mask = Mathf.Clamp01((0.90f - d) / 0.02f);
                    if (mask > 0f)
                    {
                        var glass = Color.Lerp(GlassMid, GlassDeep, Mathf.Clamp01(d / 0.9f));
                        glass.a = 0.86f * mask;
                        painter.Blend(x, y, glass);

                        // Inner rim light hugging the inside of the edge.
                        var rim = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.05f, 2f));
                        painter.Blend(x, y, new Color(1f, 1f, 1f, 0.10f * rim * mask));

                        // Diagonal gloss sweep (top-left bright band) plus a
                        // softer counter-sweep at the bottom right.
                        var sweep = Mathf.Exp(-Mathf.Pow(((dx + dy) * 0.5f + 0.34f) / 0.20f, 2f));
                        painter.Blend(x, y, new Color(1f, 1f, 1f, 0.09f * sweep * mask));
                        var sweep2 = Mathf.Exp(-Mathf.Pow(((dx + dy) * 0.5f - 0.52f) / 0.16f, 2f));
                        painter.Blend(x, y, new Color(1f, 1f, 1f, 0.06f * sweep2 * mask));
                    }

                    // Neon rings + bloom, sitting on and outside the glass edge.
                    for (var r = 0; r < rings; r++)
                    {
                        var rr = 0.90f - r * 0.075f;
                        var band = Mathf.Exp(-Mathf.Pow((d - rr) / 0.016f, 2f));
                        if (band > 0.01f)
                        {
                            painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.95f * band));
                        }

                        var bloom = Mathf.Exp(-Mathf.Pow((d - rr) / 0.07f, 2f));
                        if (bloom > 0.01f)
                        {
                            painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.22f * bloom));
                        }
                    }
                }
            }

            return painter.ToSprite("BH_HUD_Orb" + rings, 100f);
        }

        /// <summary>Wide glass disc with faint quadrant sheen and guide rings.</summary>
        private static Sprite PaintStickBase(int size)
        {
            var painter = new LobbyArtFactory.Painter(size, size);
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - c) / c;
                    var dy = (y - c) / c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1.06f)
                    {
                        continue;
                    }

                    var mask = Mathf.Clamp01((0.96f - d) / 0.02f);
                    if (mask > 0f)
                    {
                        var glass = Color.Lerp(GlassMid, GlassDeep, Mathf.Clamp01(d / 0.95f));
                        glass.a = 0.55f * mask;
                        painter.Blend(x, y, glass);

                        // Quadrant sheen: opposite pie slices catch the light.
                        var quad = dx * dy > 0f ? 0.05f : 0.0f;
                        painter.Blend(x, y, new Color(1f, 1f, 1f, quad * mask));

                        var guide = Mathf.Exp(-Mathf.Pow((d - 0.62f) / 0.012f, 2f));
                        painter.Blend(x, y, new Color(1f, 1f, 1f, 0.10f * guide * mask));
                        var guide2 = Mathf.Exp(-Mathf.Pow((d - 0.34f) / 0.012f, 2f));
                        painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.22f * guide2 * mask));
                    }

                    var rim = Mathf.Exp(-Mathf.Pow((d - 0.96f) / 0.018f, 2f));
                    painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.85f * rim));
                    var bloom = Mathf.Exp(-Mathf.Pow((d - 0.96f) / 0.06f, 2f));
                    painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.18f * bloom));
                }
            }

            return painter.ToSprite("BH_HUD_StickBase", 100f);
        }

        /// <summary>Glowing concentric-ring thumb knob.</summary>
        private static Sprite PaintStickKnob(int size)
        {
            var painter = new LobbyArtFactory.Painter(size, size);
            var c = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - c) / c;
                    var dy = (y - c) / c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1.1f)
                    {
                        continue;
                    }

                    var mask = Mathf.Clamp01((0.88f - d) / 0.02f);
                    if (mask > 0f)
                    {
                        var glass = Color.Lerp(GlassMid, GlassDeep, Mathf.Clamp01(d / 0.9f));
                        glass.a = 0.72f * mask;
                        painter.Blend(x, y, glass);
                    }

                    float[] rings = { 0.88f, 0.60f, 0.36f };
                    for (var r = 0; r < rings.Length; r++)
                    {
                        var band = Mathf.Exp(-Mathf.Pow((d - rings[r]) / 0.02f, 2f));
                        painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, (0.85f - r * 0.12f) * band));
                    }

                    var core = Mathf.Exp(-Mathf.Pow(d / 0.16f, 2f));
                    painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.8f * core));
                    var bloom = Mathf.Exp(-Mathf.Pow((d - 0.88f) / 0.09f, 2f));
                    painter.Blend(x, y, new Color(Neon.r, Neon.g, Neon.b, 0.20f * bloom));
                }
            }

            return painter.ToSprite("BH_HUD_StickKnob", 100f);
        }

        // --- pictograms (white on transparent) --------------------------------

        private static Sprite PaintBullet()
        {
            var p = new LobbyArtFactory.Painter(256, 256);
            // Ogive tip.
            p.Poly(new[]
            {
                new Vector2(104f, 150f), new Vector2(110f, 178f), new Vector2(128f, 196f),
                new Vector2(146f, 178f), new Vector2(152f, 150f),
            }, Pictogram);
            // Case with a vertical relief slit left of centre.
            p.RoundRect(104f, 74f, 152f, 146f, 6f, Pictogram);
            p.Rect(112f, 80f, 115f, 140f, new Color(0.03f, 0.05f, 0.06f, 0.9f));
            // Rim + extractor groove.
            p.Rect(108f, 62f, 148f, 70f, Pictogram);
            p.Rect(102f, 52f, 154f, 58f, Pictogram);
            return p.ToSprite("BH_HUD_IconBullet", 100f);
        }

        private static Sprite PaintCrouch()
        {
            var p = new LobbyArtFactory.Painter(256, 256);
            p.Circle(152f, 172f, 17f, Pictogram);                       // head
            p.Line(142f, 156f, 114f, 108f, 24f, Pictogram);             // curved back
            p.Line(138f, 136f, 180f, 122f, 14f, Pictogram);             // forearm
            p.Line(114f, 108f, 152f, 92f, 21f, Pictogram);              // thigh
            p.Line(152f, 92f, 150f, 58f, 17f, Pictogram);               // shin down
            p.Line(150f, 60f, 98f, 60f, 17f, Pictogram);                // folded leg
            return p.ToSprite("BH_HUD_IconCrouch", 100f);
        }

        private static Sprite PaintJump()
        {
            var p = new LobbyArtFactory.Painter(256, 256);
            p.Ring(128f, 128f, 88f, 11f, Pictogram);                    // hoop
            p.Circle(120f, 168f, 14f, Pictogram);                       // head
            p.Line(122f, 156f, 128f, 116f, 16f, Pictogram);             // torso
            p.Line(126f, 146f, 160f, 170f, 12f, Pictogram);             // raised arm
            p.Line(124f, 140f, 98f, 150f, 12f, Pictogram);              // back arm
            p.Line(128f, 116f, 156f, 102f, 14f, Pictogram);             // front thigh
            p.Line(156f, 102f, 154f, 76f, 12f, Pictogram);              // front shin
            p.Line(128f, 116f, 102f, 98f, 14f, Pictogram);              // back thigh
            p.Line(102f, 98f, 76f, 98f, 12f, Pictogram);                // back shin
            return p.ToSprite("BH_HUD_IconJump", 100f);
        }

        private static Sprite PaintSprint()
        {
            var p = new LobbyArtFactory.Painter(256, 256);
            p.Circle(152f, 168f, 16f, Pictogram);                       // head
            p.Line(146f, 154f, 118f, 112f, 19f, Pictogram);             // leaning torso
            p.Line(140f, 140f, 176f, 128f, 13f, Pictogram);             // front forearm
            p.Line(136f, 134f, 106f, 142f, 13f, Pictogram);             // back arm
            p.Line(118f, 112f, 150f, 88f, 16f, Pictogram);              // front thigh
            p.Line(150f, 88f, 146f, 56f, 13f, Pictogram);               // front shin
            p.Line(118f, 112f, 88f, 96f, 16f, Pictogram);               // back thigh
            p.Line(88f, 96f, 58f, 104f, 13f, Pictogram);                // back shin
            return p.ToSprite("BH_HUD_IconSprint", 100f);
        }

        private static Sprite PaintAim()
        {
            var p = new LobbyArtFactory.Painter(256, 256);
            p.Ring(128f, 128f, 72f, 9f, Pictogram);
            p.Line(128f, 196f, 128f, 168f, 9f, Pictogram);
            p.Line(128f, 88f, 128f, 60f, 9f, Pictogram);
            p.Line(60f, 128f, 88f, 128f, 9f, Pictogram);
            p.Line(168f, 128f, 196f, 128f, 9f, Pictogram);
            p.Circle(128f, 128f, 9f, Pictogram);
            return p.ToSprite("BH_HUD_IconAim", 100f);
        }
    }
}
