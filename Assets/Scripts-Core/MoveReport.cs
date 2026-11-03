using System.Collections.Generic;

namespace DnDCombat.Core
{
    // A snapshot of one move, taken the moment it happened, so the scene can show it:
    // snap (or later, walk) the body along the path and turn it the way it went
    public readonly struct MoveReport
    {
        public Combatant Mover { get; }
        public GridPosition From { get; }                  // where the move started
        public IReadOnlyList<GridPosition> Path { get; }   // every square stepped on, in order; the last is where it ended
        public int FeetSpent { get; }

        public MoveReport(Combatant mover, GridPosition from, IReadOnlyList<GridPosition> path, int feetSpent)
        {
            Mover = mover;
            From = from;
            Path = path;
            FeetSpent = feetSpent;
        }

        // Where the move ended
        public GridPosition To => Path.Count > 0 ? Path[Path.Count - 1] : From;

        // The square stepped from on the last step: the way the mover was heading as it arrived
        public GridPosition LastStepFrom => Path.Count > 1 ? Path[Path.Count - 2] : From;
    }
}
