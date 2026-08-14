namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// First batch of hand-authored structures. Each one should fail differently:
    /// dense stacks reward raw power, supported shapes reward hitting the pillar.
    /// </summary>
    /// <remarks>
    /// Glyphs: '#' block, 'G' brittle, '!' explosive, 'X' heavy, 'O' ball,
    /// '|' pillar, '=' beam, '.' empty.
    /// Rows are written top-first. Every non-beam cell needs something directly beneath it,
    /// because each cell is its own rigid body; only beams span a gap.
    /// Target scores assume roughly two hits per shot: dense boards chain more, so they ask more.
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

            // Wide base, narrow top. Low shots scatter the base and drop everything.
            new LevelLayout(
                "PYRAMID", 4, 70,
                "..O..",
                ".###.",
                "#####"),

            // Everything rides one central leg: the classic one-shot topple.
            new LevelLayout(
                "TABLE", 4, 50,
                ".OOO.",
                ".===.",
                "..|..",
                "..#.."),

            // Dense shell around a heavy core that resists being nudged.
            new LevelLayout(
                "VAULT", 6, 120,
                ".OOO.",
                ".###.",
                ".#X#.",
                ".###."),

            // Alternating decks: collapsing the lower legs cascades the whole stack.
            new LevelLayout(
                "SHELVES", 5, 80,
                ".OOO.",
                ".===.",
                ".|.|.",
                ".===.",
                ".|.|."),

            // Widest board; edge columns sit near the shelf lip and clear easily.
            new LevelLayout(
                "WALL", 6, 150,
                "OO.OO",
                "#####",
                "#####",
                "#####"),
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
