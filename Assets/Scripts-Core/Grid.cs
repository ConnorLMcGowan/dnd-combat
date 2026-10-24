using System;
using System.Collections.Generic;

namespace DnDCombat.Core
{
    public class Grid
    {
        public int Width { get; }    // X
        public int Height { get; }   // Y
        public int Depth { get; }    // Z

        private readonly Cell[,,] _cells;

        // The single source of truth for who stands where
        private readonly Dictionary<Combatant, GridPosition> _positions = new Dictionary<Combatant, GridPosition>();

        public Grid(int width, int height, int depth)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Depth = Math.Max(1, depth);
            _cells = new Cell[Width, Height, Depth];
        }

        ///////////////////////////////////
        // ---------- Terrain ---------- //
        ///////////////////////////////////

        public bool InBounds(GridPosition p)
        {
            return p.X >= 0 && p.X < Width
                && p.Y >= 0 && p.Y < Height
                && p.Z >= 0 && p.Z < Depth;
        }

        // Anything outside the grid counts as solid: the floor, the edges and the ceiling
        public bool IsSolid(GridPosition p)
        {
            if (!InBounds(p)) return true;
            return _cells[p.X, p.Y, p.Z].Solid;
        }

        // Returns false if out of bounds, or if you try to fill a cell somebody is standing in
        public bool SetSolid(GridPosition p, bool solid)
        {
            if (!InBounds(p)) return false;
            if (solid && GetOccupant(p) != null) return false;

            _cells[p.X, p.Y, p.Z].Solid = solid;
            return true;
        }

        // A cell is standable if it's open and the cell below it is solid.
        // Because the floor outside the grid is solid, the Y = 0 layer is ground level.
        public bool IsStandable(GridPosition p)
        {
            if (!InBounds(p) || IsSolid(p)) return false;
            return IsSolid(new GridPosition(p.X, p.Y - 1, p.Z));
        }

        /////////////////////////////////////
        // ---------- Placement ---------- //
        /////////////////////////////////////

        public bool IsFree(GridPosition p)
        {
            return IsStandable(p) && GetOccupant(p) == null;
        }

        // First-time placement. False if illegal, or if this combatant is already on the grid.
        public bool Place(Combatant combatant, GridPosition p)
        {
            if (combatant == null || _positions.ContainsKey(combatant) || !IsFree(p)) return false;

            _positions[combatant] = p;
            return true;
        }

        // Relocates a combatant who is already on the grid. Moving to your own square counts as illegal.
        public bool MoveTo(Combatant combatant, GridPosition p)
        {
            if (combatant == null || !_positions.ContainsKey(combatant) || !IsFree(p)) return false;

            _positions[combatant] = p;
            return true;
        }

        public bool Remove(Combatant combatant)
        {
            return combatant != null && _positions.Remove(combatant);
        }

        public bool TryGetPosition(Combatant combatant, out GridPosition position)
        {
            position = default;
            return combatant != null && _positions.TryGetValue(combatant, out position);
        }

        // Null if nobody is there. Arenas are small, so scanning is fine.
        public Combatant GetOccupant(GridPosition p)
        {
            foreach (KeyValuePair<Combatant, GridPosition> entry in _positions)
            {
                if (entry.Value == p) return entry.Key;
            }
            return null;
        }

        ////////////////////////////////////
        // ---------- Distance ---------- //
        ////////////////////////////////////

        // Every step, including diagonals and vertical ones, counts as one square (5 ft)
        public static int DistanceInSquares(GridPosition a, GridPosition b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            int dz = Math.Abs(a.Z - b.Z);
            return Math.Max(dx, Math.Max(dy, dz));
        }

        // Within one square in any direction. A square is not adjacent to itself.
        public static bool IsAdjacent(GridPosition a, GridPosition b)
        {
            return DistanceInSquares(a, b) == 1;
        }

        ///////////////////////////////////
        // ---------- Movement ---------- //
        ///////////////////////////////////

        // The biggest height change a creature can walk across, in cells (1 cell = 5 ft)
        public const int MaxStepUpCells = 1;
        public const int MaxStepDownCells = 1;

        // Every square you can step to from here in one move
        public IEnumerable<GridPosition> GetMoves(GridPosition from)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    // No cutting corners: both squares beside a diagonal step must be open
                    if (dx != 0 && dz != 0)
                    {
                        if (IsSolid(new GridPosition(from.X + dx, from.Y, from.Z))) continue;
                        if (IsSolid(new GridPosition(from.X, from.Y, from.Z + dz))) continue;
                    }

                    int nx = from.X + dx;
                    int nz = from.Z + dz;

                    // Flat step
                    GridPosition flat = new GridPosition(nx, from.Y, nz);
                    if (IsFree(flat))
                    {
                        yield return flat;
                        continue;
                    }

                    GridPosition destination;

                    // Step up onto a block
                    if (TryStepUp(from, nx, nz, out destination))
                    {
                        yield return destination;
                        continue;
                    }

                    // Step down off a block
                    if (TryStepDown(from, nx, nz, out destination))
                    {
                        yield return destination;
                    }
                }
            }
        }

        private bool TryStepUp(GridPosition from, int nx, int nz, out GridPosition destination)
        {
            for (int h = 1; h <= MaxStepUpCells; h++)
            {
                // Headroom: the cell above your head must be open or you'd bump the ceiling
                if (IsSolid(new GridPosition(from.X, from.Y + h, from.Z))) break;

                GridPosition candidate = new GridPosition(nx, from.Y + h, nz);
                if (IsFree(candidate))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = default;
            return false;
        }

        private bool TryStepDown(GridPosition from, int nx, int nz, out GridPosition destination)
        {
            for (int h = 1; h <= MaxStepDownCells; h++)
            {
                // The cell beside you at head height must be open, or you'd be walking into rock
                if (IsSolid(new GridPosition(nx, from.Y - h + 1, nz))) break;

                GridPosition candidate = new GridPosition(nx, from.Y - h, nz);
                if (IsFree(candidate))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = default;
            return false;
        }
    }
}