namespace HyperPuzzle2D.Board
{
    /// <summary>
    /// First batch of hand-authored structures. Each one should fail differently:
    /// dense stacks reward raw power, supported shapes reward hitting the pillar.
    /// </summary>
    /// <remarks>
    /// Glyphs: '#' block, 'X' heavy, 'O' ball, '|' pillar, '=' beam, '.' empty.
    /// Rows are written top-first. Every non-beam cell needs something directly beneath it,
    /// because each cell is its own rigid body; only beams span a gap.
    /// </remarks>
    public static class LevelLibrary
    {
        public static readonly LevelLayout[] All =
        {
            // Baseline slab. Forgiving, teaches "push things off the edge".
            new LevelLayout(
                "TOWER", 5,
                ".OOO.",
                ".###.",
                ".###.",
                ".###.",
                ".###."),

            // Arch on two legs: take out a pillar and the roof follows.
            new LevelLayout(
                "GATE", 5,
                "..O..",
                ".===.",
                ".|.|.",
                ".|.|."),

            // Split targets, both perched near the shelf lip.
            new LevelLayout(
                "TWINS", 5,
                "O...O",
                "#...#",
                "#...#",
                "#...#",
                "#...#"),

            // Wide base, narrow top. Low shots scatter the base and drop everything.
            new LevelLayout(
                "PYRAMID", 4,
                "..O..",
                ".###.",
                "#####"),

            // Everything rides one central leg: the classic one-shot topple.
            new LevelLayout(
                "TABLE", 4,
                ".OOO.",
                ".===.",
                "..|..",
                "..#.."),

            // Dense shell around a heavy core that resists being nudged.
            new LevelLayout(
                "VAULT", 6,
                ".OOO.",
                ".###.",
                ".#X#.",
                ".###."),

            // Alternating decks: collapsing the lower legs cascades the whole stack.
            new LevelLayout(
                "SHELVES", 5,
                ".OOO.",
                ".===.",
                ".|.|.",
                ".===.",
                ".|.|."),

            // Widest board; edge columns sit near the shelf lip and clear easily.
            new LevelLayout(
                "WALL", 6,
                "OO.OO",
                "#####",
                "#####",
                "#####"),
        };

        /// <summary>Deterministic pick so a Daily seed always rebuilds the same structure.</summary>
        public static LevelLayout Pick(int seed)
        {
            return All[(int)((uint)seed % (uint)All.Length)];
        }
    }
}
