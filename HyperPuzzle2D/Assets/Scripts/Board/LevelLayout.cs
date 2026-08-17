using System.Collections.Generic;

namespace HyperPuzzle2D.Board
{
    public enum PieceKind
    {
        None,
        Block,
        Heavy,
        Ball,
        Pillar,
        Brittle,
        Explosive,

        /// <summary>
        /// Spans horizontally: a run of adjacent beam cells becomes one rigid body, so it can
        /// bridge a gap between supports instead of falling through it.
        /// </summary>
        Beam,
    }

    /// <summary>
    /// A hand-authored structure, described as an ASCII grid so layouts stay readable in source.
    /// Rows are written top-first (the way they look on screen); lookups are bottom-first
    /// (the way they are built and stacked).
    /// </summary>
    public sealed class LevelLayout
    {
        public const char EmptyGlyph = '.';
        public const char BlockGlyph = '#';
        public const char HeavyGlyph = 'X';
        public const char BallGlyph = 'O';
        public const char PillarGlyph = '|';
        public const char BeamGlyph = '=';
        public const char BrittleGlyph = 'G';
        public const char ExplosiveGlyph = '!';

        public const char BallShotGlyph = 'B';
        public const char ClusterShotGlyph = 'C';
        public const char ChargeShotGlyph = 'D';

        public string Name { get; }

        /// <summary>
        /// The shots granted for this structure, in the order they are fired, written as one
        /// glyph each: B ball, C cluster, D demolition charge. Its length is the ammo count, so
        /// the two can never disagree.
        /// </summary>
        public string Loadout { get; }

        /// <summary>Shots granted for this structure; the primary difficulty dial.</summary>
        public int Ammo => Loadout.Length;

        /// <summary>
        /// Points needed to pass. Reaching it clears the stage even with pieces left standing,
        /// which keeps a run winnable when the last few targets are wedged out of reach.
        /// </summary>
        public int TargetScore { get; }

        /// <summary>
        /// Score bars for the second and third star. A run ends the moment it passes
        /// <see cref="TargetScore"/>, so these are not "keep playing to earn more": they measure
        /// how much of the board came down in the chain that finished it. Chipping a stage apart
        /// passes with one star, and taking it down in one collapse is what pays three.
        /// </summary>
        public int TwoStarScore { get; }
        public int ThreeStarScore { get; }

        public IReadOnlyList<string> Rows { get; }
        public int Width { get; }
        public int Height => Rows.Count;

        public LevelLayout(string name, string loadout, int targetScore, int twoStarScore, int threeStarScore, params string[] rows)
        {
            Name = name;
            Loadout = string.IsNullOrEmpty(loadout) ? "B" : loadout;
            TargetScore = targetScore;
            TwoStarScore = twoStarScore;
            ThreeStarScore = threeStarScore;
            Rows = rows;

            var width = 0;
            foreach (var row in rows)
            {
                width = System.Math.Max(width, row.Length);
            }

            Width = width;
        }

        /// <summary>Stars a finished run earned, from zero (failed) to three.</summary>
        public int StarsFor(int score)
        {
            if (score < TargetScore)
            {
                return 0;
            }

            if (score >= ThreeStarScore) return 3;
            if (score >= TwoStarScore) return 2;
            return 1;
        }

        /// <summary>Piece at a grid cell, where row 0 is the course that rests on the shelf.</summary>
        public PieceKind PieceAt(int column, int rowFromBottom)
        {
            var row = Rows[Height - 1 - rowFromBottom];
            return column < row.Length ? FromGlyph(row[column]) : PieceKind.None;
        }

        /// <summary>The shot fired at <paramref name="shotIndex"/>, counting from the first.</summary>
        public ProjectileKind ShotAt(int shotIndex)
        {
            if (shotIndex < 0 || shotIndex >= Loadout.Length)
            {
                return ProjectileKind.Ball;
            }

            switch (Loadout[shotIndex])
            {
                case ClusterShotGlyph: return ProjectileKind.Cluster;
                case ChargeShotGlyph: return ProjectileKind.Charge;
                default: return ProjectileKind.Ball;
            }
        }

        public static PieceKind FromGlyph(char glyph)
        {
            switch (glyph)
            {
                case BlockGlyph: return PieceKind.Block;
                case HeavyGlyph: return PieceKind.Heavy;
                case BallGlyph: return PieceKind.Ball;
                case PillarGlyph: return PieceKind.Pillar;
                case BeamGlyph: return PieceKind.Beam;
                case BrittleGlyph: return PieceKind.Brittle;
                case ExplosiveGlyph: return PieceKind.Explosive;
                default: return PieceKind.None;
            }
        }
    }
}
