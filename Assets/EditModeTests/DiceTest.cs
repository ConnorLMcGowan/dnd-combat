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
}
