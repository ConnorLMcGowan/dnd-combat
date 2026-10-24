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
    }
}