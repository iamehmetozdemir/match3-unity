using System;
using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Draws every sprite in code (distance-field shapes), so the project needs no art assets.
    /// Each gem color also has its own shape, which keeps the board readable for color-blind players.
    /// </summary>
    public static class SpriteFactory
    {
        private const int Size = 128;

        public static readonly Color[] Palette =
        {
            new Color(0.93f, 0.26f, 0.30f), // red
            new Color(0.25f, 0.52f, 0.95f), // blue
            new Color(0.30f, 0.78f, 0.38f), // green
            new Color(0.98f, 0.80f, 0.20f), // yellow
            new Color(0.62f, 0.35f, 0.85f), // purple
            new Color(0.98f, 0.55f, 0.20f), // orange
        };

        private static readonly Dictionary<int, Sprite> Gems = new Dictionary<int, Sprite>();
        private static readonly Dictionary<SpecialType, Sprite> Overlays = new Dictionary<SpecialType, Sprite>();
        private static Sprite _square;
        private static Sprite _circle;
        private static Sprite _colorBomb;

        public static Sprite Gem(int color)
        {
            if (!Gems.TryGetValue(color, out Sprite sprite))
            {
                sprite = Build((x, y) => GemPixel(color, x, y));
                Gems[color] = sprite;
            }
            return sprite;
        }

        public static Sprite Overlay(SpecialType type)
        {
            if (!Overlays.TryGetValue(type, out Sprite sprite))
            {
                sprite = Build((x, y) => OverlayPixel(type, x, y));
                Overlays[type] = sprite;
            }
            return sprite;
        }

        public static Sprite ColorBomb()
        {
            if (_colorBomb == null) _colorBomb = Build(ColorBombPixel);
            return _colorBomb;
        }

        /// <summary>White rounded square (board cells, flashes, selection).</summary>
        public static Sprite Square()
        {
            if (_square == null)
                _square = Build((x, y) => new Color(1, 1, 1, Coverage(RoundedBox(x, y, 0.78f, 0.78f, 0.14f))));
            return _square;
        }

        /// <summary>White circle (particles, bomb blast).</summary>
        public static Sprite Circle()
        {
            if (_circle == null)
                _circle = Build((x, y) => new Color(1, 1, 1, Coverage(Mathf.Sqrt(x * x + y * y) - 0.95f)));
            return _circle;
        }

        private static readonly Dictionary<int, Sprite> IceSprites = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> BoxSprites = new Dictionary<int, Sprite>();
        private static Sprite _starFilled;
        private static Sprite _starEmpty;
        private static Sprite _check;

        /// <summary>Ice under a tile; thicker and whiter with two layers.</summary>
        public static Sprite Ice(int layers)
        {
            layers = Mathf.Clamp(layers, 1, 2);
            if (!IceSprites.TryGetValue(layers, out Sprite sprite))
            {
                sprite = Build((x, y) => IcePixel(layers, x, y));
                IceSprites[layers] = sprite;
            }
            return sprite;
        }

        /// <summary>Wooden crate; with 2 health it has metal straps.</summary>
        public static Sprite Box(int health)
        {
            health = Mathf.Clamp(health, 1, 2);
            if (!BoxSprites.TryGetValue(health, out Sprite sprite))
            {
                sprite = Build((x, y) => BoxPixel(health, x, y));
                BoxSprites[health] = sprite;
            }
            return sprite;
        }

        public static Sprite Star(bool filled)
        {
            if (filled && _starFilled == null) _starFilled = Build((x, y) => StarPixel(true, x, y));
            if (!filled && _starEmpty == null) _starEmpty = Build((x, y) => StarPixel(false, x, y));
            return filled ? _starFilled : _starEmpty;
        }

        /// <summary>Green circle with a white tick, used for finished goals.</summary>
        public static Sprite Check()
        {
            if (_check == null)
            {
                _check = Build((x, y) =>
                {
                    float circle = Coverage(Mathf.Sqrt(x * x + y * y) - 0.85f);
                    if (circle <= 0f) return Color.clear;
                    float tick = Mathf.Min(Segment(x, y, -0.42f, 0.0f, -0.1f, -0.32f), Segment(x, y, -0.1f, -0.32f, 0.45f, 0.3f)) - 0.11f;
                    Color c = Color.Lerp(new Color(0.20f, 0.68f, 0.38f), Color.white, Coverage(tick));
                    c.a = circle;
                    return c;
                });
            }
            return _check;
        }

        private static Color IcePixel(int layers, float x, float y)
        {
            float d = RoundedBox(x, y, 0.98f, 0.98f, 0.22f);
            float cov = Coverage(d);
            if (cov <= 0f) return Color.clear;

            Color c = layers == 1 ? new Color(0.55f, 0.82f, 1f, 0.55f) : new Color(0.78f, 0.93f, 1f, 0.85f);
            float streak = Mathf.Repeat((x + y) * 2.2f, 1f);
            if (streak < 0.1f) c = Color.Lerp(c, Color.white, 0.5f);           // frosty diagonal streaks
            if (d > -0.07f) c = new Color(1f, 1f, 1f, 0.95f);                    // bright rim
            c.a *= cov;
            return c;
        }

        private static Color BoxPixel(int health, float x, float y)
        {
            float d = RoundedBox(x, y, 0.92f, 0.92f, 0.12f);
            float cov = Coverage(d);
            if (cov <= 0f) return Color.clear;

            var wood = new Color(0.66f, 0.43f, 0.22f);
            var dark = new Color(0.36f, 0.22f, 0.10f);
            Color c = wood;

            float plank = Mathf.Repeat((y + 1f) * 2f, 1f);
            if (plank < 0.07f) c = dark;                                         // gaps between planks
            if (Mathf.Abs(x - y) < 0.13f) c = new Color(0.76f, 0.52f, 0.28f);   // diagonal brace
            if (d > -0.14f) c = dark;                                            // frame

            if (health >= 2)
            {
                var metal = new Color(0.62f, 0.65f, 0.72f);
                if (Mathf.Abs(y) > 0.5f && Mathf.Abs(y) < 0.68f) c = metal;     // straps
                float rx = Mathf.Abs(x) - 0.66f, ry = Mathf.Abs(y) - 0.66f;
                if (rx * rx + ry * ry < 0.012f) c = Color.white;                 // rivets
            }

            c.a = cov;
            return c;
        }

        private static Color StarPixel(bool filled, float x, float y)
        {
            float d = Star5(x, y + 0.06f, 0.92f, 0.45f);
            float cov = Coverage(d);
            if (cov <= 0f) return Color.clear;
            Color c = filled
                ? Color.Lerp(new Color(1f, 0.70f, 0.10f), new Color(1f, 0.92f, 0.45f), Mathf.Clamp01(0.5f + y * 0.6f))
                : new Color(0.26f, 0.28f, 0.38f);
            if (d > -0.06f) c *= 0.75f;
            c.a = cov;
            return c;
        }

        // Signed distance to a five-pointed star (after Inigo Quilez' sdStar5).
        private static float Star5(float px, float py, float r, float rf)
        {
            const float k1x = 0.809016994f, k1y = -0.587785252f;
            const float k2x = -k1x, k2y = k1y;
            px = Mathf.Abs(px);
            float d1 = Mathf.Max(k1x * px + k1y * py, 0f);
            px -= 2f * d1 * k1x; py -= 2f * d1 * k1y;
            float d2 = Mathf.Max(k2x * px + k2y * py, 0f);
            px -= 2f * d2 * k2x; py -= 2f * d2 * k2y;
            px = Mathf.Abs(px);
            py -= r;
            float bax = rf * -k1y, bay = rf * k1x - 1f;
            float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
            float lx = px - bax * h, ly = py - bay * h;
            return Mathf.Sqrt(lx * lx + ly * ly) * Mathf.Sign(py * bax - px * bay);
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ---------- sprite building ----------

        private static Sprite Build(Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f;
                    float y = (j + 0.5f) / Size * 2f - 1f;
                    pixels[j * Size + i] = pixel(x, y);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        /// <summary>Anti-aliased coverage from a signed distance (negative = inside).</summary>
        private static float Coverage(float distance)
        {
            return Mathf.Clamp01(0.5f - distance * (Size * 0.5f));
        }

        // ---------- shapes (signed distance functions) ----------

        private static float RoundedBox(float x, float y, float halfW, float halfH, float radius)
        {
            float qx = Mathf.Abs(x) - (halfW - radius);
            float qy = Mathf.Abs(y) - (halfH - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // Distance to an edge of a counter-clockwise polygon (positive = outside).
        private static float Edge(float px, float py, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            return ((px - x0) * dy - (py - y0) * dx) / len;
        }

        private static float Triangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            return Mathf.Max(Edge(px, py, ax, ay, bx, by), Mathf.Max(Edge(px, py, bx, by, cx, cy), Edge(px, py, cx, cy, ax, ay)));
        }

        private static float GemShape(int color, float x, float y)
        {
            switch (color % 6)
            {
                case 0: // circle
                    return Mathf.Sqrt(x * x + y * y) - 0.82f;
                case 1: // rounded square
                    return RoundedBox(x, y, 0.78f, 0.78f, 0.2f);
                case 2: // diamond
                    return (Mathf.Abs(x) + Mathf.Abs(y) - 1.0f) * 0.7071f;
                case 3: // triangle
                    return Triangle(x, y, -0.88f, -0.66f, 0.88f, -0.66f, 0f, 0.86f);
                case 4: // hexagon
                    return Mathf.Max(Mathf.Abs(y), Mathf.Abs(x) * 0.866f + Mathf.Abs(y) * 0.5f) - 0.82f;
                default: // squircle
                    return Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 4f) + Mathf.Pow(Mathf.Abs(y), 4f), 0.25f) - 0.8f;
            }
        }

        private static Color GemPixel(int color, float x, float y)
        {
            float d = GemShape(color, x, y);
            float cov = Coverage(d);
            if (cov <= 0f) return Color.clear;

            Color baseColor = Palette[color % Palette.Length];
            float t = Mathf.Clamp01(0.5f + (-x + y) * 0.35f);
            Color c = Color.Lerp(baseColor * 0.7f, Color.Lerp(baseColor, Color.white, 0.3f), t);
            if (d > -0.09f) c = Color.Lerp(c, baseColor * 0.55f, 0.6f); // darker rim

            float hx = x + 0.3f, hy = y - 0.35f;
            float highlight = Mathf.Sqrt(hx * hx + hy * hy * 2.2f);
            if (highlight < 0.16f) c = Color.Lerp(c, Color.white, 0.55f * (1f - highlight / 0.16f));

            c.a = cov;
            return c;
        }

        private static Color OverlayPixel(SpecialType type, float x, float y)
        {
            float d;
            switch (type)
            {
                case SpecialType.RowRocket:
                    d = DoubleArrow(x, y);
                    break;
                case SpecialType.ColumnRocket:
                    d = DoubleArrow(y, x);
                    break;
                default: // bomb: ring with a dot
                    d = Mathf.Min(Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.36f) - 0.09f, Mathf.Sqrt(x * x + y * y) - 0.13f);
                    break;
            }

            float fill = Coverage(d);
            float outline = Coverage(d - 0.07f);
            if (outline <= 0f) return Color.clear;
            float shade = Mathf.Lerp(0.1f, 1f, fill);
            return new Color(shade, shade, shade, outline);
        }

        private static float DoubleArrow(float x, float y)
        {
            float bar = Mathf.Max(Mathf.Abs(x) - 0.45f, Mathf.Abs(y) - 0.09f);
            float right = Triangle(x, y, 0.32f, -0.32f, 0.8f, 0f, 0.32f, 0.32f);
            float left = Triangle(-x, y, 0.32f, -0.32f, 0.8f, 0f, 0.32f, 0.32f);
            return Mathf.Min(bar, Mathf.Min(right, left));
        }

        private static Color ColorBombPixel(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float cov = Coverage(r - 0.84f);
            if (cov <= 0f) return Color.clear;

            float hue = Mathf.Atan2(y, x) / (2f * Mathf.PI) + 0.5f;
            Color c = Color.HSVToRGB(hue, 0.75f, 1f);
            if (r > 0.75f) c = Color.Lerp(c, Color.black, 0.35f);   // rim
            if (r < 0.3f) c = Color.Lerp(c, Color.white, 0.9f);     // center
            c.a = cov;
            return c;
        }
    }
}
