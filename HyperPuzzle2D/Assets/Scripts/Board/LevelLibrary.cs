namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// The hand-authored campaign. Every stage carries a readable weak point and enough brittle
    /// mass to collapse within one or two shots: the draw here is watching a structure come apart,
    /// so a board that survives an honest hit costs more than an easy stage ever would.
    /// Variety comes from where the weak point sits, not from how much punishment a board absorbs.
    /// </summary>
    /// <remarks>
    /// Glyphs: '#' block, 'G' brittle, '!' explosive, 'X' heavy, 'O' ball,
    /// '|' pillar, '=' beam, '.' empty.
    /// Rows are written top-first. Every non-beam cell needs something directly beneath it,
    /// because each cell is its own rigid body; only beams span a gap.
    /// Target scores sit near what a single decent shot already pays, so progress never stalls
    /// on a board the player has visibly wrecked. The two star bars above it carry the difficulty
    /// instead: passing stays easy, and only a chain that takes most of the board at once pays
    /// three stars. Those two bars come from tools/star_bars.py, which derives them from the
    /// board's own one-shot ceiling: chain scoring grows quadratically with piece count, so bars
    /// picked by eye are wildly inconsistent between a 8 piece board and a 19 piece one.
    /// Loadout glyphs: 'B' ball, 'C' cluster, 'D' demolition charge. The first two stages are
    /// plain balls so aiming is learned on its own, then one new shot type arrives at a time on a
    /// board that rewards it.
    /// </remarks>
    public static class LevelLibrary
    {
        public static readonly LevelLayout[] All =
        {
            // Slice 1: large brittle face guarantees visible destruction on the opening shot.
            new LevelLayout(
                "TOWER", "BBB", 100, 800, 1430,
                ".GGG.",
                ".GGG.",
                ".GGG.",
                ".GGG."),

            // Slice 2: the warm core teaches explosive weak points and a roof cascade.
            new LevelLayout(
                "GATE", "BBB", 120, 390, 710,
                "..O..",
                ".===.",
                ".!||.",
                ".#||."),

            // Slice 3: two separated cores, and the cluster that can reach both at once.
            new LevelLayout(
                "TWINS", "BCB", 180, 390, 710,
                ".O.O.",
                ".G.G.",
                ".!.!.",
                ".#.#."),

            // Wide brittle base with the core one row up: a low shot sweeps the whole footing out,
            // and splitting the cluster early covers the base end to end.
            new LevelLayout(
                "PYRAMID", "CBB", 150, 490, 870,
                "..O..",
                ".G!G.",
                "GGGGG"),

            // Everything rides one leg, and that leg is the core: the classic one-shot topple.
            // The brittle plinth widens the miss window, so a low shot still shatters into the leg
            // instead of sailing under a board that is only one cell wide where it matters.
            new LevelLayout(
                "TABLE", "BDB", 120, 400, 730,
                ".OOO.",
                ".===.",
                "..!..",
                ".GGG."),

            // Brittle shell around a buried core. Any honest hit cracks through and sets it off,
            // and the heavy on top is a trophy to drop rather than armour to grind down. The
            // charge leads because planting it on the shell reaches the core without cracking in.
            new LevelLayout(
                "VAULT", "DBC", 180, 860, 1550,
                ".OXO.",
                ".GGG.",
                ".G!G.",
                ".GGG."),

            // Two decks on explosive feet: taking either foot cascades both floors.
            new LevelLayout(
                "SHELVES", "CDB", 150, 530, 950,
                ".OOO.",
                ".===.",
                ".|.|.",
                ".===.",
                ".!.!."),

            // Widest board and the finale: twin cores inside a brittle wall, so a centred shot
            // can take the entire face down at once.
            new LevelLayout(
                "WALL", "DCBD", 220, 2000, 3610,
                "OO.OO",
                "GGGGG",
                "G!G!G",
                "GGGGG"),
        };

        public static int Count => All.Length;

        /// <summary>0-based stage lookup for the ordered campaign path.</summary>
        public static LevelLayout Get(int index)
        {
            if (index < 0 || index >= All.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index), index, "Stage index out of range.");
            }

            return All[index];
        }

        /// <summary>Deterministic pick so a Daily seed always rebuilds the same structure.</summary>
        public static LevelLayout Pick(int seed)
        {
            return All[(int)((uint)seed % (uint)All.Length)];
        }
    }
}
