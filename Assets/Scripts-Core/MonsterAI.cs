namespace DnDCombat.Core
{
    public static class MonsterAI
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // "AI" here is just a short script the monster follows on its turn:
        //   look at the board, decide what to do, then do it using the same
        //   buttons a player uses (TryMove and TryAttack).
        // Because it only uses those, it can't cheat. An illegal move is
        //   refused exactly as it would be for a player.
        //
        // Your toolbox:
        //   Whose turn is it?            controller.Turns.Current
        //   Who is in the fight?         controller.Turns.Order
        //   Which side / alive?          combatant.Team, combatant.Creature.IsDefeated
        //   Where is someone?            controller.Grid.TryGetPosition(combatant, out GridPosition pos)
        //   Distance between squares     Grid.DistanceInSquares(a, b)
        //   Where can I walk this turn?  controller.GetMovementRange(monster).Destinations
        //   Steps to a square            range.StepsTo(square)
        //   Walk there                   controller.TryMove(monster, square)
        //   Attack                       controller.TryAttack(monster, target, attack)
        //   Did it work?                 .Success on either result
        //
        // Write the five steps below as short comments first, then fill each in.
        // Build and test one step at a time. Don't write the whole thing and hope.

        // Plays the monster's turn using the same methods a player would.
        // It does not end the turn; the caller does that, as it would for a player.
        public static void TakeTurn(CombatController controller, Combatant monster, AttackProfile attack)
        {
            /////////////////////////////////////////////////
            // ----------  Check for exceptions ---------- //
            /////////////////////////////////////////////////
            if (controller == null || monster == null)
                return;
            if (monster != controller.Turns.Current)
                return;
            if (monster.Creature.IsDefeated)
                return;
            if (!controller.Grid.TryGetPosition(monster, out GridPosition monsterPosition))
                return;

            ////////////////////////////////////////////
            // ----------  Choose a target ---------- //
            ////////////////////////////////////////////

            Combatant target = FindNearestEnemy(controller, monster);
            if (target == null)
            {
                return;
            }
            if (!controller.Grid.TryGetPosition(target, out GridPosition targetPosition))
            {
                return;
            }
            ////////////////////////////////////////
            // ----------  Try Attack  ---------- //
            ////////////////////////////////////////
            if (Grid.DistanceInSquares(monsterPosition, targetPosition) <= attack.RangeSquares)
            {
                AttackWhilePossible(controller, monster, target, attack);
                return;
            }

            /////////////////////////////////////////
            // ----------  Move closer  ---------- //
            /////////////////////////////////////////

            MovementRange range = controller.GetMovementRange(monster);

            GridPosition best = monsterPosition;
            foreach (GridPosition square in range.Destinations)
            {
                if (IsBetter(square, best, targetPosition, attack.RangeSquares, range))
                {
                    best = square;
                }
            }

            if (best != monsterPosition)
            {
                controller.TryMove(monster, best);
            }

            /////////////////////////////////////////////////
            // ----------  Try Attacking Again  ---------- //
            /////////////////////////////////////////////////

            if (!controller.Grid.TryGetPosition(monster, out GridPosition newMonsterPosition))
            {
                return;
            }

            if (Grid.DistanceInSquares(newMonsterPosition, targetPosition) <= attack.RangeSquares)
            {
                AttackWhilePossible(controller, monster, target, attack);
            }
        }
        private static Combatant FindNearestEnemy(CombatController controller, Combatant monster)
        {
            // Looked up once: the monster doesn't move while we search
            if (!controller.Grid.TryGetPosition(monster, out GridPosition monsterPosition))
            {
                return null;
            }

            // The best candidate so far, kept as plain numbers instead of looked up again each pass
            Combatant best = null;
            int bestDistance = int.MaxValue;
            int bestHP = int.MaxValue;

            foreach (Combatant candidate in controller.Turns.Order)
            {
                // skip anyone who isn't a valid target
                if (candidate.Team == monster.Team ||
                    candidate.Creature.IsDefeated ||
                    !controller.Grid.TryGetPosition(candidate, out GridPosition candidatePosition))
                {
                    continue;
                }
                
                // Getting the distance between the candidate and the monster plus the candidate's health.
                int distance = Grid.DistanceInSquares(monsterPosition, candidatePosition);
                int hp = candidate.Creature.CurrentHP;

                // Nearer wins. If equally near, the lower HP wins (strict "less than", so the
                // first one found keeps the job on a full tie, which keeps results repeatable).
                if (best == null ||
                    distance < bestDistance ||
                    (distance == bestDistance && hp < bestHP))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestHP = hp;
                }
            }

            return best;
        }        
        private static void AttackWhilePossible(CombatController controller, Combatant monster,
                                                Combatant target, AttackProfile attack)
        {
            for (int i = 0; i < 10; i++)
            {
                AttackOutcome outcome = controller.TryAttack(monster, target, attack);
                if (!outcome.Success) { break; }
            }
        }

        private static bool IsBetter(GridPosition a, GridPosition b, GridPosition targetPosition,
                                     int reach, MovementRange range)
        {
            bool aInReach = Grid.DistanceInSquares(a, targetPosition) <= reach;
            bool bInReach = Grid.DistanceInSquares(b, targetPosition) <= reach;

            if (aInReach && !bInReach)
            {
                return true; // a is in range and b is not
            }
            if(!aInReach && bInReach)
            {
                return false; // b is in range and a is not
            }
            if (!aInReach)
            {
                int aDistance = Grid.DistanceInSquares(a, targetPosition);
                int bDistance = Grid.DistanceInSquares(b, targetPosition);

                if (aDistance < bDistance) { return true; }
                if (aDistance > bDistance) { return false; }
            }

            int aSteps = range.StepsTo(a);
            int bSteps = range.StepsTo(b);

            if (aSteps < bSteps)
            {
                return true;     // a is cheaper to reach
            }
            if (aSteps > bSteps)
            {
                return false;    // a is harder to reach
            }

            if (a.X < b.X) { return true; }
            if (a.X > b.X) { return false; }
            if (a.Z < b.Z) { return true; }
            if (a.Z > b.Z) { return false; }

            return false;
        }
    }
}