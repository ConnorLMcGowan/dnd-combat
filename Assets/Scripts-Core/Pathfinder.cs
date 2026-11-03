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

            // How many diagonal steps the chosen route to each square uses. Only used to break
            // ties between equally short routes, so straight lines win over zig-zags.
            Dictionary<GridPosition, int> diagonals = new Dictionary<GridPosition, int>();

            // Bad input gives an empty range instead of an error
            if (grid == null || mover == null) return new MovementRange(default, steps, cameFrom);
            if (!grid.TryGetPosition(mover, out GridPosition origin)) return new MovementRange(default, steps, cameFrom);

            int budget = Math.Max(0, movementFeet) / FeetPerSquare;

            steps[origin] = 0;
            diagonals[origin] = 0;
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
                    int nextSteps = currentSteps + 1;
                    int nextDiagonals = diagonals[current] + (IsDiagonal(current, next) ? 1 : 0);

                    if (steps.ContainsKey(next))
                    {
                        // Already found a way here. If this way is just as short but uses fewer
                        // diagonals, it's straighter, so remember it instead. The step count
                        // doesn't change, so the square is already queued and stays put.
                        if (steps[next] == nextSteps && nextDiagonals < diagonals[next])
                        {
                            cameFrom[next] = current;
                            diagonals[next] = nextDiagonals;
                        }
                        continue;
                    }

                    steps[next] = nextSteps;
                    cameFrom[next] = current;
                    diagonals[next] = nextDiagonals;
                    queue.Enqueue(next);
                }
            }

            return new MovementRange(origin, steps, cameFrom);
        }

        /////////////////////////////////
        // ---------- Helpers ---------- //
        /////////////////////////////////

        // A step that changes both X and Z is diagonal. Stepping up or down doesn't count.
        private static bool IsDiagonal(GridPosition from, GridPosition to)
        {
            return from.X != to.X && from.Z != to.Z;
        }
    }
}