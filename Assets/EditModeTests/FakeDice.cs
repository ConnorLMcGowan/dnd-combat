using DnDCombat.Core;
using System.Collections.Generic;

public class FakeDice : IDice
{
    private readonly Queue<int> _results;

    public int LastRollCount { get; private set; }
    public int LastRollSides { get; private set; }
    public int Remaining => _results.Count;

    public FakeDice(params int[] results)
    {
        _results = new Queue<int>(results);
    }

    public int Roll(int count, int sides)
    {
        LastRollCount = count;
        LastRollSides = sides;
        return _results.Dequeue();
    }
    public int RollD20() { return _results.Dequeue(); }
}
