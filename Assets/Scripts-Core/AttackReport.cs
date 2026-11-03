namespace DnDCombat.Core
{
    public readonly struct AttackReport
    {
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public AttackResult Result { get; }
        public int TargetHPAfter { get; }      // the target's HP right after this attack
        public bool TargetDefeated { get; }
        public AttackProfile Attack { get; }


        public AttackReport(Combatant attacker, Combatant target, AttackProfile attack, AttackResult result,
                            int targetHPAfter, bool targetDefeated)
        {
            Attacker = attacker;
            Target = target;
            Attack = attack;
            Result = result;
            TargetHPAfter = targetHPAfter;
            TargetDefeated = targetDefeated;
        }
    }
}
