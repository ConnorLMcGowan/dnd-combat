namespace DnDCombat.Core
{
    // Premade creatures and their attacks. Numbers come from the SRD 5.2, except where noted.
    public static class StatBlocks
    {
        ///////////////////////////////////
        // ---------- Fighter ---------- //
        ///////////////////////////////////

        // A premade level 1 Fighter using starting equipment option A (chain mail, greatsword, flail)
        // and the Defense fighting style. The ability scores are a design choice, not from the SRD.
        public static Creature Fighter(string name = "Fighter")
        {
            return new Creature(name,
                strength: 16, dexterity: 12, constitution: 14,
                intelligence: 8, wisdom: 10, charisma: 10,
                maxHP: 12, armourClass: 17, speed: 30, proficiencyBonus: 2);   // chain mail 16 + Defense 1
        }

        // +3 STR and +2 proficiency to hit. Two-handed slashing.
        public static readonly AttackProfile FighterGreatsword = new AttackProfile("Greatsword", 5, 2, 6, 3);

        // Same bonuses, one-handed bludgeoning
        public static readonly AttackProfile FighterFlail = new AttackProfile("Flail", 5, 1, 8, 3);

        // One-handed longsword: +3 STR and +2 proficiency to hit, 1d8 + 3 damage
        public static readonly AttackProfile FighterLongsword = new AttackProfile("Longsword", 5, 1, 8, 3);

        ////////////////////////////////////
        // ---------- Skeleton ---------- //
        ////////////////////////////////////

        // Check every number here against the SRD 5.2 before relying on it
        public static Creature Skeleton(string name = "Skeleton")
        {
            return new Creature(name,
                strength: 10, dexterity: 16, constitution: 15,
                intelligence: 6, wisdom: 8, charisma: 5,
                maxHP: 13, armourClass: 14, speed: 30, proficiencyBonus: 2);
        }

        public static readonly AttackProfile SkeletonShortsword = new AttackProfile("Shortsword", 5, 1, 6, 3);
    }
}