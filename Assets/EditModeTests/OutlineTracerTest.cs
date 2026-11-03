using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using DnDCombat.Core;

public class OutlineTracerTest
{
    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // Twice the area inside a loop, worked out from its corners (the "shoelace" formula).
    // Positive means anticlockwise (looking down, X right and Z up), negative means clockwise.
    private static int SignedDoubleArea(List<GridPosition> loop)
    {
        int sum = 0;
        for (int i = 0; i < loop.Count; i++)
        {
            GridPosition a = loop[i];
            GridPosition b = loop[(i + 1) % loop.Count];
            sum += a.X * b.Z - b.X * a.Z;
        }
        return sum;
    }

    // Every step between neighbouring corners is exactly one cell side long
    private static void AssertConnected(List<GridPosition> loop)
    {
        for (int i = 0; i < loop.Count; i++)
        {
            GridPosition a = loop[i];
            GridPosition b = loop[(i + 1) % loop.Count];
            int step = System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Z - b.Z);
            Assert.That(step, Is.EqualTo(1), $"corner {i}");
        }
    }

    ///////////////////////////////////////
    // ---------- Simple shapes ---------- //
    ///////////////////////////////////////

    [Test]
    public void NoCells_GivesNoLoops()
    {
        Assert.That(OutlineTracer.Trace(new List<GridPosition>()), Is.Empty);
        Assert.That(OutlineTracer.Trace(null), Is.Empty);
    }

    [Test]
    public void OneCell_IsOneAnticlockwiseSquare()
    {
        var loops = OutlineTracer.Trace(new[] { P(2, 0, 3) });

        Assert.That(loops.Count, Is.EqualTo(1));
        Assert.That(loops[0].Count, Is.EqualTo(4));
        Assert.That(loops[0], Is.EquivalentTo(new[] { P(2, 0, 3), P(3, 0, 3), P(3, 0, 4), P(2, 0, 4) }));
        Assert.That(SignedDoubleArea(loops[0]), Is.EqualTo(2));   // area 1, anticlockwise
        AssertConnected(loops[0]);
    }

    [Test]
    public void ThreeByTwoBlock_IsOneLoopRoundTheOutside()
    {
        var cells = new List<GridPosition>();
        for (int x = 0; x < 3; x++)
            for (int z = 0; z < 2; z++)
                cells.Add(P(x, 0, z));

        var loops = OutlineTracer.Trace(cells);

        Assert.That(loops.Count, Is.EqualTo(1));
        Assert.That(loops[0].Count, Is.EqualTo(10));              // perimeter 3 + 2 + 3 + 2
        Assert.That(SignedDoubleArea(loops[0]), Is.EqualTo(12));  // area 6, anticlockwise
        AssertConnected(loops[0]);
    }

    [Test]
    public void LShape_TracesTheInsideCornerToo()
    {
        // X X
        // X .      (cells (0,0), (0,1), (1,1))
        var loops = OutlineTracer.Trace(new[] { P(0, 0, 0), P(0, 0, 1), P(1, 0, 1) });

        Assert.That(loops.Count, Is.EqualTo(1));
        Assert.That(loops[0].Count, Is.EqualTo(8));
        Assert.That(loops[0], Does.Contain(P(1, 0, 1)));          // the inside corner
        Assert.That(SignedDoubleArea(loops[0]), Is.EqualTo(6));   // area 3
        AssertConnected(loops[0]);
    }

    //////////////////////////////////////
    // ---------- Tricky shapes ---------- //
    //////////////////////////////////////

    [Test]
    public void BlockInTheMiddle_GivesAClockwiseHoleLoop()
    {
        // A 3 x 3 square with the centre missing, like a block inside the reachable area
        var cells = new List<GridPosition>();
        for (int x = 0; x < 3; x++)
            for (int z = 0; z < 3; z++)
                if (!(x == 1 && z == 1)) cells.Add(P(x, 0, z));

        var loops = OutlineTracer.Trace(cells);

        Assert.That(loops.Count, Is.EqualTo(2));
        var outer = loops.Single(l => SignedDoubleArea(l) > 0);
        var hole = loops.Single(l => SignedDoubleArea(l) < 0);
        Assert.That(outer.Count, Is.EqualTo(12));
        Assert.That(SignedDoubleArea(outer), Is.EqualTo(18));     // the full 3 x 3
        Assert.That(hole, Is.EquivalentTo(new[] { P(1, 0, 1), P(2, 0, 1), P(2, 0, 2), P(1, 0, 2) }));
        Assert.That(SignedDoubleArea(hole), Is.EqualTo(-2));      // the missing centre, clockwise
        AssertConnected(outer);
        AssertConnected(hole);
    }

    [Test]
    public void CellsTouchingOnlyAtACorner_GetSeparateLoops()
    {
        // . X
        // X .      (they share corner (1, 1) and nothing else)
        var loops = OutlineTracer.Trace(new[] { P(0, 0, 0), P(1, 0, 1) });

        Assert.That(loops.Count, Is.EqualTo(2));
        foreach (var loop in loops)
        {
            Assert.That(loop.Count, Is.EqualTo(4));
            Assert.That(SignedDoubleArea(loop), Is.EqualTo(2));
            AssertConnected(loop);
        }
    }

    [Test]
    public void EverySideIsUsedExactlyOnce()
    {
        // An awkward blob: a plus sign with an arm missing and a stray cell touching a corner
        var cells = new[] { P(1, 0, 0), P(0, 0, 1), P(1, 0, 1), P(2, 0, 1), P(3, 0, 2) };

        var loops = OutlineTracer.Trace(cells);

        // Total corners over all loops = total outside sides: 4 for the stray, 10 for the T shape
        Assert.That(loops.Sum(l => l.Count), Is.EqualTo(14));
        Assert.That(loops.Sum(l => SignedDoubleArea(l)), Is.EqualTo(10));   // total area 5
        foreach (var loop in loops) AssertConnected(loop);
    }

    [Test]
    public void Corners_TakeTheHeightOfTheirCell()
    {
        var loops = OutlineTracer.Trace(new[] { P(0, 2, 0) });

        Assert.That(loops[0].All(c => c.Y == 2), Is.True);
    }
}
