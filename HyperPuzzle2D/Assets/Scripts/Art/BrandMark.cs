using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// The app's brand artwork, drawn procedurally so the launcher icon, the engine splash and
    /// the in-game splash all come from one definition and cannot drift apart. Everything is
    /// authored in a resolution-independent square where the visible area spans -1..1 on both
    /// axes, so the same code renders a 36 px launcher icon and a 1024 px store icon.
    /// </summary>
    public static class BrandMark
    {
        /// <summary>
        /// Android adaptive icons let the launcher mask the artwork into a circle, squircle or
        /// rounded square, and only the middle of the frame survives every mask. Foreground
        /// layers therefore have to shrink into that safe zone.
        /// </summary>
        public const float AdaptiveSafeScale = 0.70f;

        static Sprite _markSprite;

        /// <summary>Transparent-background mark, cached for UI use.</summary>
        public static Sprite MarkSprite =>
            _markSprite != null ? _markSprite : _markSprite = CreateSprite(320);

        public static Sprite CreateSprite(int size)
        {
            var tex = Render(size, drawBackground: false, drawMark: true, markScale: 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>
        /// Renders the brand square. The three layers are separable because Android wants the
        /// background and foreground as distinct images, while iOS and the splash want them flat.
        /// </summary>
        public static Texture2D Render(int size, bool drawBackground, bool drawMark, float markScale)
        {
            var pixels = new Color[size * size];
            var texelStep = 2f / size;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(
                        (x + 0.5f) / size * 2f - 1f,
                        (y + 0.5f) / size * 2f - 1f);

                    var color = drawBackground ? Background(p) : new Color(0f, 0f, 0f, 0f);
                    if (drawMark)
                    {
                        color = Mark(color, p / Mathf.Max(markScale, 0.01f), texelStep / Mathf.Max(markScale, 0.01f));
                    }

                    pixels[y * size + x] = color;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Same dark-top / violet-bottom ramp the play field uses, plus its centre bloom.</summary>
        static Color Background(Vector2 p)
        {
            var t = Mathf.InverseLerp(-1f, 1f, p.y);
            var color = Color.Lerp(Palette.BackdropBottom, Palette.BackdropTop, t);

            var bloom = Mathf.Pow(1f - Mathf.Clamp01((p - new Vector2(0f, -0.1f)).magnitude / 1.25f), 2.2f);
            color = Color.Lerp(color, Palette.BackdropGlow, bloom * 0.35f);
            color.a = 1f;
            return color;
        }

        /// <summary>
        /// A leaning three-block stack with the cannonball streaking in from the lower left:
        /// the two things the game is about, in shapes big enough to survive a 36 px icon.
        /// </summary>
        static Color Mark(Color canvas, Vector2 rawP, float rawTexel)
        {
            // The authored shapes sit a little low and left of centre; nudge the whole group back
            // into the middle and let it fill more of the frame.
            const float fill = 1.05f;
            var p = (rawP - new Vector2(-0.02f, 0.08f)) / fill;
            var texel = rawTexel / fill;

            var trailDir = new Vector2(-0.62f, -0.78f);
            for (var i = 3; i >= 1; i--)
            {
                var offset = trailDir * (0.17f * i);
                var radius = 0.155f - 0.028f * i;
                canvas = Circle(canvas, p, new Vector2(-0.42f, -0.46f) + offset, radius, Palette.Accent, texel, 0.30f - 0.07f * i);
            }

            canvas = Glow(canvas, p, new Vector2(-0.42f, -0.46f), 0.52f, Palette.Accent, 0.45f);

            var blockHalf = new Vector2(0.40f, 0.125f);
            canvas = RoundedRect(canvas, p, new Vector2(0.10f, -0.34f), blockHalf, 0.055f, -4f, Palette.Blocks[0], texel);
            canvas = RoundedRect(canvas, p, new Vector2(0.05f, -0.045f), blockHalf, 0.055f, 3f, Palette.Blocks[2], texel);
            canvas = RoundedRect(canvas, p, new Vector2(0.14f, 0.25f), blockHalf, 0.055f, -9f, Palette.Blocks[3], texel);

            canvas = Circle(canvas, p, new Vector2(-0.42f, -0.46f), 0.155f, Palette.Accent, texel, 1f);
            return canvas;
        }

        static Color RoundedRect(Color dst, Vector2 p, Vector2 center, Vector2 half, float radius, float degrees, Color tint, float texel)
        {
            var rad = -degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var d = p - center;
            var q = new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);

            var e = new Vector2(
                Mathf.Abs(q.x) - (half.x - radius),
                Mathf.Abs(q.y) - (half.y - radius));
            var outside = new Vector2(Mathf.Max(e.x, 0f), Mathf.Max(e.y, 0f)).magnitude;
            var distance = Mathf.Min(Mathf.Max(e.x, e.y), 0f) + outside - radius;

            var alpha = Mathf.Clamp01(0.5f - distance / texel);
            if (alpha <= 0f)
            {
                return dst;
            }

            // Top-lit ramp, matching the shading baked into the in-game block sprite.
            var shade = Mathf.Lerp(0.76f, 1.14f, Mathf.InverseLerp(-half.y, half.y, q.y));
            var lit = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
            return Blend(dst, lit, alpha);
        }

        static Color Circle(Color dst, Vector2 p, Vector2 center, float radius, Color tint, float texel, float opacity)
        {
            var d = p - center;
            var distance = d.magnitude - radius;
            var alpha = Mathf.Clamp01(0.5f - distance / texel) * opacity;
            if (alpha <= 0f)
            {
                return dst;
            }

            var lit = 1f - Mathf.Clamp01((d - new Vector2(-0.3f, 0.35f) * radius).magnitude / Mathf.Max(radius, 1e-4f));
            var shade = Mathf.Lerp(0.74f, 1.2f, lit);
            return Blend(dst, new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f), alpha);
        }

        static Color Glow(Color dst, Vector2 p, Vector2 center, float radius, Color tint, float intensity)
        {
            var t = Mathf.Clamp01((p - center).magnitude / radius);
            var alpha = Mathf.Pow(1f - t, 2.5f) * intensity;
            return alpha <= 0f ? dst : Blend(dst, tint, alpha);
        }

        static Color Blend(Color dst, Color src, float alpha)
        {
            var outA = alpha + dst.a * (1f - alpha);
            if (outA <= 0f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            var rgb = (new Vector3(src.r, src.g, src.b) * alpha +
                       new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - alpha)) / outA;
            return new Color(rgb.x, rgb.y, rgb.z, outA);
        }
    }
}
