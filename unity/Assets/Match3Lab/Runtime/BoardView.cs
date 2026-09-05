using System.Collections;
using System.Collections.Generic;
using Match3Lab.Core;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>
    /// Draws a <see cref="Game"/> and replays <see cref="MoveResult"/> event streams as animation.
    /// It never decides anything: every position, clear and spawn comes from the core's events,
    /// and <see cref="SyncFromBoard"/> exists so that any drift is corrected from the truth.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public float SwapSeconds = 0.12f;
        public float ClearSeconds = 0.16f;
        public float FallSecondsPerCell = 0.07f;
        public float FallSecondsMax = 0.30f;

        private Game _game;
        private SpriteFactory _sprites;
        private Transform _root;
        private CellView[] _cells;
        private readonly Dictionary<GridPos, PieceView> _pieces = new Dictionary<GridPos, PieceView>();
        private readonly Stack<PieceView> _pool = new Stack<PieceView>();
        private int _running;

        public bool IsAnimating { get; private set; }
        public int Width => _game?.Board.Width ?? 0;
        public int Height => _game?.Board.Height ?? 0;

        public void Bind(Game game, SpriteFactory sprites)
        {
            _game = game;
            _sprites = sprites;
            if (_root == null)
            {
                _root = new GameObject("BoardRoot").transform;
                _root.SetParent(transform, false);
            }
            foreach (var p in _pieces.Values) Recycle(p);
            _pieces.Clear();
            if (_cells != null)
                foreach (var c in _cells) if (c != null) Destroy(c.gameObject);

            _cells = new CellView[game.Board.CellCount];
            foreach (var pos in game.Board.Positions())
            {
                var cell = CellView.Create(_root, sprites, pos);
                cell.transform.localPosition = WorldOf(pos);
                _cells[Index(pos)] = cell;
            }
            SyncFromBoard();
        }

        public Vector3 WorldOf(GridPos p) => new Vector3(p.X - (Width - 1) * 0.5f, (Height - 1) * 0.5f - p.Y, 0f);

        public bool TryCellAt(Vector3 world, out GridPos pos)
        {
            var local = world - transform.position;
            int x = Mathf.RoundToInt(local.x + (Width - 1) * 0.5f);
            int y = Mathf.RoundToInt((Height - 1) * 0.5f - local.y);
            pos = new GridPos(x, y);
            return _game != null && _game.Board.InBounds(pos);
        }

        /// <summary>Rebuilds every view from the board. The fallback when an animation is interrupted.</summary>
        public void SyncFromBoard()
        {
            StopAllCoroutines();
            _running = 0;
            IsAnimating = false;
            foreach (var p in _pieces.Values) Recycle(p);
            _pieces.Clear();
            foreach (var pos in _game.Board.Positions())
            {
                ref var cell = ref _game.Board[pos];
                _cells[Index(pos)].Show(cell);
                if (cell.IsPlayable && !cell.Piece.IsEmpty) Place(pos, cell.Piece).transform.localPosition = WorldOf(pos);
            }
        }

        /// <summary>Plays the event stream of one move, batch by batch, then resyncs to be safe.</summary>
        public IEnumerator Present(MoveResult result)
        {
            IsAnimating = true;
            var events = result.Events;
            int i = 0;
            while (i < events.Count)
            {
                int kindClass = ClassOf(events[i].Kind);
                int j = i;
                while (j < events.Count && ClassOf(events[j].Kind) == kindClass) j++;
                for (int k = i; k < j; k++) Apply(events[k]);
                yield return WaitForRunning();
                i = j;
            }
            SyncFromBoard();
            IsAnimating = false;
        }

        /// <summary>A rejected swap: the two pieces lean toward each other and come back.</summary>
        public IEnumerator Wiggle(Move move)
        {
            if (move.Kind != MoveKind.Swap) yield break;
            if (!_pieces.TryGetValue(move.A, out var a) || !_pieces.TryGetValue(move.B, out var b)) yield break;
            IsAnimating = true;
            var pa = WorldOf(move.A);
            var pb = WorldOf(move.B);
            var mid = Vector3.Lerp(pa, pb, 0.25f);
            var mid2 = Vector3.Lerp(pb, pa, 0.25f);
            Run(MoveTo(a.transform, mid, SwapSeconds * 0.6f));
            Run(MoveTo(b.transform, mid2, SwapSeconds * 0.6f));
            yield return WaitForRunning();
            Run(MoveTo(a.transform, pa, SwapSeconds * 0.6f));
            Run(MoveTo(b.transform, pb, SwapSeconds * 0.6f));
            yield return WaitForRunning();
            IsAnimating = false;
        }

        // 0 = swap, 1 = clears/hits/specials (simultaneous), 2 = falls/spawns (simultaneous), 3 = shuffle
        private static int ClassOf(BoardEventKind kind)
        {
            switch (kind)
            {
                case BoardEventKind.Swap: return 0;
                case BoardEventKind.Fell:
                case BoardEventKind.Spawned: return 2;
                case BoardEventKind.Shuffled: return 3;
                default: return 1;
            }
        }

        private void Apply(BoardEvent e)
        {
            switch (e.Kind)
            {
                case BoardEventKind.Swap:
                {
                    _pieces.TryGetValue(e.Pos, out var a);
                    _pieces.TryGetValue(e.To, out var b);
                    if (a != null) Run(MoveTo(a.transform, WorldOf(e.To), SwapSeconds));
                    if (b != null) Run(MoveTo(b.transform, WorldOf(e.Pos), SwapSeconds));
                    _pieces.Remove(e.Pos);
                    _pieces.Remove(e.To);
                    if (a != null) _pieces[e.To] = a;
                    if (b != null) _pieces[e.Pos] = b;
                    break;
                }
                case BoardEventKind.Cleared:
                case BoardEventKind.SpecialActivated:
                {
                    if (_pieces.TryGetValue(e.Pos, out var v))
                    {
                        _pieces.Remove(e.Pos);
                        float burst = e.Kind == BoardEventKind.SpecialActivated ? 1.6f : 1.15f;
                        Run(PopOut(v, burst));
                    }
                    break;
                }
                case BoardEventKind.ObstacleHit:
                case BoardEventKind.BoxDestroyed:
                {
                    _cells[Index(e.Pos)].Show(_game.Board[e.Pos]);
                    Run(Flash(_cells[Index(e.Pos)].transform));
                    break;
                }
                case BoardEventKind.SpecialCreated:
                {
                    if (_pieces.TryGetValue(e.Pos, out var old)) Recycle(old);
                    var v = Place(e.Pos, e.Piece);
                    v.transform.localPosition = WorldOf(e.Pos);
                    Run(PopIn(v));
                    break;
                }
                case BoardEventKind.Fell:
                {
                    if (_pieces.TryGetValue(e.Pos, out var v))
                    {
                        _pieces.Remove(e.Pos);
                        _pieces[e.To] = v;
                        float dist = Mathf.Abs(e.To.Y - e.Pos.Y);
                        Run(MoveTo(v.transform, WorldOf(e.To), Mathf.Min(FallSecondsMax, dist * FallSecondsPerCell)));
                    }
                    break;
                }
                case BoardEventKind.Spawned:
                {
                    var v = Place(e.Pos, e.Piece);
                    int above = 1 + CountAbove(e.Pos);
                    v.transform.localPosition = WorldOf(new GridPos(e.Pos.X, e.Pos.Y - above));
                    Run(MoveTo(v.transform, WorldOf(e.Pos), Mathf.Min(FallSecondsMax, above * FallSecondsPerCell)));
                    break;
                }
                case BoardEventKind.Shuffled:
                    Run(ShuffleFade());
                    break;
            }
        }

        /// <summary>Spawns in the same column stack above each other while falling in.</summary>
        private int CountAbove(GridPos p)
        {
            int n = 0;
            for (int y = p.Y - 1; y >= 0; y--)
                if (_pieces.TryGetValue(new GridPos(p.X, y), out var v) && v.transform.localPosition.y > WorldOf(new GridPos(p.X, 0)).y + 0.01f) n++;
            return n;
        }

        // ---- pooled pieces ----

        private PieceView Place(GridPos pos, Piece piece)
        {
            var v = _pool.Count > 0 ? _pool.Pop() : PieceView.Create(_root);
            v.Show(piece, _sprites);
            _pieces[pos] = v;
            return v;
        }

        private void Recycle(PieceView v)
        {
            v.Hide();
            _pool.Push(v);
        }

        private int Index(GridPos p) => p.Y * Width + p.X;

        // ---- tiny tween runtime ----

        private void Run(IEnumerator routine)
        {
            _running++;
            StartCoroutine(Track(routine));
        }

        private IEnumerator Track(IEnumerator routine)
        {
            yield return routine;
            _running--;
        }

        private IEnumerator WaitForRunning()
        {
            while (_running > 0) yield return null;
        }

        private static IEnumerator MoveTo(Transform t, Vector3 target, float seconds)
        {
            var start = t.localPosition;
            float time = 0f;
            while (time < seconds)
            {
                time += Time.deltaTime;
                float k = seconds <= 0f ? 1f : Mathf.Clamp01(time / seconds);
                k = 1f - (1f - k) * (1f - k); // ease out
                t.localPosition = Vector3.LerpUnclamped(start, target, k);
                yield return null;
            }
            t.localPosition = target;
        }

        private IEnumerator PopOut(PieceView v, float burst)
        {
            float time = 0f;
            var start = v.transform.localScale;
            while (time < ClearSeconds)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / ClearSeconds);
                float s = k < 0.35f ? Mathf.Lerp(1f, burst, k / 0.35f) : Mathf.Lerp(burst, 0f, (k - 0.35f) / 0.65f);
                v.transform.localScale = start * s;
                yield return null;
            }
            Recycle(v);
        }

        private IEnumerator PopIn(PieceView v)
        {
            float time = 0f;
            while (time < ClearSeconds)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / ClearSeconds);
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                v.transform.localScale = Vector3.one * s;
                yield return null;
            }
            v.transform.localScale = Vector3.one;
        }

        private IEnumerator Flash(Transform t)
        {
            float time = 0f;
            while (time < ClearSeconds)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / ClearSeconds);
                t.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        private IEnumerator ShuffleFade()
        {
            float time = 0f;
            const float half = 0.2f;
            var views = new List<PieceView>(_pieces.Values);
            while (time < half)
            {
                time += Time.deltaTime;
                foreach (var v in views) v.SetAlpha(1f - time / half);
                yield return null;
            }
            foreach (var v in views) Recycle(v);
            _pieces.Clear();
            foreach (var pos in _game.Board.Positions())
            {
                var cell = _game.Board[pos]; // by value: iterators cannot hold ref locals
                if (cell.IsPlayable && !cell.Piece.IsEmpty)
                {
                    var v = Place(pos, cell.Piece);
                    v.transform.localPosition = WorldOf(pos);
                    v.SetAlpha(0f);
                }
            }
            time = 0f;
            while (time < half)
            {
                time += Time.deltaTime;
                foreach (var v in _pieces.Values) v.SetAlpha(time / half);
                yield return null;
            }
            foreach (var v in _pieces.Values) v.SetAlpha(1f);
        }
    }
}
