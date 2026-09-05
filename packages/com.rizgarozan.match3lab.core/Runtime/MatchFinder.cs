using System.Collections.Generic;

namespace Match3Lab.Core
{
    /// <summary>A connected set of same-coloured runs: a plain line, or an L/T/+ shape.</summary>
    public sealed class MatchGroup
    {
        public byte Color;
        public readonly List<GridPos> Cells = new List<GridPos>();
        public int LongestRun;
        public bool LongestRunIsHorizontal;
        /// <summary>Middle cell of the longest run — where a special lands when no swapped cell is in the group.</summary>
        public GridPos LongestRunMiddle;
        public bool HasHorizontal;
        public bool HasVertical;
        /// <summary>A cell shared by a horizontal and a vertical run, when the group has one.</summary>
        public bool HasIntersection;
        public GridPos Intersection;

        public bool Contains(GridPos p)
        {
            for (int i = 0; i < Cells.Count; i++)
                if (Cells[i] == p) return true;
            return false;
        }

        internal void Reset()
        {
            Color = 0;
            Cells.Clear();
            LongestRun = 0;
            LongestRunIsHorizontal = false;
            LongestRunMiddle = default;
            HasHorizontal = false;
            HasVertical = false;
            HasIntersection = false;
            Intersection = default;
        }
    }

    /// <summary>
    /// Finds matches on a board. An instance owns reusable buffers, so a simulator can call it
    /// thousands of times without allocating; results are only valid until the next call.
    /// </summary>
    public sealed class MatchFinder
    {
        private struct Run
        {
            public int X, Y, Length;
            public bool Horizontal;
        }

        private int[] _parent = new int[0];
        private byte[] _runFlags = new byte[0];   // bit 1: in a horizontal run, bit 2: in a vertical run
        private bool[] _added = new bool[0];
        private readonly List<Run> _runs = new List<Run>();
        private readonly List<MatchGroup> _groups = new List<MatchGroup>();
        private readonly List<MatchGroup> _pool = new List<MatchGroup>();
        private readonly Dictionary<int, MatchGroup> _byRoot = new Dictionary<int, MatchGroup>();

        /// <summary>Every match group on the board, in row-major order of first appearance.</summary>
        public List<MatchGroup> FindAll(Board board)
        {
            int w = board.Width, h = board.Height, n = w * h;
            EnsureCapacity(n);
            for (int i = 0; i < n; i++)
            {
                _parent[i] = i;
                _runFlags[i] = 0;
                _added[i] = false;
            }
            _runs.Clear();
            RecycleGroups();

            // Horizontal runs.
            for (int y = 0; y < h; y++)
            {
                int x = 0;
                while (x < w)
                {
                    if (!IsMatchable(board, x, y)) { x++; continue; }
                    byte color = board[x, y].Piece.Color;
                    int x2 = x + 1;
                    while (x2 < w && IsMatchable(board, x2, y) && board[x2, y].Piece.Color == color) x2++;
                    int len = x2 - x;
                    if (len >= 3)
                    {
                        _runs.Add(new Run { X = x, Y = y, Length = len, Horizontal = true });
                        int root = y * w + x;
                        for (int k = 0; k < len; k++)
                        {
                            int idx = y * w + x + k;
                            _runFlags[idx] |= 1;
                            Union(root, idx);
                        }
                    }
                    x = x2;
                }
            }

            // Vertical runs.
            for (int x = 0; x < w; x++)
            {
                int y = 0;
                while (y < h)
                {
                    if (!IsMatchable(board, x, y)) { y++; continue; }
                    byte color = board[x, y].Piece.Color;
                    int y2 = y + 1;
                    while (y2 < h && IsMatchable(board, x, y2) && board[x, y2].Piece.Color == color) y2++;
                    int len = y2 - y;
                    if (len >= 3)
                    {
                        _runs.Add(new Run { X = x, Y = y, Length = len, Horizontal = false });
                        int root = y * w + x;
                        for (int k = 0; k < len; k++)
                        {
                            int idx = (y + k) * w + x;
                            _runFlags[idx] |= 2;
                            Union(root, idx);
                        }
                    }
                    y = y2;
                }
            }

            if (_runs.Count == 0) return _groups;

            _byRoot.Clear();
            for (int r = 0; r < _runs.Count; r++)
            {
                var run = _runs[r];
                int root = Find(run.Y * w + run.X);
                if (!_byRoot.TryGetValue(root, out var group))
                {
                    group = RentGroup();
                    group.Color = board[run.X, run.Y].Piece.Color;
                    _byRoot.Add(root, group);
                    _groups.Add(group);
                }

                if (run.Horizontal) group.HasHorizontal = true; else group.HasVertical = true;
                if (run.Length > group.LongestRun)
                {
                    group.LongestRun = run.Length;
                    group.LongestRunIsHorizontal = run.Horizontal;
                    int mid = run.Length / 2;
                    group.LongestRunMiddle = run.Horizontal
                        ? new GridPos(run.X + mid, run.Y)
                        : new GridPos(run.X, run.Y + mid);
                }

                for (int k = 0; k < run.Length; k++)
                {
                    int cx = run.Horizontal ? run.X + k : run.X;
                    int cy = run.Horizontal ? run.Y : run.Y + k;
                    int idx = cy * w + cx;
                    if (_added[idx]) continue;
                    _added[idx] = true;
                    var p = new GridPos(cx, cy);
                    group.Cells.Add(p);
                    if (_runFlags[idx] == 3 && !group.HasIntersection)
                    {
                        group.HasIntersection = true;
                        group.Intersection = p;
                    }
                }
            }
            return _groups;
        }

        /// <summary>True when a line of three or more same-coloured normal pieces passes through p.</summary>
        public static bool LineThrough(Board board, GridPos p)
        {
            if (!IsMatchable(board, p.X, p.Y)) return false;
            byte color = board[p].Piece.Color;
            int h = 1 + RunFrom(board, p, -1, 0, color) + RunFrom(board, p, 1, 0, color);
            if (h >= 3) return true;
            int v = 1 + RunFrom(board, p, 0, -1, color) + RunFrom(board, p, 0, 1, color);
            return v >= 3;
        }

        private static int RunFrom(Board board, GridPos p, int dx, int dy, byte color)
        {
            int n = 0;
            int x = p.X + dx, y = p.Y + dy;
            while (board.InBounds(x, y) && IsMatchable(board, x, y) && board[x, y].Piece.Color == color)
            {
                n++;
                x += dx;
                y += dy;
            }
            return n;
        }

        /// <summary>Only normal pieces in playable cells form matches; ice on top does not stop a match.</summary>
        private static bool IsMatchable(Board board, int x, int y)
        {
            ref var c = ref board[x, y];
            return c.IsPlayable && c.Piece.IsNormal;
        }

        private int Find(int i)
        {
            while (_parent[i] != i)
            {
                _parent[i] = _parent[_parent[i]];
                i = _parent[i];
            }
            return i;
        }

        private void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra != rb) _parent[rb] = ra;
        }

        private void EnsureCapacity(int n)
        {
            if (_parent.Length >= n) return;
            _parent = new int[n];
            _runFlags = new byte[n];
            _added = new bool[n];
        }

        private MatchGroup RentGroup()
        {
            if (_pool.Count == 0) return new MatchGroup();
            var g = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            return g;
        }

        private void RecycleGroups()
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                _groups[i].Reset();
                _pool.Add(_groups[i]);
            }
            _groups.Clear();
        }
    }
}
