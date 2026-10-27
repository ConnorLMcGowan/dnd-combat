using NUnit.Framework;
using DnDCombat.Core;

public class CombatControllerTest
{
    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);


    private class Arena
    {
        public CombatController Controller;
        public Grid Grid;
        public Combatant Hero;
        public Combatant Enemy;
        public FakeDice Dice;
    }

    // The first two rolls are initiative (hero 15, enemy 5), so the hero goes first.
    // Anything you pass in is queued after that, e.g. NewArena(14, 6) is a d20 of 14 then damage of 6.
    private static Arena NewArena(params int[] extraRolls)
    {
        return BuildArena(1, extraRolls);
    }

    // Same, but the hero gets several attacks per Attack action
    private static Arena NewArenaWithAttacks(int heroAttacks, params int[] extraRolls)
    {
        return BuildArena(heroAttacks, extraRolls);
    }

    private static Arena BuildArena(int heroAttacks, int[] extraRolls)
    {
        int[] rolls = new int[2 + extraRolls.Length];
        rolls[0] = 15;
        rolls[1] = 5;
        extraRolls.CopyTo(rolls, 2);

        FakeDice dice = new FakeDice(rolls);

        Combatant hero = new Combatant(new Creature("Hero", speed: 30, attacksPerAction: heroAttacks), Team.Heroes);
        Combatant enemy = new Combatant(new Creature("Enemy"), Team.Enemies);

        Grid grid = new Grid(15, 4, 15);
        grid.Place(hero, P(7, 0, 7));
        grid.Place(enemy, P(12, 0, 12));

        TurnManager turns = new TurnManager(dice, new[] { hero, enemy });
        turns.StartCombat();

        return new Arena
        {
            Controller = new CombatController(turns, grid, new AttackResolver(dice)),
            Grid = grid,
            Hero = hero,
            Enemy = enemy,
            Dice = dice
        };
    }

    private static readonly AttackProfile Sword = new AttackProfile("Longsword", 5, 1, 8, 3, DamageType.Slashing);

    // The hero is at (7, 0, 7)
    private static void PutEnemyNextToHero(Arena a)
    {
        a.Grid.MoveTo(a.Enemy, P(8, 0, 7));
    }

    /////////////////////////////////////
    // ---------- TryMove ---------- ///
    /////////////////////////////////////

    [Test]
    public void ValidMove_MovesTheHero_SpendsMovement_AndReturnsThePath()
    {
        Arena a = NewArena();

        MoveResult result = a.Controller.TryMove(a.Hero, P(10, 0, 7));   // 3 squares

        Assert.That(result.Success, Is.True);
        Assert.That(result.Failure, Is.EqualTo(MoveFailure.None));
        Assert.That(result.FeetSpent, Is.EqualTo(15));
        Assert.That(result.Path.Count, Is.EqualTo(3));
        Assert.That(result.Path[2], Is.EqualTo(P(10, 0, 7)));
        Assert.That(a.Hero.MovementRemaining, Is.EqualTo(15));
        a.Grid.TryGetPosition(a.Hero, out GridPosition position);
        Assert.That(position, Is.EqualTo(P(10, 0, 7)));
    }

    [Test]
    public void TwoMoves_UseUpTheBudget_ThenNoMovementIsLeft()
    {
        Arena a = NewArena();

        a.Controller.TryMove(a.Hero, P(10, 0, 7));
        a.Controller.TryMove(a.Hero, P(13, 0, 7));
        MoveResult third = a.Controller.TryMove(a.Hero, P(13, 0, 6));

        Assert.That(a.Hero.MovementRemaining, Is.EqualTo(0));
        Assert.That(third.Success, Is.False);
        Assert.That(third.Failure, Is.EqualTo(MoveFailure.NoMovementLeft));
    }

    [Test]
    public void MovingTheEnemyOnTheHeroesTurn_IsRefused_AndNothingChanges()
    {
        Arena a = NewArena();

        MoveResult result = a.Controller.TryMove(a.Enemy, P(12, 0, 10));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(MoveFailure.NotYourTurn));
        a.Grid.TryGetPosition(a.Enemy, out GridPosition position);
        Assert.That(position, Is.EqualTo(P(12, 0, 12)));
    }

    //////////////////////////////////////
    // ---------- Attacks ---------- //
    //////////////////////////////////////

    [Test]
    public void Hit_LowersTheTargetsHP_AndReportsTheRoll()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);

        AttackOutcome outcome = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Result.Hit, Is.True);
        Assert.That(outcome.Result.Damage, Is.EqualTo(9));          // 6 + 3
        Assert.That(a.Enemy.Creature.CurrentHP, Is.EqualTo(1));
    }

    [Test]
    public void Miss_ChangesNothing_ButStillSpendsTheAction_AndRollsNoDamage()
    {
        Arena a = NewArena(2, 5);                  // d20 of 2 (total 7, misses AC 10), then a spare 5
        PutEnemyNextToHero(a);

        AttackOutcome outcome = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(outcome.Success, Is.True);
        Assert.That(outcome.Result.Hit, Is.False);
        Assert.That(a.Enemy.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(a.Hero.ActionAvailable, Is.False);
        Assert.That(a.Dice.Remaining, Is.EqualTo(1));              // the spare roll was never used
    }

    [Test]
    public void SecondAttackInTheSameTurn_IsRefused_AndRollsNothing()
    {
        Arena a = NewArena(14, 6, 14, 6);
        PutEnemyNextToHero(a);
        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        AttackOutcome second = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(second.Success, Is.False);
        Assert.That(second.Failure, Is.EqualTo(AttackFailure.NoActionLeft));
        Assert.That(a.Dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void TheActionComesBackOnTheNextTurn()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);
        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        a.Controller.EndTurn();   // enemy's turn
        a.Controller.EndTurn();   // back to the hero

        Assert.That(a.Controller.Turns.Current, Is.SameAs(a.Hero));
        Assert.That(a.Hero.ActionAvailable, Is.True);
    }

    ///////////////////////////////////
    // ---------- Reach ---------- //
    ///////////////////////////////////

    [TestCase(8, 0, 7)]   // beside
    [TestCase(8, 0, 8)]   // diagonal
    public void Attack_WithinOneSquare_Works(int x, int y, int z)
    {
        Arena a = NewArena(14, 6);
        a.Grid.MoveTo(a.Enemy, P(x, y, z));

        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Success, Is.True);
    }

    [Test]
    public void Attack_OneBlockHigher_IsStillInReach()
    {
        Arena a = NewArena(14, 6);
        a.Grid.SetSolid(P(8, 0, 7), true);
        a.Grid.MoveTo(a.Enemy, P(8, 1, 7));

        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Success, Is.True);
    }

    [Test]
    public void Attack_OnAHighCliff_IsOutOfMeleeReach()
    {
        Arena a = NewArena(14, 6);
        a.Grid.SetSolid(P(8, 0, 7), true);
        a.Grid.SetSolid(P(8, 1, 7), true);
        a.Grid.MoveTo(a.Enemy, P(8, 2, 7));

        AttackOutcome outcome = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(outcome.Success, Is.False);
        Assert.That(outcome.Failure, Is.EqualTo(AttackFailure.OutOfReach));
    }

    [Test]
    public void Attack_TwoSquaresAway_IsOutOfReach_AndChangesNothing()
    {
        Arena a = NewArena(14, 6);
        a.Grid.MoveTo(a.Enemy, P(9, 0, 7));
        int diceBefore = a.Dice.Remaining;

        AttackOutcome outcome = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(outcome.Failure, Is.EqualTo(AttackFailure.OutOfReach));
        Assert.That(a.Dice.Remaining, Is.EqualTo(diceBefore));
        Assert.That(a.Hero.ActionAvailable, Is.True);
        Assert.That(a.Enemy.Creature.CurrentHP, Is.EqualTo(10));
    }

    ////////////////////////////////////
    // ---------- Refusals ---------- //
    ////////////////////////////////////

    [Test]
    public void AttackingYourself_IsInvalid()
    {
        Arena a = NewArena(14, 6);

        Assert.That(a.Controller.TryAttack(a.Hero, a.Hero, Sword).Failure, Is.EqualTo(AttackFailure.InvalidTarget));
    }

    [Test]
    public void AttackingADefeatedTarget_IsRefused()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);
        a.Enemy.Creature.TakeDamage(10);

        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.TargetDefeated));
    }

    [Test]
    public void ADefeatedAttacker_IsRefused()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);
        a.Hero.Creature.TakeDamage(10);

        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.AttackerDefeated));
    }

    [Test]
    public void AttackingOutOfTurn_IsRefused()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);

        AttackOutcome outcome = a.Controller.TryAttack(a.Enemy, a.Hero, Sword);

        Assert.That(outcome.Failure, Is.EqualTo(AttackFailure.NotYourTurn));
        Assert.That(a.Hero.Creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void UnplacedAttackerOrTarget_IsRefused()
    {
        Arena a = NewArena(14, 6);
        PutEnemyNextToHero(a);
        a.Grid.Remove(a.Enemy);
        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.NotOnGrid));

        Arena b = NewArena(14, 6);
        PutEnemyNextToHero(b);
        b.Grid.Remove(b.Hero);
        Assert.That(b.Controller.TryAttack(b.Hero, b.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.NotOnGrid));
    }

    [Test]
    public void NullAttackerOrTarget_FailsWithoutThrowing()
    {
        Arena a = NewArena(14, 6);

        AttackOutcome noAttacker = default;
        AttackOutcome noTarget = default;
        Assert.DoesNotThrow(() => noAttacker = a.Controller.TryAttack(null, a.Enemy, Sword));
        Assert.DoesNotThrow(() => noTarget = a.Controller.TryAttack(a.Hero, null, Sword));

        Assert.That(noAttacker.Failure, Is.EqualTo(AttackFailure.NotYourTurn));
        Assert.That(noTarget.Failure, Is.EqualTo(AttackFailure.InvalidTarget));
    }

    //////////////////////////////////////
    // ---------- Ending combat ---------- //
    //////////////////////////////////////

    [Test]
    public void KillingTheLastEnemy_EndsCombatImmediately()
    {
        Arena a = NewArena(14, 8);                 // 8 + 3 = 11 damage against 10 HP
        PutEnemyNextToHero(a);

        AttackOutcome outcome = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(outcome.Success, Is.True);
        Assert.That(a.Enemy.Creature.IsDefeated, Is.True);
        Assert.That(a.Controller.Turns.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(a.Controller.Turns.Current, Is.Null);
        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.CombatOver));
    }

    [Test]
    public void ANonLethalHit_DoesNotRaiseCombatEnded()
    {
        Arena a = NewArena(14, 6);   // 6 + 3 = 9 damage against 10 HP
        PutEnemyNextToHero(a);
        int fired = 0;
        a.Controller.Turns.CombatEnded += state => fired++;

        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(a.Enemy.Creature.CurrentHP, Is.EqualTo(1));
        Assert.That(fired, Is.EqualTo(0));
    }

    [Test]
    public void TheKillingBlow_RaisesCombatEndedOnce_WithHeroesWon()
    {
        Arena a = NewArena(14, 8);   // 8 + 3 = 11 damage against 10 HP
        PutEnemyNextToHero(a);
        int fired = 0;
        CombatState last = CombatState.Ongoing;
        a.Controller.Turns.CombatEnded += state => { fired++; last = state; };

        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(fired, Is.EqualTo(1));
        Assert.That(last, Is.EqualTo(CombatState.HeroesWon));
    }

    //////////////////////////////////////////
    // ---------- Multiple attacks ---------- //
    //////////////////////////////////////////

    [Test]
    public void TwoAttacksPerAction_BothWork_AndTheThirdIsRefused()
    {
        Arena a = NewArenaWithAttacks(2, 2, 2, 9);   // two misses, plus a spare roll
        PutEnemyNextToHero(a);

        AttackOutcome first = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);
        Assert.That(first.Success, Is.True);
        Assert.That(a.Hero.ActionAvailable, Is.False);
        Assert.That(a.Hero.AttacksRemaining, Is.EqualTo(1));

        AttackOutcome second = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);
        Assert.That(second.Success, Is.True);
        Assert.That(a.Hero.AttacksRemaining, Is.EqualTo(0));

        AttackOutcome third = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);
        Assert.That(third.Success, Is.False);
        Assert.That(third.Failure, Is.EqualTo(AttackFailure.NoActionLeft));
        Assert.That(a.Dice.Remaining, Is.EqualTo(1));
    }

    [Test]
    public void UnusedExtraAttacks_AreResetOnTheNextTurn()
    {
        Arena a = NewArenaWithAttacks(2, 2, 2, 2);   // three misses
        PutEnemyNextToHero(a);
        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);   // only one of the two attacks used

        a.Controller.EndTurn();
        a.Controller.EndTurn();

        Assert.That(a.Hero.ActionAvailable, Is.True);
        Assert.That(a.Hero.AttacksRemaining, Is.EqualTo(0));
        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Success, Is.True);
        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Success, Is.True);
        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.NoActionLeft));
    }

    [Test]
    public void KillingWithTheFirstAttack_MeansTheSecondIsCombatOver()
    {
        Arena a = NewArenaWithAttacks(2, 14, 8);
        PutEnemyNextToHero(a);
        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(a.Controller.TryAttack(a.Hero, a.Enemy, Sword).Failure, Is.EqualTo(AttackFailure.CombatOver));
    }

    [Test]
    public void YouCanMoveBetweenAttacks()
    {
        Arena a = NewArenaWithAttacks(2, 2, 2);
        PutEnemyNextToHero(a);

        a.Controller.TryAttack(a.Hero, a.Enemy, Sword);
        MoveResult move = a.Controller.TryMove(a.Hero, P(7, 0, 8));   // still adjacent, diagonally
        AttackOutcome second = a.Controller.TryAttack(a.Hero, a.Enemy, Sword);

        Assert.That(move.Success, Is.True);
        Assert.That(second.Success, Is.True);
    }
}