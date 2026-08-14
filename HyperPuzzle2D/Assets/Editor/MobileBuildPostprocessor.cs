using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace HyperPuzzle2D.Editor
{
    /// <summary>Adds store/runtime metadata that Unity does not expose in PlayerSettings.</summary>
    public static class MobileBuildPostprocessor
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;
            root.SetString("CFBundleDevelopmentRegion", "en");
            root.SetString("CFBundleDisplayName", "Hyper Smash");
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            var localizations = root.CreateArray("CFBundleLocalizations");
            localizations.AddString("en");
            localizations.AddString("zh-Hans");

            plist.WriteToFile(plistPath);
        }
    }
}
