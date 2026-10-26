using NUnit.Framework;
using DnDCombat.Core;

public class StatBlocksTest
{
    ///////////////////////////////////
    // ---------- Fighter ---------- //
    ///////////////////////////////////

    [Test]
    public void Fighter_HasTheExpectedStats()
    {
        Creature fighter = StatBlocks.Fighter();

        Assert.That(fighter.Name, Is.EqualTo("Fighter"));
        Assert.That(fighter.MaxHP, Is.EqualTo(12));
        Assert.That(fighter.CurrentHP, Is.EqualTo(12));
        Assert.That(fighter.ArmourClass, Is.EqualTo(17));   // chain mail 16 + Defense 1
        Assert.That(fighter.Speed, Is.EqualTo(30));
        Assert.That(fighter.ProficiencyBonus, Is.EqualTo(2));
        Assert.That(fighter.AttacksPerAction, Is.EqualTo(1));
        Assert.That(fighter.GetScore(Ability.Strength), Is.EqualTo(16));
        Assert.That(fighter.GetScore(Ability.Constitution), Is.EqualTo(14));
    }

    [Test]
    public void Fighter_CanBeNamed()
    {
        Assert.That(StatBlocks.Fighter("Aldric").Name, Is.EqualTo("Aldric"));
    }

    [Test]
    public void FighterWeapons_MatchTheFightersStrengthAndProficiency()
    {
        Creature fighter = StatBlocks.Fighter();
        int strMod = fighter.GetModifier(Ability.Strength);

        foreach (AttackProfile weapon in new[] { StatBlocks.FighterGreatsword, StatBlocks.FighterFlail })
        {
            Assert.That(weapon.AttackBonus, Is.EqualTo(strMod + fighter.ProficiencyBonus), weapon.Name);
            Assert.That(weapon.DamageBonus, Is.EqualTo(strMod), weapon.Name);
            Assert.That(weapon.RangeSquares, Is.EqualTo(1), weapon.Name);
        }

        Assert.That(StatBlocks.FighterGreatsword.DamageDiceCount, Is.EqualTo(2));
        Assert.That(StatBlocks.FighterGreatsword.DamageSides, Is.EqualTo(6));
        Assert.That(StatBlocks.FighterFlail.DamageDiceCount, Is.EqualTo(1));
        Assert.That(StatBlocks.FighterFlail.DamageSides, Is.EqualTo(8));
    }

    ////////////////////////////////////
    // ---------- Skeleton ---------- //
    ////////////////////////////////////

    [Test]
    public void Skeleton_HasTheExpectedStats()
    {
        Creature skeleton = StatBlocks.Skeleton();

        Assert.That(skeleton.Name, Is.EqualTo("Skeleton"));
        Assert.That(skeleton.MaxHP, Is.EqualTo(13));
        Assert.That(skeleton.ArmourClass, Is.EqualTo(14));
        Assert.That(skeleton.Speed, Is.EqualTo(30));
        Assert.That(skeleton.GetScore(Ability.Dexterity), Is.EqualTo(16));
        Assert.That(skeleton.GetModifier(Ability.Dexterity), Is.EqualTo(3));
        Assert.That(skeleton.GetScore(Ability.Wisdom), Is.EqualTo(8));
    }

    [Test]
    public void EachCallMakesANewCreature()
    {
        Creature first = StatBlocks.Skeleton();
        Creature second = StatBlocks.Skeleton();

        first.TakeDamage(5);

        Assert.That(second.CurrentHP, Is.EqualTo(13));
    }

    ///////////////////////////////////////
    // ---------- Consistency ---------- //
    ///////////////////////////////////////

    // These catch typos: an attack's numbers should follow from its creature
    [Test]
    public void FighterLongsword_MatchesTheFightersStrengthAndProficiency()
    {
        Creature fighter = StatBlocks.Fighter();
        AttackProfile sword = StatBlocks.FighterLongsword;

        Assert.That(sword.AttackBonus, Is.EqualTo(fighter.GetModifier(Ability.Strength) + fighter.ProficiencyBonus));
        Assert.That(sword.DamageBonus, Is.EqualTo(fighter.GetModifier(Ability.Strength)));
        Assert.That(sword.DamageSides, Is.EqualTo(8));
        Assert.That(sword.RangeSquares, Is.EqualTo(1));
    }

    [Test]
    public void SkeletonShortsword_MatchesTheSkeletonsDexterityAndProficiency()
    {
        Creature skeleton = StatBlocks.Skeleton();
        AttackProfile sword = StatBlocks.SkeletonShortsword;

        Assert.That(sword.AttackBonus, Is.EqualTo(skeleton.GetModifier(Ability.Dexterity) + skeleton.ProficiencyBonus));
        Assert.That(sword.DamageBonus, Is.EqualTo(skeleton.GetModifier(Ability.Dexterity)));
        Assert.That(sword.DamageSides, Is.EqualTo(6));
    }

    [Test]
    public void FighterHP_IsTenPlusConstitutionModifier()
    {
        Creature fighter = StatBlocks.Fighter();

        Assert.That(fighter.MaxHP, Is.EqualTo(10 + fighter.GetModifier(Ability.Constitution)));
    }
}