using System;
using System.Collections.Generic;
using System.IO;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using Match3Lab.Unity;
using UnityEditor;
using UnityEngine;

namespace Match3Lab.UnityEditor
{
    /// <summary>
    /// Paint a level, see what is wrong with it, watch the bots play it — without leaving the
    /// editor. Reads and writes the same text files the CLI and the build use. IMGUI on purpose:
    /// a grid of coloured buttons is exactly what IMGUI is good at, and there is nothing to lay out
    /// that would repay a UXML document.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private enum Brush { Random, Empty, Hole, Color0, Color1, Color2, Color3, Color4, Color5, Box1, Box2 }

        private static readonly string[] BrushNames =
            { "random", "empty", "hole", "c0", "c1", "c2", "c3", "c4", "c5", "box 1", "box 2" };

        private const float CellPx = 30f;

        private LevelDefinition _level;
        private string _path;
        private bool _dirty;
        private Brush _brush = Brush.Random;
        private int _grassLayer;   // 0..2, applied with the brush
        private int _iceLayer;     // 0..2
        private Vector2 _scroll;
        private List<string> _errors = new List<string>();

        private ulong _previewSeed = 1;
        private Game _preview;

        private int _simRuns = 1000;
        private float _bandLow = 0.45f;
        private float _bandHigh = 0.75f;
        private SimulationResult _greedy;
        private SimulationResult _random;

