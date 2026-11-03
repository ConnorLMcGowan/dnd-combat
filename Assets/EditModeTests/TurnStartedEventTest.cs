using System.Collections.Generic;
using NUnit.Framework;
using DnDCombat.Core;

// TurnManager.TurnStarted: raised exactly once each time a creature's turn begins, after its
// resources are refreshed, and never when combat ends instead of a new turn starting.
public class TurnStartedEventTest
{
    private Combatant _hero;
    private Combatant _goblin;
    private TurnManager _turns;
    private readonly List<Combatant> _started = new List<Combatant>();

    // Initiative rolls: hero 15, goblin 5, so the hero goes first. Both Dexterity 10 (+0).
    [SetUp]
    public void SetUp()
    {
        _hero = new Combatant(new Creature("Hero", speed: 30), Team.Heroes);
        _goblin = new Combatant(new Creature("Goblin", speed: 25), Team.Enemies);
        _turns = new TurnManager(new FakeDice(15, 5), new[] { _hero, _goblin });
        _started.Clear();
        _turns.TurnStarted += _started.Add;
    }

    [Test]
    public void StartCombat_RaisesItOnce_ForWhoeverGoesFirst()
    {
        _turns.StartCombat();

        Assert.That(_started, Is.EqualTo(new[] { _hero }));
    }

    [Test]
    public void EachEndTurn_RaisesItOnce_ForTheNextCreature()
    {
        _turns.StartCombat();

        _turns.EndTurn();
        _turns.EndTurn();

        Assert.That(_started, Is.EqualTo(new[] { _hero, _goblin, _hero }));
    }

    [Test]
    public void WhenItIsRaised_TheTurnIsAlreadySetUp()
    {
        // Listeners (the banner, the panel highlight) read these, so they must be ready
        Combatant current = null;
        int feet = -1;
        bool action = false;
        _turns.TurnStarted += who =>
        {
            current = _turns.Current;
            feet = who.MovementRemaining;
            action = who.ActionAvailable;
        };

        _turns.StartCombat();

        Assert.That(current, Is.SameAs(_hero));
        Assert.That(feet, Is.EqualTo(30));
        Assert.That(action, Is.True);
    }

    [Test]
    public void ANewRound_IsAlreadyCountedWhenItIsRaised()
    {
        // "Round 2: Hero's turn." needs Round to be 2 by the time the hero's second turn is announced
        List<int> rounds = new List<int>();
        _turns.TurnStarted += _ => rounds.Add(_turns.Round);

        _turns.StartCombat();
        _turns.EndTurn();
        _turns.EndTurn();

        Assert.That(rounds, Is.EqualTo(new[] { 1, 1, 2 }));
    }

    [Test]
    public void ADefeatedCreature_IsSkipped_AndNotAnnounced()
    {
        Combatant second = new Combatant(new Creature("Goblin Two"), Team.Enemies);
        _turns = new TurnManager(new FakeDice(15, 10, 5), new[] { _hero, second, _goblin });
        _turns.TurnStarted += _started.Add;
        _turns.StartCombat();
        _started.Clear();
        second.Creature.TakeDamage(999);

        _turns.EndTurn();

        Assert.That(_started, Is.EqualTo(new[] { _goblin }));
    }

    [Test]
    public void WhenTheLastEnemyFalls_EndTurnRaisesNothing()
    {
        _turns.StartCombat();
        _started.Clear();
        _goblin.Creature.TakeDamage(999);

        _turns.EndTurn();

        Assert.That(_turns.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(_started, Is.Empty);
    }

    [Test]
    public void ACombatWithOnlyOneSide_NeverRaisesIt()
    {
        _turns = new TurnManager(new FakeDice(15), new[] { _hero });
        _turns.TurnStarted += _started.Add;

        _turns.StartCombat();

        Assert.That(_started, Is.Empty);
    }

    [Test]
    public void StartingCombatAgain_AnnouncesTheFirstTurnAgain()
    {
        _turns = new TurnManager(new FakeDice(15, 5, 15, 5), new[] { _hero, _goblin });
        _turns.TurnStarted += _started.Add;
        _turns.StartCombat();
        _turns.EndTurn();

        _turns.StartCombat();

        Assert.That(_started, Is.EqualTo(new[] { _hero, _goblin, _hero }));
    }
}
