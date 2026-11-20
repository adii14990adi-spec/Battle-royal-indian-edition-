using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Procedural art engine for the code-built Home Screen (lobby).
    ///
    /// The previous lobby overlaid invisible hotspots on a baked 16:9 JPEG
    /// (`UI/battle_horizons_home_bg`), which duplicated and cropped on modern
    /// 20:9 phone panels. Every pixel of this lobby is now painted at runtime
    /// by <see cref="Painter"/>: the sunset vista with fortress silhouette,
    /// rounded panels, the golden START gradient and every icon (cart, trophy,
    /// gear, bell, coin, gem, squad glyphs…). Nothing is loaded from disk and
    /// every texture is deterministic — the same call always yields the same
    /// pixels, which EditMode tests hash to lock the look.
    ///
    /// All textures are tiny (≤1024×576, RGBA32, no mipmaps) and cached for the
    /// process lifetime, so the lobby costs a few hundred KB and one paint pass
    /// at boot — well inside the UNISOC-class budget.
    /// </summary>
    public static class LobbyArtFactory
    {
        public enum LobbyIcon
        {
            Cart,
            Luck,
            Trophy,
            Target,
            Calendar,
            Vault,
            Weapon,
            Character,
            Gear,
            Bell,
            Coin,
            Gem,
            Avatar,
            Compass,
            PersonSolo,
            PersonDuo,
            PersonSquad,
            CaretDown,
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The sunset vista: gradient sky, sun glow, ridge lines and the fort.</summary>
        public static Sprite Landscape()
        {
            return Cached("lobby.landscape", () =>
            {
                var p = new Painter(1024, 576);
                PaintVista(p);
                return p.ToSprite("BH_Lobby_Landscape", 100f);
            });
        }

        /// <summary>Vertical dusk gradient used full-bleed behind the 16:9 frame.</summary>
        public static Sprite SkyGradient()
        {
            return Cached("lobby.sky", () =>
            {
                var p = new Painter(16, 256);
                for (var y = 0; y < p.Height; y++)
                {
                    p.Row(y, SkyColor(y / (p.Height - 1f)));
                }

                return p.ToSprite("BH_Lobby_Sky", 100f);
            });
        }

        /// <summary>White rounded-rect panel; tint it with <c>Image.color</c>.</summary>
        public static Sprite Panel()
        {
            return Cached("lobby.panel", () =>
            {
                var p = new Painter(128, 128);
                p.RoundRect(1f, 1f, 126f, 126f, 22f, Color.white);
                p.RoundRectOutline(3f, 3f, 124f, 124f, 20f, 2.4f, new Color(1f, 1f, 1f, 0.35f));
                return p.ToSprite("BH_Lobby_Panel", 100f);
            });
        }

        /// <summary>Warm gold vertical gradient for the START button.</summary>
        public static Sprite GoldButton()
        {
            return Cached("lobby.gold", () =>
            {
                var p = new Painter(256, 96);
                var top = new Color(1f, 0.86f, 0.32f, 1f);
                var bottom = new Color(0.93f, 0.62f, 0.08f, 1f);
                for (var y = 0; y < p.Height; y++)
                {
                    var t = y / (p.Height - 1f);
                    p.Row(y, Color.Lerp(bottom, top, t));
                }

                p.RoundRectMask(0f, 0f, 255f, 95f, 16f);
                p.RoundRectOutline(2f, 2f, 253f, 93f, 15f, 3f, new Color(1f, 0.95f, 0.70f, 0.9f));
                return p.ToSprite("BH_Lobby_Gold", 100f);
            });
        }

        /// <summary>Soft radial ground glow used under the lobby operator.</summary>
        public static Sprite Spotlight()
        {
            return Cached("lobby.spotlight", () =>
            {
                var p = new Painter(128, 128);
                p.RadialGlow(64f, 64f, 62f, Color.white);
                return p.ToSprite("BH_Lobby_Spotlight", 100f);
            });
        }

        /// <summary>One of the lobby glyphs, painted white so Image.color can tint it.</summary>
        public static Sprite Icon(LobbyIcon kind)
        {
            return Cached("lobby.icon." + kind, () =>
            {
                var p = new Painter(96, 96);
                PaintIcon(p, kind);
                return p.ToSprite("BH_Lobby_Icon_" + kind, 100f);
            });
        }

        private static Sprite Cached(string key, System.Func<Sprite> paint)
        {
            if (Cache.TryGetValue(key, out var existing) && existing != null)
            {
                return existing;
            }

            var sprite = paint();
            Cache[key] = sprite;
            return sprite;
        }

        // --- vista -----------------------------------------------------------

        private static void PaintVista(Painter p)
        {
            var w = p.Width;
            var h = p.Height;

            // Sky.
            for (var y = 0; y < h; y++)
            {
                p.Row(y, SkyColor(y / (h - 1f)));
            }

            // Sun glow, low over the plains (fixed position = deterministic).
            var sunX = w * 0.78f;
            var sunY = h * 0.52f;
            p.RadialGlow(sunX, sunY, h * 0.42f, new Color(1f, 0.80f, 0.42f, 0.55f));
            p.Circle(sunX, sunY, h * 0.045f, new Color(1f, 0.93f, 0.72f, 0.95f));

            // Horizon haze.
            for (var y = (int)(h * 0.44f); y < (int)(h * 0.56f); y++)
            {
                var t = 1f - Mathf.Abs((y - h * 0.50f) / (h * 0.06f));
                p.Row(y, new Color(1f, 0.66f, 0.34f, 0.45f * Mathf.Clamp01(t)), true);
            }

            // Far ridge (violet dust).
            Ridge(p, h * 0.46f, h * 0.05f, 1.7f, 0.9f, new Color(0.45f, 0.32f, 0.34f, 1f));

            // Middle ridge carrying the fortress hill, right of centre.
            Ridge(p, h * 0.40f, h * 0.07f, 2.6f, 1.6f, new Color(0.33f, 0.23f, 0.22f, 1f));
            PaintFortress(p, w * 0.62f, h * 0.40f, h * 0.16f);

            // Near dunes.
            Ridge(p, h * 0.24f, h * 0.05f, 3.4f, 2.2f, new Color(0.20f, 0.14f, 0.12f, 1f));

            // Foreground ground plane with a soft path highlight.
            p.Rect(0, 0, w - 1, h * 0.14f, new Color(0.13f, 0.10f, 0.09f, 1f));
            for (var x = 0; x < w; x++)
            {
                var t = Mathf.Clamp01(1f - Mathf.Abs(x - w * 0.42f) / (w * 0.30f));
                p.Column(x, 0, h * 0.10f * t, new Color(0.35f, 0.26f, 0.20f, 0.5f * t), true);
            }

            // Vignette so overlay UI keeps contrast at the edges.
            p.Vignette(new Color(0.02f, 0.03f, 0.05f, 0.55f));
        }

        private static Color SkyColor(float t01)
        {
            // t01: 0 = bottom (horizon), 1 = top (zenith).
            var horizon = new Color(0.98f, 0.70f, 0.40f, 1f);
            var mid = new Color(0.86f, 0.44f, 0.28f, 1f);
            var zenith = new Color(0.10f, 0.14f, 0.28f, 1f);
            if (t01 < 0.35f)
            {
                return Color.Lerp(horizon, mid, t01 / 0.35f);
            }

            return Color.Lerp(mid, zenith, (t01 - 0.35f) / 0.65f);
        }

        private static void Ridge(Painter p, float baseY, float amplitude, float freqA, float freqB, Color color)
        {
            for (var x = 0; x < p.Width; x++)
            {
                var u = x / (float)p.Width;
                var height = baseY
                    + amplitude * Mathf.Sin(u * freqA * Mathf.PI * 2f + freqB)
                    + amplitude * 0.45f * Mathf.Sin(u * freqA * 5.13f * Mathf.PI + 1.3f)
                    + amplitude * 0.22f * Mathf.Sin(u * freqA * 11.7f * Mathf.PI + 4.1f);
                p.Column(x, 0, height, color);
            }
        }

        private static void PaintFortress(Painter p, float cx, float hillTopY, float size)
        {
            var wall = new Color(0.26f, 0.18f, 0.16f, 1f);
            var dark = new Color(0.20f, 0.14f, 0.12f, 1f);

            // Hill the fort sits on.
            for (var x = (int)(cx - size * 1.9f); x < (int)(cx + size * 1.9f); x++)
            {
                if (x < 0 || x >= p.Width)
                {
                    continue;
                }

                var t = Mathf.Clamp01(1f - Mathf.Abs(x - cx) / (size * 1.9f));
                p.Column(x, 0, hillTopY + size * 0.55f * t * t, wall);
            }

            // Curtain wall + crenellations.
            var wallTop = hillTopY + size * 0.62f;
            p.Rect(cx - size * 1.15f, hillTopY, cx + size * 1.15f, wallTop, wall);
            for (var i = 0; i < 12; i++)
            {
                var toothX = cx - size * 1.1f + i * (size * 0.2f);
                p.Rect(toothX, wallTop, toothX + size * 0.10f, wallTop + size * 0.10f, wall);
            }

            // Three towers, centre one tallest, with lit windows.
            Tower(p, cx - size * 0.85f, wallTop, size * 0.34f, size * 0.55f, dark);
            Tower(p, cx + size * 0.55f, wallTop, size * 0.34f, size * 0.62f, dark);
            Tower(p, cx - size * 0.14f, wallTop, size * 0.42f, size * 0.95f, dark);
            p.Rect(cx - size * 0.05f, wallTop + size * 0.45f, cx + size * 0.05f, wallTop + size * 0.55f,
                new Color(1f, 0.80f, 0.40f, 0.9f));
        }

        private static void Tower(Painter p, float x, float baseY, float width, float height, Color color)
        {
            p.Rect(x, baseY, x + width, baseY + height, color);
            p.Rect(x - width * 0.15f, baseY + height, x + width * 1.15f, baseY + height + width * 0.22f, color);
        }

        // --- icons -----------------------------------------------------------

        private static void PaintIcon(Painter p, LobbyIcon kind)
        {
            var ink = Color.white;
            switch (kind)
            {
                case LobbyIcon.Cart:
                    p.Poly(new[] { new Vector2(20, 34), new Vector2(76, 34), new Vector2(68, 62), new Vector2(28, 62) }, ink);
                    p.Line(14, 74, 24, 74, 5, ink);
                    p.Line(24, 74, 30, 40, 5, ink);
                    p.Circle(36, 24, 7, ink);
                    p.Circle(62, 24, 7, ink);
                    break;
                case LobbyIcon.Luck:
                    p.Rect(26, 26, 70, 70, ink);
                    p.Rect(44, 26, 52, 70, new Color(1f, 1f, 1f, 0.35f));
                    p.Line(14, 62, 14, 78, 5, ink);
                    p.Line(6, 70, 22, 70, 5, ink);
                    p.Line(78, 78, 78, 90, 4, ink);
                    p.Line(72, 84, 84, 84, 4, ink);
                    break;
                case LobbyIcon.Trophy:
                    p.Rect(32, 52, 64, 78, ink);
                    p.Ring(30, 66, 10, 5, ink);
                    p.Ring(66, 66, 10, 5, ink);
                    p.Rect(44, 40, 52, 52, ink);
                    p.Rect(30, 30, 66, 40, ink);
                    break;
                case LobbyIcon.Target:
                    p.Ring(48, 48, 26, 6, ink);
                    p.Circle(48, 48, 8, ink);
                    p.Line(48, 82, 48, 92, 5, ink);
                    p.Line(48, 4, 48, 14, 5, ink);
                    p.Line(4, 48, 14, 48, 5, ink);
                    p.Line(82, 48, 92, 48, 5, ink);
                    break;
                case LobbyIcon.Calendar:
                    p.Rect(20, 24, 76, 74, ink);
                    p.Rect(20, 62, 76, 74, new Color(1f, 1f, 1f, 0.35f));
                    p.Line(32, 74, 32, 84, 6, ink);
                    p.Line(64, 74, 64, 84, 6, ink);
                    p.Circle(34, 42, 5, new Color(0f, 0f, 0f, 1f));
                    p.Circle(50, 42, 5, new Color(0f, 0f, 0f, 1f));
                    p.Circle(66, 42, 5, new Color(0f, 0f, 0f, 1f));
                    break;
                case LobbyIcon.Vault:
                    p.RoundRect(18, 20, 78, 76, 8, ink);
                    p.Ring(48, 48, 14, 6, new Color(0f, 0f, 0f, 1f));
                    p.Line(48, 48, 48, 60, 5, new Color(0f, 0f, 0f, 1f));
                    p.Line(82, 40, 88, 40, 6, new Color(0f, 0f, 0f, 1f));
                    break;
                case LobbyIcon.Weapon:
                    p.Rect(16, 46, 74, 56, ink);
                    p.Rect(74, 49, 92, 53, ink);
                    p.Rect(20, 38, 40, 46, ink);
                    p.Rect(40, 30, 48, 46, new Color(1f, 1f, 1f, 0.7f));
                    p.Poly(new[] { new Vector2(44, 46), new Vector2(56, 46), new Vector2(50, 24), new Vector2(42, 26) }, ink);
                    p.Poly(new[] { new Vector2(20, 46), new Vector2(30, 46), new Vector2(26, 30), new Vector2(18, 32) }, ink);
                    break;
                case LobbyIcon.Character:
                    p.Circle(48, 66, 14, ink);
                    p.RoundRect(28, 20, 68, 52, 14, ink);
                    break;
                case LobbyIcon.Gear:
                    p.Ring(48, 48, 22, 10, ink);
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * Mathf.PI / 4f;
                        p.Line(48 + Mathf.Cos(a) * 26, 48 + Mathf.Sin(a) * 26,
                            48 + Mathf.Cos(a) * 38, 48 + Mathf.Sin(a) * 38, 9, ink);
                    }

                    break;
                case LobbyIcon.Bell:
                    p.Circle(48, 58, 24, ink);
                    p.Rect(24, 40, 72, 58, ink);
                    p.Rect(18, 34, 78, 42, ink);
                    p.Circle(48, 26, 7, ink);
                    p.Circle(48, 16, 6, ink);
                    break;
                case LobbyIcon.Coin:
                    p.Circle(48, 48, 30, ink);
                    p.Ring(48, 48, 20, 5, new Color(0f, 0f, 0f, 0.55f));
                    break;
                case LobbyIcon.Gem:
                    p.Poly(new[]
                    {
                        new Vector2(28, 62), new Vector2(68, 62), new Vector2(84, 48),
                        new Vector2(48, 16), new Vector2(12, 48),
                    }, ink);
                    p.Line(28, 62, 48, 48, 3, new Color(0f, 0f, 0f, 0.4f));
                    p.Line(68, 62, 48, 48, 3, new Color(0f, 0f, 0f, 0.4f));
                    p.Line(12, 48, 84, 48, 3, new Color(0f, 0f, 0f, 0.4f));
                    break;
                case LobbyIcon.Avatar:
                    p.Circle(48, 60, 16, ink);
                    p.Circle(48, 64, 18, new Color(1f, 1f, 1f, 0.5f));
                    p.RoundRect(22, 18, 74, 48, 16, ink);
                    p.Rect(30, 44, 66, 50, new Color(0f, 0f, 0f, 0.5f));
                    break;
                case LobbyIcon.Compass:
                    p.Ring(48, 48, 34, 6, ink);
                    p.Poly(new[]
                    {
                        new Vector2(48, 78), new Vector2(56, 52), new Vector2(78, 48),
                        new Vector2(56, 44), new Vector2(48, 18), new Vector2(40, 44),
                        new Vector2(18, 48), new Vector2(40, 52),
                    }, ink);
                    break;
                case LobbyIcon.PersonSolo:
                    Person(p, 48, ink);
                    break;
                case LobbyIcon.PersonDuo:
                    Person(p, 34, ink);
                    Person(p, 62, ink);
                    break;
                case LobbyIcon.PersonSquad:
                    Person(p, 24, ink);
                    Person(p, 48, ink);
                    Person(p, 72, ink);
                    break;
                case LobbyIcon.CaretDown:
                    p.Poly(new[] { new Vector2(20, 62), new Vector2(76, 62), new Vector2(48, 26) }, ink);
                    break;
            }
        }

        private static void Person(Painter p, float cx, Color ink)
        {
            p.Circle(cx, 62, 9, ink);
            p.RoundRect(cx - 11, 22, cx + 11, 50, 9, ink);
        }

        // --- pixel painter ----------------------------------------------------

        /// <summary>
        /// Minimal deterministic 2D painter over an RGBA32 buffer (y up, like
        /// Texture2D). Source-over alpha blending; no allocations per pixel.
        /// </summary>
        public sealed class Painter
        {
            public int Width { get; }
            public int Height { get; }

            private readonly Color32[] _pixels;

            public Painter(int width, int height)
            {
                Width = width;
                Height = height;
                _pixels = new Color32[width * height];
            }

            public Texture2D ToTexture(string name)
            {
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tex.SetPixels32(_pixels);
                tex.Apply(false, false);
                tex.name = name;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }

            public Sprite ToSprite(string name, float pixelsPerUnit)
            {
                var tex = ToTexture(name);
                var sprite = Sprite.Create(tex, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                // Sprite.Create leaves sprite.name empty; the HUD/lobby tests
                // (and debugging) rely on the BH_* naming.
                sprite.name = name;
                return sprite;
            }

            public void Row(int y, Color color, bool blend = false)
            {
                if (y < 0 || y >= Height)
                {
                    return;
                }

                for (var x = 0; x < Width; x++)
                {
                    if (blend)
                    {
                        Blend(x, y, color);
                    }
                    else
                    {
                        _pixels[y * Width + x] = color;
                    }
                }
            }

            public void Column(int x, float yBottom, float yTop, Color color, bool blend = false)
            {
                if (x < 0 || x >= Width)
                {
                    return;
                }

                var from = Mathf.Max(0, Mathf.CeilToInt(yBottom));
                var to = Mathf.Min(Height - 1, Mathf.FloorToInt(yTop));
                for (var y = from; y <= to; y++)
                {
                    if (blend)
                    {
                        Blend(x, y, color);
                    }
                    else
                    {
                        _pixels[y * Width + x] = color;
                    }
                }
            }

            public void Blend(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height || c.a <= 0f)
                {
                    return;
                }

                var i = y * Width + x;
                // Color32 fields are bytes; normalize to 0..1 floats or the
                // composite below mixes scales and blows every channel to white.
                var dst = (Color)_pixels[i];
                var a = c.a + dst.a * (1f - c.a);
                if (a <= 0f)
                {
                    return;
                }

                _pixels[i] = new Color(
                    (c.r * c.a + dst.r * dst.a * (1f - c.a)) / a,
                    (c.g * c.a + dst.g * dst.a * (1f - c.a)) / a,
                    (c.b * c.a + dst.b * dst.a * (1f - c.a)) / a,
                    a);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color color)
            {
                for (var y = (int)Mathf.Max(0, y0); y <= (int)Mathf.Min(Height - 1, y1); y++)
                {
                    for (var x = (int)Mathf.Max(0, x0); x <= (int)Mathf.Min(Width - 1, x1); x++)
                    {
                        Blend(x, y, color);
                    }
                }
            }

            public void RoundRect(float x0, float y0, float x1, float y1, float radius, Color color)
            {
                var cx0 = x0 + radius;
                var cx1 = x1 - radius;
                var cy0 = y0 + radius;
                var cy1 = y1 - radius;
                for (var y = (int)Mathf.Max(0, y0); y <= (int)Mathf.Min(Height - 1, y1); y++)
                {
                    for (var x = (int)Mathf.Max(0, x0); x <= (int)Mathf.Min(Width - 1, x1); x++)
                    {
                        var dx = x < cx0 ? cx0 - x : (x > cx1 ? x - cx1 : 0f);
                        var dy = y < cy0 ? cy0 - y : (y > cy1 ? y - cy1 : 0f);
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            public void RoundRectOutline(float x0, float y0, float x1, float y1, float radius, float thickness, Color color)
            {
                var outer = radius;
                var inner = Mathf.Max(0f, radius - thickness);
                var cx0 = x0 + outer;
                var cx1 = x1 - outer;
                var cy0 = y0 + outer;
                var cy1 = y1 - outer;
                for (var y = (int)Mathf.Max(0, y0); y <= (int)Mathf.Min(Height - 1, y1); y++)
                {
                    for (var x = (int)Mathf.Max(0, x0); x <= (int)Mathf.Min(Width - 1, x1); x++)
                    {
                        var dx = x < cx0 ? cx0 - x : (x > cx1 ? x - cx1 : 0f);
                        var dy = y < cy0 ? cy0 - y : (y > cy1 ? y - cy1 : 0f);
                        var d = Mathf.Sqrt(dx * dx + dy * dy);
                        var inOuter = d <= outer;
                        var dxI = x < cx0 + 0 ? cx0 - x : (x > cx1 ? x - cx1 : 0f);
                        var dyI = y < cy0 ? cy0 - y : (y > cy1 ? y - cy1 : 0f);
                        var dI = Mathf.Sqrt(dxI * dxI + dyI * dyI);
                        var edge = x <= x0 + thickness || x >= x1 - thickness || y <= y0 + thickness || y >= y1 - thickness;
                        if (inOuter && (edge || dI > inner || d > inner))
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            /// <summary>Clears everything outside a rounded rect (used to mask gradients).</summary>
            public void RoundRectMask(float x0, float y0, float x1, float y1, float radius)
            {
                var cx0 = x0 + radius;
                var cx1 = x1 - radius;
                var cy0 = y0 + radius;
                var cy1 = y1 - radius;
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var dx = x < cx0 ? cx0 - x : (x > cx1 ? x - cx1 : 0f);
                        var dy = y < cy0 ? cy0 - y : (y > cy1 ? y - cy1 : 0f);
                        if (dx * dx + dy * dy > radius * radius)
                        {
                            _pixels[y * Width + x] = new Color(0f, 0f, 0f, 0f);
                        }
                    }
                }
            }

            public void Circle(float cx, float cy, float r, Color color)
            {
                for (var y = (int)Mathf.Max(0, cy - r); y <= (int)Mathf.Min(Height - 1, cy + r); y++)
                {
                    for (var x = (int)Mathf.Max(0, cx - r); x <= (int)Mathf.Min(Width - 1, cx + r); x++)
                    {
                        var dx = x - cx;
                        var dy = y - cy;
                        if (dx * dx + dy * dy <= r * r)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            public void Ring(float cx, float cy, float r, float thickness, Color color)
            {
                var inner = r - thickness * 0.5f;
                var outer = r + thickness * 0.5f;
                for (var y = (int)Mathf.Max(0, cy - outer); y <= (int)Mathf.Min(Height - 1, cy + outer); y++)
                {
                    for (var x = (int)Mathf.Max(0, cx - outer); x <= (int)Mathf.Min(Width - 1, cx + outer); x++)
                    {
                        var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d >= inner && d <= outer)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            public void Line(float x0, float y0, float x1, float y1, float thickness, Color color)
            {
                var minX = (int)Mathf.Max(0, Mathf.Min(x0, x1) - thickness);
                var maxX = (int)Mathf.Min(Width - 1, Mathf.Max(x0, x1) + thickness);
                var minY = (int)Mathf.Max(0, Mathf.Min(y0, y1) - thickness);
                var maxY = (int)Mathf.Min(Height - 1, Mathf.Max(y0, y1) + thickness);
                var half = thickness * 0.5f;
                var lenSq = (x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        float t = lenSq > 0f
                            ? Mathf.Clamp01(((x - x0) * (x1 - x0) + (y - y0) * (y1 - y0)) / lenSq)
                            : 0f;
                        var px = x0 + (x1 - x0) * t;
                        var py = y0 + (y1 - y0) * t;
                        var d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                        if (d <= half)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            /// <summary>Even-odd scanline polygon fill.</summary>
            public void Poly(Vector2[] points, Color color)
            {
                var minY = int.MaxValue;
                var maxY = int.MinValue;
                for (var i = 0; i < points.Length; i++)
                {
                    minY = Mathf.Min(minY, Mathf.FloorToInt(points[i].y));
                    maxY = Mathf.Max(maxY, Mathf.CeilToInt(points[i].y));
                }

                minY = Mathf.Max(0, minY);
                maxY = Mathf.Min(Height - 1, maxY);
                for (var y = minY; y <= maxY; y++)
                {
                    var crossings = new List<float>();
                    for (var i = 0; i < points.Length; i++)
                    {
                        var a = points[i];
                        var b = points[(i + 1) % points.Length];
                        if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                        {
                            var t = (y - a.y) / (b.y - a.y);
                            crossings.Add(a.x + t * (b.x - a.x));
                        }
                    }

                    crossings.Sort();
                    for (var i = 0; i + 1 < crossings.Count; i += 2)
                    {
                        for (var x = (int)Mathf.Max(0, crossings[i]); x <= (int)Mathf.Min(Width - 1, crossings[i + 1]); x++)
                        {
                            Blend(x, y, color);
                        }
                    }
                }
            }

            public void RadialGlow(float cx, float cy, float radius, Color color)
            {
                for (var y = (int)Mathf.Max(0, cy - radius); y <= (int)Mathf.Min(Height - 1, cy + radius); y++)
                {
                    for (var x = (int)Mathf.Max(0, cx - radius); x <= (int)Mathf.Min(Width - 1, cx + radius); x++)
                    {
                        var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                        if (d < 1f)
                        {
                            var falloff = (1f - d) * (1f - d);
                            Blend(x, y, new Color(color.r, color.g, color.b, color.a * falloff));
                        }
                    }
                }
            }

            public void Vignette(Color edge)
            {
                var cx = Width * 0.5f;
                var cy = Height * 0.5f;
                var maxD = Mathf.Sqrt(cx * cx + cy * cy);
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxD;
                        var t = Mathf.Clamp01((d - 0.55f) / 0.45f);
                        if (t > 0f)
                        {
                            Blend(x, y, new Color(edge.r, edge.g, edge.b, edge.a * t * t));
                        }
                    }
                }
            }

            /// <summary>Stable fingerprint of the buffer, for EditMode regression locks.</summary>
            public int PixelHash()
            {
                unchecked
                {
                    var hash = 17;
                    for (var i = 0; i < _pixels.Length; i++)
                    {
                        hash = hash * 31 + _pixels[i].r + _pixels[i].g * 3 + _pixels[i].b * 7 + _pixels[i].a * 13;
                    }

                    return hash;
                }
            }
        }
    }
}
