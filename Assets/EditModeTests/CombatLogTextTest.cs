using NUnit.Framework;
using DnDCombat.Core;

// CombatLogText: the exact sentence for every kind of attack. These compare whole strings,
// so a stray space or a changed word shows up straight away.
//
// Most use the real Fighter (Flail: +5 to hit, 1d8 + 3 bludgeoning) against the real Skeleton
// (AC 14, 13 HP, vulnerable to bludgeoning, immune to poison).
public class CombatLogTextTest
{
    private static Combatant Fighter() => new Combatant(StatBlocks.Fighter(), Team.Heroes);
    private static Combatant Skeleton() => new Combatant(StatBlocks.Skeleton(), Team.Enemies);

    // A report built by hand, so every number is known
    private static AttackReport Report(Combatant attacker, Combatant target, AttackProfile attack,
                                       int d20, int total, bool hit, bool crit,
                                       int rolled, int damage, DamageEffect effect,
                                       int hpAfter, bool defeated = false)
    {
        AttackResult result = new AttackResult(d20, total, hit, crit, rolled, damage, effect);
        return new AttackReport(attacker, target, attack, result, hpAfter, defeated);
    }

    ////////////////////////////////////
    // ---------- The roll ---------- //
    ////////////////////////////////////

    [Test]
    public void AMiss_ShowsTheSum_AndStops()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterFlail,
                                     4, 9, false, false, 0, 0, DamageEffect.Normal, 13);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Flail: 4 + 5 = 9 vs AC 14. Miss."));
    }

    [Test]
    public void ANaturalOne_IsACriticalMiss_WithNoSum()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterFlail,
                                     1, 6, false, false, 0, 0, DamageEffect.Normal, 13);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Flail: rolled a natural 1. Critical miss."));
    }

    [Test]
    public void ANaturalTwenty_IsACriticalHit_WithNoSum()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterFlail,
                                     20, 25, true, true, 13, 26, DamageEffect.Vulnerable, 0, defeated: true);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Flail: rolled a natural 20! Critical hit! 13 bludgeoning, doubled (vulnerable) = 26. Skeleton is defeated."));
    }

    [Test]
    public void ANegativeBonus_ReadsAsMinus_NotPlusMinus()
    {
        // Strength 8 is -1, so "6 - 1", never "6 + -1"
        Combatant weakling = new Combatant(new Creature("Weakling", strength: 8), Team.Heroes);
        Combatant target = new Combatant(new Creature("Target", armourClass: 12), Team.Enemies);
        AttackProfile club = new AttackProfile("Club", -1, 1, 4, -1, DamageType.Bludgeoning);
        AttackReport report = Report(weakling, target, club, 6, 5, false, false, 0, 0, DamageEffect.Normal, 10);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Weakling attacks Target with Club: 6 - 1 = 5 vs AC 12. Miss."));
    }

    [Test]
    public void AZeroBonus_ReadsAsPlusZero()
    {
        Combatant commoner = new Combatant(new Creature("Commoner"), Team.Heroes);
        Combatant target = new Combatant(new Creature("Target", armourClass: 12), Team.Enemies);
        AttackProfile club = new AttackProfile("Club", 0, 1, 4, 0, DamageType.Bludgeoning);
        AttackReport report = Report(commoner, target, club, 7, 7, false, false, 0, 0, DamageEffect.Normal, 10);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Commoner attacks Target with Club: 7 + 0 = 7 vs AC 12. Miss."));
    }

    //////////////////////////////////////
    // ---------- The damage ---------- //
    //////////////////////////////////////

    [Test]
    public void AnOrdinaryHit_ShowsTheDamageAndHPLeft()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterLongsword,
                                     12, 17, true, false, 8, 8, DamageEffect.Normal, 5);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Longsword: 12 + 5 = 17 vs AC 14. Hit! 8 slashing. Skeleton has 5 HP left."));
    }

    [Test]
    public void AVulnerableTarget_ShowsTheDoubling()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterFlail,
                                     9, 14, true, false, 7, 14, DamageEffect.Vulnerable, 0, defeated: true);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Flail: 9 + 5 = 14 vs AC 14. Hit! 7 bludgeoning, doubled (vulnerable) = 14. Skeleton is defeated."));
    }

    [Test]
    public void AResistantTarget_ShowsTheHalving()
    {
        Combatant ghoul = new Combatant(new Creature("Ghoul", armourClass: 12, maxHP: 22,
                                                     resistances: new[] { DamageType.Slashing }), Team.Enemies);
        AttackReport report = Report(Fighter(), ghoul, StatBlocks.FighterLongsword,
                                     10, 15, true, false, 9, 4, DamageEffect.Resisted, 18);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Ghoul with Longsword: 10 + 5 = 15 vs AC 12. Hit! 9 slashing, halved (resistant) = 4. Ghoul has 18 HP left."));
    }

    [Test]
    public void AnImmuneTarget_SaysSo()
    {
        AttackProfile dart = new AttackProfile("Poison Dart", 4, 1, 6, 2, DamageType.Poison);
        AttackReport report = Report(Fighter(), Skeleton(), dart,
                                     15, 19, true, false, 6, 0, DamageEffect.Immune, 13);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Poison Dart: 15 + 4 = 19 vs AC 14. Hit! 6 poison, but Skeleton is immune. Skeleton has 13 HP left."));
    }

    [Test]
    public void ACriticalHit_OnAnOrdinaryTarget_ShowsTheDamagePlainly()
    {
        AttackReport report = Report(Fighter(), Skeleton(), StatBlocks.FighterLongsword,
                                     20, 25, true, true, 14, 14, DamageEffect.Normal, 0, defeated: true);

        Assert.That(CombatLogText.Describe(report),
                    Is.EqualTo("Fighter attacks Skeleton with Longsword: rolled a natural 20! Critical hit! 14 slashing. Skeleton is defeated."));
    }

    ////////////////////////////////////////
    // ---------- Where numbers come from ---------- //
    ////////////////////////////////////////

    [Test]
    public void HPLeft_ComesFromTheReport_NotTheCreaturesHPNow()
    {
        // The line is written when the blade lands, which can be after LATER attacks resolved.
        // The report keeps the HP right after this attack: 8, even though it's 3 by now.
        Combatant skeleton = Skeleton();
        skeleton.Creature.TakeDamage(10);           // 13 -> 3, as if a second attack already landed
        AttackReport report = Report(Fighter(), skeleton, StatBlocks.FighterLongsword,
                                     12, 17, true, false, 5, 5, DamageEffect.Normal, 8);

        Assert.That(CombatLogText.Describe(report), Does.EndWith("Skeleton has 8 HP left."));
    }

    [Test]
    public void TheArmourClass_IsTheTargetsOwn()
    {
        Combatant tank = new Combatant(new Creature("Tank", armourClass: 21), Team.Enemies);
        AttackReport report = Report(Fighter(), tank, StatBlocks.FighterFlail,
                                     10, 15, false, false, 0, 0, DamageEffect.Normal, 10);

        Assert.That(CombatLogText.Describe(report), Does.Contain("vs AC 21."));
    }

    [Test]
    public void NoSentence_EndsInAStraySpace()
    {
        AttackReport[] reports =
        {
            Report(Fighter(), Skeleton(), StatBlocks.FighterFlail, 4, 9, false, false, 0, 0, DamageEffect.Normal, 13),
            Report(Fighter(), Skeleton(), StatBlocks.FighterFlail, 1, 6, false, false, 0, 0, DamageEffect.Normal, 13),
            Report(Fighter(), Skeleton(), StatBlocks.FighterFlail, 12, 17, true, false, 5, 10, DamageEffect.Vulnerable, 3),
        };

        foreach (AttackReport report in reports)
        {
            string text = CombatLogText.Describe(report);
            Assert.That(text, Is.EqualTo(text.Trim()), text);
            Assert.That(text, Does.Not.Contain("  "), $"Double space in: {text}");
        }
    }

    ///////////////////////////////////////////
    // ---------- The turn line ---------- //
    ///////////////////////////////////////////

    [Test]
    public void TurnStart_SaysTheRoundAndWhoseTurn()
    {
        Assert.That(CombatLogText.TurnStart(Skeleton(), 2), Is.EqualTo("Round 2: Skeleton's turn."));
    }

    [Test]
    public void TurnStart_UsesTheCombatantsOwnName()
    {
        // Numbered duplicates must show their number, so the log can tell them apart
        Combatant zombie = new Combatant(new Creature("Zombie 3"), Team.Enemies);

        Assert.That(CombatLogText.TurnStart(zombie, 1), Is.EqualTo("Round 1: Zombie 3's turn."));
    }
}
