using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperPuzzle2D.Editor
{
    /// <summary>
    /// The paper-target artwork is sliced into puzzle pieces at runtime with pixel-exact UV rects,
    /// so its texture must not be rescaled on import. The default "Default" texture type applies
    /// non-power-of-two scaling on some platforms, which would shift every crop; forcing a Sprite
    /// import with NPOT scaling off keeps the sheet at its authored size everywhere.
    ///
    /// The character art ships on a flat cream background rather than pre-cut, so on import this
    /// flood-fills that background to transparent from the four edges. That is what makes hits
    /// follow the silhouette (a shot on the removed area is a miss) and lets a peeled zone reveal
    /// the washi backdrop behind it. Interior cream (white socks, blouses) survives because the
    /// fill only travels through pixels connected to a border.
    /// </summary>
    public sealed class PaperArtImporter : AssetPostprocessor
    {
        /// <summary>Squared RGB distance (0..1 space) under which a pixel counts as background.</summary>
        const float BackgroundThreshold = 0.020f;

        static bool IsPaperSheet(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains("_Mask") || normalized.Contains("/Mask.png"))
            {
                return false;
            }

            return normalized.Contains("Resources/Art/Paper")
                   || normalized.Contains("Resources/Art/Chars/");
        }

        static bool IsClothesMask(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.Contains("Resources/Art/")
                   && (normalized.Contains("_Mask") || normalized.EndsWith("/Mask.png") || normalized.Contains("/Mask."));
        }

        void OnPreprocessTexture()
        {
            if (IsClothesMask(assetPath))
            {
                var maskImporter = (TextureImporter)assetImporter;
                maskImporter.textureType = TextureImporterType.Default;
                maskImporter.mipmapEnabled = false;
                maskImporter.wrapMode = TextureWrapMode.Clamp;
                maskImporter.filterMode = FilterMode.Point;
                maskImporter.npotScale = TextureImporterNPOTScale.None;
                maskImporter.textureCompression = TextureImporterCompression.Uncompressed;
                maskImporter.alphaIsTransparency = true;
                maskImporter.isReadable = true;
                maskImporter.maxTextureSize = 2048;
                return;
            }

            if (!IsPaperSheet(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            // Runtime slices the sheet and samples its alpha for hit-testing, both of which need
            // CPU-side pixel access.
            importer.isReadable = true;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 2048;
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsPaperSheet(assetPath))
            {
                return;
            }

            // Layered Base/Outfit PNGs are authored with transparency already; flood-fill would
            // only risk eating into dark hair/outlines from transparent corner RGB.
            if (assetPath.Replace('\\', '/').Contains("Resources/Art/Chars/"))
            {
                return;
            }

            CutOutBackground(texture);
        }

        static void CutOutBackground(Texture2D texture)
        {
            var w = texture.width;
            var h = texture.height;
            var pixels = texture.GetPixels32();

            // Average the four corners for a stable background reference; a single corner can sit
            // on a stray dark pixel.
            var reference = AverageColor(
                pixels[0],
                pixels[w - 1],
                pixels[(h - 1) * w],
                pixels[h * w - 1]);

            var visited = new bool[w * h];
            var stack = new Stack<int>(1024);

            // Seed from every border pixel that matches the background.
            for (var x = 0; x < w; x++)
            {
                TrySeed(x, pixels, reference, visited, stack);
                TrySeed((h - 1) * w + x, pixels, reference, visited, stack);
            }

            for (var y = 0; y < h; y++)
            {
                TrySeed(y * w, pixels, reference, visited, stack);
                TrySeed(y * w + (w - 1), pixels, reference, visited, stack);
            }

            while (stack.Count > 0)
            {
                var index = stack.Pop();
                pixels[index].a = 0;

                var x = index % w;
                var y = index / w;

                if (x > 0) TryVisit(index - 1, pixels, reference, visited, stack);
                if (x < w - 1) TryVisit(index + 1, pixels, reference, visited, stack);
                if (y > 0) TryVisit(index - w, pixels, reference, visited, stack);
                if (y < h - 1) TryVisit(index + w, pixels, reference, visited, stack);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        static void TrySeed(int index, Color32[] pixels, Color32 reference, bool[] visited, Stack<int> stack)
        {
            if (visited[index] || !IsBackground(pixels[index], reference))
            {
                return;
            }

            visited[index] = true;
            stack.Push(index);
        }

        static void TryVisit(int index, Color32[] pixels, Color32 reference, bool[] visited, Stack<int> stack)
        {
            if (visited[index])
            {
                return;
            }

            visited[index] = true;
            if (IsBackground(pixels[index], reference))
            {
                stack.Push(index);
            }
        }

        static bool IsBackground(Color32 c, Color32 reference)
        {
            var dr = (c.r - reference.r) / 255f;
            var dg = (c.g - reference.g) / 255f;
            var db = (c.b - reference.b) / 255f;
            return dr * dr + dg * dg + db * db <= BackgroundThreshold;
        }

        static Color32 AverageColor(params Color32[] colors)
        {
            int r = 0, g = 0, b = 0;
            foreach (var c in colors)
            {
                r += c.r;
                g += c.g;
                b += c.b;
            }

            var n = colors.Length;
            return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }
    }
}
