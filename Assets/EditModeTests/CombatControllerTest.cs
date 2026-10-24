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
    }

    // A flat 15 x 15 arena. The hero (30 ft speed) rolls highest, so the hero has the first turn.
    private static Arena NewArena()
    {
        Combatant hero = new Combatant(new Creature("Hero", speed: 30), Team.Heroes);
        Combatant enemy = new Combatant(new Creature("Enemy"), Team.Enemies);

        Grid grid = new Grid(15, 2, 15);
        grid.Place(hero, P(7, 0, 7));
        grid.Place(enemy, P(12, 0, 12));

        TurnManager turns = new TurnManager(new FakeDice(15, 5), new[] { hero, enemy });
        turns.StartCombat();

        return new Arena
        {
            Controller = new CombatController(turns, grid),
            Grid = grid,
            Hero = hero,
            Enemy = enemy
        };
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
}