using System;
using System.Collections.Generic;

namespace Match3Lab.Core
{
    /// <summary>A fixed-size grid of <see cref="Cell"/>s, stored row-major (index = y * Width + x).</summary>
    public sealed class Board
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Cell[] _cells;

        public Board(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _cells = new Cell[width * height];
        }

        private Board(int width, int height, Cell[] cells)
        {
            Width = width;
            Height = height;
            _cells = cells;
        }

        public ref Cell this[int x, int y] => ref _cells[y * Width + x];
        public ref Cell this[GridPos p] => ref _cells[p.Y * Width + p.X];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(GridPos p) => InBounds(p.X, p.Y);

        public int CellCount => _cells.Length;

        /// <summary>Deep copy. Cells are value types, so an array copy is enough.</summary>
        public Board Clone() => new Board(Width, Height, (Cell[])_cells.Clone());

        /// <summary>Every position, top row first, left to right.</summary>
        public IEnumerable<GridPos> Positions()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridPos(x, y);
        }

        public int Count(Func<Cell, bool> predicate)
        {
            int n = 0;
            for (int i = 0; i < _cells.Length; i++)
                if (predicate(_cells[i])) n++;
            return n;
        }

        /// <summary>Compact one-line-per-row dump for test failures and logs.</summary>
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(LevelText.CellToken(this[x, y]));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
