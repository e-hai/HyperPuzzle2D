using UnityEngine;

namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Single source of colour truth so world and UI stay visually consistent.
    /// Theme: sunlit washi paper craft. The base is a warm off-white, so this palette inverts the
    /// old dusk-site one: text is sumi ink rather than paper white, and anything that used to be a
    /// bright glow has to become a deep saturated pigment or it vanishes against the paper.
    /// Accents come from a traditional pigment set: vermilion, kincha gold, indigo, matcha.
    /// </summary>
    public static class Palette
    {
        // Daylight through shoji screens. The top is the lit paper itself, warming down into the
        // tatami and timber of the floor, with a pool of sunlight behind the objective.
        public static readonly Color BackdropTop = Hex("EFE2CB");
        public static readonly Color BackdropBottom = Hex("DCC7A6");
        public static readonly Color BackdropGlow = Hex("FFE0AC");

        // Scenery behind the action, in three depths. These are ink washes on paper, so they get
        // paler and lower contrast the further back they sit rather than darker.
        public static readonly Color SkylineFar = Hex("DCCEB6");
        public static readonly Color SkylineNear = Hex("CBB99B");
        public static readonly Color Rigging = Hex("A89272");

        /// <summary>Stub under the target pad: the same timber as the deck, dropped into shadow.</summary>
        public static readonly Color PadStub = Hex("B39A78");

        // Tatami and timber. The deck edge is vermilion, which the trim stripes build on.
        public static readonly Color Ground = Hex("C9AE86");
        public static readonly Color GroundEdge = Hex("C0392B");
        public static readonly Color Wall = Hex("BCA684");
        public static readonly Color Shelf = Hex("C6AE8A");

        public static readonly Color CannonBase = Hex("5C4633");
        public static readonly Color CannonBarrel = Hex("B5842F");
        public static readonly Color CannonHub = Hex("3E2F22");

        /// <summary>
        /// A lacquered temari ball. Deliberately the darkest saturated colour in the world layer:
        /// on paper a pale projectile would disappear the moment it left the muzzle.
        /// </summary>
        public static readonly Color Projectile = Hex("C0392B");

        public static readonly Color Accent = Hex("C0392B");
        public static readonly Color AccentCool = Hex("3E7A6B");

        public static readonly Color TextPrimary = Hex("2E2318");
        public static readonly Color TextMuted = Hex("7C6B57");
        public static readonly Color TextOnAccent = Hex("FFF7EA");

        /// <summary>
        /// Fresh rice paper, kept brighter than <see cref="BackdropTop"/> so cards laid straight on
        /// the home backdrop still read as a separate sheet. The separation is carried by
        /// <see cref="Shadow"/> as much as by the tone difference.
        /// </summary>
        public static readonly Color CardFill = Hex("FCF6EA");
        public static readonly Color HudFill = new Color(1f, 0.97f, 0.90f, 0.72f);

        /// <summary>
        /// In-game HUD chips sit straight on the play field with no scrim behind them, so they need
        /// full opacity and the brightest paper tone available to separate from the backdrop.
        /// </summary>
        public static readonly Color HudPanel = Hex("FDF8EE");

        /// <summary>
        /// Warm brown-black rather than neutral. A dialog still needs the field behind it pushed
        /// back, and a neutral scrim over this much warm cream reads as dirty grey.
        /// </summary>
        public static readonly Color Scrim = new Color(0.16f, 0.12f, 0.08f, 0.55f);

        /// <summary>
        /// Drop shadow under paper. Warm and soft: pure black under cream panels muddies the tone
        /// and makes flat UI look like it was cut out of a darker image.
        /// </summary>
        public static readonly Color Shadow = new Color(0.30f, 0.22f, 0.14f, 0.35f);

        /// <summary>
        /// Painted paper boxes, ordered warm-to-cool so a stack reads as layers. Every entry is a
        /// deep pigment: the old poster-bright set washed out against the paper backdrop.
        /// </summary>
        public static readonly Color[] Blocks =
        {
            Hex("C98A21"),
            Hex("C75B27"),
            Hex("B93A2E"),
            Hex("2E5A82"),
            Hex("4A7C4E"),
        };

        /// <summary>Matcha drum: a distinct collectible against the warm crate ramp.</summary>
        public static readonly Color Can = Hex("3E7A6B");

        /// <summary>Timber shoring reads as the weak point of a structure.</summary>
        public static readonly Color Pillar = Hex("9C6B42");

        /// <summary>Same timber family as the shoring, a shade lighter to read as the spanning beam.</summary>
        public static readonly Color Beam = Hex("B08050");

        /// <summary>Stone mass that resists being nudged.</summary>
        public static readonly Color Heavy = Hex("8C8378");
        public static readonly Color Brittle = Hex("3E96AC");
        public static readonly Color Explosive = Hex("D6402E");

        /// <summary>
        /// Explosion core, also used for the score popups. Kincha gold rather than the old pale
        /// yellow: this colour has to stay legible as small text and thin particles on paper.
        /// </summary>
        public static readonly Color ExplosionCore = Hex("D4881A");

        /// <summary>Kohaku curtain: the red-and-white banding baked onto platform edges.</summary>
        public static readonly Color HazardStripe = Hex("C0392B");
        public static readonly Color HazardStripeAlt = Hex("F5EBD8");

        /// <summary>
        /// Kincha gold for earned stars. Separated from <see cref="GroundEdge"/> so the platform
        /// trim can stay vermilion without turning every rating into a red hazard mark.
        /// </summary>
        public static readonly Color StarGold = Hex("D4A017");

        /// <summary>Combo tiers: higher combos read hotter, all kept dark enough to sit on paper.</summary>
        public static Color ComboTint(int combo)
        {
            if (combo >= 6) return Hex("B5122E");
            if (combo >= 4) return Hex("C2571F");
            return Hex("B07D14");
        }

        static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.magenta;
        }
    }
}
