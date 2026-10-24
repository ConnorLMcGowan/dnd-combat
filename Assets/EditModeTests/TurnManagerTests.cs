using NUnit.Framework;
using DnDCombat.Core;

public class TurnManagerTest
{
    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private static Combatant Make(string name, Team team, int dex = 10, int maxHP = 10, int speed = 30)
    {
        return new Combatant(new Creature(name, dexterity: dex, maxHP: maxHP, speed: speed), team);
    }

    private static void Kill(Combatant c)
    {
        c.Creature.TakeDamage(c.Creature.MaxHP);
    }

    // Dice results are used in the order the combatants are listed.
    private static TurnManager Start(int[] rolls, params Combatant[] combatants)
    {
        TurnManager manager = new TurnManager(new FakeDice(rolls), combatants);
        manager.StartCombat();
        return manager;
    }

    //////////////////////////////////////
    // ---------- Initiative ---------- //
    //////////////////////////////////////

    [Test]
    public void StartCombat_OrdersByInitiative_HighestFirst()
    {
        Combatant a = Make("A", Team.Heroes);
        Combatant b = Make("B", Team.Enemies);
        Combatant c = Make("C", Team.Heroes);

        TurnManager manager = Start(new[] { 5, 18, 12 }, a, b, c);

        Assert.That(manager.Order, Is.EqualTo(new[] { b, c, a }));
    }

    [Test]
    public void StartCombat_InitiativeIsRollPlusDexModifier()
    {
        Combatant quick = Make("Quick", Team.Heroes, dex: 14);   // +2
        Combatant enemy = Make("Enemy", Team.Enemies);

        Start(new[] { 10, 3 }, quick, enemy);

        Assert.That(quick.Initiative, Is.EqualTo(12));
    }

    [Test]
    public void Tie_HigherDexModifierGoesFirst()
    {
        Combatant slow = Make("Slow", Team.Heroes, dex: 10);     // rolls 12, total 12
        Combatant quick = Make("Quick", Team.Heroes, dex: 14);   // rolls 10 + 2, total 12
        Combatant enemy = Make("Enemy", Team.Enemies);

        TurnManager manager = Start(new[] { 12, 10, 1 }, slow, quick, enemy);

        Assert.That(manager.Order[0], Is.SameAs(quick));
        Assert.That(manager.Order[1], Is.SameAs(slow));
    }

    [Test]
    public void Tie_WithSameDex_HeroGoesBeforeEnemy()
    {
        Combatant enemy = Make("Enemy", Team.Enemies);           // added first on purpose
        Combatant hero = Make("Hero", Team.Heroes);

        TurnManager manager = Start(new[] { 10, 10 }, enemy, hero);

        Assert.That(manager.Order[0], Is.SameAs(hero));
        Assert.That(manager.Order[1], Is.SameAs(enemy));
    }

    [Test]
    public void FullTie_KeepsTheOrderTheyWereAdded()
    {
        Combatant first = Make("First", Team.Heroes);
        Combatant second = Make("Second", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);

        TurnManager manager = Start(new[] { 10, 10, 1 }, first, second, enemy);

        Assert.That(manager.Order[0], Is.SameAs(first));
        Assert.That(manager.Order[1], Is.SameAs(second));
    }

    /////////////////////////////////////
    // ---------- Turn flow ---------- //
    /////////////////////////////////////

    [Test]
    public void StartCombat_FirstInOrderGetsTurnInRoundOne_WithFullResources()
    {
        Combatant hero = Make("Hero", Team.Heroes, speed: 25);
        Combatant enemy = Make("Enemy", Team.Enemies);

        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        Assert.That(manager.Current, Is.SameAs(hero));
        Assert.That(manager.Round, Is.EqualTo(1));
        Assert.That(hero.MovementRemaining, Is.EqualTo(25));
        Assert.That(hero.ActionAvailable, Is.True);
        Assert.That(hero.BonusActionAvailable, Is.True);
    }

    [Test]
    public void EndTurn_MovesToTheNextCombatant()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        manager.EndTurn();

