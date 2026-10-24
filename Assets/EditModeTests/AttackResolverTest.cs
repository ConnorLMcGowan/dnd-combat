using NUnit.Framework;
using DnDCombat.Core;

public class AttackResolverTest
{
    [Test]
    public void NaturalTwenty_HitsAndCrits_EvenAgainstHugeAC()
    {
        FakeDice dice = new FakeDice(20, 9);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 30);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, 3);

        Assert.That(result.Hit, Is.True);
        Assert.That(result.IsCrit, Is.True);
    }

    [Test]
    public void NaturalOne_MissesEvenWithHugeBonus_AndRollsNoDamage()
    {
        FakeDice dice = new FakeDice(1, 5);   // the 5 should never be used
        Creature target = new Creature("Target", maxHP: 30, armourClass: 1);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 20, 1, 8, 3);

        Assert.That(result.Hit, Is.False);
        Assert.That(result.IsCrit, Is.False);
        Assert.That(result.Damage, Is.EqualTo(0));
        Assert.That(dice.Remaining, Is.EqualTo(1));
        Assert.That(target.CurrentHP, Is.EqualTo(30));
    }

    [TestCase(10, true)]    // 10 + 5 = 15, exactly AC 15
    [TestCase(9, false)]    // 9 + 5 = 14, one short
    public void TotalVersusAC_HitsOnlyWhenEqualOrHigher(int d20, bool expectedHit)
    {
        FakeDice dice = new FakeDice(d20, 4);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 15);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 5, 1, 8, 0);

        Assert.That(result.Hit, Is.EqualTo(expectedHit));
    }

    [Test]
    public void NormalHit_RollsTheGivenDice_AndAddsBonusOnce()
    {
        FakeDice dice = new FakeDice(15, 6);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 10);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 2, 6, 3);

        Assert.That(dice.LastRollCount, Is.EqualTo(2));
        Assert.That(dice.LastRollSides, Is.EqualTo(6));
        Assert.That(result.Damage, Is.EqualTo(9));      // 6 + 3
        Assert.That(target.CurrentHP, Is.EqualTo(21));
    }

    [Test]
    public void Crit_DoublesTheDice_ButNotTheBonus()
    {
        FakeDice dice = new FakeDice(20, 12);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 10);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, 3);

        Assert.That(dice.LastRollCount, Is.EqualTo(2));
        Assert.That(result.Damage, Is.EqualTo(15));     // 12 + 3, not 12 + 6
        Assert.That(target.CurrentHP, Is.EqualTo(15));
    }

    [Test]
    public void HeavyPenalty_DamageNeverGoesNegative()
    {
        FakeDice dice = new FakeDice(15, 1);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 10);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, -5);

        Assert.That(result.Damage, Is.EqualTo(0));
        Assert.That(target.CurrentHP, Is.EqualTo(30));
    }

    [Test]
    public void KillingBlow_DefeatsTarget()
    {
        FakeDice dice = new FakeDice(15, 8);
        Creature target = new Creature("Target", maxHP: 5, armourClass: 10);
        AttackResolver resolver = new AttackResolver(dice);

        AttackResult result = resolver.Resolve(new Creature("Attacker"), target, 0, 1, 8, 0);

        Assert.That(result.Damage, Is.EqualTo(8));      // rolled damage, as decided
        Assert.That(target.CurrentHP, Is.EqualTo(0));
        Assert.That(target.IsDefeated, Is.True);
    }
}