using DnDCombat.Core;
using NUnit.Framework;
using System.Collections.Generic;

public class HeadlessBattleTest
{
    //////////////////////////////////////////
    // ---------- How this works ---------- //
    //////////////////////////////////////////
    // "Headless" means no Unity scene and no UI: the AI plays BOTH sides of a
    //   whole fight using real dice, over and over with different seeds.
    // It isn't testing damage numbers (your unit tests do that). It proves that
    //   every fight finishes, nothing crashes, and the board stays sane.
    //
    // Your toolbox:
    //   Real dice                    new Dice(seed)   (NOT FakeDice, one shared by everything)
    //   Premade creatures            StatBlocks.Fighter(), StatBlocks.Skeleton()
    //   Attacks                      StatBlocks.FighterFlail, StatBlocks.SkeletonShortsword
    //   Play one turn                MonsterAI.TakeTurn(controller, combatant, attack)
    //   End a turn                   controller.EndTurn()
    //   Is the fight still on?       controller.Turns.State == CombatState.Ongoing
    //   Whose turn is it?            controller.Turns.Current   (null once the fight is over)
    //   Everyone in the fight        controller.Turns.Order
    //   Where is someone?            grid.TryGetPosition(combatant, out GridPosition pos)
    //
    // Build it one step at a time and run the test after each one.

    private const int MaxTurns = 200;   // every combatant's turn counts as one
    // What one fight tells us. RunBattle fills it in, the tests assert on it.
    private class BattleResult
    {
        public int Seed;
        public CombatState FinalState;
        public int TurnsTaken;
        public bool HitTheCap;
        public int HeroesAlive;
        public int EnemiesAlive;
        public string Problem;   // null means the board stayed sane the whole fight
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    private static BattleResult RunBattle(int seed)
    {
        ///////////////////////////////////////////
        // ---------- RunBattle(seed) ---------- //
        ///////////////////////////////////////////

        // Step 1: Build the world
        Dice dice = new Dice(seed);
        Combatant fighter = new Combatant(StatBlocks.Fighter(), Team.Heroes);
        Combatant skeleton = new Combatant(StatBlocks.Skeleton(), Team.Enemies);
        Grid arena = new Grid(15, 2, 15);
        TurnManager turnManager = new TurnManager(dice, new[] { fighter, skeleton });
        AttackResolver attackResolver = new AttackResolver(dice);
        CombatController combatController = new CombatController(turnManager, arena, attackResolver);

        // Step 2: Place the combatants
        arena.Place(fighter, P(2, 0, 7));
        arena.Place(skeleton, P(14, 0, 7));

        bool fighterFound = arena.TryGetPosition(fighter, out GridPosition fighterPos);

        Assert.That(fighterFound, Is.True);
        Assert.That(fighterPos, Is.EqualTo(P(2, 0, 7)));

        bool skeletonFound = arena.TryGetPosition(skeleton, out GridPosition skeletonPos);

        Assert.That(skeletonFound, Is.True);
        Assert.That(skeletonPos, Is.EqualTo(P(14, 0, 7)));

        // Step 3: Start combat
        turnManager.StartCombat();
        Assert.That(combatController.Turns.State, Is.EqualTo(CombatState.Ongoing));
        Assert.That(turnManager.Current, Is.Not.Null);

        // Step 4: The loop
        int turnsTaken = 0;
        string problem = null;

        while (combatController.Turns.State == CombatState.Ongoing && turnsTaken < MaxTurns)
        {
            Combatant who = combatController.Turns.Current;
            MonsterAI.TakeTurn(combatController, who, AttackFor(who));
            combatController.EndTurn();
            turnsTaken++;

            // Keep only the FIRST problem found
            string found = CheckBoard(arena, turnManager.Order);
            if (problem == null)
            {
                problem = found;
            }
        }

        // Step 5: Report what happened
        int heroesAlive = 0;
        int enemiesAlive = 0;
        foreach (Combatant c in turnManager.Order)
        {
            if (c.Creature.IsDefeated) { continue; }

            if (c.Team == Team.Heroes) { heroesAlive++; }
            else { enemiesAlive++; }
        }

        return new BattleResult
        {
            Seed = seed,
            FinalState = turnManager.State,
            TurnsTaken = turnsTaken,
            HitTheCap = turnManager.State == CombatState.Ongoing,   // the loop only stops early if someone won
            HeroesAlive = heroesAlive,
            EnemiesAlive = enemiesAlive,
            Problem = problem
        };
    }
    private static AttackProfile AttackFor(Combatant combatant)
    {
        if (combatant.Team == Team.Heroes)
        {
            return StatBlocks.FighterFlail;
        }

        return StatBlocks.SkeletonShortsword;
    }
    private static string CheckBoard(Grid grid, IReadOnlyList<Combatant> everyone)
    {
        // Every square we've seen so far this check
        HashSet<GridPosition> taken = new HashSet<GridPosition>();

        foreach (Combatant c in everyone)
        {
            Creature creature = c.Creature;

            // HP must stay between 0 and max
            if (creature.CurrentHP < 0 || creature.CurrentHP > creature.MaxHP)
            {
                return $"{creature.Name} has {creature.CurrentHP} HP (max {creature.MaxHP})";
            }

            // Everyone must still be on the grid
            if (!grid.TryGetPosition(c, out GridPosition position))
            {
                return $"{creature.Name} is no longer on the grid";
            }

            // Nobody may share a square. Add() returns false if the square is already in the set
            if (!taken.Add(position))
            {
                return $"{creature.Name} shares the square {position} with another combatant";
            }
        }

        return null;   // nothing wrong
    }
    // Returns a description of what's wrong with a finished fight, or null if it's fine
    private static string FightProblem(BattleResult result)
    {
        if (result.FinalState == CombatState.Ongoing)
        {
            return "never finished (hit the turn cap)";
        }
        if (result.FinalState == CombatState.HeroesWon && (result.EnemiesAlive != 0 || result.HeroesAlive == 0))
        {
            return $"HeroesWon, but {result.HeroesAlive} heroes and {result.EnemiesAlive} enemies are alive";
        }
        if (result.FinalState == CombatState.EnemiesWon && (result.HeroesAlive != 0 || result.EnemiesAlive == 0))
        {
            return $"EnemiesWon, but {result.HeroesAlive} heroes and {result.EnemiesAlive} enemies are alive";
        }

        return null;
    }