        Assert.That(manager.Current, Is.SameAs(enemy));
        Assert.That(manager.Round, Is.EqualTo(1));
    }

    [Test]
    public void EndTurn_AfterTheLastCombatant_WrapsAndStartsRoundTwo()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        manager.EndTurn();
        manager.EndTurn();

        Assert.That(manager.Current, Is.SameAs(hero));
        Assert.That(manager.Round, Is.EqualTo(2));
    }

    [Test]
    public void EndTurn_SkipsDefeatedCombatants()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy1 = Make("Enemy1", Team.Enemies);
        Combatant enemy2 = Make("Enemy2", Team.Enemies);
        TurnManager manager = Start(new[] { 20, 15, 10 }, hero, enemy1, enemy2);

        Kill(enemy1);
        manager.EndTurn();

        Assert.That(manager.Current, Is.SameAs(enemy2));
        Assert.That(manager.State, Is.EqualTo(CombatState.Ongoing));
    }

    [Test]
    public void StartCombat_CombatantAlreadyDefeated_DoesNotGetTheFirstTurn()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant deadEnemy = Make("Dead", Team.Enemies);
        Combatant enemy2 = Make("Enemy2", Team.Enemies);
        Kill(deadEnemy);

        TurnManager manager = Start(new[] { 15, 20, 10 }, hero, deadEnemy, enemy2);   // deadEnemy rolls highest

        Assert.That(manager.Current, Is.SameAs(hero));
        Assert.That(manager.Round, Is.EqualTo(1));
    }

    [Test]
    public void EachNewTurn_ResetsActionBonusActionAndMovement()
    {
        Combatant hero = Make("Hero", Team.Heroes, speed: 30);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        hero.ActionAvailable = false;
        hero.BonusActionAvailable = false;
        hero.MovementRemaining = 0;

        manager.EndTurn();   // enemy's turn
        manager.EndTurn();   // back to the hero

        Assert.That(manager.Current, Is.SameAs(hero));
        Assert.That(hero.ActionAvailable, Is.True);
        Assert.That(hero.BonusActionAvailable, Is.True);
        Assert.That(hero.MovementRemaining, Is.EqualTo(30));
    }

    /////////////////////////////////////////
    // ---------- Ending combat ---------- //
    /////////////////////////////////////////

    [Test]
    public void KillingTheLastEnemy_HeroesWin_AndNobodyHasATurn()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        Kill(enemy);
        manager.CheckCombatEnd();

        Assert.That(manager.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void KillingTheLastHero_EnemiesWin_AndNobodyHasATurn()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        Kill(hero);
        manager.CheckCombatEnd();

        Assert.That(manager.State, Is.EqualTo(CombatState.EnemiesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void EndTurn_NoticesTheFightIsOver()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        Kill(enemy);
        manager.EndTurn();

        Assert.That(manager.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void EndTurn_AfterTheFightIsOver_DoesNothingAndDoesNotThrow()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);
        Kill(enemy);
        manager.CheckCombatEnd();

        Assert.DoesNotThrow(() => manager.EndTurn());

        Assert.That(manager.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void EndTurn_BeforeStartCombat_DoesNothingAndDoesNotThrow()
    {
        TurnManager manager = new TurnManager(new FakeDice(), new[] { Make("Hero", Team.Heroes) });

        Assert.DoesNotThrow(() => manager.EndTurn());

        Assert.That(manager.Current, Is.Null);
        Assert.That(manager.State, Is.EqualTo(CombatState.Ongoing));
    }

    //////////////////////////////////////
    // ---------- Edge cases ---------- //
    //////////////////////////////////////

    [Test]
    public void EmptyList_DoesNotThrow_AndCountsAsEnemiesWon()
    {
        TurnManager manager = new TurnManager(new FakeDice(), new Combatant[0]);

        Assert.DoesNotThrow(() => manager.StartCombat());

        Assert.That(manager.Order, Is.Empty);
        Assert.That(manager.State, Is.EqualTo(CombatState.EnemiesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void OnlyHeroes_HeroesWinImmediately()
    {
        TurnManager manager = Start(new[] { 10 }, Make("Hero", Team.Heroes));

        Assert.That(manager.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(manager.Current, Is.Null);
    }

    [Test]
    public void EveryoneDefeated_DoesNotHangOrThrow_AndEnemiesWin()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant enemy = Make("Enemy", Team.Enemies);
        TurnManager manager = Start(new[] { 15, 5 }, hero, enemy);

        Kill(hero);
        Kill(enemy);

        Assert.DoesNotThrow(() => manager.EndTurn());
        Assert.That(manager.State, Is.EqualTo(CombatState.EnemiesWon));
    }
}