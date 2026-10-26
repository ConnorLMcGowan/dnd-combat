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

        public AttackResult Resolve(Creature attacker, Creature target, int attackBonus, int damageDiceCount, int damageSides, int damageBonus, DamageType damageType)
        {
            int d20Roll = _dice.RollD20();
            int attackTotal = d20Roll + attackBonus;
            bool hit;
            bool isCrit;

            if (d20Roll == 20)
            {
                isCrit = true;
                hit = true;
                damageDiceCount = damageDiceCount * 2;
            }
            else if (d20Roll == 1)
            {
                return new AttackResult(d20Roll, attackTotal, false, false, 0, 0, DamageEffect.Normal);
            }
            else if (attackTotal < target.ArmourClass)
            {
                return new AttackResult(d20Roll, attackTotal, false, false, 0, 0, DamageEffect.Normal);
            }
            else
            {
                isCrit = false;
                hit = true;
            }

            int rolledDamage = Math.Max(0, _dice.Roll(damageDiceCount, damageSides) + damageBonus);
            DamageAdjustment adjusted = DamageRules.Adjust(target, damageType, rolledDamage);
            target.TakeDamage(adjusted.Amount);

            return new AttackResult(d20Roll, attackTotal, hit, isCrit, rolledDamage, adjusted.Amount, adjusted.Effect);
        }
    }
}
