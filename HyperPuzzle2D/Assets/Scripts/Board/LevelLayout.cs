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

        public string Name { get; }

        /// <summary>Shots granted for this structure; the primary difficulty dial.</summary>
        public int Ammo { get; }

        public IReadOnlyList<string> Rows { get; }
        public int Width { get; }
        public int Height => Rows.Count;

        public LevelLayout(string name, int ammo, params string[] rows)
        {
            Name = name;
            Ammo = ammo;
            Rows = rows;

            var width = 0;
            foreach (var row in rows)
            {
                width = System.Math.Max(width, row.Length);
            }

            Width = width;
        }

        /// <summary>Piece at a grid cell, where row 0 is the course that rests on the shelf.</summary>
        public PieceKind PieceAt(int column, int rowFromBottom)
        {
            var row = Rows[Height - 1 - rowFromBottom];
            return column < row.Length ? FromGlyph(row[column]) : PieceKind.None;
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
                default: return PieceKind.None;
            }
        }
    }
}
