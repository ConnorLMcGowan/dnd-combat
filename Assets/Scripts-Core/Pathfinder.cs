using System;
using System.Collections.Generic;

namespace DnDCombat.Core
{
    public static class Pathfinder
    {
        public const int FeetPerSquare = 5;

        // Finds every square the mover can walk to with the given movement (in feet)
        public static MovementRange FindReachable(Grid grid, Combatant mover, int movementFeet)
        {
            Dictionary<GridPosition, int> steps = new Dictionary<GridPosition, int>();
            Dictionary<GridPosition, GridPosition> cameFrom = new Dictionary<GridPosition, GridPosition>();

            // Bad input gives an empty range instead of an error
            if (grid == null || mover == null) return new MovementRange(default, steps, cameFrom);
            if (!grid.TryGetPosition(mover, out GridPosition origin)) return new MovementRange(default, steps, cameFrom);

            int budget = Math.Max(0, movementFeet) / FeetPerSquare;

            steps[origin] = 0;
            Queue<GridPosition> queue = new Queue<GridPosition>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                GridPosition current = queue.Dequeue();
                int currentSteps = steps[current];

                // Out of movement: don't spread any further from here
                if (currentSteps >= budget) continue;

                foreach (GridPosition next in grid.GetMoves(current))
                {
                    if (steps.ContainsKey(next)) continue;   // already found a way here

                    steps[next] = currentSteps + 1;
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            return new MovementRange(origin, steps, cameFrom);
        }
    }
}