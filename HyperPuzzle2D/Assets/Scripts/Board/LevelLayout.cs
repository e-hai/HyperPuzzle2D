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
        /// Points needed to pass. Zero means the stage is a clear-all board: every piece must leave
        /// play, and wiping it awards three stars. Non-zero keeps the early teaching path where a
        /// decent chain can pass without grinding the last wedged block.
        /// </summary>
        public int TargetScore { get; }

        /// <summary>True when the stage only ends after every piece is gone.</summary>
        public bool RequiresClearAll => TargetScore <= 0;

        /// <summary>
        /// Score bars for the second and third star on score-goal stages. Clear-all boards skip
        /// these and pay three stars for the wipe itself.
        /// </summary>
        public int TwoStarScore { get; }
        public int ThreeStarScore { get; }

        public IReadOnlyList<string> Rows { get; }
        public int Width { get; }
        public int Height => Rows.Count;

        /// <summary>
        /// Columns that actually carry a piece. Most layouts are written on a five wide grid but
        /// only fill the middle three, so <see cref="Width"/> overstates the footprint; the pad the
        /// structure stands on is sized from this instead, and the stack is centred on it.
        /// </summary>
        public int FirstColumn { get; }
        public int LastColumn { get; }
        public int FootprintWidth => LastColumn - FirstColumn + 1;

        /// <summary>Grid column the footprint is centred on; fractional when the span is even.</summary>
        public float CenterColumn => (FirstColumn + LastColumn) * 0.5f;

        /// <summary>Localization key for the one-line weak-point tip shown on the briefing.</summary>
        public string HintKey => "hint." + Name;

        public LevelLayout(string name, string loadout, int targetScore, int twoStarScore, int threeStarScore, params string[] rows)
        {
            Name = name;
            Loadout = string.IsNullOrEmpty(loadout) ? "B" : loadout;
            TargetScore = targetScore;
            TwoStarScore = twoStarScore;
            ThreeStarScore = threeStarScore;
            Rows = rows;

            var width = 0;
            var first = int.MaxValue;
            var last = -1;
            foreach (var row in rows)
            {
                width = System.Math.Max(width, row.Length);
                for (var column = 0; column < row.Length; column++)
                {
                    if (FromGlyph(row[column]) == PieceKind.None)
                    {
                        continue;
                    }

                    first = System.Math.Min(first, column);
                    last = System.Math.Max(last, column);
                }
            }

            Width = width;
            FirstColumn = last < 0 ? 0 : first;
            LastColumn = last < 0 ? System.Math.Max(0, width - 1) : last;
        }

        /// <summary>Stars a finished run earned, from zero (failed) to three.</summary>
        public int StarsFor(int score, bool boardCleared = false)
        {
            // Clear-all stages: the wipe is the grade. Score bars do not apply.
            if (RequiresClearAll)
            {
                return boardCleared ? 3 : 0;
            }

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
        public ProjectileKind ShotAt(int shotIndex) => ShotAt(Loadout, shotIndex);

        /// <summary>Resolves a shot glyph from any loadout string, including a player-reordered one.</summary>
        public static ProjectileKind ShotAt(string loadout, int shotIndex)
        {
            if (string.IsNullOrEmpty(loadout) || shotIndex < 0 || shotIndex >= loadout.Length)
            {
                return ProjectileKind.Ball;
            }

            switch (loadout[shotIndex])
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
