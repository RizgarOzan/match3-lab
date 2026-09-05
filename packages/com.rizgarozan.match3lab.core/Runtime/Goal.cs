using System;

namespace Match3Lab.Core
{
    public enum GoalKind : byte
    {
        /// <summary>Clear N pieces of a given colour.</summary>
        Color,
        /// <summary>Clear N grass layers.</summary>
        Grass,
        /// <summary>Destroy N box hit points.</summary>
        Box,
        /// <summary>Clear N ice layers.</summary>
        Ice,
    }

    /// <summary>A level objective. <see cref="Color"/> is only meaningful for <see cref="GoalKind.Color"/>.</summary>
    public readonly struct Goal : IEquatable<Goal>
    {
        public readonly GoalKind Kind;
        public readonly byte Color;
        public readonly int Target;

        public Goal(GoalKind kind, byte color, int target)
        {
            if (target <= 0) throw new ArgumentOutOfRangeException(nameof(target), "A goal needs a positive target.");
            Kind = kind;
            Color = kind == GoalKind.Color ? color : (byte)0;
            Target = target;
        }

        public static Goal CollectColor(byte color, int target) => new Goal(GoalKind.Color, color, target);
        public static Goal ClearGrass(int target) => new Goal(GoalKind.Grass, 0, target);
        public static Goal ClearBoxes(int target) => new Goal(GoalKind.Box, 0, target);
        public static Goal ClearIce(int target) => new Goal(GoalKind.Ice, 0, target);

        public bool Equals(Goal other) => Kind == other.Kind && Color == other.Color && Target == other.Target;
        public override bool Equals(object obj) => obj is Goal other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Kind * 31 + Color) * 31 + Target);
        public override string ToString() =>
            Kind == GoalKind.Color ? "color " + Color + " x" + Target : Kind.ToString().ToLowerInvariant() + " x" + Target;
    }
}
