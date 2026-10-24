using System.Linq;
using NUnit.Framework;
using DnDCombat.Core;

public class MovementTest
{
    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    private static Combatant Make(string name = "Combatant")
    {
        return new Combatant(new Creature(name), Team.Heroes);
    }

    // A column of solid cells, heightCells tall, starting at the ground
    private static void Pillar(Grid grid, int x, int z, int heightCells)
    {
        for (int y = 0; y < heightCells; y++)
        {
            grid.SetSolid(P(x, y, z), true);
        }
    }

    ////////////////////////////////////
    // ---------- GetMoves ---------- //
    ////////////////////////////////////

    [Test]
    public void GetMoves_OpenGround_CentreHasEightMoves()
    {
        Grid grid = new Grid(3, 2, 3);

        var moves = grid.GetMoves(P(1, 0, 1)).ToList();

        Assert.That(moves.Count, Is.EqualTo(8));
        Assert.That(moves.All(m => m.Y == 0), Is.True);
    }

    [Test]
    public void GetMoves_Corner_HasThreeMoves()
    {
        Grid grid = new Grid(3, 2, 3);

        Assert.That(grid.GetMoves(P(0, 0, 0)).Count(), Is.EqualTo(3));
    }

    [Test]
    public void GetMoves_OccupiedNeighbour_IsNotAMove()
    {
        Grid grid = new Grid(3, 2, 3);
        grid.Place(Make(), P(2, 0, 1));

        var moves = grid.GetMoves(P(1, 0, 1)).ToList();

        Assert.That(moves.Count, Is.EqualTo(7));
        Assert.That(moves.Contains(P(2, 0, 1)), Is.False);
    }

    [Test]
    public void GetMoves_OneBlockHigh_CanStepUp()
    {
        Grid grid = new Grid(3, 3, 3);
        Pillar(grid, 1, 1, 1);

        Assert.That(grid.GetMoves(P(0, 0, 1)).Contains(P(1, 1, 1)), Is.True);
    }

    [Test]
    public void GetMoves_TwoBlocksHigh_CannotBeClimbed()
    {
        Grid grid = new Grid(3, 4, 3);
        Pillar(grid, 1, 1, 2);

        var moves = grid.GetMoves(P(0, 0, 1)).ToList();

        Assert.That(moves.Contains(P(1, 1, 1)), Is.False);
        Assert.That(moves.Contains(P(1, 2, 1)), Is.False);
    }

    [Test]
    public void GetMoves_LowCeiling_BlocksSteppingUp()
    {
        Grid grid = new Grid(3, 3, 3);
        Pillar(grid, 1, 1, 1);
        grid.SetSolid(P(0, 1, 1), true);   // ceiling directly above the mover

        Assert.That(grid.GetMoves(P(0, 0, 1)).Contains(P(1, 1, 1)), Is.False);
    }

    [Test]
    public void GetMoves_DropOfOneBlock_IsAllowed()
    {
        Grid grid = new Grid(3, 3, 3);
        Pillar(grid, 0, 1, 1);

        Assert.That(grid.GetMoves(P(0, 1, 1)).Contains(P(1, 0, 1)), Is.True);
    }

    [Test]
    public void GetMoves_DropOfTwoBlocks_IsBlocked()
    {
        Grid grid = new Grid(3, 4, 3);
        Pillar(grid, 0, 1, 2);

        Assert.That(grid.GetMoves(P(0, 2, 1)).Contains(P(1, 0, 1)), Is.False);
    }

    [Test]
    public void GetMoves_DoesNotCutCornersPastSolidCells()
    {
        Grid grid = new Grid(3, 3, 3);
        Pillar(grid, 2, 1, 2);   // a tall pillar beside the mover

        var moves = grid.GetMoves(P(1, 0, 1)).ToList();

        Assert.That(moves.Contains(P(2, 0, 2)), Is.False);   // diagonal past the pillar
        Assert.That(moves.Contains(P(2, 0, 0)), Is.False);   // diagonal past the pillar
        Assert.That(moves.Contains(P(1, 0, 2)), Is.True);    // straight along is fine
    }

    ////////////////////////////////////
    // ---------- Pathfinder ---------- //
    ////////////////////////////////////

    [Test]
    public void OpenArena_ThirtyFeet_ReachesExactlySixSquaresOut()
    {
        Grid grid = new Grid(15, 2, 15);
        Combatant mover = Make();
        grid.Place(mover, P(7, 0, 7));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 30);

