namespace DnDCombat.Core
{
    // A snapshot of one attack, taken the moment it landed, so the UI can play it back later
    public readonly struct AttackReport
    {
        public Combatant Attacker { get; }
        public Combatant Target { get; }
        public AttackResult Result { get; }
        public int TargetHPAfter { get; }      // the target's HP right after this attack
        public bool TargetDefeated { get; }

        public AttackReport(Combatant attacker, Combatant target, AttackResult result,
                            int targetHPAfter, bool targetDefeated)
        {
            Attacker = attacker;
            Target = target;
            Result = result;
            TargetHPAfter = targetHPAfter;
            TargetDefeated = targetDefeated;
        }
    }
}