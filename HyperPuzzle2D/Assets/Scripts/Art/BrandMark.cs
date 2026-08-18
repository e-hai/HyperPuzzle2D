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

        /// <summary>Same sunlit-paper ramp the play field uses, plus its centre bloom.</summary>
        static Color Background(Vector2 p)
        {
            var t = Mathf.InverseLerp(-1f, 1f, p.y);
            var color = Color.Lerp(Palette.BackdropBottom, Palette.BackdropTop, t);

            var bloom = Mathf.Pow(1f - Mathf.Clamp01((p - new Vector2(0f, -0.1f)).magnitude / 1.25f), 2.2f);
            color = Color.Lerp(color, Palette.BackdropGlow, bloom * 0.35f);
            color.a = 1f;
            return color;
        }

        /// <summary>Sumi-ink for the paper figure the reticle is aimed at.</summary>
        static readonly Color Ink = new Color(0.20f, 0.16f, 0.12f, 1f);

        /// <summary>
        /// A paper-target mark: a sumi-ink head-and-shoulders bust behind a vermilion aiming
        /// reticle. It reads at a 36 px launcher size as "shoot the paper figure", matching the
        /// gameplay instead of the old brick-and-cannonball icon.
        /// </summary>
        static Color Mark(Color canvas, Vector2 rawP, float rawTexel)
        {
            const float fill = 1.02f;
            var p = rawP / fill;
            var texel = rawTexel / fill;

            // The paper figure: a bust silhouette, kept a touch back so the reticle sits on top.
            canvas = RoundedRect(canvas, p, new Vector2(0f, -0.62f), new Vector2(0.62f, 0.40f), 0.26f, 0f, Ink, texel);
            canvas = Circle(canvas, p, new Vector2(0f, 0.02f), 0.30f, Ink, texel, 1f);

            // The reticle: a warm bloom, a vermilion ring, four ticks and a centre pip.
            canvas = Glow(canvas, p, Vector2.zero, 0.72f, Palette.Accent, 0.28f);
            canvas = Ring(canvas, p, Vector2.zero, 0.40f, 0.52f, Palette.Accent, texel);

            var tickHalf = new Vector2(0.055f, 0.14f);
            canvas = RoundedRect(canvas, p, new Vector2(0f, 0.52f), tickHalf, 0.03f, 0f, Palette.Accent, texel);
            canvas = RoundedRect(canvas, p, new Vector2(0f, -0.52f), tickHalf, 0.03f, 0f, Palette.Accent, texel);
            canvas = RoundedRect(canvas, p, new Vector2(0.52f, 0f), new Vector2(tickHalf.y, tickHalf.x), 0.03f, 0f, Palette.Accent, texel);
            canvas = RoundedRect(canvas, p, new Vector2(-0.52f, 0f), new Vector2(tickHalf.y, tickHalf.x), 0.03f, 0f, Palette.Accent, texel);

            canvas = Circle(canvas, p, Vector2.zero, 0.07f, Palette.Accent, texel, 1f);
            return canvas;
        }

        /// <summary>Anti-aliased annulus, so the reticle ring works on a transparent foreground.</summary>
        static Color Ring(Color dst, Vector2 p, Vector2 center, float inner, float outer, Color tint, float texel)
        {
            var r = (p - center).magnitude;
            var mid = (inner + outer) * 0.5f;
            var halfWidth = (outer - inner) * 0.5f;
            var distance = Mathf.Abs(r - mid) - halfWidth;
            var alpha = Mathf.Clamp01(0.5f - distance / texel);
            return alpha <= 0f ? dst : Blend(dst, tint, alpha);
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
            var shade = Mathf.Lerp(0.86f, 1.08f, Mathf.InverseLerp(-half.y, half.y, q.y));
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