        [MenuItem("Match3 Lab/Level Editor")]
        public static void Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(560, 520);
        }

        public static void Open(string path)
        {
            Open();
            GetWindow<LevelEditorWindow>().Load(path);
        }

        private void OnEnable()
        {
            if (_level == null) NewLevel();
        }

        // ---- file operations -----------------------------------------------------------------

        private void NewLevel()
        {
            _level = new LevelDefinition { Name = "New Level", Width = 7, Height = 7, Moves = 20, ColorCount = 5 };
            _level.Goals.Add(Goal.CollectColor(0, 20));
            _level.Cells = new CellSpec[_level.Width * _level.Height];
            for (int i = 0; i < _level.Cells.Length; i++) _level.Cells[i] = CellSpec.Random;
            _path = null;
            _dirty = false;
            Changed();
        }

        private void Load(string path)
        {
            try
            {
                _level = LevelText.Parse(File.ReadAllText(path));
                _path = path;
                _dirty = false;
                Changed();
            }
            catch (LevelFormatException ex)
            {
                EditorUtility.DisplayDialog("Cannot open level", ex.Message, "OK");
            }
        }

        private void Save(bool saveAs)
        {
            string path = _path;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                Directory.CreateDirectory(RepoPaths.LevelsDir);
                string suggested = SuggestFileName(_level.Name);
                path = EditorUtility.SaveFilePanel("Save level", RepoPaths.LevelsDir, suggested, "txt");
                if (string.IsNullOrEmpty(path)) return;
            }
            File.WriteAllText(path, LevelText.Write(_level));
            _path = path;
            _dirty = false;
            LevelSync.Sync();
        }

        private static string SuggestFileName(string name)
        {
            var chars = new List<char>();
            foreach (char c in name.ToLowerInvariant())
                chars.Add(char.IsLetterOrDigit(c) ? c : '-');
            return new string(chars.ToArray()).Trim('-');
        }

        private void Changed()
        {
            _dirty = true;
            _errors = _level.Validate();
            _preview = null;
            _greedy = null;
            _random = null;
            Repaint();
        }

        // ---- GUI --------------------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeaderFields();
            DrawGoals();
            EditorGUILayout.Space(6);
            DrawBrushes();
            DrawGrid();
            DrawValidation();
            DrawPreviewAndSimulation();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50))) NewLevel();

            var files = RepoPaths.LevelFiles();
            var names = new string[files.Length + 1];
            names[0] = "Open…";
            for (int i = 0; i < files.Length; i++) names[i + 1] = Path.GetFileNameWithoutExtension(files[i]);
            int pick = EditorGUILayout.Popup(0, names, EditorStyles.toolbarPopup, GUILayout.Width(180));
            if (pick > 0) Load(files[pick - 1]);

            using (new EditorGUI.DisabledScope(_errors.Count > 0))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50))) Save(false);
                if (GUILayout.Button("Save As…", EditorStyles.toolbarButton, GUILayout.Width(70))) Save(true);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label((_path == null ? "(unsaved)" : Path.GetFileName(_path)) + (_dirty ? " *" : ""), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeaderFields()
        {
            EditorGUI.BeginChangeCheck();
            _level.Name = EditorGUILayout.TextField("Name", _level.Name);
            EditorGUILayout.BeginHorizontal();
            int w = EditorGUILayout.IntSlider("Width", _level.Width, 3, LevelDefinition.MaxSize);
            int h = EditorGUILayout.IntSlider("Height", _level.Height, 3, LevelDefinition.MaxSize);
            EditorGUILayout.EndHorizontal();
            _level.Moves = EditorGUILayout.IntSlider("Moves", _level.Moves, 1, 60);
            _level.ColorCount = EditorGUILayout.IntSlider("Colors", _level.ColorCount, LevelDefinition.MinColors, LevelDefinition.MaxColors);
            if (EditorGUI.EndChangeCheck())
            {
                if (w != _level.Width || h != _level.Height) Resize(w, h);
                Changed();
            }
        }

        private void Resize(int w, int h)
        {
            var cells = new CellSpec[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    cells[y * w + x] = x < _level.Width && y < _level.Height ? _level.GetCell(x, y) : CellSpec.Random;
            _level.Width = w;
            _level.Height = h;
            _level.Cells = cells;
        }

        private void DrawGoals()
        {
            EditorGUILayout.LabelField("Goals", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < _level.Goals.Count; i++)
            {
                var g = _level.Goals[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var kind = (GoalKind)EditorGUILayout.EnumPopup(g.Kind, GUILayout.Width(80));
                int color = g.Color;
                using (new EditorGUI.DisabledScope(kind != GoalKind.Color))
                    color = EditorGUILayout.IntSlider(color, 0, _level.ColorCount - 1, GUILayout.Width(160));
                int target = EditorGUILayout.IntField(g.Target, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    _level.Goals[i] = new Goal(kind, (byte)color, Mathf.Max(1, target));
                    Changed();
                }
                if (GUILayout.Button("×", GUILayout.Width(24))) remove = i;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            if (remove >= 0) { _level.Goals.RemoveAt(remove); Changed(); }
            if (GUILayout.Button("+ goal", GUILayout.Width(70))) { _level.Goals.Add(Goal.ClearGrass(1)); Changed(); }
        }

        private void DrawBrushes()
        {
            EditorGUILayout.LabelField("Brush  (click or drag on the grid)", EditorStyles.boldLabel);
            _brush = (Brush)GUILayout.SelectionGrid((int)_brush, BrushNames, BrushNames.Length);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("grass", GUILayout.Width(40));
            _grassLayer = GUILayout.Toolbar(_grassLayer, new[] { "0", "1", "2" }, GUILayout.Width(90));
            GUILayout.Space(16);
            GUILayout.Label("ice", GUILayout.Width(24));
            _iceLayer = GUILayout.Toolbar(_iceLayer, new[] { "0", "1", "2" }, GUILayout.Width(90));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGrid()
        {
            var area = GUILayoutUtility.GetRect(_level.Width * CellPx, _level.Height * CellPx, GUILayout.ExpandWidth(false));
            var e = Event.current;
            for (int y = 0; y < _level.Height; y++)
            {
                for (int x = 0; x < _level.Width; x++)
                {
                    var r = new Rect(area.x + x * CellPx, area.y + y * CellPx, CellPx - 2, CellPx - 2);
                    var spec = _level.GetCell(x, y);
                    EditorGUI.DrawRect(r, CellColor(spec));
                    if (spec.Grass > 0) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 5, r.width, 5), new Color(0.3f, 0.7f, 0.3f, spec.Grass >= 2 ? 1f : 0.7f));
                    if (spec.Ice > 0) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 5), new Color(0.6f, 0.85f, 1f, spec.Ice >= 2 ? 1f : 0.7f));
                    GUI.Label(r, LevelText.SpecToken(spec), EditorStyles.centeredGreyMiniLabel);

                    bool paint = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition);
                    if (paint)
                    {
                        _level.Cells[y * _level.Width + x] = BrushSpec();
                        Changed();
                        e.Use();
                    }
                }
            }
        }

        private CellSpec BrushSpec()
        {
            byte grass = (byte)_grassLayer, ice = (byte)_iceLayer;
            switch (_brush)
            {
                case Brush.Empty: return new CellSpec(CellSpecKind.Empty, grass: grass);
                case Brush.Hole: return CellSpec.Hole;
                case Brush.Box1: return new CellSpec(CellSpecKind.Box, boxHitPoints: 1);
                case Brush.Box2: return new CellSpec(CellSpecKind.Box, boxHitPoints: 2);
                case Brush.Random: return new CellSpec(CellSpecKind.Random, ice: ice, grass: grass);
                default: return new CellSpec(CellSpecKind.FixedColor, (byte)((int)_brush - (int)Brush.Color0), ice: ice, grass: grass);
            }
        }

        private static Color CellColor(CellSpec spec)
        {
            switch (spec.Kind)
            {
                case CellSpecKind.Hole: return new Color(0.12f, 0.12f, 0.12f);
                case CellSpecKind.Empty: return new Color(0.30f, 0.30f, 0.34f);
                case CellSpecKind.Box: return spec.BoxHitPoints >= 2 ? new Color(0.45f, 0.30f, 0.18f) : new Color(0.62f, 0.45f, 0.28f);
                case CellSpecKind.FixedColor: return SpriteFactory.Palette[spec.Color % SpriteFactory.Palette.Length];
                default: return new Color(0.45f, 0.47f, 0.52f);
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.Space(4);
            if (_errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Valid level.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Error);
        }

        private void DrawPreviewAndSimulation()
        {
            using (new EditorGUI.DisabledScope(_errors.Count > 0))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Start board preview", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                _previewSeed = (ulong)Mathf.Max(0, EditorGUILayout.IntField("Seed", (int)_previewSeed, GUILayout.Width(220)));
                if (GUILayout.Button("Preview", GUILayout.Width(80))) _preview = Game.Start(_level, _previewSeed);
                if (GUILayout.Button("Next seed", GUILayout.Width(80))) { _previewSeed++; _preview = Game.Start(_level, _previewSeed); }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                if (_preview != null) DrawBoardPreview(_preview.Board);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Simulation", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                _simRuns = EditorGUILayout.IntSlider("Runs", _simRuns, 100, 5000);
                if (GUILayout.Button("Simulate", GUILayout.Width(90))) RunSimulation();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target win-rate band (greedy)", GUILayout.Width(190));
                EditorGUILayout.MinMaxSlider(ref _bandLow, ref _bandHigh, 0f, 1f);
                GUILayout.Label(Mathf.RoundToInt(_bandLow * 100) + "–" + Mathf.RoundToInt(_bandHigh * 100) + "%", GUILayout.Width(64));
                EditorGUILayout.EndHorizontal();
                if (_greedy != null) DrawResult(_greedy, true);
                if (_random != null) DrawResult(_random, false);
            }
        }

        private void RunSimulation()
        {
            var options = new SimulationOptions { Runs = _simRuns };
            _greedy = Simulator.Run(_level, () => new GreedyBot(), options);
            _random = Simulator.Run(_level, () => new RandomBot(), options);
        }

        private void DrawResult(SimulationResult r, bool judge)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(r.BotName + " bot — " + r.Runs + " runs in " + r.ElapsedMilliseconds + " ms", EditorStyles.boldLabel);
            string verdict = "";
            if (judge)
            {
                if (r.WinRate < _bandLow) verdict = "   ← too hard for the band";
                else if (r.WinRate > _bandHigh) verdict = "   ← too easy for the band";
                else verdict = "   ✓ in band";
            }
            EditorGUILayout.LabelField("win rate  " + (r.WinRate * 100).ToString("F1") + "% ± " + (r.WinRateStandardError * 100).ToString("F1") + verdict);
            EditorGUILayout.LabelField("moves left when won  " + r.AverageMovesLeftWhenWon.ToString("F2") + "     cascades/move  " + r.AverageCascadesPerMove.ToString("F2") + "     shuffles  " + r.TotalShuffles);
            for (int i = 0; i < r.GoalCompletions.Length; i++)
                EditorGUILayout.LabelField("goal " + i + " (" + _level.Goals[i] + ")  completed " + (r.GoalCompletionRate(i) * 100).ToString("F0") + "%, average progress " + (r.AverageGoalFraction(i) * 100).ToString("F0") + "%");
            EditorGUILayout.EndVertical();
        }

        private static void DrawBoardPreview(Board board)
        {
            const float px = 18f;
            var area = GUILayoutUtility.GetRect(board.Width * px, board.Height * px, GUILayout.ExpandWidth(false));
            foreach (var p in board.Positions())
            {
                ref var c = ref board[p];
                var r = new Rect(area.x + p.X * px, area.y + p.Y * px, px - 1, px - 1);
                if (c.Hole) continue;
                Color color = c.Box > 0 ? new Color(0.55f, 0.38f, 0.22f)
                    : c.Piece.IsNormal ? SpriteFactory.Palette[c.Piece.Color % SpriteFactory.Palette.Length]
                    : new Color(0.9f, 0.9f, 0.9f);
                EditorGUI.DrawRect(r, color);
                if (c.Grass > 0) EditorGUI.DrawRect(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(0.3f, 0.7f, 0.3f));
                if (c.Ice > 0) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3), new Color(0.6f, 0.85f, 1f));
            }
        }
    }
}
