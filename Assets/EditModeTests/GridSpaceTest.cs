using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;

public class GridSpaceTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.001f);

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // The arena's real setup: 15 x 15 cells, centred on the world origin
    private static GridSpace Arena() => new GridSpace(new Vector3(-7.5f, 0f, -7.5f), 1f);

    ///////////////////////////////////
    // ---------- ToWorld ---------- //
    ///////////////////////////////////

    [Test]
    public void ToWorld_GivesTheCentreOfTheCell()
    {
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Vector3 result = space.ToWorld(new GridPosition(2, 0, 3));

        Assert.That(result, Is.EqualTo(new Vector3(2.5f, 0.5f, 3.5f)).Using(Near));
    }

    [Test]
    public void ToWorld_AddsTheOrigin()
    {
        // Cell (7, 0, 7) is the middle of a 15 x 15 arena: (-7.5 + 7.5, 0.5, -7.5 + 7.5)
        Assert.That(Arena().ToWorld(P(7, 0, 7)), Is.EqualTo(new Vector3(0f, 0.5f, 0f)).Using(Near));
    }

    [Test]
    public void ToWorld_ScalesByTheCellSize()
    {
        GridSpace space = new GridSpace(Vector3.zero, 2f);

        // (1 + 0.5) * 2 = 3,  (0 + 0.5) * 2 = 1,  (4 + 0.5) * 2 = 9
        Assert.That(space.ToWorld(P(1, 0, 4)), Is.EqualTo(new Vector3(3f, 1f, 9f)).Using(Near));
    }

    [Test]
    public void ToWorld_HigherLayers_AreHigherUp()
    {
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToWorld(P(0, 2, 0)).y, Is.EqualTo(2.5f).Within(0.001f));
    }

    //////////////////////////////////
    // ---------- ToGrid ---------- //
    //////////////////////////////////

    [Test]
    public void ToGrid_AnywhereInsideACell_GivesThatCell()
    {
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToGrid(new Vector3(2.1f, 0.1f, 3.9f)), Is.EqualTo(P(2, 0, 3)));
        Assert.That(space.ToGrid(new Vector3(2.5f, 0.5f, 3.5f)), Is.EqualTo(P(2, 0, 3)));
        Assert.That(space.ToGrid(new Vector3(2.99f, 0.99f, 3.01f)), Is.EqualTo(P(2, 0, 3)));
    }

    [Test]
    public void ToGrid_ExactlyOnALowerEdge_BelongsToThatCell()
    {
        // Floor, not round: 2.0 is the first point of cell 2, not the last point of cell 1
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToGrid(new Vector3(2f, 0f, 3f)), Is.EqualTo(P(2, 0, 3)));
    }

    [Test]
    public void ToGrid_NegativeWorldPositions_RoundDown_NotTowardsZero()
    {
        // -0.5 is in cell -1. Casting to int would wrongly give 0.
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToGrid(new Vector3(-0.5f, 0.5f, -0.5f)), Is.EqualTo(P(-1, 0, -1)));
    }

    [Test]
    public void ToGrid_UsesTheOriginAndCellSize()
    {
        GridSpace space = new GridSpace(new Vector3(-10f, 0f, -10f), 2f);

        // World (3, 1, 5) minus the origin is (13, 1, 15). Divided by 2 that's (6.5, 0.5, 7.5).
        Assert.That(space.ToGrid(new Vector3(3f, 1f, 5f)), Is.EqualTo(P(6, 0, 7)));
    }

    [Test]
    public void ToGrid_IsTheReverseOfToWorld_ForEveryCellInTheArena()
    {
        GridSpace space = Arena();

        for (int x = 0; x < 15; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 15; z++)
                {
                    GridPosition cell = P(x, y, z);
                    Assert.That(space.ToGrid(space.ToWorld(cell)), Is.EqualTo(cell), $"cell {cell}");
                }
    }

    /////////////////////////////////////////////////
    // ---------- Floor centres and corners ---------- //
    /////////////////////////////////////////////////

    [Test]
    public void ToWorldFloorCentre_IsTheMiddleOfTheCellsBottomFace()
    {
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToWorldFloorCentre(P(2, 0, 3)), Is.EqualTo(new Vector3(2.5f, 0f, 3.5f)).Using(Near));
        Assert.That(space.ToWorldFloorCentre(P(2, 1, 3)), Is.EqualTo(new Vector3(2.5f, 1f, 3.5f)).Using(Near));
    }

    [Test]
    public void ToWorldFloorCentre_ScalesByTheCellSize()
    {
        GridSpace space = new GridSpace(Vector3.zero, 2f);

        // Centre (3, 1, 3), minus half a cell (1) straight down
        Assert.That(space.ToWorldFloorCentre(P(1, 0, 1)), Is.EqualTo(new Vector3(3f, 0f, 3f)).Using(Near));
    }

    [Test]
    public void ToWorldCorner_IsTheCellsLowestCorner()
    {
        Assert.That(Arena().ToWorldCorner(P(0, 0, 0)), Is.EqualTo(new Vector3(-7.5f, 0f, -7.5f)).Using(Near));
        Assert.That(Arena().ToWorldCorner(P(7, 1, 7)), Is.EqualTo(new Vector3(-0.5f, 1f, -0.5f)).Using(Near));
    }

    [Test]
    public void ToWorldCorner_NextCellsCorner_IsOneCellAlong()
    {
        GridSpace space = new GridSpace(new Vector3(1f, 2f, 3f), 2f);

        Vector3 here = space.ToWorldCorner(P(4, 0, 5));
        Vector3 next = space.ToWorldCorner(P(5, 1, 6));

        Assert.That(next - here, Is.EqualTo(new Vector3(2f, 2f, 2f)).Using(Near));
    }

    [Test]
    public void ToWorldCorner_IsHalfACellFromTheCentre()
    {
        GridSpace space = Arena();
        GridPosition cell = P(3, 0, 9);

        Assert.That(space.ToWorld(cell) - space.ToWorldCorner(cell), Is.EqualTo(new Vector3(0.5f, 0.5f, 0.5f)).Using(Near));
    }

    ///////////////////////////////////////////
    // ---------- ToGridOnSurface ---------- //
    ///////////////////////////////////////////
    // A mouse click lands exactly ON a surface, which is the boundary between two cells.
    // Float maths can put that point a hair on the wrong side. Nudging along the surface's
    // normal (which points out of the thing you clicked) always lands in the cell in front.

    [Test]
    public void ToGridOnSurface_ClickingTheFloor_GivesTheCellStandingOnIt()
    {
        GridSpace space = Arena();

        // The floor's top is y = 0. A hit a hair below it would give layer -1 without the nudge.
        Vector3 hit = new Vector3(0.2f, -0.000001f, 0.3f);

        Assert.That(space.ToGrid(hit).Y, Is.EqualTo(-1));                       // the problem
        Assert.That(space.ToGridOnSurface(hit, Vector3.up), Is.EqualTo(P(7, 0, 7)));   // the fix
    }

    [Test]
    public void ToGridOnSurface_ClickingTheTopOfABlock_GivesTheCellAboveIt()
    {
        // A block filling cell (2, 0, 3) has its top at y = 1
        GridSpace space = new GridSpace(Vector3.zero, 1f);
        Vector3 hit = new Vector3(2.5f, 0.9999999f, 3.5f);

        Assert.That(space.ToGridOnSurface(hit, Vector3.up), Is.EqualTo(P(2, 1, 3)));
    }

    [Test]
    public void ToGridOnSurface_ClickingTheSideOfABlock_GivesTheCellBesideIt()
    {
        // A block filling cell (2, 0, 3): its +X face is at x = 3, its -X face at x = 2
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Vector3 plusXFace = new Vector3(2.999999f, 0.5f, 3.5f);
        Assert.That(space.ToGrid(plusXFace).X, Is.EqualTo(2));                                  // inside the block
        Assert.That(space.ToGridOnSurface(plusXFace, Vector3.right), Is.EqualTo(P(3, 0, 3)));   // in front of it

        Vector3 minusXFace = new Vector3(2.000001f, 0.5f, 3.5f);
        Assert.That(space.ToGridOnSurface(minusXFace, Vector3.left), Is.EqualTo(P(1, 0, 3)));

        Vector3 plusZFace = new Vector3(2.5f, 0.5f, 3.999999f);
        Assert.That(space.ToGridOnSurface(plusZFace, Vector3.forward), Is.EqualTo(P(2, 0, 4)));
    }

    [Test]
    public void ToGridOnSurface_MiddleOfACell_IsNotChangedByTheNudge()
    {
        // The nudge is tiny (1% of a cell), so it never pushes a point that's well inside a cell
        GridSpace space = new GridSpace(Vector3.zero, 1f);

        Assert.That(space.ToGridOnSurface(new Vector3(2.5f, 0.5f, 3.5f), Vector3.up), Is.EqualTo(P(2, 0, 3)));
    }

    [Test]
    public void ToGridOnSurface_NudgeGrowsWithTheCellSize()
    {
        // With 2-unit cells the nudge is 0.02, enough to clear a 0.015 error
        GridSpace space = new GridSpace(Vector3.zero, 2f);
        Vector3 hit = new Vector3(1f, -0.015f, 1f);

        Assert.That(space.ToGridOnSurface(hit, Vector3.up), Is.EqualTo(P(0, 0, 0)));
    }

    //////////////////////////////////////
    // ---------- Bad cell sizes ---------- //
    //////////////////////////////////////

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(-0.0001f)]
    [TestCase(float.NaN)]
    public void ZeroNegativeOrNaNCellSize_FallsBackToOne(float bad)
    {
        GridSpace space = new GridSpace(Vector3.zero, bad);

        Assert.That(space.CellSize, Is.EqualTo(1f));
        Assert.That(space.ToGrid(new Vector3(2.5f, 0.5f, 3.5f)), Is.EqualTo(P(2, 0, 3)));   // no divide by zero
    }

    [Test]
    public void DefaultCellSize_IsOne()
    {
        Assert.That(new GridSpace(Vector3.zero).CellSize, Is.EqualTo(1f));
    }

    [Test]
    public void TinyPositiveCellSize_IsKept()
    {
        Assert.That(new GridSpace(Vector3.zero, 0.25f).CellSize, Is.EqualTo(0.25f));
    }
}
