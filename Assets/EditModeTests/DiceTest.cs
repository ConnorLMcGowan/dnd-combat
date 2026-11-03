using NUnit.Framework;
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
        AttackResolver resolver = new AttackResolver(dice);
        int strMod = fighter.GetModifier(Ability.Strength);   // 16 Strength gives +3

        // +5 to hit, 1d8 damage plus the Strength modifier
        AttackResult result = resolver.Resolve(fighter, target, 5, 1, 8, strMod, DamageType.Slashing);

        // It hit despite the AC: 20 + 5 = 25 is still under AC 30
        Assert.That(result.D20Roll, Is.EqualTo(20));
        Assert.That(result.AttackTotal, Is.EqualTo(25));
        Assert.That(result.Hit, Is.True);
        Assert.That(result.IsCrit, Is.True);

        // A crit doubles the dice, not the bonus: 1d8 becomes 2d8
        Assert.That(dice.LastRollCount, Is.EqualTo(2));
        Assert.That(dice.LastRollSides, Is.EqualTo(8));

        // Damage: the dice total 9, plus the +3 bonus once = 12
        Assert.That(result.RolledDamage, Is.EqualTo(12));
        Assert.That(result.Damage, Is.EqualTo(12));
        Assert.That(result.Effect, Is.EqualTo(DamageEffect.Normal));
        Assert.That(target.CurrentHP, Is.EqualTo(18));   // 30 - 12

        // Both rolls were used, and no others
        Assert.That(dice.Remaining, Is.EqualTo(0));
    }
}
