using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Match3Lab.Core
{
    /// <summary>
    /// The level text format. Chosen over JSON so that a level is readable in a diff, editable in
    /// any text editor, and parseable identically in Unity and in .NET without a JSON library.
    ///
    /// <code>
    /// # comment
    /// name Tutorial 1
    /// size 7 8          # width height
    /// moves 20
    /// colors 5
    /// goal color 0 15   # kind [color] target
    /// goal grass 10
    /// grid
    /// . . . . . . .
    /// . .g .g . . . .
    /// </code>
    ///
    /// Grid rows are listed top row first, one space-separated token per cell:
    /// base character <c>.</c> random piece, <c>-</c> empty, <c>#</c> hole, <c>0</c>–<c>5</c> fixed colour,
    /// <c>b</c>/<c>B</c> box with 1/2 hit points; optional modifiers <c>g</c>/<c>G</c> one/two grass layers,
    /// <c>i</c>/<c>I</c> one/two ice layers. Holes and boxes take no modifiers.
    /// </summary>
    public static class LevelText
    {
        public static LevelDefinition Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var level = new LevelDefinition();
            var rows = new List<CellSpec[]>();
            bool inGrid = false;
            bool sawSize = false;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0) continue;

                if (inGrid)
                {
                    rows.Add(ParseRow(line, lineNo));
                    continue;
                }

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string key = parts[0].ToLowerInvariant();
                switch (key)
                {
                    case "name":
                        level.Name = line.Substring(parts[0].Length).Trim();
                        break;
                    case "size":
                        if (parts.Length != 3) throw new LevelFormatException("size needs two numbers: size <width> <height>", lineNo);
                        level.Width = ParseInt(parts[1], lineNo, "width");
                        level.Height = ParseInt(parts[2], lineNo, "height");
                        sawSize = true;
                        break;
                    case "moves":
                        if (parts.Length != 2) throw new LevelFormatException("moves needs one number", lineNo);
                        level.Moves = ParseInt(parts[1], lineNo, "moves");
                        break;
                    case "colors":
                        if (parts.Length != 2) throw new LevelFormatException("colors needs one number", lineNo);
                        level.ColorCount = ParseInt(parts[1], lineNo, "colors");
                        break;
                    case "goal":
                        level.Goals.Add(ParseGoal(parts, lineNo));
                        break;
                    case "grid":
                        if (!sawSize) throw new LevelFormatException("size must come before grid", lineNo);
                        inGrid = true;
                        break;
                    default:
                        throw new LevelFormatException("unknown key '" + parts[0] + "'", lineNo);
                }
            }

            if (!sawSize) throw new LevelFormatException("missing 'size'");
            if (!inGrid) throw new LevelFormatException("missing 'grid'");
            if (rows.Count != level.Height)
                throw new LevelFormatException("grid has " + rows.Count + " rows but size says " + level.Height);

            level.Cells = new CellSpec[level.Width * level.Height];
            for (int y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != level.Width)
                    throw new LevelFormatException("grid row " + (y + 1) + " has " + rows[y].Length + " cells but size says " + level.Width);
                Array.Copy(rows[y], 0, level.Cells, y * level.Width, level.Width);
            }

            level.EnsureValid();
            return level;
        }

        public static string Write(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(level.Name)) sb.Append("name ").Append(level.Name).Append('\n');
            sb.Append("size ").Append(level.Width).Append(' ').Append(level.Height).Append('\n');
            sb.Append("moves ").Append(level.Moves).Append('\n');
            sb.Append("colors ").Append(level.ColorCount).Append('\n');
            foreach (var g in level.Goals)
            {
                sb.Append("goal ");
                switch (g.Kind)
                {
                    case GoalKind.Color: sb.Append("color ").Append(g.Color); break;
                    case GoalKind.Grass: sb.Append("grass"); break;
                    case GoalKind.Box: sb.Append("box"); break;
                    case GoalKind.Ice: sb.Append("ice"); break;
                }
                sb.Append(' ').Append(g.Target).Append('\n');
            }
            sb.Append("grid\n");
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    if (x > 0) sb.Append(' ');
                    sb.Append(SpecToken(level.GetCell(x, y)));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public static string SpecToken(CellSpec c)
        {
            switch (c.Kind)
            {
                case CellSpecKind.Hole: return "#";
                case CellSpecKind.Box: return c.BoxHitPoints >= 2 ? "B" : "b";
            }
            var sb = new StringBuilder(3);
            switch (c.Kind)
            {
                case CellSpecKind.Empty: sb.Append('-'); break;
                case CellSpecKind.Random: sb.Append('.'); break;
                case CellSpecKind.FixedColor: sb.Append((char)('0' + c.Color)); break;
            }
            if (c.Grass == 1) sb.Append('g');
            else if (c.Grass >= 2) sb.Append('G');
            if (c.Ice == 1) sb.Append('i');
            else if (c.Ice >= 2) sb.Append('I');
            return sb.ToString();
        }

        /// <summary>Token for a live cell, using the same alphabet plus the special-piece letters
        /// <c>h</c>/<c>v</c> (rockets), <c>o</c> (bomb) and <c>*</c> (rainbow). For dumps and tests.</summary>
        public static string CellToken(Cell c)
        {
            if (c.Hole) return "#";
            if (c.Box > 0) return c.Box >= 2 ? "B" : "b";
            var sb = new StringBuilder(3);
            switch (c.Piece.Type)
            {
                case PieceType.Empty: sb.Append('-'); break;
                case PieceType.Normal: sb.Append((char)('0' + c.Piece.Color)); break;
                case PieceType.RocketH: sb.Append('h'); break;
                case PieceType.RocketV: sb.Append('v'); break;
                case PieceType.Bomb: sb.Append('o'); break;
                case PieceType.Rainbow: sb.Append('*'); break;
            }
            if (c.Grass == 1) sb.Append('g');
            else if (c.Grass >= 2) sb.Append('G');
            if (c.Ice == 1) sb.Append('i');
            else if (c.Ice >= 2) sb.Append('I');
            return sb.ToString();
        }

        private static CellSpec[] ParseRow(string line, int lineNo)
        {
            string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var row = new CellSpec[tokens.Length];
            for (int x = 0; x < tokens.Length; x++) row[x] = ParseToken(tokens[x], lineNo);
            return row;
        }

        private static CellSpec ParseToken(string token, int lineNo)
        {
            char b = token[0];
            CellSpecKind kind;
            byte color = 0, boxHp = 0;
            switch (b)
            {
                case '#': kind = CellSpecKind.Hole; break;
                case '-': kind = CellSpecKind.Empty; break;
                case '.': kind = CellSpecKind.Random; break;
                case 'b': kind = CellSpecKind.Box; boxHp = 1; break;
                case 'B': kind = CellSpecKind.Box; boxHp = 2; break;
                default:
                    if (b >= '0' && b <= '9') { kind = CellSpecKind.FixedColor; color = (byte)(b - '0'); }
                    else throw new LevelFormatException("unknown cell token '" + token + "'", lineNo);
                    break;
            }

            byte ice = 0, grass = 0;
            for (int i = 1; i < token.Length; i++)
            {
                switch (token[i])
                {
                    case 'g': grass = 1; break;
                    case 'G': grass = 2; break;
                    case 'i': ice = 1; break;
                    case 'I': ice = 2; break;
                    default: throw new LevelFormatException("unknown modifier '" + token[i] + "' in '" + token + "'", lineNo);
                }
            }
            if ((kind == CellSpecKind.Hole || kind == CellSpecKind.Box) && token.Length > 1)
                throw new LevelFormatException("'" + b + "' takes no modifiers, got '" + token + "'", lineNo);

            return new CellSpec(kind, color, boxHp, ice, grass);
        }

        private static Goal ParseGoal(string[] parts, int lineNo)
        {
            if (parts.Length < 3) throw new LevelFormatException("goal needs a kind and a target", lineNo);
            string kind = parts[1].ToLowerInvariant();
            switch (kind)
            {
                case "color":
                    if (parts.Length != 4) throw new LevelFormatException("goal color needs: goal color <color> <target>", lineNo);
                    return Goal.CollectColor((byte)ParseInt(parts[2], lineNo, "color"), ParseInt(parts[3], lineNo, "target"));
                case "grass": return Goal.ClearGrass(ParseInt(parts[2], lineNo, "target"));
                case "box": return Goal.ClearBoxes(ParseInt(parts[2], lineNo, "target"));
                case "ice": return Goal.ClearIce(ParseInt(parts[2], lineNo, "target"));
                default: throw new LevelFormatException("unknown goal kind '" + parts[1] + "'", lineNo);
            }
        }

        private static int ParseInt(string s, int lineNo, string what)
        {
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                throw new LevelFormatException(what + " must be an integer, got '" + s + "'", lineNo);
            return v;
        }

        private static string StripComment(string line)
        {
            int hash = line.IndexOf('#');
            if (hash < 0) return line;
            // '#' is also the hole token inside the grid; a comment must be preceded by whitespace
            // or start the line, while a hole token is followed by whitespace or end of line.
            // Rule: '#' starts a comment only when followed by a space (or is "# ..." at line start).
            for (int i = hash; i >= 0; i = line.IndexOf('#', i + 1))
            {
                bool startsLine = i == 0 || char.IsWhiteSpace(line[i - 1]);
                bool followedBySpace = i + 1 < line.Length && line[i + 1] == ' ';
                bool holeToken = startsLine && (i + 1 >= line.Length || line[i + 1] == ' ' || line[i + 1] == '\t') && LooksLikeGridRow(line);
                if (startsLine && followedBySpace && !holeToken) return line.Substring(0, i);
                if (i + 1 >= line.Length) break;
            }
            return line;
        }

        private static bool LooksLikeGridRow(string line)
        {
            // A grid row consists solely of cell tokens; a comment line starts with "# " followed by prose.
            foreach (var t in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                char b = t[0];
                bool baseOk = b == '#' || b == '-' || b == '.' || b == 'b' || b == 'B' || (b >= '0' && b <= '9');
                if (!baseOk) return false;
                for (int i = 1; i < t.Length; i++)
                    if ("gGiI".IndexOf(t[i]) < 0) return false;
            }
            return true;
        }
    }
}
