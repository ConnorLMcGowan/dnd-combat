namespace DnDCombat.Core
{
    public readonly struct AttackProfile
    {
        public string Name { get; }
        public int AttackBonus { get; }
        public int DamageDiceCount { get; }
        public int DamageSides { get; }
        public int DamageBonus { get; }
        public int RangeSquares { get; }   // 1 = melee

        public AttackProfile(string name, int attackBonus, int damageDiceCount,
                             int damageSides, int damageBonus, int rangeSquares = 1)
        {
            Name = name;
            AttackBonus = attackBonus;
            DamageDiceCount = damageDiceCount;
            DamageSides = damageSides;
            DamageBonus = damageBonus;
            RangeSquares = rangeSquares;
        }
    }
}