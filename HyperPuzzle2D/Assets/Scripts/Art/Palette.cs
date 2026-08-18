using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Single source of colour truth so world and UI stay visually consistent.
    /// Theme: sunlit washi paper craft — warm cream base, sumi ink text, vermilion / kincha / indigo accents.
    /// </summary>
    public static class Palette
    {
        public static readonly Color BackdropTop = Hex("EFE2CB");
        public static readonly Color BackdropBottom = Hex("DCC7A6");
        public static readonly Color BackdropGlow = Hex("FFE0AC");

        public static readonly Color CannonBase = Hex("5C4633");
        public static readonly Color CannonBarrel = Hex("B5842F");

        public static readonly Color Accent = Hex("C0392B");
        public static readonly Color AccentCool = Hex("3E7A6B");

        public static readonly Color TextPrimary = Hex("2E2318");
        public static readonly Color TextMuted = Hex("7C6B57");
        public static readonly Color TextOnAccent = Hex("FFF7EA");

        public static readonly Color CardFill = Hex("FCF6EA");
        public static readonly Color HudFill = new Color(1f, 0.97f, 0.90f, 0.72f);
        public static readonly Color HudPanel = Hex("FDF8EE");
        public static readonly Color Scrim = new Color(0.16f, 0.12f, 0.08f, 0.55f);
        public static readonly Color Shadow = new Color(0.30f, 0.22f, 0.14f, 0.35f);

        /// <summary>Kept for the procedural launcher mark (stacked sheets + vermilion ball).</summary>
        public static readonly Color[] Blocks =
        {
            Hex("C98A21"),
            Hex("C75B27"),
            Hex("B93A2E"),
            Hex("2E5A82"),
            Hex("4A7C4E"),
        };

        public static readonly Color Brittle = Hex("3E96AC");
        public static readonly Color ExplosionCore = Hex("D4881A");
        public static readonly Color StarGold = Hex("D4A017");

        static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.magenta;
        }
    }
}