        Assert.That(range.Destinations.Count(), Is.EqualTo(168));   // 13 x 13 squares, minus the start
        Assert.That(range.Contains(P(13, 0, 7)), Is.True);          // 6 away
        Assert.That(range.Contains(P(14, 0, 7)), Is.False);         // 7 away
        Assert.That(range.Contains(P(7, 1, 7)), Is.False);          // floating layer
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(-10)]
    public void NotEnoughMovementForAStep_OnlyTheStartingSquare(int feet)
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant mover = Make();
        grid.Place(mover, P(2, 0, 2));

        MovementRange range = Pathfinder.FindReachable(grid, mover, feet);

        Assert.That(range.Destinations, Is.Empty);
        Assert.That(range.Contains(P(2, 0, 2)), Is.True);
    }

    [Test]
    public void FiveFeet_ReachesOnlyTheEightNeighbours()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant mover = Make();
        grid.Place(mover, P(2, 0, 2));

        Assert.That(Pathfinder.FindReachable(grid, mover, 5).Destinations.Count(), Is.EqualTo(8));
    }

    [Test]
    public void SolidWall_SplitsTheArena()
    {
        Grid grid = new Grid(9, 3, 3);
        for (int z = 0; z < 3; z++) Pillar(grid, 4, z, 2);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 1));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Contains(P(3, 0, 1)), Is.True);
        Assert.That(range.Contains(P(5, 0, 1)), Is.False);
    }

    [Test]
    public void GapInTheWall_LetsThePathGoThrough()
    {
        Grid grid = new Grid(9, 3, 3);
        for (int z = 0; z < 3; z++) Pillar(grid, 4, z, 2);
        grid.SetSolid(P(4, 0, 1), false);
        grid.SetSolid(P(4, 1, 1), false);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 1));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Contains(P(5, 0, 1)), Is.True);
        Assert.That(range.PathTo(P(5, 0, 1)).Contains(P(4, 0, 1)), Is.True);
    }

    [Test]
    public void WalledInMover_CannotGoAnywhere()
    {
        Grid grid = new Grid(5, 3, 5);
        for (int x = 1; x <= 3; x++)
        {
            for (int z = 1; z <= 3; z++)
            {
                if (x == 2 && z == 2) continue;
                Pillar(grid, x, z, 2);
            }
        }
        Combatant mover = Make();
        grid.Place(mover, P(2, 0, 2));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Destinations, Is.Empty);
    }

    [Test]
    public void AnotherCombatant_BlocksANarrowCorridor()
    {
        Grid grid = new Grid(5, 2, 1);
        Combatant mover = Make("Mover");
        grid.Place(mover, P(0, 0, 0));
        grid.Place(Make("Blocker"), P(2, 0, 0));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Contains(P(1, 0, 0)), Is.True);
        Assert.That(range.Contains(P(2, 0, 0)), Is.False);
        Assert.That(range.Contains(P(3, 0, 0)), Is.False);
    }

    [Test]
    public void Staircase_CanBeClimbedOneBlockAtATime()
    {
        Grid grid = new Grid(5, 5, 1);
        Pillar(grid, 1, 0, 1);
        Pillar(grid, 2, 0, 2);
        Pillar(grid, 3, 0, 3);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 0));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Contains(P(1, 1, 0)), Is.True);
        Assert.That(range.Contains(P(2, 2, 0)), Is.True);
        Assert.That(range.Contains(P(3, 3, 0)), Is.True);
    }

    [Test]
    public void TwoBlockWall_CannotBeClimbed()
    {
        Grid grid = new Grid(4, 4, 1);
        Pillar(grid, 1, 0, 2);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 0));

        MovementRange range = Pathfinder.FindReachable(grid, mover, 100);

        Assert.That(range.Contains(P(1, 2, 0)), Is.False);
        Assert.That(range.Contains(P(2, 0, 0)), Is.False);
    }

    [Test]
    public void MoverNotOnTheGrid_GivesAnEmptyRange_AndDoesNotThrow()
    {
        Grid grid = new Grid(5, 2, 5);

        MovementRange range = null;
        Assert.DoesNotThrow(() => range = Pathfinder.FindReachable(grid, Make(), 30));

        Assert.That(range.Destinations, Is.Empty);
    }

    [Test]
    public void NullArguments_GiveAnEmptyRange_AndDoNotThrow()
    {
        Assert.DoesNotThrow(() => Pathfinder.FindReachable(null, Make(), 30));
        Assert.DoesNotThrow(() => Pathfinder.FindReachable(new Grid(3, 2, 3), null, 30));
    }

    ////////////////////////////////
    // ---------- Paths ---------- //
    ////////////////////////////////

    [Test]
    public void PathTo_IsConnected_StartsNextToTheOrigin_AndEndsAtTheDestination()
    {
        Grid grid = new Grid(8, 2, 8);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 0));
        MovementRange range = Pathfinder.FindReachable(grid, mover, 30);
        GridPosition destination = P(3, 0, 2);

        var path = range.PathTo(destination);

        Assert.That(path.Count, Is.EqualTo(range.StepsTo(destination)));
        Assert.That(path.Count, Is.EqualTo(3));
        Assert.That(Grid.IsAdjacent(P(0, 0, 0), path[0]), Is.True);
        Assert.That(path.Last(), Is.EqualTo(destination));

        for (int i = 1; i < path.Count; i++)
        {
            Assert.That(Grid.IsAdjacent(path[i - 1], path[i]), Is.True, $"step {i}");
        }
    }

    [Test]
    public void PathTo_Unreachable_OrTheStartSquare_IsEmpty()
    {
        Grid grid = new Grid(15, 2, 15);
        Combatant mover = Make();
        grid.Place(mover, P(0, 0, 0));
        MovementRange range = Pathfinder.FindReachable(grid, mover, 10);

        Assert.That(range.PathTo(P(14, 0, 14)), Is.Empty);
        Assert.That(range.PathTo(P(0, 0, 0)), Is.Empty);
        Assert.That(range.StepsTo(P(14, 0, 14)), Is.EqualTo(-1));
    }
}