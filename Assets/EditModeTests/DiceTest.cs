using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DnDCombat.Core;

public class DiceTest
{
    // A Test behaves as an ordinary method
    [Test]
    public void RollD20_StaysWithinOneToTwenty()
    {
        Dice dice = new Dice(12345);
        for(int i = 0; i < 5000; i++)
        {
            int result = dice.RollD20();
            Assert.That(result, Is.InRange(1, 20));
        }
    }
    [Test]
    public void Roll1d6_StaysWithinOneToSix()
    {
        Dice dice = new Dice(54321);
        for (int i = 0; i < 5000; i++)
        {
            int result = dice.Roll(1, 6);
            Assert.That(result, Is.InRange(1, 6));
        }
    }
    [Test]
    public void Roll3d8_StaysWithinThreeToTwentyFour()
    {
        Dice dice = new Dice(54321);
        for (int i = 0; i < 5000; i++)
        {
            int result = dice.Roll(3, 8);
            Assert.That(result, Is.InRange(3, 24));
        }
    }
    [Test]
    public void NaturalTwenty_AlwaysHitsAndIsACrit()
    {
        FakeDice dice = new FakeDice(20, 9);            // d20 shows 20, then the damage roll totals 9
        Creature fighter = new Creature("Fighter", strength: 16);
        Creature target = new Creature("Target", maxHP: 30, armourClass: 30);   // AC far too high to hit normally

        // resolve an attack: +5 to hit, 1d8 damage plus the Strength modifier
        // assert: it hit despite the AC, IsCrit is true, and the damage is what you expect
    }
}
