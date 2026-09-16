using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Match3Lab.Core.Simulation
{
    /// <summary>
    /// Proposes small layout mutations and keeps only those that move the win rate toward a target
    /// band. The move budget is left alone — that is <see cref="MoveBudgetTuner"/>'s dial. This one
    /// turns the dials a designer turns when the budget is already right: obstacle layers (always
    /// together with the matching goal, so the change actually matters), colour-goal sizes, the
    /// colour count and the shape of the board.
    ///
    /// Hill-climbing, not search: one mutation per step, accepted when it does not move the win rate
    /// further from the band. Equal-distance steps have to count: the win rate saturates, so a level
    /// the bot wins every time answers a single dial-turn with the same 100% whichever dial moved, and
    /// a climber that demands a strict improvement never takes its first step. The direction is
    /// re-read from the current rate every step, so an equal step is still a step the right way — it
    /// has only not shown up in the measurement yet. The result therefore stays recognisably the
    /// designer's level. Every step is recorded, so the designer can read why the level ended up the
    /// way it did.
    /// </summary>
    public static class LevelEvolver
    {
        public sealed class Step
        {
            public int Index;
            public string Mutation;
            public double WinRate;
            public bool Accepted;
            public override string ToString() => (Accepted ? "keep  " : "drop  ") + Mutation + " → " + (WinRate * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        public sealed class Result
        {
            public LevelDefinition Original;
            public LevelDefinition Level;
            public double OriginalWinRate;
            public double WinRate;
            public bool InBand;
            public int Accepted;
            public readonly List<Step> Steps = new List<Step>();

            public string Summary()
            {
                var sb = new StringBuilder();
                sb.Append("  start ").Append((OriginalWinRate * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("%\n");
                foreach (var s in Steps) sb.Append("  ").Append(s).Append('\n');
                sb.Append(InBand ? "in band" : "not in band").Append(" at ").Append((WinRate * 100).ToString("F1", CultureInfo.InvariantCulture)).Append("% after ")
                  .Append(Steps.Count).Append(Steps.Count == 1 ? " step, " : " steps, ").Append(Accepted).Append(" kept");
                return sb.ToString();
            }
        }

        public static Result Evolve(LevelDefinition level, double bandLow, double bandHigh, Func<IBot> botFactory,
            SimulationOptions options = null, int maxSteps = 40, ulong seed = 1)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (botFactory == null) throw new ArgumentNullException(nameof(botFactory));
            if (bandLow < 0 || bandHigh > 1 || bandLow > bandHigh) throw new ArgumentOutOfRangeException(nameof(bandLow), "band must satisfy 0 <= low <= high <= 1");
            options = options ?? new SimulationOptions { Runs = 300 };
            level.EnsureValid();

            double Distance(double w) => w < bandLow ? bandLow - w : w > bandHigh ? w - bandHigh : 0;

            var rng = new Pcg32(seed, 0xE701E5);
            var current = level;
            double rate = Simulator.Run(current, botFactory, options).WinRate;
            var result = new Result { Original = level, Level = level, OriginalWinRate = rate, WinRate = rate, InBand = Distance(rate) == 0 };
            if (result.InBand) return result;

            for (int step = 1; step <= maxSteps; step++)
            {
                bool harder = rate > bandHigh;
                var candidate = Mutate(current, harder, rng, out string description);
                if (candidate == null) break; // nothing left to change in the needed direction

                double candidateRate = Simulator.Run(candidate, botFactory, options).WinRate;
                // Equal distance counts as an acceptance: near a saturated win rate every single
                // mutation measures the same, and only the accumulated ones ever move the metric.
                bool accepted = Distance(candidateRate) <= Distance(rate);
                result.Steps.Add(new Step { Index = step, Mutation = description, WinRate = candidateRate, Accepted = accepted });
                if (!accepted) continue;

                current = candidate;
                rate = candidateRate;
                result.Accepted++;
                if (Distance(rate) == 0) break;
            }

            result.Level = current;
            result.WinRate = rate;
            result.InBand = Distance(rate) == 0;
            return result;
        }

        /// <summary>One random, valid mutation in the requested direction, or null when none applies.</summary>
        public static LevelDefinition Mutate(LevelDefinition level, bool harder, Pcg32 rng, out string description)
        {
            // A handful of attempts: a chosen mutation kind may find no eligible cell, or produce an invalid level.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var copy = Copy(level);
                string what = ApplyRandomMutation(copy, harder, rng);
                if (what == null) continue;
                if (copy.Validate().Count > 0) continue;
                description = what;
                return copy;
            }
            description = null;
            return null;
        }

        private static string ApplyRandomMutation(LevelDefinition level, bool harder, Pcg32 rng)
        {
            // Weighted pick among the six kinds; the layer mutations are only offered when the level has the matching goal.
            int hasGrass = GoalIndex(level, GoalKind.Grass) >= 0 ? 3 : 0;
            int hasIce = GoalIndex(level, GoalKind.Ice) >= 0 ? 3 : 0;
            int hasBox = GoalIndex(level, GoalKind.Box) >= 0 ? 3 : 0;
            int hasColor = GoalIndex(level, GoalKind.Color) >= 0 ? 3 : 0;
            const int holes = 1, colors = 1;
            int total = hasGrass + hasIce + hasBox + hasColor + holes + colors;
            int pick = rng.NextInt(total);

            if ((pick -= hasGrass) < 0) return MutateLayer(level, GoalKind.Grass, harder, rng);
            if ((pick -= hasIce) < 0) return MutateLayer(level, GoalKind.Ice, harder, rng);
            if ((pick -= hasBox) < 0) return MutateBox(level, harder, rng);
            if ((pick -= hasColor) < 0) return MutateColorGoal(level, harder, rng);
            if ((pick -= holes) < 0) return MutateHole(level, harder, rng);
            return MutateColorCount(level, harder);
        }

        // Grass or ice: add one layer and raise the goal by one (harder), or the reverse (easier).
        private static string MutateLayer(LevelDefinition level, GoalKind kind, bool harder, Pcg32 rng)
        {
            var eligible = new List<int>();
            for (int i = 0; i < level.Cells.Length; i++)
            {
                var c = level.Cells[i];
                bool canHold = kind == GoalKind.Grass
                    ? c.Kind == CellSpecKind.Random || c.Kind == CellSpecKind.FixedColor || c.Kind == CellSpecKind.Empty
                    : c.Kind == CellSpecKind.Random || c.Kind == CellSpecKind.FixedColor;
                if (!canHold) continue;
                byte layers = kind == GoalKind.Grass ? c.Grass : c.Ice;
                if (harder ? layers < 2 : layers > 0) eligible.Add(i);
            }
            if (eligible.Count == 0) return null;

            int gi = GoalIndex(level, kind);
            var goal = level.Goals[gi];
            int target = goal.Target + (harder ? 1 : -1);
            if (target < 1) return null;

            int idx = eligible[rng.NextInt(eligible.Count)];
            var cell = level.Cells[idx];
            int delta = harder ? 1 : -1;
            level.Cells[idx] = kind == GoalKind.Grass
                ? new CellSpec(cell.Kind, cell.Color, cell.BoxHitPoints, cell.Ice, (byte)(cell.Grass + delta))
                : new CellSpec(cell.Kind, cell.Color, cell.BoxHitPoints, (byte)(cell.Ice + delta), cell.Grass);
            level.Goals[gi] = new Goal(kind, 0, target);
            return (kind == GoalKind.Grass ? "grass " : "ice ") + (harder ? "+1" : "-1") + " at " + Pos(level, idx) + ", goal " + goal.Target + "→" + target;
        }

        // Boxes: turn a plain cell into a 1-hp box (or a 1-hp box into 2 hp) and raise the goal; or the reverse.
        private static string MutateBox(LevelDefinition level, bool harder, Pcg32 rng)
        {
            int gi = GoalIndex(level, GoalKind.Box);
            var goal = level.Goals[gi];
            var eligible = new List<int>();
            for (int i = 0; i < level.Cells.Length; i++)
            {
                var c = level.Cells[i];
                if (harder)
                {
                    if (c.Kind == CellSpecKind.Random && c.Grass == 0 && c.Ice == 0) eligible.Add(i);
                    else if (c.Kind == CellSpecKind.Box && c.BoxHitPoints == 1) eligible.Add(i);
                }
                else if (c.Kind == CellSpecKind.Box) eligible.Add(i);
            }
            if (eligible.Count == 0) return null;
            int target = goal.Target + (harder ? 1 : -1);
            if (target < 1) return null;

            int idx = eligible[rng.NextInt(eligible.Count)];
            var cell = level.Cells[idx];
            string what;
            if (harder)
            {
                if (cell.Kind == CellSpecKind.Box) { level.Cells[idx] = new CellSpec(CellSpecKind.Box, 0, 2); what = "box 1→2 hp"; }
                else { level.Cells[idx] = new CellSpec(CellSpecKind.Box, 0, 1); what = "box added"; }
            }
            else
            {
                if (cell.BoxHitPoints >= 2) { level.Cells[idx] = new CellSpec(CellSpecKind.Box, 0, 1); what = "box 2→1 hp"; }
                else { level.Cells[idx] = CellSpec.Random; what = "box removed"; }
            }
            level.Goals[gi] = Goal.ClearBoxes(target);
            return what + " at " + Pos(level, idx) + ", goal " + goal.Target + "→" + target;
        }

        // Colour goals: the target moves by roughly an eighth of itself, at least one.
        private static string MutateColorGoal(LevelDefinition level, bool harder, Pcg32 rng)
        {
            var indices = new List<int>();
            for (int i = 0; i < level.Goals.Count; i++) if (level.Goals[i].Kind == GoalKind.Color) indices.Add(i);
            int gi = indices[rng.NextInt(indices.Count)];
            var goal = level.Goals[gi];
            int delta = Math.Max(1, goal.Target / 8);
            int target = goal.Target + (harder ? delta : -delta);
            if (target < 1) return null;
            level.Goals[gi] = Goal.CollectColor(goal.Color, target);
            return "color " + goal.Color + " goal " + goal.Target + "→" + target;
        }

        // Shape: carve a hole into a plain edge cell (harder), or fill a hole that touches at least two playable cells (easier).
        private static string MutateHole(LevelDefinition level, bool harder, Pcg32 rng)
        {
            var eligible = new List<int>();
            int w = level.Width, h = level.Height;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = level.Cells[y * w + x];
                if (harder)
                {
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    if (edge && c.Kind == CellSpecKind.Random && c.Grass == 0 && c.Ice == 0) eligible.Add(y * w + x);
                }
                else if (c.Kind == CellSpecKind.Hole && PlayableNeighbours(level, x, y) >= 2) eligible.Add(y * w + x);
            }
            if (eligible.Count == 0) return null;
            int idx = eligible[rng.NextInt(eligible.Count)];
            level.Cells[idx] = harder ? CellSpec.Hole : CellSpec.Random;
            return (harder ? "hole carved" : "hole filled") + " at " + Pos(level, idx);
        }

        private static string MutateColorCount(LevelDefinition level, bool harder)
        {
            int count = level.ColorCount + (harder ? 1 : -1);
            if (count < LevelDefinition.MinColors || count > LevelDefinition.MaxColors) return null;
            string what = "colors " + level.ColorCount + "→" + count;
            level.ColorCount = count; // fixed cells or goals above the new count make the level invalid → rejected by the caller
            return what;
        }

        private static int PlayableNeighbours(LevelDefinition level, int x, int y)
        {
            int n = 0;
            if (x > 0 && level.GetCell(x - 1, y).Kind != CellSpecKind.Hole) n++;
            if (y > 0 && level.GetCell(x, y - 1).Kind != CellSpecKind.Hole) n++;
            if (x < level.Width - 1 && level.GetCell(x + 1, y).Kind != CellSpecKind.Hole) n++;
            if (y < level.Height - 1 && level.GetCell(x, y + 1).Kind != CellSpecKind.Hole) n++;
            return n;
        }

        private static int GoalIndex(LevelDefinition level, GoalKind kind)
        {
            for (int i = 0; i < level.Goals.Count; i++) if (level.Goals[i].Kind == kind) return i;
            return -1;
        }

        private static string Pos(LevelDefinition level, int index) => "(" + index % level.Width + "," + index / level.Width + ")";

        public static LevelDefinition Copy(LevelDefinition level)
        {
            return new LevelDefinition
            {
                Name = level.Name,
                Width = level.Width,
                Height = level.Height,
                Moves = level.Moves,
                ColorCount = level.ColorCount,
                Goals = new List<Goal>(level.Goals),
                Cells = (CellSpec[])level.Cells.Clone(),
            };
        }
    }
}
