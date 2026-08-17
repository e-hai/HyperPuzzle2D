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
        static Sprite _hazard;
        static Sprite _star;
        static Sprite _paperFiber;

        public static Sprite Solid => _solid != null ? _solid : _solid = BuildSolid();

        /// <summary>9-sliced panel. Use with <see cref="SpriteDrawMode.Sliced"/> or a sliced Image.</summary>
        public static Sprite RoundedRect => _roundedRect != null ? _roundedRect : _roundedRect = BuildRoundedRect(22f, 26f, 0.18f);

        /// <summary>
        /// Playfield variant with tighter corners. Pieces are under a world unit, so the panel
        /// radius would eat most of the edge and every block would read as a pill. The ink line is
        /// heavier here: world pieces overlap each other, and the outline is what keeps a stack of
        /// same-family timber tones from merging into one silhouette.
        /// </summary>
        public static Sprite BlockRect => _blockRect != null ? _blockRect : _blockRect = BuildRoundedRect(10f, 14f, 0.32f);

        /// <summary>
        /// One tile of washi grain, white with the fibre carried in the alpha so it can be laid
        /// over any backdrop and tinted by the renderer. Authored to wrap on both axes, so a
        /// <see cref="SpriteDrawMode.Tiled"/> renderer keeps the grain at a constant size instead
        /// of stretching it across the field.
        /// </summary>
        public static Sprite PaperFiber => _paperFiber != null ? _paperFiber : _paperFiber = BuildPaperFiber();

        public static Sprite Circle => _circle != null ? _circle : _circle = BuildCircle();

        /// <summary>Soft radial falloff used for glows and light pools.</summary>
        public static Sprite Glow => _glow != null ? _glow : _glow = BuildGlow();

        /// <summary>
        /// One 0.5-unit tile of diagonal hazard tape. Authored to wrap seamlessly so a
        /// <see cref="SpriteDrawMode.Tiled"/> renderer keeps the stripes the same size on trims of
        /// any length instead of stretching them into thin streaks.
        /// </summary>
        public static Sprite Hazard => _hazard != null ? _hazard : _hazard = BuildHazard();

        /// <summary>Five-point star used by the stage rating, tinted by the caller.</summary>
        public static Sprite Star => _star != null ? _star : _star = BuildStar();

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

        static Sprite BuildRoundedRect(float radius, float border, float ink)
        {
            const int size = 128;

            // Width of the darkened band just inside the silhouette. Kept well under the 9-slice
            // border so the line survives slicing instead of being stretched across the middle.
            const float inkWidth = 2.5f;

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

                    // Bake a top-lit ramp so flat-tinted blocks still read as volumes. The range is
                    // shallow on purpose: paper craft is lit flat, and the form is carried by the
                    // ink line below rather than by a glossy gradient.
                    var shade = Mathf.Lerp(0.86f, 1.08f, y / (float)(size - 1));

                    // Ink line hugging the edge, the way a cut-out reads against its own shadow.
                    var edge = Mathf.Clamp01((distance + inkWidth) / inkWidth);
                    shade *= Mathf.Lerp(1f, 1f - ink, edge);

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

        static Sprite BuildPaperFiber()
        {
            const int size = 128;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Cells are wider than they are tall so the grain draws out horizontally, the
                    // way pulp fibres settle on a screen. Three octaves: long strands, short
                    // strands, then a fine tooth over the top.
                    var fibre = PeriodicNoise(x, y, 32, 8, size) * 0.5f +
                                PeriodicNoise(x, y, 64, 16, size) * 0.3f +
                                PeriodicNoise(x, y, 16, 4, size) * 0.2f;

                    // Bias dark: only the denser half of the range becomes a visible strand, so the
                    // sheet stays mostly clean instead of reading as uniform static.
                    var strand = Mathf.Clamp01((fibre - 0.45f) / 0.55f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, strand * strand);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
        }

        /// <summary>
        /// Value noise on a lattice that wraps at <paramref name="size"/>, which is what makes the
        /// tile seamless. Separate cell sizes per axis let callers stretch the grain directionally.
        /// Both cell sizes must divide <paramref name="size"/> evenly.
        /// </summary>
        static float PeriodicNoise(int x, int y, int cellX, int cellY, int size)
        {
            var columns = size / cellX;
            var rows = size / cellY;

            var fx = x / (float)cellX;
            var fy = y / (float)cellY;
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            // Smoothstep, otherwise the lattice shows up as diamond creases.
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            var v00 = LatticeValue(x0, y0, columns, rows);
            var v10 = LatticeValue(x0 + 1, y0, columns, rows);
            var v01 = LatticeValue(x0, y0 + 1, columns, rows);
            var v11 = LatticeValue(x0 + 1, y0 + 1, columns, rows);

            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        static float LatticeValue(int x, int y, int columns, int rows)
        {
            // Wrapping the lattice indices is the whole trick: the right edge samples the same
            // corner values as the left, so the tile joins itself without a seam.
            var hx = ((x % columns) + columns) % columns;
            var hy = ((y % rows) + rows) % rows;

            var h = hx * 374761393 + hy * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        static Sprite BuildHazard()
        {
            // 64 px tile at 128 PPU = 0.5 world units. Stripe period along (x + y) is 32 px, which
            // divides 64 evenly, so the pattern tiles seamlessly on both axes.
            const int size = 64;
            const int period = 32;
            var yellow = Palette.HazardStripe;
            var black = Palette.HazardStripeAlt;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var band = Mathf.Repeat(x + y, period) < period * 0.5f;
                    pixels[y * size + x] = band ? yellow : black;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
        }

        static Sprite BuildStar()
        {
            const int size = 128;
            const int points = 5;
            const float outer = 0.48f;
            const float inner = 0.20f;

            // The star is drawn white and tinted by the renderer, so only coverage matters here.
            // Supersampling stands in for an analytic distance field: the spikes are thin enough
            // that a hard point-in-polygon test alone leaves visibly jagged edges.
            const int samples = 3;

            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var px = (x + (sx + 0.5f) / samples) / size - 0.5f;
                            var py = (y + (sy + 0.5f) / samples) / size - 0.5f;
                            if (InStar(px, py, points, outer, inner))
                            {
                                covered++;
                            }
                        }
                    }

                    var alpha = covered / (float)(samples * samples);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static bool InStar(float x, float y, int points, float outer, float inner)
        {
            var radius = Mathf.Sqrt(x * x + y * y);
            if (radius > outer)
            {
                return false;
            }

            if (radius <= inner)
            {
                return true;
            }

            // Fold the angle into one spike, then compare against the edge running from an outer
            // tip to the neighbouring inner valley.
            var sector = Mathf.PI * 2f / points;
            var half = sector * 0.5f;

            // Measuring from +y puts a tip at the top; the extra half-sector centres each spike
            // on local = 0 so the edge test below runs from tip to valley.
            var angle = Mathf.Atan2(x, y) + half;
            var local = Mathf.Repeat(angle, sector) - half;
            var edge = inner * outer * Mathf.Sin(half) /
                       (inner * Mathf.Sin(half - Mathf.Abs(local)) + outer * Mathf.Sin(Mathf.Abs(local)));
            return radius <= edge;
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
