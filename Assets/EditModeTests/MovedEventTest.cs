using System.Collections.Generic;
using NUnit.Framework;
using DnDCombat.Core;

// CombatController.Moved: raised once for every successful move, never for a refused one,
// with everything the scene needs to show it.
public class MovedEventTest
{
    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    private CombatController _controller;
    private Grid _grid;
    private Combatant _hero;
    private Combatant _enemy;
    private readonly List<MoveReport> _moves = new List<MoveReport>();

    // Hero at (7, 0, 7) goes first (initiative rolls 15 then 5), enemy far away at (12, 0, 12).
    // The next rolls (10 then 3) are only used by the test where the enemy attacks.
    [SetUp]
    public void SetUp()
    {
        FakeDice dice = new FakeDice(15, 5, 10, 3);
        _hero = new Combatant(new Creature("Hero", speed: 30), Team.Heroes);
        _enemy = new Combatant(new Creature("Enemy", maxHP: 999), Team.Enemies);
        _grid = new Grid(15, 4, 15);
        _grid.Place(_hero, P(7, 0, 7));
        _grid.Place(_enemy, P(12, 0, 12));
        TurnManager turns = new TurnManager(dice, new[] { _hero, _enemy });
        turns.StartCombat();
        _controller = new CombatController(turns, _grid, new AttackResolver(dice));
        _moves.Clear();
        _controller.Moved += _moves.Add;
    }

    ////////////////////////////////////////////
    // ---------- Successful moves ---------- //
    ////////////////////////////////////////////

    [Test]
    public void ASuccessfulMove_RaisesMovedOnce()
    {
        _controller.TryMove(_hero, P(7, 0, 10));

        Assert.That(_moves.Count, Is.EqualTo(1));
    }

    [Test]
    public void TheReport_SaysWhoMoved_FromWhere_AndHowFar()
    {
        MoveResult result = _controller.TryMove(_hero, P(7, 0, 10));

        MoveReport report = _moves[0];
        Assert.That(report.Mover, Is.SameAs(_hero));
        Assert.That(report.From, Is.EqualTo(P(7, 0, 7)));
        Assert.That(report.To, Is.EqualTo(P(7, 0, 10)));
        Assert.That(report.Path, Is.EqualTo(result.Path));
        Assert.That(report.FeetSpent, Is.EqualTo(15));
    }

    [Test]
    public void WhenMovedIsRaised_TheGridAndMovementAreAlreadyUpdated()
    {
        // Anything listening sees the finished move, not a half-done one
        GridPosition seenPosition = default;
        int seenFeet = -1;
        _controller.Moved += report =>
        {
            _grid.TryGetPosition(report.Mover, out seenPosition);
            seenFeet = report.Mover.MovementRemaining;
        };

        _controller.TryMove(_hero, P(7, 0, 10));

        Assert.That(seenPosition, Is.EqualTo(P(7, 0, 10)));
        Assert.That(seenFeet, Is.EqualTo(15));
    }

    [Test]
    public void TwoMoves_GiveTwoReports_AndTheSecondStartsWhereTheFirstEnded()
    {
        _controller.TryMove(_hero, P(7, 0, 9));
        _controller.TryMove(_hero, P(9, 0, 9));

        Assert.That(_moves.Count, Is.EqualTo(2));
        Assert.That(_moves[1].From, Is.EqualTo(P(7, 0, 9)));
        Assert.That(_moves[1].To, Is.EqualTo(P(9, 0, 9)));
    }

    /////////////////////////////////////////
    // ---------- The last step ---------- //
    /////////////////////////////////////////

    [Test]
    public void LastStepFrom_ForAOneSquareMove_IsTheStart()
    {
        _controller.TryMove(_hero, P(8, 0, 8));

        Assert.That(_moves[0].LastStepFrom, Is.EqualTo(P(7, 0, 7)));
    }

    [Test]
    public void LastStepFrom_ForALongerMove_IsTheSquareBeforeTheEnd()
    {
        // Straight north: (7,0,8), (7,0,9), (7,0,10). The last step is from (7,0,9).
        _controller.TryMove(_hero, P(7, 0, 10));

        Assert.That(_moves[0].LastStepFrom, Is.EqualTo(P(7, 0, 9)));
    }

    [Test]
    public void AReportWithNoPath_EndsWhereItStarted()
    {
        MoveReport empty = new MoveReport(_hero, P(3, 0, 3), new List<GridPosition>(), 0);

        Assert.That(empty.To, Is.EqualTo(P(3, 0, 3)));
        Assert.That(empty.LastStepFrom, Is.EqualTo(P(3, 0, 3)));
    }

    /////////////////////////////////////////
    // ---------- Refused moves ---------- //
    /////////////////////////////////////////

    [Test]
    public void ARefusedMove_RaisesNothing()
    {
        _controller.TryMove(_hero, P(7, 0, 7));      // already there
        _controller.TryMove(_hero, P(0, 0, 0));      // too far
        _controller.TryMove(_enemy, P(11, 0, 11));   // not the enemy's turn
        _controller.TryMove(null, P(7, 0, 8));       // nobody

        Assert.That(_moves, Is.Empty);
    }

    [Test]
    public void OutOfMovement_RaisesNothing()
    {
        _controller.TryMove(_hero, P(7, 0, 13));     // all 30 ft
        _moves.Clear();

        _controller.TryMove(_hero, P(7, 0, 12));

        Assert.That(_moves, Is.Empty);
    }

    ///////////////////////////////////////
    // ---------- Event order ---------- //
    ///////////////////////////////////////

    [Test]
    public void AMonsterThatWalksThenAttacks_ReportsTheMoveFirst()
    {
        // The scene relies on this order: turn the way it walked, THEN turn to face the target
        List<string> order = new List<string>();
        _controller.Moved += _ => order.Add("moved");
        _controller.AttackResolved += _ => order.Add("attacked");
        _grid.MoveTo(_enemy, P(10, 0, 7));          // 3 squares east of the hero
        _controller.EndTurn();                       // the enemy's go
        AttackProfile claw = new AttackProfile("Claw", 4, 1, 6, 2, DamageType.Slashing);

        MonsterAI.TakeTurn(_controller, _enemy, claw);   // d20 10 + 4 = 14 hits AC 10, then 3 damage

        Assert.That(order, Is.EqualTo(new[] { "moved", "attacked" }));
    }
}
