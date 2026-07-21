using System.Collections.Generic;
using UnityEngine;

namespace Yahtzee.Presentation
{
    /// <summary>Rounded-rectangle sprites, generated at runtime.
    ///
    /// A uGUI Image with no sprite is a hard rectangle, and hard rectangles are most of what made
    /// the old UI read as a spreadsheet. These are 9-sliced, so one small texture gives any panel,
    /// button or tile soft corners at any size. Generated rather than authored because the whole
    /// UI is built in code — there are no art assets to slice yet.</summary>
    public static class UiSprites
    {
        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        /// <summary>A rounded rectangle with corner <paramref name="radius"/> in reference pixels.
        /// Slice borders are set to the radius, so corners keep their shape however far the Image
        /// is stretched.</summary>
        public static Sprite Rounded(int radius = 24) => Get(radius, 0);

        /// <summary>A rounded rectangle with a soft outline — the cartoon "sticker" look, and it
        /// keeps a pale tile legible against a pale background.</summary>
        public static Sprite RoundedOutlined(int radius = 24, int outline = 3) => Get(radius, outline);

        private static Sprite Get(int radius, int outline)
        {
            int key = radius * 100 + outline;
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            int size = radius * 2 + 4; // +4 so the sliced centre is never degenerate
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Sample(x, y, size, radius, outline);
            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Coverage of one pixel, antialiased across the corner arc. Alpha carries the
        /// shape; the Image's own colour tints it, so one texture serves every colour we use.</summary>
        private static Color32 Sample(int x, int y, int size, int radius, int outline)
        {
            // Distance from the rounded-rect edge, negative inside.
            float px = x + 0.5f, py = y + 0.5f;
            float dx = Mathf.Max(Mathf.Abs(px - size / 2f) - (size / 2f - radius), 0f);
            float dy = Mathf.Max(Mathf.Abs(py - size / 2f) - (size / 2f - radius), 0f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

            float alpha = Mathf.Clamp01(0.5f - distance); // 1px antialiased edge
            if (alpha <= 0f)
                return new Color32(255, 255, 255, 0);

            if (outline > 0 && distance > -outline)
            {
                // Outline band: darker, so a pale tile still has an edge on a pale ground.
                byte shade = (byte)Mathf.RoundToInt(255 * 0.72f);
                return new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(alpha * 255));
            }
            return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
        }
    }
}
