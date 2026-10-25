using System;

namespace DnDCombat.Core
{
    public class AttackResolver
    {
        private readonly IDice _dice;
        public AttackResolver(IDice dice)
        {
            _dice = dice;
        }

        public AttackResult Resolve(Creature attacker, Creature target, int attackBonus, int damageDiceCount, int damageSides, int damageBonus)
        {
            int d20Roll = _dice.RollD20();
            int attackTotal = d20Roll + attackBonus;
            bool hit;
            bool isCrit;
            int damage;

            if (d20Roll == 20)
            {
                isCrit = true;
                hit = true;
                damageDiceCount = damageDiceCount * 2;
            }
            else if (d20Roll == 1)
            {
                isCrit = false;
                hit = false;
                damage = 0;
                return new AttackResult(d20Roll, attackTotal, hit, isCrit, damage);
            }
            else if(attackTotal < target.ArmourClass)
            {
                isCrit = false;
                hit = false;
                damage = 0;
                return new AttackResult(d20Roll, attackTotal, hit, isCrit, damage);
            }
            else
            {
                isCrit = false;
                hit = true;
            }

            damage = Math.Max(0, _dice.Roll(damageDiceCount, damageSides) + damageBonus);
            target.TakeDamage(damage);

            return new AttackResult(d20Roll, attackTotal, hit, isCrit, damage);
        }
    }
}
