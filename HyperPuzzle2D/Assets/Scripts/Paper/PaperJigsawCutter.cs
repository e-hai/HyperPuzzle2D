using UnityEngine;

namespace HyperPuzzle2D.Paper
{
    /// <summary>
    /// Classic grid jigsaw over a clothes mask: each shared edge gets one interlocking tab
    /// (parabola bulge). Output is a low-res ownership map so hit-tests and sprite crops stay fast.
    /// </summary>
    public static class PaperJigsawCutter
    {
        public const byte Empty = 255;

        public sealed class Result
        {
            public byte[] Region;
            public int Width;
            public int Height;
            public int Cols;
            public int Rows;
            public int PieceCount => Cols * Rows;
            public int[] PixelCounts;
        }

        struct Tab
        {
            public float Center;
            public float HalfSpan;
            public float Depth;
            public int Sign;
        }

        public static Result Cut(bool[] clothesMask, int sheetW, int sheetH, int cols, int rows, int regionWidth)
        {
            var regionHeight = Mathf.Max(1, Mathf.RoundToInt(regionWidth * (sheetH / (float)sheetW)));
            var region = new byte[regionWidth * regionHeight];
            var pixelCounts = new int[cols * rows];

            if (!TryClothesBounds(clothesMask, sheetW, sheetH, out var minX, out var minY, out var maxX, out var maxY))
            {
                for (var i = 0; i < region.Length; i++)
                {
                    region[i] = Empty;
                }

                return new Result
                {
                    Region = region,
                    Width = regionWidth,
                    Height = regionHeight,
                    Cols = cols,
                    Rows = rows,
                    PixelCounts = pixelCounts,
                };
            }

            var vTabs = new Tab[cols - 1, rows];
            var hTabs = new Tab[cols, rows - 1];
            for (var c = 0; c < cols - 1; c++)
            {
                for (var r = 0; r < rows; r++)
                {
                    vTabs[c, r] = RandomTab();
                }
            }

            for (var c = 0; c < cols; c++)
            {
                for (var r = 0; r < rows - 1; r++)
                {
                    hTabs[c, r] = RandomTab();
                }
            }

            var bboxW = Mathf.Max(1, maxX - minX + 1);
            var bboxH = Mathf.Max(1, maxY - minY + 1);

            for (var cy = 0; cy < regionHeight; cy++)
            {
                var v = (cy + 0.5f) / regionHeight;
                var py = Mathf.Clamp((int)(v * sheetH), 0, sheetH - 1);

                for (var cx = 0; cx < regionWidth; cx++)
                {
                    var index = cy * regionWidth + cx;
                    var u = (cx + 0.5f) / regionWidth;
                    var px = Mathf.Clamp((int)(u * sheetW), 0, sheetW - 1);

                    if (!clothesMask[py * sheetW + px])
                    {
                        region[index] = Empty;
                        continue;
                    }

                    var gx = (px - minX + 0.5f) / bboxW * cols;
                    var gy = (py - minY + 0.5f) / bboxH * rows;
                    var id = PieceAt(gx, gy, cols, rows, vTabs, hTabs);
                    region[index] = (byte)id;
                    pixelCounts[id]++;
                }
            }

            return new Result
            {
                Region = region,
                Width = regionWidth,
                Height = regionHeight,
                Cols = cols,
                Rows = rows,
                PixelCounts = pixelCounts,
            };
        }

        public static bool[] MaskFromTexture(Texture2D mask, int sheetW, int sheetH)
        {
            var result = new bool[sheetW * sheetH];
            var pixels = mask.GetPixels32();
            for (var y = 0; y < sheetH; y++)
            {
                var my = Mathf.Clamp(y * mask.height / sheetH, 0, mask.height - 1);
                for (var x = 0; x < sheetW; x++)
                {
                    var mx = Mathf.Clamp(x * mask.width / sheetW, 0, mask.width - 1);
                    var c = pixels[my * mask.width + mx];
                    result[y * sheetW + x] = c.a > 128 && (c.r > 128 || c.g > 128 || c.b > 128);
                }
            }

            return result;
        }

        static Tab RandomTab()
        {
            // Shallower tabs so a dense clothes grid still reads as interlocking pieces, not giant bites.
            return new Tab
            {
                Center = 0.35f + UnityEngine.Random.value * 0.30f,
                HalfSpan = 0.16f + UnityEngine.Random.value * 0.08f,
                Depth = 0.14f + UnityEngine.Random.value * 0.06f,
                Sign = UnityEngine.Random.value < 0.5f ? -1 : 1,
            };
        }

        static int PieceAt(float x, float y, int cols, int rows, Tab[,] vTabs, Tab[,] hTabs)
        {
            x = Mathf.Clamp(x, 0f, cols - 0.0001f);
            y = Mathf.Clamp(y, 0f, rows - 0.0001f);

            var col = Mathf.FloorToInt(x);
            var row = Mathf.FloorToInt(y);

            // Vertical edge immediately left of the sample's cell, then right — tabs override floor.
            if (col >= 1)
            {
                TryClaimVertical(ref col, x, y, col, rows, vTabs);
            }

            if (col < cols - 1)
            {
                TryClaimVertical(ref col, x, y, col + 1, rows, vTabs);
            }

            if (row >= 1)
            {
                TryClaimHorizontal(ref row, x, y, row, cols, hTabs);
            }

            if (row < rows - 1)
            {
                TryClaimHorizontal(ref row, x, y, row + 1, cols, hTabs);
            }

            col = Mathf.Clamp(col, 0, cols - 1);
            row = Mathf.Clamp(row, 0, rows - 1);
            return row * cols + col;
        }

        /// <summary>Edge at integer <paramref name="edge"/> lies between columns edge-1 and edge.</summary>
        static void TryClaimVertical(ref int col, float x, float y, int edge, int rows, Tab[,] vTabs)
        {
            var r = Mathf.Clamp(Mathf.FloorToInt(y), 0, rows - 1);
            var tab = vTabs[edge - 1, r];
            var along = y - r;
            if (!InTab(along, tab))
            {
                return;
            }

            var depth = TabProfile(along, tab);
            if (tab.Sign > 0)
            {
                if (x >= edge - depth && x < edge)
                {
                    col = edge;
                }
            }
            else if (x >= edge && x <= edge + depth)
            {
                col = edge - 1;
            }
        }

        /// <summary>Edge at integer <paramref name="edge"/> lies between rows edge-1 and edge.</summary>
        static void TryClaimHorizontal(ref int row, float x, float y, int edge, int cols, Tab[,] hTabs)
        {
            var c = Mathf.Clamp(Mathf.FloorToInt(x), 0, cols - 1);
            var tab = hTabs[c, edge - 1];
            var along = x - c;
            if (!InTab(along, tab))
            {
                return;
            }

            var depth = TabProfile(along, tab);
            if (tab.Sign > 0)
            {
                if (y >= edge - depth && y < edge)
                {
                    row = edge;
                }
            }
            else if (y >= edge && y <= edge + depth)
            {
                row = edge - 1;
            }
        }

        static bool InTab(float along, Tab tab) => Mathf.Abs(along - tab.Center) <= tab.HalfSpan;

        static float TabProfile(float along, Tab tab)
        {
            var t = (along - tab.Center) / tab.HalfSpan;
            return tab.Depth * (1f - t * t);
        }

        static bool TryClothesBounds(
            bool[] mask, int w, int h, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = w;
            minY = h;
            maxX = -1;
            maxY = -1;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!mask[y * w + x])
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX >= minX;
        }
    }
}
