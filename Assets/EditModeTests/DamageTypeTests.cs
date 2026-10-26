using NUnit.Framework;
using DnDCombat.Core;

public class DamageTypeTest
{
    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    // A sturdy target with an easy AC, so hits land and nothing dies by accident
    private static Creature Target(DamageType[] resist = null, DamageType[] vulnerable = null, DamageType[] immune = null)
    {
        return new Creature("Target", maxHP: 30, armourClass: 10,
                            resistances: resist, vulnerabilities: vulnerable, immunities: immune);
    }

    private static AttackResult Hit(Creature target, int damageRoll, int bonus, DamageType type, int d20 = 15)
    {
        AttackResolver resolver = new AttackResolver(new FakeDice(d20, damageRoll));
        return resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, bonus, type);
    }

    ///////////////////////////////////////
    // ---------- DamageRules ---------- //
    ///////////////////////////////////////

    [Test]
    public void NoRules_DamageIsUnchanged()
    {
        DamageAdjustment result = DamageRules.Adjust(Target(), DamageType.Fire, 9);

        Assert.That(result.Amount, Is.EqualTo(9));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Normal));
    }

    [Test]
    public void Vulnerable_DoublesTheDamage()
    {
        DamageAdjustment result = DamageRules.Adjust(Target(vulnerable: new[] { DamageType.Fire }), DamageType.Fire, 9);

        Assert.That(result.Amount, Is.EqualTo(18));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Vulnerable));
    }

    [TestCase(8, 4)]
    [TestCase(7, 3)]   // rounds down
    [TestCase(1, 0)]
    public void Resistant_HalvesTheDamage_RoundingDown(int rolled, int expected)
    {
        DamageAdjustment result = DamageRules.Adjust(Target(resist: new[] { DamageType.Fire }), DamageType.Fire, rolled);

        Assert.That(result.Amount, Is.EqualTo(expected));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Resisted));
    }

    [Test]
    public void Immune_TakesNothing()
    {
        DamageAdjustment result = DamageRules.Adjust(Target(immune: new[] { DamageType.Poison }), DamageType.Poison, 9);

        Assert.That(result.Amount, Is.EqualTo(0));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Immune));
    }

    [Test]
    public void ImmunityBeatsVulnerability()
    {
        Creature target = Target(vulnerable: new[] { DamageType.Poison }, immune: new[] { DamageType.Poison });

        DamageAdjustment result = DamageRules.Adjust(target, DamageType.Poison, 9);

        Assert.That(result.Amount, Is.EqualTo(0));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Immune));
    }

    [Test]
    public void ResistanceAndVulnerability_HalveThenDouble_AndReportNormal()
    {
        Creature target = Target(resist: new[] { DamageType.Fire }, vulnerable: new[] { DamageType.Fire });

        DamageAdjustment result = DamageRules.Adjust(target, DamageType.Fire, 7);   // 7 -> 3 -> 6

        Assert.That(result.Amount, Is.EqualTo(6));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Normal));
    }

    [Test]
    public void OtherDamageTypes_AreNotAffected()
    {
        Creature target = Target(vulnerable: new[] { DamageType.Bludgeoning });

        DamageAdjustment result = DamageRules.Adjust(target, DamageType.Slashing, 9);

        Assert.That(result.Amount, Is.EqualTo(9));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Normal));
    }

    [Test]
    public void NullTarget_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => DamageRules.Adjust(null, DamageType.Fire, 5));
    }

    ////////////////////////////////////////
    // ---------- Through Resolve ---------- //
    ////////////////////////////////////////

    [Test]
    public void VulnerableHit_ReportsBothNumbers_AndLowersHPByTheAdjustedOne()
    {
        Creature target = Target(vulnerable: new[] { DamageType.Bludgeoning });

        AttackResult result = Hit(target, 5, 3, DamageType.Bludgeoning);   // rolled 8

        Assert.That(result.RolledDamage, Is.EqualTo(8));
        Assert.That(result.Damage, Is.EqualTo(16));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Vulnerable));
        Assert.That(target.CurrentHP, Is.EqualTo(14));
    }

    [Test]
    public void ImmuneHit_DoesNoDamage_ButStillCountsAsAHit()
    {
        Creature target = Target(immune: new[] { DamageType.Poison });

        AttackResult result = Hit(target, 5, 3, DamageType.Poison);

        Assert.That(result.Hit, Is.True);
        Assert.That(result.RolledDamage, Is.EqualTo(8));
        Assert.That(result.Damage, Is.EqualTo(0));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Immune));
        Assert.That(target.CurrentHP, Is.EqualTo(30));
    }

    [Test]
    public void ResistedHit_RoundsDown()
    {
        Creature target = Target(resist: new[] { DamageType.Slashing });

        AttackResult result = Hit(target, 6, 1, DamageType.Slashing);   // rolled 7

        Assert.That(result.Damage, Is.EqualTo(3));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Resisted));
        Assert.That(target.CurrentHP, Is.EqualTo(27));
    }

    [Test]
    public void Crit_DoublesTheDiceFirst_ThenTheAdjustmentApplies()
    {
        Creature target = Target(vulnerable: new[] { DamageType.Bludgeoning });

        AttackResult result = Hit(target, 12, 3, DamageType.Bludgeoning, d20: 20);   // rolled 15

        Assert.That(result.IsCrit, Is.True);
        Assert.That(result.RolledDamage, Is.EqualTo(15));
        Assert.That(result.Damage, Is.EqualTo(30));
    }

    [Test]
    public void Miss_ReportsNoDamage_AndANormalEffect()
    {
        Creature target = Target(vulnerable: new[] { DamageType.Bludgeoning });
        AttackResolver resolver = new AttackResolver(new FakeDice(2));

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, 3, DamageType.Bludgeoning);

        Assert.That(result.Hit, Is.False);
        Assert.That(result.RolledDamage, Is.EqualTo(0));
        Assert.That(result.Damage, Is.EqualTo(0));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Normal));
    }

    ////////////////////////////////////////////
    // ---------- Through the controller ---------- //
    ////////////////////////////////////////////

    [Test]
    public void TryAttack_UsesTheProfilesDamageType()
    {
        FakeDice dice = new FakeDice(15, 5, 14, 6);   // initiative (hero first), then a d20 and a damage roll
        Combatant hero = new Combatant(new Creature("Hero"), Team.Heroes);
        Combatant enemy = new Combatant(Target(vulnerable: new[] { DamageType.Bludgeoning }), Team.Enemies);
        Grid grid = new Grid(5, 2, 5);
        grid.Place(hero, new GridPosition(1, 0, 1));
        grid.Place(enemy, new GridPosition(2, 0, 1));
        TurnManager turns = new TurnManager(dice, new[] { hero, enemy });
        turns.StartCombat();
        CombatController controller = new CombatController(turns, grid, new AttackResolver(dice));
        AttackProfile mace = new AttackProfile("Mace", 5, 1, 6, 2, DamageType.Bludgeoning);

        AttackOutcome outcome = controller.TryAttack(hero, enemy, mace);

        Assert.That(outcome.Result.Effect, Is.EqualTo(DamageEffect.Vulnerable));
        Assert.That(outcome.Result.Damage, Is.EqualTo(16));   // (6 + 2) doubled
        Assert.That(enemy.Creature.CurrentHP, Is.EqualTo(14));
    }

    /////////////////////////////////////////
    // ---------- Stat blocks ---------- //
    /////////////////////////////////////////

    [Test]
    public void Skeleton_IsVulnerableToBludgeoning_ImmuneToPoison_AndNothingElse()
    {
        Creature skeleton = StatBlocks.Skeleton();

        Assert.That(skeleton.IsVulnerableTo(DamageType.Bludgeoning), Is.True);
        Assert.That(skeleton.IsImmuneTo(DamageType.Poison), Is.True);
        Assert.That(skeleton.IsVulnerableTo(DamageType.Slashing), Is.False);
        Assert.That(skeleton.IsResistantTo(DamageType.Bludgeoning), Is.False);
    }

    [Test]
    public void WeaponsHaveTheRightTypes()
    {
        Assert.That(StatBlocks.FighterGreatsword.DamageType, Is.EqualTo(DamageType.Slashing));
        Assert.That(StatBlocks.FighterLongsword.DamageType, Is.EqualTo(DamageType.Slashing));
        Assert.That(StatBlocks.FighterHalberd.DamageType, Is.EqualTo(DamageType.Slashing));
        Assert.That(StatBlocks.FighterFlail.DamageType, Is.EqualTo(DamageType.Bludgeoning));
        Assert.That(StatBlocks.SkeletonShortsword.DamageType, Is.EqualTo(DamageType.Piercing));
        Assert.That(StatBlocks.FighterHalberd.RangeSquares, Is.EqualTo(2));
    }

    [Test]
    public void FighterFlail_DoublesAgainstTheSkeleton_ButTheGreatswordDoesNot()
    {
        Creature fighter = StatBlocks.Fighter();
        AttackProfile flail = StatBlocks.FighterFlail;
        AttackProfile greatsword = StatBlocks.FighterGreatsword;

        Creature skeleton1 = StatBlocks.Skeleton();
        AttackResult flailHit = new AttackResolver(new FakeDice(15, 4)).Resolve(
            fighter, skeleton1, flail.AttackBonus, flail.DamageDiceCount, flail.DamageSides, flail.DamageBonus, flail.DamageType);

        Creature skeleton2 = StatBlocks.Skeleton();
        AttackResult swordHit = new AttackResolver(new FakeDice(15, 4)).Resolve(
            fighter, skeleton2, greatsword.AttackBonus, greatsword.DamageDiceCount, greatsword.DamageSides, greatsword.DamageBonus, greatsword.DamageType);

        Assert.That(flailHit.RolledDamage, Is.EqualTo(7));   // 4 + 3
        Assert.That(flailHit.Damage, Is.EqualTo(14));
        Assert.That(flailHit.Effect, Is.EqualTo(DamageEffect.Vulnerable));
        Assert.That(swordHit.Damage, Is.EqualTo(7));
        Assert.That(swordHit.Effect, Is.EqualTo(DamageEffect.Normal));
    }
}
