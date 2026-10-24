using System;

namespace DnDCombat.Core
{
    public class AttackResult
    {
        public int D20Roll { get; }
        public int AttackTotal { get; }
        public bool Hit { get; }
        public bool IsCrit { get; }
        public int Damage { get; }

        public AttackResult(int d20Roll, int attackTotal, bool hit, bool isCrit, int damage)
        {
            D20Roll = d20Roll;
            AttackTotal = attackTotal;
            Hit = hit;
            IsCrit = isCrit;
            Damage = damage;
        }
    }

}

