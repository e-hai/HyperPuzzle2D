using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperPuzzle2D.Editor
{
    /// <summary>
    /// Splits a legacy full-body Paper*.png sheet into the two layers the jigsaw needs:
    /// Outfit (every outer garment, torn away piece by piece) and Base (the figure that stays,
    /// wearing a plain tank and shorts).
    ///
    /// The split is geometric on purpose. In this artwork a cream blouse and bare skin are within a
    /// couple of RGB steps of each other, so a colour classifier tears holes in the wrong places.
    /// The garment is instead the band between collar and hem, minus the hair hanging over it and
    /// minus the hands. The body under it is redrawn rather than recovered: a tapered torso plus
    /// arms and legs anchored to the hands and legs the sheet already shows, so the rebuilt figure
    /// lines up with the art that stays.
    /// </summary>
    public static class PaperLayerBaker
    {
        static readonly Color Tank = new Color(0.94f, 0.90f, 0.84f, 1f);
        static readonly Color Shorts = new Color(0.53f, 0.51f, 0.56f, 1f);
        static readonly Color Ink = new Color(0.24f, 0.19f, 0.16f, 1f);

        /// <summary>Squared RGB distance under which a border-connected pixel is the cream backdrop.</summary>
        const float BackgroundTolerance = 0.02f;

        /// <summary>Squared RGB distance to the sampled hair palette that still counts as hair.</summary>
        const float HairTolerance = 0.05f;

        /// <summary>Squared RGB distance a single hair flood step may cross.</summary>
        const float HairStep = 0.035f;

        struct BakeSpec
        {
            public string LegacySheet;
            public string Character;
            public string Outfit;

            /// <summary>Top of the garment, just under the collar. Above it is head, hair and neck.</summary>
            public float ShoulderV;

            /// <summary>Waistline: tank above, shorts below.</summary>
            public float WaistV;

            /// <summary>Where the shorts end and the thighs start.</summary>
            public float HipEndV;

            /// <summary>Bottom of the garment. Below it the original legs, socks and shoes stay.</summary>
            public float HemV;

            /// <summary>Skin inside these normalised boxes stays on the figure (hands poking out of sleeves).</summary>
            public Rect[] Hands;

            /// <summary>
            /// Scales the hair flood thresholds: below 1 for hair that shares the outfit's colour,
            /// and exactly 0 for a hood or hat, where nothing tells hair and garment apart at all.
            /// </summary>
            public float HairGrip;

            public float HueShift;
            public bool WriteBase;
        }

        static Rect[] HandBoxes(float v0, float v1, float inner)
        {
            var height = v1 - v0;
            return new[]
            {
                new Rect(0.04f, v0, inner - 0.04f, height),
                new Rect(1f - inner, v0, inner - 0.04f, height),
            };
        }

        [MenuItem("Hyper Smash/Bake Character Layers")]
        public static void BakeAllMenu() => BakeAll();

        /// <summary>Batch entry: Unity -executeMethod HyperPuzzle2D.Editor.PaperLayerBaker.BakeAll</summary>
        public static void BakeAll()
        {
            var specs = new[]
            {
                new BakeSpec { LegacySheet = "PaperMaiden", Character = "Maiden", Outfit = "Teal", ShoulderV = 0.787f, WaistV = 0.64f, HipEndV = 0.54f, HemV = 0.43f, Hands = HandBoxes(0.47f, 0.58f, 0.25f), HairGrip = 1f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperMaiden", Character = "Maiden", Outfit = "Violet", ShoulderV = 0.787f, WaistV = 0.64f, HipEndV = 0.54f, HemV = 0.43f, Hands = HandBoxes(0.47f, 0.58f, 0.25f), HairGrip = 1f, HueShift = 0.42f, WriteBase = false },
                new BakeSpec { LegacySheet = "PaperSailor", Character = "Sailor", Outfit = "Navy", ShoulderV = 0.790f, WaistV = 0.63f, HipEndV = 0.54f, HemV = 0.46f, Hands = HandBoxes(0.48f, 0.58f, 0.25f), HairGrip = 0.4f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperScholar", Character = "Scholar", Outfit = "Crimson", ShoulderV = 0.790f, WaistV = 0.62f, HipEndV = 0.53f, HemV = 0.43f, Hands = HandBoxes(0.45f, 0.57f, 0.27f), HairGrip = 1f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperMiko", Character = "Miko", Outfit = "Vermilion", ShoulderV = 0.790f, WaistV = 0.65f, HipEndV = 0.54f, HemV = 0.11f, Hands = HandBoxes(0.46f, 0.57f, 0.24f), HairGrip = 1f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperRanger", Character = "Ranger", Outfit = "Forest", ShoulderV = 0.780f, WaistV = 0.63f, HipEndV = 0.53f, HemV = 0.28f, Hands = HandBoxes(0.45f, 0.57f, 0.26f), HairGrip = 0f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperWitch", Character = "Witch", Outfit = "Night", ShoulderV = 0.780f, WaistV = 0.63f, HipEndV = 0.53f, HemV = 0.20f, Hands = HandBoxes(0.45f, 0.57f, 0.26f), HairGrip = 0f, HueShift = 0f, WriteBase = true },
                new BakeSpec { LegacySheet = "PaperSakura", Character = "Sakura", Outfit = "Bloom", ShoulderV = 0.790f, WaistV = 0.64f, HipEndV = 0.54f, HemV = 0.12f, Hands = HandBoxes(0.46f, 0.56f, 0.24f), HairGrip = 0.25f, HueShift = 0f, WriteBase = true },
            };

            foreach (var spec in specs)
            {
                BakeOne(spec);
            }

            AssetDatabase.Refresh();
            Debug.Log("[PaperLayerBaker] Done.");
        }

        static void BakeOne(BakeSpec spec)
        {
            var srcPath = $"Assets/Resources/Art/{spec.LegacySheet}.png";
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
            if (src == null)
            {
                Debug.LogError("[PaperLayerBaker] Missing " + srcPath);
                return;
            }

            MakeReadable(srcPath);
            src = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);

            var pixels = src.GetPixels32();
            var w = src.width;
            var h = src.height;

            var figure = BuildFigure(pixels, w, h);
            var hair = Dilate(BuildHair(pixels, figure, w, h, spec), figure, w, h, 2);
            var hands = BuildHands(pixels, figure, w, h, spec.Hands);
            var centre = FigureCentre(figure, w, h);
            var garment = BuildGarment(figure, hair, hands, w, h, spec);
            var bodyHalf = BodyHalf(garment, w, h, spec.WaistV, centre);
            ExtendCollar(pixels, garment, figure, hair, hands, w, h, spec, centre, bodyHalf);

            var charDir = $"Assets/Resources/Art/Chars/{spec.Character}";
            var outfitDir = $"{charDir}/Outfits/{spec.Outfit}";
            Directory.CreateDirectory(AbsoluteFromAssets(outfitDir));

            if (spec.WriteBase)
            {
                Directory.CreateDirectory(AbsoluteFromAssets(charDir));
                WritePng(
                    $"{charDir}/Base.png",
                    w,
                    h,
                    BuildBase(pixels, figure, garment, hands, w, h, spec, centre, bodyHalf));
            }

            WritePng($"{outfitDir}/Outfit.png", w, h, BuildOutfit(pixels, garment, spec.HueShift));

            var maskPixels = new Color32[garment.Length];
            for (var i = 0; i < garment.Length; i++)
            {
                maskPixels[i] = garment[i] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }

            WritePng($"{outfitDir}/Mask.png", w, h, maskPixels);
            Debug.Log($"[PaperLayerBaker] {spec.Character}/{spec.Outfit}");
        }

        /// <summary>The sheet ships on a flat cream backdrop; flood it away from the four edges.</summary>
        static bool[] BuildFigure(Color32[] pixels, int w, int h)
        {
            var background = new bool[w * h];
            var reference = Average(pixels[0], pixels[w - 1], pixels[(h - 1) * w], pixels[h * w - 1]);
            var stack = new Stack<int>(4096);

            for (var x = 0; x < w; x++)
            {
                SeedBackground(x, pixels, reference, background, stack);
                SeedBackground((h - 1) * w + x, pixels, reference, background, stack);
            }

            for (var y = 0; y < h; y++)
            {
                SeedBackground(y * w, pixels, reference, background, stack);
                SeedBackground(y * w + w - 1, pixels, reference, background, stack);
            }

            while (stack.Count > 0)
            {
                var index = stack.Pop();
                var x = index % w;
                var y = index / w;

                if (x > 0) SeedBackground(index - 1, pixels, reference, background, stack);
                if (x < w - 1) SeedBackground(index + 1, pixels, reference, background, stack);
                if (y > 0) SeedBackground(index - w, pixels, reference, background, stack);
                if (y < h - 1) SeedBackground(index + w, pixels, reference, background, stack);
            }

            var figure = new bool[w * h];
            for (var i = 0; i < figure.Length; i++)
            {
                figure[i] = pixels[i].a > 128 && !background[i];
            }

            return figure;
        }

        static void SeedBackground(int index, Color32[] pixels, Color32 reference, bool[] background, Stack<int> stack)
        {
            if (background[index] || Distance2(pixels[index], reference) > BackgroundTolerance)
            {
                return;
            }

            background[index] = true;
            stack.Push(index);
        }

        static bool[] BuildGarment(bool[] figure, bool[] hair, bool[] hands, int w, int h, BakeSpec spec)
        {
            var garment = new bool[w * h];
            for (var y = 0; y < h; y++)
            {
                var v = (y + 0.5f) / h;
                if (v >= spec.ShoulderV || v < spec.HemV)
                {
                    continue;
                }

                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    garment[i] = figure[i] && !hair[i] && !hands[i];
                }
            }

            return CloseHoles(garment, w, h);
        }

        /// <summary>
        /// A collar, a neckerchief or a sailor flap sits above the shoulder line and would be left
        /// hanging in mid-air once the rest of the outfit is torn off, so the band is carried up to
        /// the chin everywhere except the narrow column of the neck itself.
        /// </summary>
        static void ExtendCollar(
            Color32[] pixels, bool[] garment, bool[] figure, bool[] hair, bool[] hands, int w, int h,
            BakeSpec spec, float centre, float bodyHalf)
        {
            // Without hair protection there is nothing to shield the jaw, and the band would take
            // the lower half of the face with the collar. Hooded characters keep their heads whole.
            if (bodyHalf <= 1f || spec.HairGrip <= 0f)
            {
                return;
            }

            var neckHalf = bodyHalf * 0.40f;
            var y0 = Mathf.Clamp(Mathf.RoundToInt(spec.ShoulderV * h), 0, h - 1);
            var y1 = Mathf.Clamp(Mathf.RoundToInt((spec.ShoulderV + 0.055f) * h), 0, h - 1);

            for (var y = y0; y <= y1; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (!figure[i] || hair[i] || hands[i])
                    {
                        continue;
                    }

                    // Spare the throat itself, but not a collar that closes across it.
                    if (Mathf.Abs(x - centre) <= neckHalf && IsSkin(pixels[i]))
                    {
                        continue;
                    }

                    garment[i] = true;
                }
            }
        }

        /// <summary>
        /// Hair hanging below the collar must survive the tear, so it is flood-filled down from the
        /// head. The flood only walks through colours sampled from the top of the head, which is
        /// what stops it from leaking across the neck into a skin-coloured blouse.
        /// </summary>
        static bool[] BuildHair(Color32[] pixels, bool[] figure, int w, int h, BakeSpec spec)
        {
            var hair = new bool[w * h];
            var grip = spec.HairGrip;
            if (grip <= 0f)
            {
                // Hooded and hatted characters: the head covering is part of the outfit, so no
                // palette separates hair from garment. Nothing below the collar is spared.
                return hair;
            }

            var top = FigureTop(figure, w, h);
            var palette = SamplePalette(pixels, figure, w, h, top - 0.12f, top);
            if (palette.Count == 0)
            {
                return hair;
            }

            var tolerance = HairTolerance * grip;
            var step = HairStep * grip;

            var queue = new Queue<int>(4096);
            var shoulderRow = Mathf.Clamp(Mathf.RoundToInt(spec.ShoulderV * h), 1, h - 1);

            // Seed on hair-coloured pixels only. Seeding the whole head would also start the flood
            // on the face and on a collar that reaches above the shoulder line, and it would then
            // run down the whole garment.
            for (var y = shoulderRow; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (!figure[i] || hair[i] || PaletteDistance2(palette, pixels[i]) > tolerance)
                    {
                        continue;
                    }

                    hair[i] = true;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % w;
                var y = index / w;

                TryHair(index, x - 1, y, pixels, figure, hair, palette, queue, w, h, shoulderRow, tolerance, step);
                TryHair(index, x + 1, y, pixels, figure, hair, palette, queue, w, h, shoulderRow, tolerance, step);
                TryHair(index, x, y - 1, pixels, figure, hair, palette, queue, w, h, shoulderRow, tolerance, step);
                TryHair(index, x, y + 1, pixels, figure, hair, palette, queue, w, h, shoulderRow, tolerance, step);
            }

            return hair;
        }

        static void TryHair(
            int from, int x, int y, Color32[] pixels, bool[] figure, bool[] hair,
            List<Color32> palette, Queue<int> queue, int w, int h, int shoulderRow,
            float tolerance, float step)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || y >= shoulderRow)
            {
                return;
            }

            var to = y * w + x;
            if (hair[to] || !figure[to])
            {
                return;
            }

            if (Distance2(pixels[from], pixels[to]) > step || PaletteDistance2(palette, pixels[to]) > tolerance)
            {
                return;
            }

            hair[to] = true;
            queue.Enqueue(to);
        }

        /// <summary>
        /// Hands are skin blobs poking out of the sleeves. Colour alone would also match the cream
        /// fabric, so they are only looked for inside the authored boxes, then cleaned up to the
        /// solid blobs and grown a little to take their ink outline with them.
        /// </summary>
        static bool[] BuildHands(Color32[] pixels, bool[] figure, int w, int h, Rect[] boxes)
        {
            var hands = new bool[w * h];
            if (boxes == null)
            {
                return hands;
            }

            var seeds = new bool[w * h];
            foreach (var box in boxes)
            {
                var x0 = Mathf.Clamp(Mathf.RoundToInt(box.xMin * w), 0, w - 1);
                var x1 = Mathf.Clamp(Mathf.RoundToInt(box.xMax * w), 0, w - 1);
                var y0 = Mathf.Clamp(Mathf.RoundToInt(box.yMin * h), 0, h - 1);
                var y1 = Mathf.Clamp(Mathf.RoundToInt(box.yMax * h), 0, h - 1);

                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var i = y * w + x;
                        seeds[i] = figure[i] && IsSkin(pixels[i]);
                    }
                }
            }

            // Keep only blobs big enough to be a hand; stray skin-toned speckles on lace or cuffs
            // would otherwise punch holes in the outfit.
            var visited = new bool[w * h];
            var blob = new List<int>(2048);
            var stack = new Stack<int>(2048);

            for (var i = 0; i < seeds.Length; i++)
            {
                if (!seeds[i] || visited[i])
                {
                    continue;
                }

                blob.Clear();
                stack.Push(i);
                visited[i] = true;

                while (stack.Count > 0)
                {
                    var index = stack.Pop();
                    blob.Add(index);
                    var x = index % w;
                    var y = index / w;

                    PushSeed(index - 1, x > 0, seeds, visited, stack);
                    PushSeed(index + 1, x < w - 1, seeds, visited, stack);
                    PushSeed(index - w, y > 0, seeds, visited, stack);
                    PushSeed(index + w, y < h - 1, seeds, visited, stack);
                }

                if (blob.Count < w * h / 4000)
                {
                    continue;
                }

                foreach (var index in blob)
                {
                    hands[index] = true;
                }
            }

            return Dilate(hands, figure, w, h, 3);
        }

        static void PushSeed(int index, bool inBounds, bool[] seeds, bool[] visited, Stack<int> stack)
        {
            if (!inBounds || visited[index] || !seeds[index])
            {
                return;
            }

            visited[index] = true;
            stack.Push(index);
        }

        static bool[] Dilate(bool[] mask, bool[] figure, int w, int h, int radius)
        {
            var current = mask;
            for (var step = 0; step < radius; step++)
            {
                var next = (bool[])current.Clone();
                for (var y = 1; y < h - 1; y++)
                {
                    for (var x = 1; x < w - 1; x++)
                    {
                        var i = y * w + x;
                        if (current[i] || !figure[i])
                        {
                            continue;
                        }

                        if (current[i - 1] || current[i + 1] || current[i - w] || current[i + w])
                        {
                            next[i] = true;
                        }
                    }
                }

                current = next;
            }

            return current;
        }

        static float FigureTop(bool[] figure, int w, int h)
        {
            for (var y = h - 1; y >= 0; y--)
            {
                for (var x = 0; x < w; x++)
                {
                    if (figure[y * w + x])
                    {
                        return (y + 0.5f) / h;
                    }
                }
            }

            return 1f;
        }

        static List<Color32> SamplePalette(Color32[] pixels, bool[] figure, int w, int h, float v0, float v1)
        {
            var buckets = new Dictionary<int, int>();
            var colours = new Dictionary<int, Color32>();

            for (var y = Mathf.Max(Mathf.RoundToInt(v0 * h), 0); y < Mathf.RoundToInt(v1 * h) && y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    if (!figure[i])
                    {
                        continue;
                    }

                    var c = pixels[i];
                    var key = (c.r >> 4) << 8 | (c.g >> 4) << 4 | c.b >> 4;
                    buckets.TryGetValue(key, out var count);
                    buckets[key] = count + 1;
                    colours[key] = c;
                }
            }

            var total = 0;
            foreach (var count in buckets.Values)
            {
                total += count;
            }

            var palette = new List<Color32>();
            foreach (var pair in buckets)
            {
                var colour = colours[pair.Key];
                Color.RGBToHSV(colour, out _, out var saturation, out _);

                // Skin from a forehead and the strong dye of a ribbon or hat are not hair, and
                // letting either into the palette hands the flood a route into the garment.
                if (pair.Value > total * 0.01f && saturation < 0.6f && !IsSkin(colour))
                {
                    palette.Add(colour);
                }
            }

            return palette;
        }

        static float PaletteDistance2(List<Color32> palette, Color32 c)
        {
            var best = float.MaxValue;
            foreach (var entry in palette)
            {
                var d = Distance2(entry, c);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        /// <summary>Fills pinholes (buttons, lace, outlines) so the outfit stays one solid sheet.</summary>
        static bool[] CloseHoles(bool[] mask, int w, int h)
        {
            var next = (bool[])mask.Clone();
            for (var y = 1; y < h - 1; y++)
            {
                for (var x = 1; x < w - 1; x++)
                {
                    var i = y * w + x;
                    if (mask[i])
                    {
                        continue;
                    }

                    var n = 0;
                    if (mask[i - 1]) n++;
                    if (mask[i + 1]) n++;
                    if (mask[i - w]) n++;
                    if (mask[i + w]) n++;
                    if (n >= 4)
                    {
                        next[i] = true;
                    }
                }
            }

            return next;
        }

        static bool IsSkin(Color32 c)
        {
            Color.RGBToHSV(c, out var hue, out var s, out var v);
            return hue > 0.02f && hue < 0.12f && s > 0.13f && s < 0.34f && v > 0.82f;
        }

        /// <summary>
        /// Everything the garment did not claim is kept as drawn. Inside it the figure is rebuilt in
        /// two passes: first the silhouette is laid out as zones (tank, shorts, skin), then it is
        /// shaded and outlined as one shape. Shading each limb as it is drawn would leave seams
        /// where an arm crosses the torso, so the outline is only applied to the finished edge.
        /// </summary>
        static Color32[] BuildBase(
            Color32[] src, bool[] figure, bool[] garment, bool[] hands, int w, int h, BakeSpec spec,
            float centre, float bodyHalf)
        {
            var dst = new Color32[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                if (figure[i] && !garment[i])
                {
                    dst[i] = src[i];
                }
            }

            if (bodyHalf <= 1f)
            {
                return dst;
            }

            var zones = new byte[w * h];
            var legs = MeasureLegs(figure, w, h, Mathf.Clamp(Mathf.RoundToInt(spec.HemV * h), 1, h - 1), centre, bodyHalf);

            // The drawn legs are the only true measurement of this body, so the torso is sized to
            // sit on them. Falling back to the garment's own width would make a puffed skirt into
            // wide hips. Characters in floor-length robes show only their feet, hence the clamp.
            var hipsHalf = Mathf.Clamp(
                (legs.Right - legs.Left) * 0.5f + legs.Half, bodyHalf * 0.72f, bodyHalf * 1.05f);

            ShapeTorso(zones, garment, w, h, spec, centre, hipsHalf);
            var seam = ShapeLegs(zones, garment, w, h, spec, legs, hipsHalf);
            ShapeArms(zones, garment, hands, w, h, spec, centre, hipsHalf);
            Shade(dst, zones, src, figure, garment, w, h, SampleSkin(src, figure, garment, w, h, spec), seam);
            return dst;
        }

        const byte ZoneTank = 1;
        const byte ZoneShorts = 2;
        const byte ZoneSkin = 3;

        /// <summary>Shoulders, tank, bare midriff and shorts, narrowing at the waist like a figure.</summary>
        static void ShapeTorso(
            byte[] zones, bool[] garment, int w, int h, BakeSpec spec, float centre, float bodyHalf)
        {
            var chinV = spec.ShoulderV + 0.055f;
            var strapV = spec.ShoulderV - 0.03f;
            var midriffV = spec.WaistV + 0.015f;
            var shortsHem = spec.HipEndV - 0.02f;

            var y0 = Mathf.Clamp(Mathf.RoundToInt(shortsHem * h), 0, h - 1);
            var y1 = Mathf.Clamp(Mathf.RoundToInt(chinV * h), 0, h - 1);

            for (var y = y0; y <= y1; y++)
            {
                var v = (y + 0.5f) / h;
                float half;
                float notch = 0f;
                byte zone;

                if (v >= spec.ShoulderV)
                {
                    // Bare shoulders above the strap line, tapering in towards the neck.
                    var t = Mathf.InverseLerp(spec.ShoulderV, chinV, v);
                    half = bodyHalf * Mathf.Lerp(1.14f, 0.42f, t * t);
                    zone = ZoneSkin;
                }
                else if (v >= strapV)
                {
                    // Tank neckline: the straps sit at the sides, the middle stays bare.
                    var t = Mathf.InverseLerp(strapV, spec.ShoulderV, v);
                    half = bodyHalf * Mathf.Lerp(1.06f, 1.14f, t);
                    notch = half * Mathf.Lerp(0f, 0.62f, t);
                    zone = ZoneTank;
                }
                else if (v >= midriffV)
                {
                    var t = Mathf.InverseLerp(midriffV, strapV, v);
                    half = bodyHalf * Mathf.Lerp(0.90f, 1.06f, t * t);
                    zone = ZoneTank;
                }
                else if (v >= spec.WaistV - 0.012f)
                {
                    half = bodyHalf * 0.90f;
                    zone = ZoneSkin;
                }
                else
                {
                    var t = Mathf.InverseLerp(spec.WaistV, shortsHem, v);
                    half = bodyHalf * Mathf.Lerp(0.92f, 1.04f, t);
                    notch = t > 0.75f ? bodyHalf * Mathf.Lerp(0f, 0.16f, (t - 0.75f) * 4f) : 0f;
                    zone = ZoneShorts;
                }

                Span(zones, garment, w, y, centre, half, notch, zone);
            }
        }

        /// <summary>
        /// Thighs bridging the shorts to the legs the sheet still shows below the hem. They leave
        /// the hem at the drawn legs' exact width and heading and only then swell towards the hips,
        /// which is what keeps the join invisible. The returned row is where the two art meet.
        /// </summary>
        static int ShapeLegs(byte[] zones, bool[] garment, int w, int h, BakeSpec spec, Legs legs, float hipsHalf)
        {
            var hemRow = Mathf.Clamp(Mathf.RoundToInt(spec.HemV * h), 1, h - 1);
            var hipRow = Mathf.Clamp(Mathf.RoundToInt((spec.HipEndV - 0.02f) * h), 0, h - 1);
            if (hipRow <= hemRow)
            {
                return hemRow;
            }

            var span = hipRow - hemRow;
            var hipHalf = Mathf.Min(legs.Half * 1.15f, hipsHalf * 0.46f);
            var hipLeft = -hipsHalf * 0.48f;
            var hipRight = hipsHalf * 0.48f;

            for (var y = hemRow; y <= hipRow; y++)
            {
                var rows = y - hemRow;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rows / (float)span - 0.15f) / 0.85f));

                // Continue the leg's own taper out of the hem before bending towards the hip.
                var left = Mathf.Lerp(legs.Left - legs.Slope * rows, legs.Centre + hipLeft, t);
                var right = Mathf.Lerp(legs.Right + legs.Slope * rows, legs.Centre + hipRight, t);
                var half = Mathf.Lerp(legs.Half, hipHalf, t);

                // Keep a gap between the thighs; without it a floor-length robe, whose only visible
                // legs are a pair of feet, bakes into one pillar.
                half = Mathf.Min(half, (right - left) * 0.5f - hipsHalf * 0.09f);
                Span(zones, garment, w, y, left, half, 0f, ZoneSkin);
                Span(zones, garment, w, y, right, half, 0f, ZoneSkin);
            }

            return hemRow;
        }

        /// <summary>Where the drawn legs are at the hem, how wide, and how fast they are widening.</summary>
        struct Legs
        {
            public float Left;
            public float Right;
            public float Half;

            /// <summary>Pixels each leg widens per row going up, taken from the drawn contour.</summary>
            public float Slope;

            public float Centre => (Left + Right) * 0.5f;
        }

        /// <summary>
        /// Finds where the real legs are, just under the hem. A cloak or a long skirt often hangs
        /// lower than the hem line, so several rows are tried and the narrowest pair of runs wins:
        /// that is the row showing legs rather than more fabric.
        /// </summary>
        static Legs MeasureLegs(bool[] figure, int w, int h, int hemRow, float centre, float bodyHalf)
        {
            var legs = new Legs
            {
                Left = centre - bodyHalf * 0.42f,
                Right = centre + bodyHalf * 0.42f,
                Half = bodyHalf * 0.34f,
                Slope = 0f,
            };

            var near = LegRow(figure, w, h, hemRow - 3, centre);
            if (!near.HasValue)
            {
                return legs;
            }

            legs.Left = near.Value.Left;
            legs.Right = near.Value.Right;
            legs.Half = near.Value.Half;

            var far = LegRow(figure, w, h, hemRow - 3 - Mathf.RoundToInt(h * 0.03f), centre);
            if (far.HasValue)
            {
                var rows = Mathf.Max(Mathf.RoundToInt(h * 0.03f), 1);
                // Damped so a knee's curvature does not run away over the length of a thigh.
                legs.Slope = Mathf.Clamp((legs.Half - far.Value.Half) / rows, -0.2f, 0.2f) * 0.6f;
            }

            return legs;
        }

        /// <summary>The two leg runs on one row, or nothing if that row is still fabric.</summary>
        static Legs? LegRow(bool[] figure, int w, int h, int row, float centre)
        {
            if (row < 0 || row >= h)
            {
                return null;
            }

            var runs = new List<Vector2Int>(8);
            CollectRuns(figure, w, row, runs);

            var leftRun = new Vector2Int(-1, -1);
            var rightRun = new Vector2Int(-1, -1);
            foreach (var run in runs)
            {
                var mid = (run.x + run.y) * 0.5f;
                if (mid < centre && (leftRun.x < 0 || run.y - run.x > leftRun.y - leftRun.x))
                {
                    leftRun = run;
                }

                if (mid >= centre && (rightRun.x < 0 || run.y - run.x > rightRun.y - rightRun.x))
                {
                    rightRun = run;
                }
            }

            if (leftRun.x < 0 || rightRun.x < 0)
            {
                return null;
            }

            return new Legs
            {
                Left = (leftRun.x + leftRun.y) * 0.5f,
                Right = (rightRun.x + rightRun.y) * 0.5f,
                Half = Mathf.Max((leftRun.y - leftRun.x + rightRun.y - rightRun.x) * 0.25f, 3f),
            };
        }

        /// <summary>Arms bowing out from the shoulders and landing on the hands the sheet shows.</summary>
        static void ShapeArms(
            byte[] zones, bool[] garment, bool[] hands, int w, int h, BakeSpec spec,
            float centre, float bodyHalf)
        {
            var shoulderRow = Mathf.Clamp(Mathf.RoundToInt(spec.ShoulderV * h), 0, h - 1);
            var fallbackRow = Mathf.Clamp(Mathf.RoundToInt((spec.HipEndV + 0.02f) * h), 0, h - 1);

            HandAnchor(hands, w, h, centre, true, out var leftHandX, out var leftHandRow, out var leftHandHalf);
            HandAnchor(hands, w, h, centre, false, out var rightHandX, out var rightHandRow, out var rightHandHalf);

            if (leftHandRow < 0)
            {
                leftHandX = centre - bodyHalf * 1.25f;
                leftHandRow = fallbackRow;
                leftHandHalf = bodyHalf * 0.30f;
            }

            if (rightHandRow < 0)
            {
                rightHandX = centre + bodyHalf * 1.25f;
                rightHandRow = fallbackRow;
                rightHandHalf = bodyHalf * 0.30f;
            }

            // An arm is sized off the hand it ends in, not off the garment: a puffed sleeve and a
            // sailor top give wildly different torso widths for the same slim arm.
            ShapeArm(zones, garment, w, centre - bodyHalf * 1.02f, shoulderRow, leftHandX, leftHandRow, leftHandHalf, -1f);
            ShapeArm(zones, garment, w, centre + bodyHalf * 1.02f, shoulderRow, rightHandX, rightHandRow, rightHandHalf, 1f);
        }

        static void ShapeArm(
            byte[] zones, bool[] garment, int w, float shoulderX, int shoulderRow,
            float handX, int handRow, float handHalf, float outward)
        {
            if (shoulderRow <= handRow)
            {
                return;
            }

            for (var y = handRow; y <= shoulderRow; y++)
            {
                var t = Mathf.InverseLerp(shoulderRow, handRow, y);
                var bow = outward * handHalf * 0.35f * Mathf.Sin(t * Mathf.PI);
                var x = Mathf.Lerp(shoulderX, handX, t) + bow;
                // Shoulder end stays thick, wrist thins out, and the last stretch rounds off so the
                // arm meets the hand instead of stopping in a flat bar.
                var taper = t > 0.9f ? Mathf.Sqrt(Mathf.Max(1f - (t - 0.9f) * 10f, 0.05f)) : 1f;
                var half = handHalf * Mathf.Lerp(1.05f, 0.62f, t) * taper;
                Span(zones, garment, w, y, x, half, 0f, ZoneSkin);
            }
        }

        static void HandAnchor(
            bool[] hands, int w, int h, float centre, bool leftSide,
            out float x, out int row, out float halfWidth)
        {
            x = 0f;
            row = -1;
            halfWidth = 0f;

            long sumX = 0;
            long sumY = 0;
            long count = 0;
            var top = -1;
            var min = int.MaxValue;
            var max = int.MinValue;

            for (var y = 0; y < h; y++)
            {
                for (var px = 0; px < w; px++)
                {
                    if (!hands[y * w + px])
                    {
                        continue;
                    }

                    if (leftSide ? px >= centre : px < centre)
                    {
                        continue;
                    }

                    sumX += px;
                    sumY += y;
                    count++;
                    if (y > top)
                    {
                        top = y;
                    }

                    if (px < min) min = px;
                    if (px > max) max = px;
                }
            }

            if (count == 0)
            {
                return;
            }

            x = (float)sumX / count;
            halfWidth = Mathf.Max((max - min) * 0.5f * 0.7f, 3f);
            // Meet the hand a little above its centre so wrist and arm overlap instead of butting.
            row = Mathf.RoundToInt(Mathf.Lerp((float)sumY / count, top, 0.5f));
        }

        static void Span(byte[] zones, bool[] garment, int w, int y, float centre, float half, float notch, byte zone)
        {
            var min = Mathf.RoundToInt(centre - half);
            var max = Mathf.RoundToInt(centre + half);

            for (var x = min; x <= max; x++)
            {
                if (x < 0 || x >= w)
                {
                    continue;
                }

                if (notch > 0f && Mathf.Abs(x - centre) < notch)
                {
                    continue;
                }

                var i = y * w + x;
                if (garment[i])
                {
                    zones[i] = zone;
                }
            }
        }

        /// <summary>
        /// Turns the zone layout into pixels: a soft round-off towards the edges, one ink line on
        /// the finished silhouette, and a blend into the drawn legs so the thigh has no hard seam.
        /// </summary>
        static void Shade(
            Color32[] dst, byte[] zones, Color32[] src, bool[] figure, bool[] garment,
            int w, int h, Color skin, int seamRow)
        {
            var seamFade = Mathf.Max(Mathf.RoundToInt(h * 0.02f), 6);

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = y * w + x;
                    var zone = zones[i];
                    if (zone == 0)
                    {
                        continue;
                    }

                    var tone = zone switch
                    {
                        ZoneTank => Tank,
                        ZoneShorts => Shorts,
                        _ => skin,
                    };

                    var depth = EdgeDepth(zones, w, x, y, zone);
                    var body = tone * Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(depth / 7f));

                    if (depth <= 1.5f && OnSilhouette(zones, figure, garment, w, h, x, y))
                    {
                        body = Color.Lerp(body, Ink, 0.5f);
                    }

                    // Right under the hem the rebuilt thigh has to become the drawn leg.
                    if (zone == ZoneSkin && y - seamRow < seamFade && y >= seamRow)
                    {
                        var below = SampleColumn(src, figure, garment, w, seamRow - 3, x);
                        if (below.HasValue)
                        {
                            body = Color.Lerp(below.Value, body, (y - seamRow) / (float)seamFade);
                        }
                    }

                    body.a = 1f;
                    dst[i] = body;
                }
            }
        }

        /// <summary>Horizontal distance to the nearest pixel outside the body, capped for speed.</summary>
        static float EdgeDepth(byte[] zones, int w, int x, int y, byte zone)
        {
            const int Limit = 8;
            var left = Limit;
            var right = Limit;

            for (var d = 1; d <= Limit; d++)
            {
                if (x - d < 0 || zones[y * w + x - d] == 0)
                {
                    left = d;
                    break;
                }
            }

            for (var d = 1; d <= Limit; d++)
            {
                if (x + d >= w || zones[y * w + x + d] == 0)
                {
                    right = d;
                    break;
                }
            }

            return Mathf.Min(left, right);
        }

        /// <summary>
        /// True when the pixel borders on empty space rather than on art that stays. An outline is
        /// wanted where the body meets the backdrop, not where it runs into a hand or a knee.
        /// </summary>
        static bool OnSilhouette(byte[] zones, bool[] figure, bool[] garment, int w, int h, int x, int y)
        {
            return IsOpen(zones, figure, garment, w, h, x - 1, y)
                   || IsOpen(zones, figure, garment, w, h, x + 1, y)
                   || IsOpen(zones, figure, garment, w, h, x, y - 1)
                   || IsOpen(zones, figure, garment, w, h, x, y + 1);
        }

        static bool IsOpen(byte[] zones, bool[] figure, bool[] garment, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
            {
                return true;
            }

            var i = y * w + x;
            return zones[i] == 0 && (!figure[i] || garment[i]);
        }

        static Color? SampleColumn(Color32[] src, bool[] figure, bool[] garment, int w, int row, int x)
        {
            if (row < 0)
            {
                return null;
            }

            var i = row * w + x;
            return figure[i] && !garment[i] ? (Color)src[i] : null;
        }

        static void CollectRuns(bool[] mask, int w, int y, List<Vector2Int> runs)
        {
            runs.Clear();
            var start = -1;
            for (var x = 0; x < w; x++)
            {
                var on = mask[y * w + x];
                if (on && start < 0)
                {
                    start = x;
                }
                else if (!on && start >= 0)
                {
                    if (x - start > 3)
                    {
                        runs.Add(new Vector2Int(start, x - 1));
                    }

                    start = -1;
                }
            }

            if (start >= 0)
            {
                runs.Add(new Vector2Int(start, w - 1));
            }
        }

        static float FigureCentre(bool[] figure, int w, int h)
        {
            long sum = 0;
            long count = 0;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!figure[y * w + x])
                    {
                        continue;
                    }

                    sum += x;
                    count++;
                }
            }

            return count == 0 ? w * 0.5f : (float)sum / count;
        }

        static float BodyHalf(bool[] garment, int w, int h, float waistV, float centre)
        {
            var y = Mathf.Clamp(Mathf.RoundToInt(waistV * h), 0, h - 1);
            var runs = new List<Vector2Int>(8);
            CollectRuns(garment, w, y, runs);
            if (runs.Count == 0)
            {
                return 0f;
            }

            var best = runs[0];
            var bestScore = float.MaxValue;
            foreach (var run in runs)
            {
                var mid = (run.x + run.y) * 0.5f;
                var score = Mathf.Abs(mid - centre) - (run.y - run.x) * 0.25f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = run;
                }
            }

            return (best.y - best.x) * 0.5f * 0.66f;
        }

        /// <summary>
        /// Skin is sampled from the legs just below the hem, the exact art the rebuilt thighs have
        /// to meet. The face is a shade lighter and using it leaves a visible band at the knee.
        /// </summary>
        static Color SampleSkin(
            Color32[] pixels, bool[] figure, bool[] garment, int w, int h, BakeSpec spec)
        {
            var legs = AverageSkin(
                pixels, figure, garment, w, h,
                Mathf.RoundToInt((spec.HemV - 0.06f) * h),
                Mathf.RoundToInt(spec.HemV * h),
                0, w);

            if (legs.HasValue)
            {
                return legs.Value;
            }

            var face = AverageSkin(
                pixels, figure, garment, w, h,
                Mathf.RoundToInt(0.79f * h),
                Mathf.RoundToInt(0.85f * h),
                Mathf.RoundToInt(0.44f * w),
                Mathf.RoundToInt(0.56f * w));

            return face ?? new Color(0.98f, 0.87f, 0.79f, 1f);
        }

        static Color? AverageSkin(
            Color32[] pixels, bool[] figure, bool[] garment, int w, int h,
            int y0, int y1, int x0, int x1)
        {
            float r = 0, g = 0, b = 0;
            var count = 0;

            for (var y = Mathf.Max(y0, 0); y < Mathf.Min(y1, h); y++)
            {
                for (var x = Mathf.Max(x0, 0); x < Mathf.Min(x1, w); x++)
                {
                    var i = y * w + x;
                    if (!figure[i] || garment[i] || !IsSkin(pixels[i]))
                    {
                        continue;
                    }

                    r += pixels[i].r;
                    g += pixels[i].g;
                    b += pixels[i].b;
                    count++;
                }
            }

            return count < 64
                ? null
                : new Color(r / count / 255f, g / count / 255f, b / count / 255f, 1f);
        }

        static Color32[] BuildOutfit(Color32[] src, bool[] garment, float hueShift)
        {
            var dst = new Color32[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                if (!garment[i])
                {
                    continue;
                }

                var c = (Color)src[i];
                if (hueShift > 0.001f)
                {
                    Color.RGBToHSV(c, out var hue, out var s, out var v);
                    hue = Mathf.Repeat(hue + hueShift, 1f);
                    c = Color.HSVToRGB(hue, Mathf.Clamp01(s * 1.05f), v);
                }

                c.a = 1f;
                dst[i] = c;
            }

            return dst;
        }

        static float Distance2(Color32 a, Color32 b)
        {
            var dr = (a.r - b.r) / 255f;
            var dg = (a.g - b.g) / 255f;
            var db = (a.b - b.b) / 255f;
            return dr * dr + dg * dg + db * db;
        }

        static Color32 Average(params Color32[] colors)
        {
            int r = 0, g = 0, b = 0;
            foreach (var c in colors)
            {
                r += c.r;
                g += c.g;
                b += c.b;
            }

            return new Color32((byte)(r / colors.Length), (byte)(g / colors.Length), (byte)(b / colors.Length), 255);
        }

        static string AbsoluteFromAssets(string assetsPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, assetsPath.StartsWith("Assets/")
                ? assetsPath.Substring("Assets/".Length)
                : assetsPath));
        }

        static void MakeReadable(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer || importer.isReadable)
            {
                return;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        static void WritePng(string assetsPath, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            var abs = AbsoluteFromAssets(assetsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? ".");
            File.WriteAllBytes(abs, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
