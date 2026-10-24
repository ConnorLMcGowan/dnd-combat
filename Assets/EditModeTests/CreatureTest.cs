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
}