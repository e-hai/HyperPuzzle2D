using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Single source of colour truth so world and UI stay visually consistent.
    /// </summary>
    public static class Palette
    {
        public static readonly Color BackdropTop = Hex("120C33");
        public static readonly Color BackdropBottom = Hex("3D2168");
        public static readonly Color BackdropGlow = Hex("7B4BD8");

        public static readonly Color Ground = Hex("1E1745");
        public static readonly Color GroundEdge = Hex("46E0C8");
        public static readonly Color Wall = Hex("191238");
        public static readonly Color Shelf = Hex("2B2159");

        public static readonly Color CannonBase = Hex("2B2159");
        public static readonly Color CannonBarrel = Hex("FF8A3D");
        public static readonly Color CannonHub = Hex("3C2E75");
        public static readonly Color Projectile = Hex("FFE7C2");

        public static readonly Color Accent = Hex("FF8A3D");
        public static readonly Color AccentCool = Hex("46E0C8");

        public static readonly Color TextPrimary = Hex("F5F1FF");
        public static readonly Color TextMuted = Hex("A294D6");
        public static readonly Color TextOnAccent = Hex("2A1400");

        public static readonly Color CardFill = Hex("241B4F");
        public static readonly Color HudFill = new Color(0.07f, 0.05f, 0.18f, 0.62f);

        /// <summary>
        /// In-game HUD chips sit straight on the play field with no scrim behind them, so they
        /// need far more body than <see cref="HudFill"/>: at dialog opacity the drifting backdrop
        /// motes show through the panels and the readouts stop reading as a layer of their own.
        /// </summary>
        public static readonly Color HudPanel = new Color(0.16f, 0.13f, 0.35f, 1f);
        public static readonly Color Scrim = new Color(0.04f, 0.02f, 0.11f, 0.78f);

        /// <summary>Block tints, ordered so a structure reads as a warm-to-cool stack.</summary>
        public static readonly Color[] Blocks =
        {
            Hex("FFD166"),
            Hex("F9A03F"),
            Hex("EF476F"),
            Hex("C77DFF"),
            Hex("4CC9F0"),
        };

        public static readonly Color Can = Hex("06D6A0");

        /// <summary>Timber-toned supports read as the weak point of a structure.</summary>
        public static readonly Color Pillar = Hex("C08552");

        /// <summary>Same timber family as pillars, a shade lighter to read as the spanning member.</summary>
        public static readonly Color Beam = Hex("D9A066");

        /// <summary>Stone-toned mass that resists being nudged.</summary>
        public static readonly Color Heavy = Hex("7C89AD");
        public static readonly Color Brittle = Hex("77E8FF");
        public static readonly Color Explosive = Hex("FF5D3D");
        public static readonly Color ExplosionCore = Hex("FFE66D");

        /// <summary>Combo tiers: higher combos read hotter.</summary>
        public static Color ComboTint(int combo)
        {
            if (combo >= 6) return Hex("FF4D6D");
            if (combo >= 4) return Hex("FF8A3D");
            return Hex("FFD166");
        }

        static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.magenta;
        }
    }
}
