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
    /// on a board the player has visibly wrecked.
    /// </remarks>
    public static class LevelLibrary
    {
        public static readonly LevelLayout[] All =
        {
            // Slice 1: large brittle face guarantees visible destruction on the opening shot.
            new LevelLayout(
                "TOWER", 3, 100,
                ".GGG.",
                ".GGG.",
                ".GGG.",
                ".GGG."),

            // Slice 2: the warm core teaches explosive weak points and a roof cascade.
            new LevelLayout(
                "GATE", 3, 120,
                "..O..",
                ".===.",
                ".!||.",
                ".#||."),

            // Slice 3: choose an entry angle that chains both separated explosive cores.
            new LevelLayout(
                "TWINS", 3, 180,
                ".O.O.",
                ".G.G.",
                ".!.!.",
                ".#.#."),

            // Wide brittle base with the core one row up: a low shot sweeps the whole footing out.
            new LevelLayout(
                "PYRAMID", 3, 150,
                "..O..",
                ".G!G.",
                "GGGGG"),

            // Everything rides one leg, and that leg is the core: the classic one-shot topple.
            // The brittle plinth widens the miss window, so a low shot still shatters into the leg
            // instead of sailing under a board that is only one cell wide where it matters.
            new LevelLayout(
                "TABLE", 3, 120,
                ".OOO.",
                ".===.",
                "..!..",
                ".GGG."),

            // Brittle shell around a buried core. Any honest hit cracks through and sets it off,
            // and the heavy on top is a trophy to drop rather than armour to grind down.
            new LevelLayout(
                "VAULT", 3, 180,
                ".OXO.",
                ".GGG.",
                ".G!G.",
                ".GGG."),

            // Two decks on explosive feet: taking either foot cascades both floors.
            new LevelLayout(
                "SHELVES", 3, 150,
                ".OOO.",
                ".===.",
                ".|.|.",
                ".===.",
                ".!.!."),

            // Widest board and the finale: twin cores inside a brittle wall, so a centred shot
            // can take the entire face down at once.
            new LevelLayout(
                "WALL", 4, 220,
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
