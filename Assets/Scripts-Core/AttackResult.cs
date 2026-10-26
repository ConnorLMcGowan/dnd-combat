using System;

namespace DnDCombat.Core
{
    public class AttackResult
    {
        public int D20Roll { get; }
        public int AttackTotal { get; }
        public bool Hit { get; }
        public bool IsCrit { get; }
        public int RolledDamage { get; }      // the raw roll, before the target's damage rules
        public int Damage { get; }            // what landed, after resistance, vulnerability or immunity (before the HP cap)
        public DamageEffect Effect { get; }
        public bool IsCriticalMiss => D20Roll == 1;

        public AttackResult(int d20Roll, int attackTotal, bool hit, bool isCrit,
                            int rolledDamage, int damage, DamageEffect effect)
        {
            D20Roll = d20Roll;
            AttackTotal = attackTotal;
            Hit = hit;
            IsCrit = isCrit;
            RolledDamage = rolledDamage;
            Damage = damage;
            Effect = effect;
        }
    }

}
