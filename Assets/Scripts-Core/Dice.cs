using System;

namespace DnDCombat.Core
{
    public class Dice : IDice
    {
        private readonly Random _random;
        public Dice(int seed)
        {
            // Receive a random seed
            _random = new Random(seed);
        }
        public int Roll(int count, int sides)
        {
            if(count <= 0 || sides <= 0)
            {
                return 0;
            }
            else
            {
                int total = 0;
                for (int i = 0; i < count; i++)
                {
                    total += _random.Next(1, sides + 1);
                }
                return total;
            }   
        }
        public int RollD20()
        {
            int roll = Roll(1, 20);
            return roll;
        }
    }
}

