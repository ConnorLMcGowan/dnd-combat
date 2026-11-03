namespace DnDCombat.Core
{
    // A snapshot of one attack, taken the moment it landed, so the UI can play it back later
    // TODO(guide): UI 2. The log needs to say WHAT was used ("with Flail") and the damage type
    //   ("7 bludgeoning"), and neither is in here yet. Both live in the AttackProfile.
    //   a. Add a property:  public AttackProfile Attack { get; }
    //   b. Add an AttackProfile attack parameter to the constructor, straight after target,
    //      and store it.
    //   c. In CombatController.TryAttack (step 4, "Announce the attack"), pass attack in that spot.
    //   Checkpoint: compiles (that's the only place a report is made), every test still passes.
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