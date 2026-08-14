using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Procedural sprites so the MVP ships without imported art.
    /// Every sprite is authored at 1x1 world unit (or 9-sliced) so callers size things
    /// in world units instead of fighting pixels-per-unit.
    /// </summary>
    public static class Shapes
    {
        static Sprite _solid;
        static Sprite _roundedRect;
        static Sprite _blockRect;
        static Sprite _circle;
        static Sprite _glow;

        public static Sprite Solid => _solid != null ? _solid : _solid = BuildSolid();

        /// <summary>9-sliced panel. Use with <see cref="SpriteDrawMode.Sliced"/> or a sliced Image.</summary>
        public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : _roundedRect = BuildRoundedRect(22f, 26f);

        /// <summary>
        /// Playfield variant with tighter corners. Pieces are only 0.8 world units, so the panel
        /// radius would eat most of the edge and every block would read as a pill.
        /// </summary>
        public static Sprite BlockRect => _blockRect != null ? _blockRect : _blockRect = BuildRoundedRect(10f, 14f);

        public static Sprite Circle => _circle != null ? _circle : _circle = BuildCircle();

        /// <summary>Soft radial falloff used for glows and light pools.</summary>
        public static Sprite Glow => _glow != null ? _glow : _glow = BuildGlow();

        public static Sprite VerticalGradient(Color bottom, Color top)
        {
            const int size = 256;
            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                var t = y / (float)(size - 1);
                var row = Color.Lerp(bottom, top, t);
                for (var x = 0; x < size; x++)
                {
                    // Tiny ordered dither keeps large gradients from banding.
                    var dither = (((x + y) & 1) == 0 ? 1f : -1f) / 510f;
                    pixels[y * size + x] = new Color(row.r + dither, row.g + dither, row.b + dither, 1f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildSolid()
        {
            const int size = 8;
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildRoundedRect(float radius, float border)
        {
            const int size = 128;

            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                    var dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    var alpha = Mathf.Clamp01(0.5f - distance);

                    // Bake a top-lit ramp so flat-tinted blocks still read as volumes.
                    var shade = Mathf.Lerp(0.74f, 1.14f, y / (float)(size - 1));
                    pixels[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static Sprite BuildCircle()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(radius - distance);

                    // Off-centre highlight reads as a lit sphere.
                    var lit = 1f - Mathf.Clamp01(
                        Mathf.Sqrt((dx + radius * 0.3f) * (dx + radius * 0.3f) +
                                   (dy - radius * 0.35f) * (dy - radius * 0.35f)) / radius);
                    var shade = Mathf.Lerp(0.72f, 1.2f, lit);
                    pixels[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite BuildGlow()
        {
            const int size = 128;
            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / radius);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 2.5f));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Texture2D NewTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }
    }
}
