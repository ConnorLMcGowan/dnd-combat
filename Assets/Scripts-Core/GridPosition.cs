using System;

namespace DnDCombat.Core
{
    // X and Z are the ground plane, Y is up (matches Unity)
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public GridPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }

        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}