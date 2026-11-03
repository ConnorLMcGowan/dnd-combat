using System.Collections.Generic;

namespace DnDCombat.Core
{
    // The result of a reachability search: where a creature can walk, and how to get there
    public class MovementRange
    {
        private readonly GridPosition _origin;
        private readonly Dictionary<GridPosition, int> _steps;
        private readonly Dictionary<GridPosition, GridPosition> _cameFrom;

        public MovementRange(GridPosition origin,
                             Dictionary<GridPosition, int> steps,
                             Dictionary<GridPosition, GridPosition> cameFrom)
        {
            _origin = origin;
            _steps = steps;
            _cameFrom = cameFrom;
        }

        /////////////////////////////////
        // ---------- Queries ---------- //
        /////////////////////////////////

        // True for the starting square too, since standing still is always possible
        public bool Contains(GridPosition position)
        {
            return _steps.ContainsKey(position);
        }

        // Number of 5 ft steps to get there, or -1 if it can't be reached
        public int StepsTo(GridPosition position)
        {
            return _steps.TryGetValue(position, out int steps) ? steps : -1;
        }

        // Every square you can walk to, not counting the one you're standing on.
        // This is the set the UI highlights.
        public IEnumerable<GridPosition> Destinations
        {
            get
            {
                foreach (KeyValuePair<GridPosition, int> entry in _steps)
                {
                    if (entry.Key != _origin) yield return entry.Key;
                }
            }
        }

        // The route from the start to the destination, in order. It excludes the starting
        // square and includes the destination. Empty if unreachable or already there.
        public List<GridPosition> PathTo(GridPosition destination)
        {
            List<GridPosition> path = new List<GridPosition>();

            if (!_steps.ContainsKey(destination) || destination == _origin) return path;

            GridPosition current = destination;
            while (current != _origin)
            {
                path.Add(current);
                current = _cameFrom[current];
            }

            path.Reverse();
            return path;
        }

        //////////////////////////////////////
        // ---------- Approaching ---------- //
        //////////////////////////////////////

        // The best square to stand on to attack a target at `target` with the given reach.
        // Returns the starting square when nothing beats it (for example, already in reach).
        // Callers check Grid.DistanceInSquares(result, target) <= reach to see if they can attack.
        public GridPosition BestApproach(GridPosition target, int reach)
        {
            GridPosition best = _origin;
            foreach (GridPosition square in Destinations)
            {
                if (IsBetter(square, best, target, reach))
                {
                    best = square;
                }
            }
            return best;
        }

        // True if square a is a better place to stand than square b
        private bool IsBetter(GridPosition a, GridPosition b, GridPosition target, int reach)
        {
            bool aInReach = Grid.DistanceInSquares(a, target) <= reach;
            bool bInReach = Grid.DistanceInSquares(b, target) <= reach;

            if (aInReach && !bInReach)
            {
                return true; // a is in range and b is not
            }
            if (!aInReach && bInReach)
            {
                return false; // b is in range and a is not
            }
            if (!aInReach)
            {
                int aDistance = Grid.DistanceInSquares(a, target);
                int bDistance = Grid.DistanceInSquares(b, target);

                if (aDistance < bDistance) { return true; }
                if (aDistance > bDistance) { return false; }
            }

            int aSteps = StepsTo(a);
            int bSteps = StepsTo(b);

            if (aSteps < bSteps)
            {
                return true;     // a is cheaper to reach
            }
            if (aSteps > bSteps)
            {
                return false;    // a is harder to reach
            }        

            int aLine = StraightLineSquared(a, target);
            int bLine = StraightLineSquared(b, target);
            if (aLine < bLine) { return true; }
            if (aLine > bLine) { return false; }           

            if (a.X < b.X) { return true; }
            if (a.X > b.X) { return false; }
            if (a.Z < b.Z) { return true; }
            if (a.Z > b.Z) { return false; }

            return false;
        }

        private static int StraightLineSquared(GridPosition from, GridPosition to)
        {
            int dx = from.X - to.X;
            int dy = from.Y - to.Y;
            int dz = from.Z - to.Z;
            return dx * dx + dy * dy + dz * dz;      // each gap times itself, all added together   
        }
    }
}