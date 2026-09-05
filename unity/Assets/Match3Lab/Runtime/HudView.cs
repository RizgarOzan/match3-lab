using System;
using System.Text;
using Match3Lab.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Match3Lab.Unity
{
    /// <summary>
    /// Moves, goals, level name, the control buttons and the end-of-level panel — built in code so
    /// the demo scene has no hand-made UI to maintain. Legacy UI Text on purpose: it needs no
    /// imported font assets and renders identically in WebGL.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public event Action PrevRequested;
        public event Action NextRequested;
        public event Action RetryRequested;
        public event Action AutoToggled;

        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color ButtonFace = new Color(0.20f, 0.23f, 0.30f);
        private static readonly Color Accent = new Color(0.30f, 0.72f, 0.78f);

        private Font _font;
        private Text _title;
        private Text _moves;
        private Text _goals;
        private Text _status;
        private Text _autoLabel;
        private GameObject _endPanel;
        private Text _endTitle;
        private Game _game;

        public void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(900, 1600);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var top = Rect(canvasGo.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-40, 190), Panel);
            _title = Label(top.transform, "Title", 34, TextAnchor.UpperLeft, new Vector2(24, -18), new Vector2(-48, 44));
            _moves = Label(top.transform, "Moves", 30, TextAnchor.UpperRight, new Vector2(-24, -18), new Vector2(-48, 44));
            _moves.rectTransform.anchorMin = new Vector2(0, 1);
            _moves.rectTransform.anchorMax = new Vector2(1, 1);
            _goals = Label(top.transform, "Goals", 26, TextAnchor.UpperLeft, new Vector2(24, -66), new Vector2(-48, 110));
            _goals.color = new Color(0.85f, 0.88f, 0.92f);

            var bottom = Rect(canvasGo.transform, "Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(-40, 120), Panel);
            var row = bottom.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 16, 16);
            row.spacing = 12;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            Button(row.transform, "◀ Prev", () => PrevRequested?.Invoke());
            Button(row.transform, "Retry", () => RetryRequested?.Invoke());
            _autoLabel = Button(row.transform, "Bot: off", () => AutoToggled?.Invoke());
            Button(row.transform, "Next ▶", () => NextRequested?.Invoke());

            _status = Label(canvasGo.transform, "Status", 22, TextAnchor.LowerCenter, new Vector2(0, 150), new Vector2(-80, 30));
            _status.rectTransform.anchorMin = new Vector2(0, 0);
            _status.rectTransform.anchorMax = new Vector2(1, 0);
            _status.rectTransform.pivot = new Vector2(0.5f, 0);
            _status.color = new Color(0.7f, 0.74f, 0.8f);

            _endPanel = Rect(canvasGo.transform, "End", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 300), Panel);
            _endTitle = Label(_endPanel.transform, "EndTitle", 48, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(-40, 90));
            _endTitle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            _endTitle.rectTransform.anchorMax = new Vector2(1, 0.5f);
            var endRow = Rect(_endPanel.transform, "EndRow", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(-48, 84), new Color(0, 0, 0, 0));
            var endLayout = endRow.AddComponent<HorizontalLayoutGroup>();
            endLayout.spacing = 12;
            endLayout.childForceExpandWidth = true;
            endLayout.childForceExpandHeight = true;
            Button(endRow.transform, "Retry", () => RetryRequested?.Invoke());
            Button(endRow.transform, "Next ▶", () => NextRequested?.Invoke());
            _endPanel.SetActive(false);
        }

        public void Bind(Game game)
        {
            _game = game;
            _endPanel.SetActive(false);
            Refresh();
        }

        public void SetAuto(bool on)
        {
            _autoLabel.text = on ? "Bot: on" : "Bot: off";
            _autoLabel.color = on ? Accent : Color.white;
        }

        public void SetStatus(string text)
        {
            _status.text = text;
        }

        public void Refresh()
        {
            if (_game == null) return;
            _title.text = _game.Level.Name;
            _moves.text = _game.MovesLeft + " moves";
            var sb = new StringBuilder();
            foreach (var g in _game.Goals)
            {
                if (sb.Length > 0) sb.Append("    ");
                sb.Append(GoalLabel(g.Goal)).Append(' ').Append(g.IsComplete ? "✓" : g.Remaining.ToString());
            }
            _goals.text = sb.ToString();

            if (_game.Status != GameStatus.Playing)
            {
                _endPanel.SetActive(true);
                _endTitle.text = _game.Status == GameStatus.Won ? "Level complete" : "Out of moves";
                _endTitle.color = _game.Status == GameStatus.Won ? Accent : new Color(0.95f, 0.55f, 0.5f);
            }
        }

        private static string GoalLabel(Goal g)
        {
            switch (g.Kind)
            {
                case GoalKind.Color: return ColorName(g.Color);
                case GoalKind.Grass: return "grass";
                case GoalKind.Ice: return "ice";
                case GoalKind.Box: return "boxes";
                default: return g.Kind.ToString();
            }
        }

        private static string ColorName(byte c)
        {
            switch (c)
            {
                case 0: return "red";
                case 1: return "blue";
                case 2: return "green";
                case 3: return "yellow";
                case 4: return "purple";
                default: return "orange";
            }
        }

        // ---- builders ----

        private static GameObject Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text Label(Transform parent, string name, int size, TextAnchor anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Text Button(Transform parent, string label, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = ButtonFace;
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => onClick());
            var text = Label(go.transform, "Text", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.text = label;
            return text;
        }
    }
}
