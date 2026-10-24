using NUnit.Framework;
using DnDCombat.Core;

public class GridTest
{
    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    private static Combatant Make(string name = "Combatant")
    {
        return new Combatant(new Creature(name), Team.Heroes);
    }

    // Width 5 (X), height 3 (Y), depth 4 (Z)
    private static Grid NewGrid() => new Grid(5, 3, 4);

    ///////////////////////////////////
    // ---------- Terrain ---------- //
    ///////////////////////////////////

    [Test]
    public void Constructor_NonPositiveSize_IsClampedToOne()
    {
        Grid grid = new Grid(0, -3, 0);

        Assert.That(grid.Width, Is.EqualTo(1));
        Assert.That(grid.Height, Is.EqualTo(1));
        Assert.That(grid.Depth, Is.EqualTo(1));
    }

    [TestCase(0, 0, 0, true)]
    [TestCase(4, 2, 3, true)]
    [TestCase(-1, 0, 0, false)]
    [TestCase(5, 0, 0, false)]
    [TestCase(0, -1, 0, false)]
    [TestCase(0, 3, 0, false)]
    [TestCase(0, 0, -1, false)]
    [TestCase(0, 0, 4, false)]
    public void InBounds_CheckEveryEdge(int x, int y, int z, bool expected)
    {
        Assert.That(NewGrid().InBounds(P(x, y, z)), Is.EqualTo(expected));
    }

    [Test]
    public void OpenGroundCell_IsStandable()
    {
        Assert.That(NewGrid().IsStandable(P(2, 0, 2)), Is.True);
    }

    [Test]
    public void SolidCell_IsNotStandable()
    {
        Grid grid = NewGrid();
        grid.SetSolid(P(2, 0, 2), true);

        Assert.That(grid.IsSolid(P(2, 0, 2)), Is.True);
        Assert.That(grid.IsStandable(P(2, 0, 2)), Is.False);
    }

    [Test]
    public void FloatingCell_IsNotStandable()
    {
        Assert.That(NewGrid().IsStandable(P(2, 1, 2)), Is.False);
    }

    [Test]
    public void CellOnTopOfASolidBlock_IsStandable()
    {
        Grid grid = NewGrid();
        grid.SetSolid(P(2, 0, 2), true);

        Assert.That(grid.IsStandable(P(2, 1, 2)), Is.True);
    }

    [Test]
    public void OutOfBounds_IsNotStandable_AndCountsAsSolid()
    {
        Grid grid = NewGrid();

        Assert.That(grid.IsStandable(P(9, 0, 9)), Is.False);
        Assert.That(grid.IsSolid(P(9, 0, 9)), Is.True);
    }

    [Test]
    public void SetSolid_OutOfBounds_ReturnsFalse()
    {
        Assert.That(NewGrid().SetSolid(P(9, 9, 9), true), Is.False);
    }

    [Test]
    public void SetSolid_OnAnOccupiedCell_IsRefused()
    {
        Grid grid = NewGrid();
        grid.Place(Make(), P(1, 0, 1));

        Assert.That(grid.SetSolid(P(1, 0, 1), true), Is.False);
        Assert.That(grid.IsSolid(P(1, 0, 1)), Is.False);
    }

    /////////////////////////////////////
    // ---------- Placement ---------- //
    /////////////////////////////////////

    [Test]
    public void Place_OnAFreeStandableCell_Works()
    {
        Grid grid = NewGrid();
        Combatant hero = Make();

        bool placed = grid.Place(hero, P(1, 0, 2));
        bool found = grid.TryGetPosition(hero, out GridPosition position);

        Assert.That(placed, Is.True);
        Assert.That(found, Is.True);
        Assert.That(position, Is.EqualTo(P(1, 0, 2)));
        Assert.That(grid.GetOccupant(P(1, 0, 2)), Is.SameAs(hero));
    }

    [Test]
    public void Place_OutOfBounds_SolidOrFloating_IsRefused()
    {
        Grid grid = NewGrid();
        grid.SetSolid(P(3, 0, 3), true);

        Assert.That(grid.Place(Make(), P(9, 0, 9)), Is.False);
        Assert.That(grid.Place(Make(), P(3, 0, 3)), Is.False);
        Assert.That(grid.Place(Make(), P(2, 2, 2)), Is.False);
    }

    [Test]
    public void Place_OnAnOccupiedCell_IsRefused_AndTheOccupantStays()
    {
        Grid grid = NewGrid();
        Combatant first = Make("First");
        grid.Place(first, P(1, 0, 1));

        bool placed = grid.Place(Make("Second"), P(1, 0, 1));

        Assert.That(placed, Is.False);
        Assert.That(grid.GetOccupant(P(1, 0, 1)), Is.SameAs(first));
    }

