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
    /// Target scores sit near what a single decent shot already pays on the teaching stages
    /// (1–3). From TABLE onward the board must be wiped: clear-all is the grade, and wiping it
    /// pays three stars. The two star bars on score-goal stages come from tools/star_bars.py.
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

            // Wide brittle base with the core one row up: a low shot sweeps the whole footing out.
            // Clear-all from here on: the teaching score goal has done its job.
            new LevelLayout(
                "PYRAMID", "CBB", 0, 490, 870,
                "..O..",
                ".G!G.",
                "GGGGG"),

            // Everything rides one leg, and that leg is the core: the classic one-shot topple.
            new LevelLayout(
                "TABLE", "BDB", 0, 400, 730,
                ".OOO.",
                ".===.",
                "..!..",
                ".GGG."),

            // Brittle shell around a buried core. The charge leads because planting it on the
            // shell reaches the core without cracking in.
            new LevelLayout(
                "VAULT", "DBC", 0, 860, 1550,
                ".OXO.",
                ".GGG.",
                ".G!G.",
                ".GGG."),

            // Two decks on explosive feet: taking either foot cascades both floors.
            new LevelLayout(
                "SHELVES", "CDB", 0, 530, 950,
                ".OOO.",
                ".===.",
                ".|.|.",
                ".===.",
                ".!.!."),

            // Widest board and the finale: twin cores inside a brittle wall.
            new LevelLayout(
                "WALL", "DCBD", 0, 2000, 3610,
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
