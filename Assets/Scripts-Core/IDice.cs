namespace DnDCombat.Core
{
    public interface IDice
    {
        int Roll(int count, int sides);
        int RollD20();
    }
}