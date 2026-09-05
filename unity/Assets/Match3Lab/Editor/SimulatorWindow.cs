using System.Collections.Generic;
using System.IO;
using System.Text;
using Match3Lab.Core;
using Match3Lab.Core.Simulation;
using UnityEditor;
using UnityEngine;

namespace Match3Lab.UnityEditor
{
    /// <summary>
    /// The difficulty curve of every level in <c>levels/</c>, as a table and a bar chart, with the
    /// target band shaded so an out-of-band level is visible from across the room.
    /// </summary>
    public sealed class SimulatorWindow : EditorWindow
    {
        private sealed class Row
        {
            public string File;
            public LevelDefinition Level;
            public SimulationResult Greedy;
            public SimulationResult Random;
        }

        private readonly List<Row> _rows = new List<Row>();
        private int _runs = 1000;
        private float _bandLow = 0.45f;
        private float _bandHigh = 0.75f;
        private Vector2 _scroll;
        private long _lastMs;

        [MenuItem("Match3 Lab/Difficulty Curve")]
        public static void Open()
        {
            var w = GetWindow<SimulatorWindow>("Difficulty Curve");
            w.minSize = new Vector2(640, 420);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _runs = EditorGUILayout.IntSlider(_runs, 100, 5000, GUILayout.Width(220));
            GUILayout.Label("runs / level", EditorStyles.miniLabel, GUILayout.Width(70));
            if (GUILayout.Button("Run all levels", EditorStyles.toolbarButton, GUILayout.Width(100))) RunAll();
            using (new EditorGUI.DisabledScope(_rows.Count == 0))
                if (GUILayout.Button("Export CSV…", EditorStyles.toolbarButton, GUILayout.Width(90))) ExportCsv();
            GUILayout.FlexibleSpace();
            if (_lastMs > 0) GUILayout.Label(_rows.Count + " levels · " + _lastMs + " ms", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target band (greedy win rate)", GUILayout.Width(190));
            EditorGUILayout.MinMaxSlider(ref _bandLow, ref _bandHigh, 0f, 1f);
            GUILayout.Label(Mathf.RoundToInt(_bandLow * 100) + "–" + Mathf.RoundToInt(_bandHigh * 100) + "%", GUILayout.Width(64));
            EditorGUILayout.EndHorizontal();

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox("Levels are read from " + RepoPaths.LevelsDir + ". Press 'Run all levels'.", MessageType.Info);
                return;
            }

            DrawChart();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawTable();
            EditorGUILayout.EndScrollView();
        }

        private void RunAll()
        {
            _rows.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var files = RepoPaths.LevelFiles();
            var options = new SimulationOptions { Runs = _runs };
            for (int i = 0; i < files.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Simulating", Path.GetFileName(files[i]), (float)i / files.Length);
                try
                {
                    var level = LevelText.Parse(File.ReadAllText(files[i]));
                    _rows.Add(new Row
                    {
                        File = files[i],
                        Level = level,
                        Greedy = Simulator.Run(level, () => new GreedyBot(), options),
                        Random = Simulator.Run(level, () => new RandomBot(), options),
                    });
                }
                catch (LevelFormatException ex)
                {
                    Debug.LogError(Path.GetFileName(files[i]) + ": " + ex.Message);
                }
            }
            EditorUtility.ClearProgressBar();
            _lastMs = watch.ElapsedMilliseconds;
        }

