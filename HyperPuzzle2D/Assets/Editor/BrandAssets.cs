using System;
using System.IO;
using HyperPuzzle2D.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HyperPuzzle2D.Editor
{
    /// <summary>
    /// Renders the launcher icon and splash logo from <see cref="BrandMark"/> and binds them to
    /// Player Settings. Keeping this in code means the icon can never fall out of sync with the
    /// in-game art, and a fresh clone can rebuild every image without any binary art in git.
    /// </summary>
    public static class BrandAssets
    {
        const string BrandFolder = "Assets/Art/Brand";
        const string IconPath = BrandFolder + "/AppIcon.png";
        const string IconForegroundPath = BrandFolder + "/AppIconForeground.png";
        const string IconBackgroundPath = BrandFolder + "/AppIconBackground.png";
        const string SplashLogoPath = BrandFolder + "/SplashLogo.png";

        const int IconSize = 1024;
        const int SplashLogoSize = 512;

        /// <summary>Unity refuses shorter logo durations, so this is as brief as the engine splash gets.</summary>
        const float SplashLogoDuration = 2f;

        [MenuItem("HyperSmash/Regenerate Brand Assets")]
        public static void Regenerate()
        {
            GenerateTextures();
            Apply();
            Debug.Log("[BrandAssets] Icons and splash regenerated.");
        }

        /// <summary>Batch-mode entry point so CI can refresh the art without opening the editor.</summary>
        public static void RegenerateBatch()
        {
            Regenerate();
            EditorApplication.Exit(0);
        }

        /// <summary>Called from the build configuration so every player ships current branding.</summary>
        public static void EnsureAndApply()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath) == null ||
                AssetDatabase.LoadAssetAtPath<Sprite>(SplashLogoPath) == null)
            {
                GenerateTextures();
            }

            Apply();
        }

        static void GenerateTextures()
        {
            Directory.CreateDirectory(BrandFolder);

            WritePng(IconPath, BrandMark.Render(IconSize, drawBackground: true, drawMark: true, markScale: 1f));
            WritePng(IconBackgroundPath, BrandMark.Render(IconSize, drawBackground: true, drawMark: false, markScale: 1f));
            WritePng(IconForegroundPath, BrandMark.Render(IconSize, drawBackground: false, drawMark: true, markScale: BrandMark.AdaptiveSafeScale));
            WritePng(SplashLogoPath, BrandMark.Render(SplashLogoSize, drawBackground: false, drawMark: true, markScale: 1f));

            AssetDatabase.Refresh();

            ConfigureImporter(IconPath, TextureImporterType.Default);
            ConfigureImporter(IconBackgroundPath, TextureImporterType.Default);
            ConfigureImporter(IconForegroundPath, TextureImporterType.Default);
            ConfigureImporter(SplashLogoPath, TextureImporterType.Sprite);
        }

        static void WritePng(string assetPath, Texture2D texture)
        {
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void ConfigureImporter(string assetPath, TextureImporterType type)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                Debug.LogWarning($"[BrandAssets] No texture importer for {assetPath}.");
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            if (type == TextureImporterType.Sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.SaveAndReimport();
        }

        static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(IconForegroundPath);
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(IconBackgroundPath);
            var splashLogo = AssetDatabase.LoadAssetAtPath<Sprite>(SplashLogoPath);

            if (icon == null || foreground == null || background == null)
            {
                Debug.LogWarning("[BrandAssets] Icon textures missing; skipping icon assignment.");
            }
            else
            {
                AssignIcons(NamedBuildTarget.Android, icon, foreground, background);
                AssignIcons(NamedBuildTarget.iOS, icon, foreground, background);
                AssignIcons(NamedBuildTarget.Standalone, icon, foreground, background);
            }

            ApplySplash(splashLogo);
            AssetDatabase.SaveAssets();
        }

        static void AssignIcons(NamedBuildTarget target, Texture2D icon, Texture2D foreground, Texture2D background)
        {
            PlatformIconKind[] kinds;
            try
            {
                kinds = PlayerSettings.GetSupportedIconKinds(target);
            }
            catch (Exception e)
            {
                // Happens when a platform module is not installed on this machine.
                Debug.LogWarning($"[BrandAssets] {target.TargetName} icons skipped: {e.Message}");
                return;
            }

            foreach (var kind in kinds)
            {
                var slots = PlayerSettings.GetPlatformIcons(target, kind);
                if (slots == null || slots.Length == 0)
                {
                    continue;
                }

                // Adaptive icons take a background plus a foreground the launcher can mask and animate.
                var adaptive = kind.ToString().StartsWith("Adaptive", StringComparison.OrdinalIgnoreCase);
                foreach (var slot in slots)
                {
                    if (adaptive && slot.maxLayerCount >= 2)
                    {
                        slot.SetTextures(background, foreground);
                    }
                    else
                    {
                        slot.SetTextures(icon);
                    }
                }

                PlayerSettings.SetPlatformIcons(target, kind, slots);
            }

            // The legacy list wants one texture per expected size; Unity downscales from the source.
            var sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes != null && sizes.Length > 0)
            {
                var legacy = new Texture2D[sizes.Length];
                for (var i = 0; i < legacy.Length; i++)
                {
                    legacy[i] = icon;
                }

                PlayerSettings.SetIcons(target, legacy, IconKind.Application);
            }
        }

        static void ApplySplash(Sprite logo)
        {
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Palette.BackdropTop;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.backgroundPortrait = null;

            if (logo == null)
            {
                Debug.LogWarning("[BrandAssets] Splash logo sprite missing; engine splash left without a logo.");
                PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();
                return;
            }

            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(SplashLogoDuration, logo),
            };
        }
    }
}
