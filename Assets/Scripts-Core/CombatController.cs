using System;
using System.Collections.Generic;

namespace DnDCombat.Core
{
    public enum MoveFailure { None, CombatOver, NotYourTurn, CreatureDefeated, NotOnGrid, CannotReach, NoMovementLeft, AlreadyThere, UnexpectedError }

    // Says what happened, so the UI can animate the path or explain a refusal
    public readonly struct MoveResult
    {
        public bool Success { get; }
        public MoveFailure Failure { get; }
        public IReadOnlyList<GridPosition> Path { get; }   // empty on failure
        public int FeetSpent { get; }

        public MoveResult(bool success, MoveFailure failure, IReadOnlyList<GridPosition> path, int feetSpent)
        {
            Success = success;
            Failure = failure;
            Path = path;
            FeetSpent = feetSpent;
        }
    }

    public class CombatController
    {
        public TurnManager Turns { get; }
        public Grid Grid { get; }

        public CombatController(TurnManager turns, Grid grid)
        {
            Turns = turns;
            Grid = grid;
        }

        /////////////////////////////////
        // ---------- Queries ---------- //
        /////////////////////////////////

        // The squares this combatant could walk to right now (the UI highlights these)
        public MovementRange GetMovementRange(Combatant combatant) 
        {
            bool canMove = Turns.State == CombatState.Ongoing 
                                          && combatant != null 
                                          && combatant == Turns.Current 
                                          && !combatant.Creature.IsDefeated;
            if(!canMove)
            {
                return Pathfinder.FindReachable(Grid, null, 0);
            }

            return Pathfinder.FindReachable(Grid, combatant, combatant.MovementRemaining);
        }

        /////////////////////////////////
        // ---------- Actions ---------- //
        /////////////////////////////////

        public MoveResult TryMove(Combatant mover, GridPosition destination) 
        {
            if(Turns.State != CombatState.Ongoing)
            {
                return Fail(MoveFailure.CombatOver);
            }
            if(mover == null || mover != Turns.Current)
            {
                return Fail(MoveFailure.NotYourTurn);
            }
            if(mover.Creature.IsDefeated == true)
            {
                return Fail(MoveFailure.CreatureDefeated);
            }

            if (!Grid.TryGetPosition(mover, out GridPosition position))
            {
                return Fail(MoveFailure.NotOnGrid);
            }
            if (position == destination)
            {
                return Fail(MoveFailure.AlreadyThere);
            }
            if (mover.MovementRemaining < Pathfinder.FeetPerSquare)
            {
                return Fail(MoveFailure.NoMovementLeft);
            }

            MovementRange range = Pathfinder.FindReachable(Grid, mover, mover.MovementRemaining);
            List<GridPosition> path = range.PathTo(destination);

            if (path.Count == 0)
            {
                return Fail(MoveFailure.CannotReach);
            }

            int cost = path.Count * Pathfinder.FeetPerSquare;

            if (!Grid.MoveTo(mover, destination))
            {
                return Fail(MoveFailure.UnexpectedError);
            }

            // spend the movement here
            mover.MovementRemaining = mover.MovementRemaining - cost;

            // return the success result here
            return new MoveResult(true, MoveFailure.None, path, cost);

        }

        private static MoveResult Fail(MoveFailure reason)
        {
            return new MoveResult(false, reason, new List<GridPosition>(), 0);

        }
    }
}