    [Test]
    public void Place_SameCombatantTwice_IsRefused_AndPositionIsUnchanged()
    {
        Grid grid = NewGrid();
        Combatant hero = Make();
        grid.Place(hero, P(1, 0, 1));

        bool placedAgain = grid.Place(hero, P(2, 0, 2));
        grid.TryGetPosition(hero, out GridPosition position);

        Assert.That(placedAgain, Is.False);
        Assert.That(position, Is.EqualTo(P(1, 0, 1)));
    }

    [Test]
    public void Place_Null_ReturnsFalse()
    {
        Assert.That(NewGrid().Place(null, P(1, 0, 1)), Is.False);
    }

    [Test]
    public void MoveTo_RelocatesTheCombatant_AndFreesTheOldCell()
    {
        Grid grid = NewGrid();
        Combatant hero = Make();
        grid.Place(hero, P(1, 0, 1));

        bool moved = grid.MoveTo(hero, P(3, 0, 2));

        Assert.That(moved, Is.True);
        Assert.That(grid.GetOccupant(P(3, 0, 2)), Is.SameAs(hero));
        Assert.That(grid.GetOccupant(P(1, 0, 1)), Is.Null);
    }

    [Test]
    public void MoveTo_OccupiedOrIllegalCell_IsRefused_AndNothingChanges()
    {
        Grid grid = NewGrid();
        Combatant hero = Make("Hero");
        Combatant enemy = Make("Enemy");
        grid.Place(hero, P(1, 0, 1));
        grid.Place(enemy, P(2, 0, 1));

        Assert.That(grid.MoveTo(hero, P(2, 0, 1)), Is.False);   // occupied
        Assert.That(grid.MoveTo(hero, P(9, 0, 9)), Is.False);   // out of bounds
        Assert.That(grid.MoveTo(hero, P(1, 0, 1)), Is.False);   // own square

        grid.TryGetPosition(hero, out GridPosition position);
        Assert.That(position, Is.EqualTo(P(1, 0, 1)));
    }

    [Test]
    public void MoveTo_ACombatantNotOnTheGrid_ReturnsFalse()
    {
        Assert.That(NewGrid().MoveTo(Make(), P(1, 0, 1)), Is.False);
    }

    [Test]
    public void Remove_FreesTheCell()
    {
        Grid grid = NewGrid();
        Combatant hero = Make();
        grid.Place(hero, P(1, 0, 1));

        Assert.That(grid.Remove(hero), Is.True);
        Assert.That(grid.GetOccupant(P(1, 0, 1)), Is.Null);
        Assert.That(grid.TryGetPosition(hero, out GridPosition _), Is.False);
    }

    [Test]
    public void Remove_ACombatantNotOnTheGrid_ReturnsFalse()
    {
        Assert.That(NewGrid().Remove(Make()), Is.False);
    }

    [Test]
    public void TryGetPosition_ForAnUnplacedCombatant_ReturnsFalse()
    {
        Assert.That(NewGrid().TryGetPosition(Make(), out GridPosition _), Is.False);
    }

    ////////////////////////////////////
    // ---------- Distance ---------- //
    ////////////////////////////////////

    [TestCase(0, 0, 0, 0, 0, 0, 0)]
    [TestCase(0, 0, 0, 3, 1, 0, 3)]
    [TestCase(0, 0, 0, 1, 1, 1, 1)]
    [TestCase(0, 0, 0, 0, 2, 1, 2)]
    [TestCase(2, 1, 4, 0, 0, 1, 3)]
    [TestCase(50,23,12, 21, 6, 2, 56)]
    public void DistanceInSquares_IsTheLargestGap_AndSymmetric(int ax, int ay, int az, int bx, int by, int bz, int expected)
    {
        GridPosition a = P(ax, ay, az);
        GridPosition b = P(bx, by, bz);

        Assert.That(Grid.DistanceInSquares(a, b), Is.EqualTo(expected));
        Assert.That(Grid.DistanceInSquares(b, a), Is.EqualTo(expected));
    }

    [Test]
    public void IsAdjacent_AllTwentySixNeighbours_AreAdjacent()
    {
        GridPosition origin = P(5, 5, 5);
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;

                    Assert.That(Grid.IsAdjacent(origin, P(5 + dx, 5 + dy, 5 + dz)), Is.True, $"offset ({dx}, {dy}, {dz})");
                    count++;
                }
            }
        }

        Assert.That(count, Is.EqualTo(26));
    }

    [Test]
    public void IsAdjacent_SameSquare_AndTwoSquaresAway_AreNotAdjacent()
    {
        GridPosition origin = P(5, 5, 5);

        Assert.That(Grid.IsAdjacent(origin, origin), Is.False);
        Assert.That(Grid.IsAdjacent(origin, P(7, 5, 5)), Is.False);
        Assert.That(Grid.IsAdjacent(origin, P(5, 7, 5)), Is.False);
    }
}