        private void DrawChart()
        {
            const float height = 150f;
            var area = GUILayoutUtility.GetRect(10, height, GUILayout.ExpandWidth(true));
            area = new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 24);
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.14f));

            // Shaded target band.
            float bandTop = area.yMax - _bandHigh * area.height;
            float bandBottom = area.yMax - _bandLow * area.height;
            EditorGUI.DrawRect(new Rect(area.x, bandTop, area.width, bandBottom - bandTop), new Color(0.3f, 0.7f, 0.5f, 0.15f));

            float slot = area.width / Mathf.Max(1, _rows.Count);
            float bar = Mathf.Min(28f, slot * 0.35f);
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                float cx = area.x + slot * (i + 0.5f);
                DrawBar(area, cx - bar - 2, bar, row.Greedy.WinRate, InBand(row.Greedy.WinRate) ? new Color(0.30f, 0.72f, 0.78f) : new Color(0.95f, 0.55f, 0.45f));
                DrawBar(area, cx + 2, bar, row.Random.WinRate, new Color(0.5f, 0.5f, 0.55f));
                var label = new Rect(cx - slot * 0.5f, area.yMax + 2, slot, 16);
                GUI.Label(label, Path.GetFileNameWithoutExtension(row.File), EditorStyles.centeredGreyMiniLabel);
            }
            GUI.Label(new Rect(area.x + 4, area.y + 2, 200, 16), "greedy (colour) · random (grey)", EditorStyles.miniLabel);
        }

        private static void DrawBar(Rect area, float x, float width, double value, Color color)
        {
            float h = (float)value * area.height;
            EditorGUI.DrawRect(new Rect(x, area.yMax - h, width, h), color);
        }

        private bool InBand(double winRate) => winRate >= _bandLow && winRate <= _bandHigh;

        private void DrawTable()
        {
            var head = new GUIStyle(EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("level", head, GUILayout.Width(150));
            GUILayout.Label("moves", head, GUILayout.Width(44));
            GUILayout.Label("greedy %", head, GUILayout.Width(64));
            GUILayout.Label("±", head, GUILayout.Width(36));
            GUILayout.Label("left", head, GUILayout.Width(40));
            GUILayout.Label("casc", head, GUILayout.Width(40));
            GUILayout.Label("shuf", head, GUILayout.Width(40));
            GUILayout.Label("random %", head, GUILayout.Width(64));
            GUILayout.Label("verdict", head, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            foreach (var row in _rows)
            {
                var g = row.Greedy;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(row.File), EditorStyles.linkLabel, GUILayout.Width(150)))
                    LevelEditorWindow.Open(row.File);
                GUILayout.Label(row.Level.Moves.ToString(), GUILayout.Width(44));
                GUILayout.Label((g.WinRate * 100).ToString("F1"), GUILayout.Width(64));
                GUILayout.Label((g.WinRateStandardError * 100).ToString("F1"), GUILayout.Width(36));
                GUILayout.Label(g.AverageMovesLeftWhenWon.ToString("F1"), GUILayout.Width(40));
                GUILayout.Label(g.AverageCascadesPerMove.ToString("F2"), GUILayout.Width(40));
                GUILayout.Label(g.TotalShuffles.ToString(), GUILayout.Width(40));
                GUILayout.Label((row.Random.WinRate * 100).ToString("F1"), GUILayout.Width(64));
                string verdict = g.WinRate < _bandLow ? "too hard" : g.WinRate > _bandHigh ? "too easy" : "in band";
                GUILayout.Label(verdict, GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("Export curve", RepoPaths.LevelsDir, "curve", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder("level,moves,greedy_win_rate,greedy_se,greedy_moves_left,greedy_cascades,greedy_shuffles,random_win_rate\n");
            foreach (var r in _rows)
            {
                sb.Append(Path.GetFileNameWithoutExtension(r.File)).Append(',').Append(r.Level.Moves).Append(',')
                  .Append(r.Greedy.WinRate.ToString("F4")).Append(',').Append(r.Greedy.WinRateStandardError.ToString("F4")).Append(',')
                  .Append(r.Greedy.AverageMovesLeftWhenWon.ToString("F3")).Append(',').Append(r.Greedy.AverageCascadesPerMove.ToString("F3")).Append(',')
                  .Append(r.Greedy.TotalShuffles).Append(',').Append(r.Random.WinRate.ToString("F4")).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}