    //////////////////////////////////
    // ---------- Tests  ---------- //
    //////////////////////////////////

    [Test]
    public void OneBattle_Finishes()
    {
        BattleResult result = RunBattle(1);

        Assert.That(result.HitTheCap, Is.False, $"seed {result.Seed} hit the turn cap");
        Assert.That(result.FinalState, Is.Not.EqualTo(CombatState.Ongoing), $"seed {result.Seed} never finished");
        Assert.That(result.Problem, Is.Null, $"seed {result.Seed}: {result.Problem}");
    }

    [Test]
    public void WinnerMakesSense()
    {
        BattleResult result = RunBattle(1);

        Assert.That(FightProblem(result), Is.Null, $"seed {result.Seed}");
    }

    [Test]
    public void SameSeed_GivesTheSameFight()
    {
        BattleResult first = RunBattle(42);
        BattleResult second = RunBattle(42);

        Assert.That(second.FinalState, Is.EqualTo(first.FinalState));
        Assert.That(second.TurnsTaken, Is.EqualTo(first.TurnsTaken));
        Assert.That(second.HeroesAlive, Is.EqualTo(first.HeroesAlive));
        Assert.That(second.EnemiesAlive, Is.EqualTo(first.EnemiesAlive));
    }

    [Test]
    public void ManySeeds_EveryFightFinishes_AndTheBoardStaysSane()
    {
        List<string> failures = new List<string>();
        int heroWins = 0;
        int enemyWins = 0;

        for (int seed = 1; seed < 1000; seed++)
        {
            BattleResult result = RunBattle(seed);

            if (result.FinalState == CombatState.HeroesWon) { heroWins++; }
            if (result.FinalState == CombatState.EnemiesWon) { enemyWins++; }

            // A board problem from the loop comes first, then the result check
            string problem = result.Problem;
            if (problem == null)
            {
                problem = FightProblem(result);
            }
            if (problem != null)
            {
                failures.Add($"seed {seed}: {result.FinalState}, {problem}");
            }
        }

        TestContext.WriteLine($"Heroes won {heroWins}, enemies won {enemyWins} out of 1000");

        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }
}