using System;
using System.Collections.Generic;
using NUnit.Framework;
using DnDCombat.Core;

public class CreatureEdgeCaseTest
{
    // Builds a creature with every score at 10 except the one under test
    private static Creature CreateWithScore(Ability ability, int score)
    {
        int[] s = { 10, 10, 10, 10, 10, 10 };
        s[(int)ability] = score;
        return new Creature("TestCreature", s[0], s[1], s[2], s[3], s[4], s[5]);
    }

    [TestCase(1)]
    [TestCase(30)]
    [TestCase(0)]
    [TestCase(-5)]
    [TestCase(45)]
    public void GetScore_EdgeScores_DoNotThrowAndReturnStoredValue(int score)
    {
        foreach (Ability ability in Enum.GetValues(typeof(Ability)))
        {
            Creature creature = CreateWithScore(ability, score);
            int result = 0;
            Assert.DoesNotThrow(() => result = creature.GetScore(ability), $"{ability} threw");
            Assert.That(result, Is.EqualTo(score), $"Wrong score for {ability}");
        }
    }

    [TestCase(1, -5)]
    [TestCase(29, 9)]
    [TestCase(30, 10)]
    [TestCase(0, -5)]
    [TestCase(-5, -8)]
    [TestCase(45, 17)]
    public void GetModifier_EdgeScores_DoNotThrowAndRoundDown(int score, int expected)
    {
        foreach (Ability ability in Enum.GetValues(typeof(Ability)))
        {
            Creature creature = CreateWithScore(ability, score);
            int result = 0;
            Assert.DoesNotThrow(() => result = creature.GetModifier(ability), $"{ability} threw");
            Assert.That(result, Is.EqualTo(expected), $"Wrong modifier for {ability}");
        }
    }

    [Test]
    public void GetScore_InvalidAbility_Throws()
    {
        Creature creature = CreateWithScore(Ability.Strength, 10);
        Assert.Throws<KeyNotFoundException>(() => creature.GetScore((Ability)99));
    }

    //////////////////////////////////
    // ---------- Damage ---------- //
    //////////////////////////////////

    [TestCase(1, 9)]
    [TestCase(0, 10)]
    [TestCase(5, 5)]
    public void TakeDamage_ShouldDecreaseCurrentHealthByAmount(int amount, int expected)
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        creature.TakeDamage(amount);
        Assert.That(creature.CurrentHP, Is.EqualTo(expected));
    }

    [Test]
    public void TakeDamage_MoreThanCurrentHP_StopsAtZeroAndDefeats()
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        creature.TakeDamage(50);
        Assert.That(creature.CurrentHP, Is.EqualTo(0));
        Assert.That(creature.IsDefeated, Is.True);
    }

    [Test]
    public void TakeDamage_EqualToCurrentHP_Defeats()
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        creature.TakeDamage(10);
        Assert.That(creature.CurrentHP, Is.EqualTo(0));
        Assert.That(creature.IsDefeated, Is.True);
    }

    [Test]
    public void CreatureAtOneHP_IsNotDefeated()
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        creature.TakeDamage(9);
        Assert.That(creature.CurrentHP, Is.EqualTo(1));
        Assert.That(creature.IsDefeated, Is.False);
    }

    [TestCase(-5)]
    [TestCase(int.MinValue)]
    public void TakeDamage_NegativeAmount_LeavesHPUnchanged(int amount)
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        creature.TakeDamage(amount);
        Assert.That(creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void TakeDamage_IntMaxValue_DoesNotThrowAndLeavesHPAtZero()
    {
        Creature creature = new Creature("DamageTest", maxHP: 10);
        Assert.DoesNotThrow(() => creature.TakeDamage(int.MaxValue));
        Assert.That(creature.CurrentHP, Is.EqualTo(0));
    }
    ////////////////////////////////
    // ---------- Heal ---------- //
    ////////////////////////////////

    [TestCase(1, 6)]
    [TestCase(0, 5)]
    [TestCase(5, 10)]
    public void Heal_ShouldIncreaseCurrentHealth(int amount, int expected)
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.TakeDamage(5);
        creature.Heal(amount);
        Assert.That(creature.CurrentHP, Is.EqualTo(expected));
    }

    [TestCase(6)]
    [TestCase(100)]
    public void Heal_PastMaximum_StopsAtMaxHP(int amount)
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.TakeDamage(5);
        creature.Heal(amount);
        Assert.That(creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void Heal_AtFullHP_ChangesNothing()
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.Heal(5);
        Assert.That(creature.CurrentHP, Is.EqualTo(10));
    }

    [TestCase(-5)]
    [TestCase(int.MinValue)]
    public void Heal_NegativeAmount_LeavesHPUnchanged(int amount)
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.TakeDamage(5);
        creature.Heal(amount);
        Assert.That(creature.CurrentHP, Is.EqualTo(5));
    }

    [Test]
    public void Heal_IntMaxValue_DoesNotThrowAndEndsAtMaxHP()
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.TakeDamage(5);
        Assert.DoesNotThrow(() => creature.Heal(int.MaxValue));
        Assert.That(creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void Heal_DefeatedCreature_RevivesAboveZero()
    {
        Creature creature = new Creature("HealTest", maxHP: 10);
        creature.TakeDamage(10);
        Assert.That(creature.IsDefeated, Is.True);

        creature.Heal(3);
        Assert.That(creature.CurrentHP, Is.EqualTo(3));
        Assert.That(creature.IsDefeated, Is.False);
    }
}