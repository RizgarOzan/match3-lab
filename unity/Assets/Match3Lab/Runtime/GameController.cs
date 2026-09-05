using System;
using System.Collections;
using System.Collections.Generic;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using UnityEngine;

namespace Match3Lab.Unity
{
    /// <summary>
    /// Owns the current <see cref="Game"/> and wires input, board and HUD together. The only place
    /// where the presentation talks to the core, and it does so through two calls: Play and Present.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private int _levelIndex;
        [SerializeField] private bool _fixedSeed;
        [SerializeField] private ulong _seed = 1;

        private List<(string name, TextAsset asset)> _levels;
        private Game _game;
        private BoardView _board;
        private HudView _hud;
        private InputController _input;
        private SpriteFactory _sprites;
        private Camera _camera;
        private bool _busy;
        private bool _auto;
        private readonly GreedyBot _bot = new GreedyBot();
        private Pcg32 _botRng;
        private readonly List<Move> _moveBuffer = new List<Move>();

        /// <summary>The game being played. Read-only for tests and tools; mutate only through moves.</summary>
        public Game Current => _game;
        public bool IsBusy => _busy;
        public int LevelCount => _levels?.Count ?? 0;

        /// <summary>Lets the greedy bot play up to <paramref name="count"/> moves with full animation. Used by the play-mode tests.</summary>
        public IEnumerator PlayMovesWithBot(int count)
        {
            for (int i = 0; i < count && _game.Status == GameStatus.Playing; i++)
            {
                while (_busy) yield return null;
                var moves = _game.LegalMoves(_moveBuffer);
                if (moves.Count == 0) yield break;
                yield return PlayAndPresent(_bot.Choose(_game, moves, _botRng));
            }
            while (_busy) yield return null;
        }

        private void Awake()
        {
            _sprites = new SpriteFactory();
            _camera = Camera.main;
            if (_camera == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera));
                camGo.tag = "MainCamera";
                _camera = camGo.GetComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
            _camera.transform.position = new Vector3(0, 0, -10);

            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(transform, false);
            _board.transform.localPosition = new Vector3(0, -0.15f, 0);

            _input = gameObject.AddComponent<InputController>();
            _input.Bind(_board, _camera);
            _input.MoveRequested += OnMoveRequested;

            _hud = new GameObject("Hud").AddComponent<HudView>();
            _hud.transform.SetParent(transform, false);
            _hud.Build();
            _hud.PrevRequested += () => LoadLevel(_levelIndex - 1);
            _hud.NextRequested += () => LoadLevel(_levelIndex + 1);
            _hud.RetryRequested += () => LoadLevel(_levelIndex);
            _hud.AutoToggled += ToggleAuto;

            _levels = LevelCatalog.Load();
        }

        private void Start()
        {
            if (StartupWantsAutoplay(out int startLevel))
            {
                if (startLevel >= 0) _levelIndex = startLevel;
                _auto = true;
                _hud.SetAuto(true);
                _input.Enabled = false;
            }
            LoadLevel(_levelIndex);
        }

        /// <summary>
        /// <c>-autoplay [n]</c> on the command line (standalone) or <c>?auto=1&amp;level=n</c> in the
        /// page URL (WebGL) starts with the bot playing. Used by the capture script and the demo page's
        /// "watch the bot" link.
        /// </summary>
        private static bool StartupWantsAutoplay(out int level)
        {
            level = -1;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-autoplay", StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n)) level = n;
                return true;
            }
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url) || url.IndexOf("auto=1", StringComparison.OrdinalIgnoreCase) < 0) return false;
            int at = url.IndexOf("level=", StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                int end = at + 6;
                while (end < url.Length && char.IsDigit(url[end])) end++;
                int.TryParse(url.Substring(at + 6, end - at - 6), out level);
            }
            return true;
        }

        public void LoadLevel(int index)
        {
            if (_levels.Count > 0)
            {
                _levelIndex = ((index % _levels.Count) + _levels.Count) % _levels.Count;
            }
            var level = _levels.Count > 0 ? LevelCatalog.Parse(_levels[_levelIndex].asset) : LevelCatalog.Builtin();
            ulong seed = _fixedSeed ? _seed : unchecked((ulong)DateTime.UtcNow.Ticks);
            _game = Game.Start(level, seed);
            _botRng = new Pcg32(seed, 0x5EEDBEEF);
            _busy = false;
            StopAllCoroutines();

            _board.Bind(_game, _sprites);
            FitCamera(level);
            _hud.Bind(_game);
            _hud.SetStatus("seed " + seed + "  ·  level " + (_levelIndex + 1) + "/" + Math.Max(1, _levels.Count));
            if (_auto) StartCoroutine(AutoPlay());
        }

        private void FitCamera(LevelDefinition level)
        {
            float aspect = (float)Screen.width / Screen.height;
            float halfH = level.Height * 0.5f + 2.3f; // room for the HUD bands
            float halfW = (level.Width * 0.5f + 0.6f) / aspect;
            _camera.orthographicSize = Mathf.Max(halfH, halfW);
        }

        private int _lastScreenW, _lastScreenH;

        private void Update()
        {
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                _lastScreenW = Screen.width;
                _lastScreenH = Screen.height;
                if (_game != null) FitCamera(_game.Level);
            }
        }

        private void OnMoveRequested(Move move)
        {
            if (_busy || _auto || _game.Status != GameStatus.Playing) return;
            StartCoroutine(PlayAndPresent(move));
        }

        private IEnumerator PlayAndPresent(Move move)
        {
            _busy = true;
            var result = _game.Play(move);
            if (!result.Legal)
            {
                yield return _board.Wiggle(move);
                _busy = false;
                yield break;
            }
            yield return _board.Present(result);
            _hud.Refresh();
            if (result.Cascades > 0) _hud.SetStatus(result.Cascades + (result.Cascades == 1 ? " cascade" : " cascades") + "  ·  +" + result.ScoreGained);
            _busy = false;
        }

        private void ToggleAuto()
        {
            _auto = !_auto;
            _hud.SetAuto(_auto);
            _input.Enabled = !_auto;
            if (_auto) StartCoroutine(AutoPlay());
        }

        /// <summary>The greedy bot plays the level on screen, with the same animation a player would see.</summary>
        private IEnumerator AutoPlay()
        {
            while (_auto && _game.Status == GameStatus.Playing)
            {
                if (_busy) { yield return null; continue; }
                var moves = _game.LegalMoves(_moveBuffer);
                if (moves.Count == 0) yield break;
                var move = _bot.Choose(_game, moves, _botRng);
                yield return PlayAndPresent(move);
                yield return new WaitForSeconds(0.15f);
            }
        }
    }
}
