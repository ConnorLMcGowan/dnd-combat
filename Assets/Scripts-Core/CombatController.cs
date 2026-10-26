using System;
using System.Collections.Generic;

namespace DnDCombat.Core
{
    public enum MoveFailure { None, CombatOver, NotYourTurn, CreatureDefeated, NotOnGrid, CannotReach, NoMovementLeft, AlreadyThere, UnexpectedError }
    public enum AttackFailure    { None, CombatOver, NotYourTurn, AttackerDefeated, InvalidTarget, TargetDefeated, NoActionLeft, NotOnGrid, OutOfReach }

    public readonly struct AttackOutcome
    {
        public bool Success { get; }
        public AttackFailure Failure { get; }
        public AttackResult Result { get; }   // default when it failed

        public AttackOutcome(bool success, AttackFailure failure, AttackResult result)
        {
            Success = success;
            Failure = failure;
            Result = result;
        }
    }

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
        private readonly AttackResolver _resolver;
        public event Action<AttackReport> AttackResolved;

        public CombatController(TurnManager turns, Grid grid, AttackResolver resolver)
        {
            Turns = turns;
            Grid = grid;
            _resolver = resolver;
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

        public AttackOutcome TryAttack(Combatant attacker, Combatant target, AttackProfile attack)
        {
            // 1. Check if attack is valid
            if (Turns.State != CombatState.Ongoing)
            {
                return FailAttack(AttackFailure.CombatOver);
            }
            if (attacker == null || attacker != Turns.Current)
            {
                return FailAttack(AttackFailure.NotYourTurn);
            }
            if (attacker.Creature.IsDefeated)
            {
                return FailAttack(AttackFailure.AttackerDefeated);
            }
            if (target == null || target.Team == attacker.Team)
            {
                return FailAttack(AttackFailure.InvalidTarget);
            }
            if (target.Creature.IsDefeated)
            {
                return FailAttack(AttackFailure.TargetDefeated);
            }
            if (!attacker.ActionAvailable && attacker.AttacksRemaining <= 0)
            {
                return FailAttack(AttackFailure.NoActionLeft);
            }
            if (!Grid.TryGetPosition(attacker, out GridPosition attackerPosition) ||
                !Grid.TryGetPosition(target, out GridPosition targetPosition))
            {
                return FailAttack(AttackFailure.NotOnGrid);
            }

            if (Grid.DistanceInSquares(attackerPosition, targetPosition) > attack.RangeSquares)
            {
                return FailAttack(AttackFailure.OutOfReach);
            }

            // 2. Resolve the attack
            AttackResult result = _resolver.Resolve(
                                  attacker.Creature, 
                                  target.Creature, 
                                  attack.AttackBonus, 
                                  attack.DamageDiceCount, 
                                  attack.DamageSides, 
                                  attack.DamageBonus,
                                  attack.DamageType);
            // 3. Spend an attack
            if (attacker.AttacksRemaining > 0)
            {
                attacker.AttacksRemaining--;          // continuing an Attack action already started
            }
            else
            {
                attacker.ActionAvailable = false;     // starting the Attack action
                attacker.AttacksRemaining = attacker.Creature.AttacksPerAction - 1;
            }
            // 4. Announce the attack (HP is read now, so each report keeps its own value)
            AttackReport report = new AttackReport(attacker, target, result,
                                                   target.Creature.CurrentHP,
                                                   target.Creature.IsDefeated);
            AttackResolved?.Invoke(report);
            // 5. Check whether that ended the fight
            Turns.CheckCombatEnd();
            // 6. Return a successful outcome
            return new AttackOutcome(true, AttackFailure.None, result);
        }

        public MoveResult TryMove(Combatant mover, GridPosition destination) 
        {
            if(Turns.State != CombatState.Ongoing)
            {
                return FailMove(MoveFailure.CombatOver);
            }
            if(mover == null || mover != Turns.Current)
            {
                return FailMove(MoveFailure.NotYourTurn);
            }
            if(mover.Creature.IsDefeated)
            {
                return FailMove(MoveFailure.CreatureDefeated);
            }

            if (!Grid.TryGetPosition(mover, out GridPosition position))
            {
                return FailMove(MoveFailure.NotOnGrid);
            }
            if (position == destination)
            {
                return FailMove(MoveFailure.AlreadyThere);
            }
            if (mover.MovementRemaining < Pathfinder.FeetPerSquare)
            {
                return FailMove(MoveFailure.NoMovementLeft);
            }

            MovementRange range = Pathfinder.FindReachable(Grid, mover, mover.MovementRemaining);
            List<GridPosition> path = range.PathTo(destination);

            if (path.Count == 0)
            {
                return FailMove(MoveFailure.CannotReach);
            }

            int cost = path.Count * Pathfinder.FeetPerSquare;

            if (!Grid.MoveTo(mover, destination))
            {
                return FailMove(MoveFailure.UnexpectedError);
            }

            // spend the movement here
            mover.MovementRemaining = mover.MovementRemaining - cost;

            // return the success result here
            return new MoveResult(true, MoveFailure.None, path, cost);

        }
        public void EndTurn()
        {
            Turns.EndTurn();
        }
        private static AttackOutcome FailAttack(AttackFailure reason)
        {
            return new AttackOutcome(false, reason, default);
        }
        private static MoveResult FailMove(MoveFailure reason)
        {
            return new MoveResult(false, reason, new List<GridPosition>(), 0);

        }
    }
}