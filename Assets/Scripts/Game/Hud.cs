using System.Collections.Generic;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Look and layout helpers for the IMGUI interface: rounded panels and buttons generated in code,
    /// text styles that scale with the screen, and goal / star widgets.
    /// </summary>
    public static class Hud
    {
        public static readonly Color Background = new Color(0.06f, 0.07f, 0.12f);
        private static readonly Color PanelColor = new Color(0.12f, 0.14f, 0.23f, 0.96f);
        private static readonly Color ChipColor = new Color(0.17f, 0.19f, 0.30f, 1f);
        private static readonly Color Accent = new Color(0.98f, 0.78f, 0.22f);
        private static readonly Color AccentHover = new Color(1f, 0.86f, 0.40f);
        private static readonly Color Secondary = new Color(0.22f, 0.25f, 0.38f);
        private static readonly Color SecondaryHover = new Color(0.29f, 0.32f, 0.47f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 1f);
        private static readonly Color MutedText = new Color(0.66f, 0.70f, 0.85f);
        private static readonly Color DarkText = new Color(0.12f, 0.10f, 0.05f);

        private static readonly Dictionary<Color, Texture2D> RoundedCache = new Dictionary<Color, Texture2D>();
        private static int _styleUnit;

        public static GUIStyle Panel, Chip, Button, SecondaryButton, Title, Heading, Text, Small, Number, BoosterText, BoosterTextDark;

        /// <summary>Base size for text and spacing; follows the smaller screen side so phones in portrait work too.</summary>
        public static float Unit => Mathf.Max(12f, Mathf.Min(Screen.width / 14f, Screen.height / 28f));

        public static void EnsureStyles()
        {
            int unit = Mathf.RoundToInt(Unit);
            if (Panel != null && _styleUnit == unit) return;
            _styleUnit = unit;

            Panel = BoxStyle(PanelColor);
            Chip = BoxStyle(ChipColor);
            Button = ButtonStyle(Accent, AccentHover, DarkText, unit);
            SecondaryButton = ButtonStyle(Secondary, SecondaryHover, TextColor, unit);

            Title = TextStyle(unit * 2.4f, TextColor, FontStyle.Bold);
            Heading = TextStyle(unit * 1.5f, TextColor, FontStyle.Bold);
            Text = TextStyle(unit, TextColor, FontStyle.Bold);
            BoosterText = TextStyle(unit * 0.85f, TextColor, FontStyle.Bold);
            BoosterText.wordWrap = false;
            BoosterTextDark = new GUIStyle(BoosterText);
            BoosterTextDark.normal.textColor = DarkText;
            Small = TextStyle(unit * 0.75f, MutedText, FontStyle.Normal);
            Number = TextStyle(unit * 1.6f, TextColor, FontStyle.Bold);
        }

        private static GUIStyle BoxStyle(Color color)
        {
            var style = new GUIStyle { border = new RectOffset(20, 20, 20, 20) };
            style.normal.background = Rounded(color);
            return style;
        }

        private static GUIStyle ButtonStyle(Color color, Color hover, Color text, int unit)
        {
            var style = new GUIStyle
            {
                border = new RectOffset(20, 20, 20, 20),
                alignment = TextAnchor.MiddleCenter,
                fontSize = unit,
                fontStyle = FontStyle.Bold
            };
            style.normal.background = Rounded(color);
            style.hover.background = Rounded(hover);
            style.active.background = Rounded(color * 0.85f);
            style.normal.textColor = style.hover.textColor = style.active.textColor = text;
            return style;
        }

        private static GUIStyle TextStyle(float size, Color color, FontStyle fontStyle)
        {
            var style = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(size), fontStyle = fontStyle, wordWrap = true };
            style.normal.textColor = color;
            return style;
        }

        // A 64x64 rounded rectangle used as a 9-slice background.
        private static Texture2D Rounded(Color color)
        {
            color.a = Mathf.Clamp01(color.a);
            if (RoundedCache.TryGetValue(color, out Texture2D cached) && cached != null) return cached;

            const int size = 64;
            const float radius = 18f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    Color c = color;
                    c.a *= Mathf.Clamp01(0.5f - d);
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            RoundedCache[color] = texture;
            return texture;
        }

        // ---------- widgets ----------

        public static void Icon(Rect rect, Sprite sprite)
        {
            if (sprite != null) GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
        }

        public static void Stars(Rect rect, int filled, float gap = 0.1f)
        {
            float size = Mathf.Min(rect.height, rect.width / (3f + 2f * gap));
            float total = size * 3f + size * gap * 2f;
            float x = rect.x + (rect.width - total) / 2f;
            float y = rect.y + (rect.height - size) / 2f;
            for (int i = 0; i < 3; i++)
                Icon(new Rect(x + i * size * (1f + gap), y, size, size), SpriteFactory.Star(i < filled));
        }

        public static Sprite GoalIcon(Goal goal)
        {
            switch (goal.Type)
            {
                case GoalType.Collect: return SpriteFactory.Gem(goal.Color);
                case GoalType.Ice: return SpriteFactory.Ice(2);
                case GoalType.Boxes: return SpriteFactory.Box(1);
                default: return SpriteFactory.Star(true);
            }
        }

        /// <summary>A goal chip: icon plus what is left (or a tick when done).</summary>
        public static void GoalChip(Rect rect, GoalProgress goal)
        {
            GUI.Box(rect, GUIContent.none, Chip);
            float pad = rect.height * 0.14f;
            float iconSize = rect.height - pad * 2f;
            Icon(new Rect(rect.x + pad, rect.y + pad, iconSize, iconSize), GoalIcon(goal.Goal));

            var textRect = new Rect(rect.x + pad * 2f + iconSize, rect.y, rect.width - iconSize - pad * 3f, rect.height);
            if (goal.Done)
            {
                float check = iconSize * 0.8f;
                Icon(new Rect(textRect.x + (textRect.width - check) / 2f, rect.y + (rect.height - check) / 2f, check, check), SpriteFactory.Check());
            }
            else
            {
                string text = goal.Goal.Type == GoalType.Score ? goal.Current + "/" + goal.Target : goal.Remaining.ToString();
                GUI.Label(textRect, text, goal.Goal.Type == GoalType.Score ? Text : Number);
            }
        }

        /// <summary>Short description of a goal for the level card and the start of a level.</summary>
        public static string Describe(Goal goal, int target)
        {
            switch (goal.Type)
            {
                case GoalType.Collect: return "Collect " + target;
                case GoalType.Ice: return "Clear the ice";
                case GoalType.Boxes: return "Break the boxes";
                default: return "Score " + target;
            }
        }
    }
}
