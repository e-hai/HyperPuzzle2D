using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Single source of colour truth so world and UI stay visually consistent.
    /// Theme: a dusk demolition site. The base stays dark so the existing light HUD text and
    /// translucent panels keep their contrast, but the hue leaves the generic violet template
    /// behind for steel, concrete, safety orange and hazard yellow.
    /// </summary>
    public static class Palette
    {
        // Sky over the site: near-black steel at the top warming to a dust-lit horizon, with an
        // amber worklight pooled behind the objective instead of a purple bloom.
        public static readonly Color BackdropTop = Hex("12161C");
        public static readonly Color BackdropBottom = Hex("2C2620");
        public static readonly Color BackdropGlow = Hex("E8A23D");

        // Poured concrete and steel. The edge is safety yellow, which the hazard trim builds on.
        public static readonly Color Ground = Hex("3A3E45");
        public static readonly Color GroundEdge = Hex("F2C230");
        public static readonly Color Wall = Hex("24272E");
        public static readonly Color Shelf = Hex("42464E");

        public static readonly Color CannonBase = Hex("31353D");
        public static readonly Color CannonBarrel = Hex("F5761F");
        public static readonly Color CannonHub = Hex("44392E");
        public static readonly Color Projectile = Hex("FFE0AE");

        public static readonly Color Accent = Hex("FF7A1A");
        public static readonly Color AccentCool = Hex("3FD07C");

        public static readonly Color TextPrimary = Hex("F2F0EA");
        public static readonly Color TextMuted = Hex("9AA1AB");
        public static readonly Color TextOnAccent = Hex("241000");

        public static readonly Color CardFill = Hex("232830");
        public static readonly Color HudFill = new Color(0.06f, 0.07f, 0.09f, 0.62f);

        /// <summary>
        /// In-game HUD chips sit straight on the play field with no scrim behind them, so they
        /// need far more body than <see cref="HudFill"/>: at dialog opacity the drifting backdrop
        /// motes show through the panels and the readouts stop reading as a layer of their own.
        /// </summary>
        public static readonly Color HudPanel = new Color(0.14f, 0.16f, 0.19f, 1f);
        public static readonly Color Scrim = new Color(0.02f, 0.03f, 0.04f, 0.80f);

        /// <summary>Painted crates and containers, ordered warm-to-cool so a stack reads as layers.</summary>
        public static readonly Color[] Blocks =
        {
            Hex("F4C020"),
            Hex("EE7A2E"),
            Hex("D6472C"),
            Hex("3E8FB8"),
            Hex("47AE86"),
        };

        /// <summary>Safety-green drum: a distinct collectible against the crate ramp.</summary>
        public static readonly Color Can = Hex("2FBF71");

        /// <summary>Timber shoring reads as the weak point of a structure.</summary>
        public static readonly Color Pillar = Hex("C08552");

        /// <summary>Same timber family as the shoring, a shade lighter to read as the spanning beam.</summary>
        public static readonly Color Beam = Hex("D9A066");

        /// <summary>Poured-concrete mass that resists being nudged.</summary>
        public static readonly Color Heavy = Hex("8A8F98");
        public static readonly Color Brittle = Hex("77E8FF");
        public static readonly Color Explosive = Hex("FF5D3D");
        public static readonly Color ExplosionCore = Hex("FFE66D");

        /// <summary>Hazard tape: the diagonal warning stripes baked onto platform edges.</summary>
        public static readonly Color HazardStripe = Hex("F2C230");
        public static readonly Color HazardStripeAlt = Hex("1A1A1A");

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
