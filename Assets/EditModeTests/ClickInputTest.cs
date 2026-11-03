using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// ClickInput turns clicks into moves and attacks. Reading the real mouse is Unity's job, so
// these tests skip it and call the two click handlers directly: "the ground was clicked here"
// and "this creature was clicked". That's where all of ClickInput's rules live.
public class ClickInputTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.01f);
    private readonly List<Object> _made = new List<Object>();

    private GameManager _game;
    private ClickInput _input;
    private Combatant _hero;
    private readonly List<AttackReport> _attacks = new List<AttackReport>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in _made)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        _made.Clear();
        _attacks.Clear();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private CreatureDefinition Def(string name, int maxHP = 999)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name + " Body";
        _made.Add(body);
        CreatureDefinition definition = MakeDefinition(name, body, maxHP: maxHP);
        _made.Add(definition);
        return definition;
    }

    // A 10 x 10 arena, the given creatures, combat started and a hero's turn.
    // Enemies have lots of HP so a lucky hit can't end the fight mid-test.
    private void StartBattle(params Spawn[] spawns)
    {
        GameObject arenaGo = new GameObject("Test Arena");
        _made.Add(arenaGo);
        ArenaBuilder arena = arenaGo.AddComponent<ArenaBuilder>();
        SetInt(arena, "_width", 10);
        SetInt(arena, "_height", 3);
        SetInt(arena, "_depth", 10);
        SetBlocks(arena, new Block[0]);

        GameObject gameGo = new GameObject("Test Game Manager");
        _made.Add(gameGo);
        _game = gameGo.AddComponent<GameManager>();
        SetObject(_game, "_arena", arena);
        SetInt(_game, "_seed", 99);
        SetSpawns(_game, spawns);
        Call(_game, "Awake");
        Call(_game, "Start");
        PassToFirstHero(_game.Controller);
        _hero = _game.Controller.Turns.Current;
        _game.Controller.AttackResolved += _attacks.Add;

        // The BodyDirector moves the bodies when the Core reports a move, as in the real scene
        GameObject directorGo = new GameObject("Test Body Director");
        _made.Add(directorGo);
        BodyDirector director = directorGo.AddComponent<BodyDirector>();
        SetObject(director, "_game", _game);
        SetField(director, "_instant", p => p.boolValue = true);     // edit mode can't play the walk, so jump to the end
        Call(director, "Start");

        GameObject inputGo = new GameObject("Test Click Input");
        _made.Add(inputGo);
        _input = inputGo.AddComponent<ClickInput>();
        SetObject(_input, "_game", _game);
        Call(_input, "Start");
    }

    // The usual fight: the hero at (1, 0, 1), a goblin wherever the test wants it
    private Combatant HeroAndGoblin(Vector3Int goblinCell)
    {
        StartBattle(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
                    new Spawn(Def("Goblin"), Team.Enemies, goblinCell));
        return Other("Goblin");
    }

    private Combatant Other(string name)
    {
        foreach (Combatant c in _game.Combatants)
        {
            if (c.Creature.Name == name) return c;
        }
        Assert.Fail($"No combatant called {name}");
        return null;
    }

    private GridPosition Where(Combatant c)
    {
        Assert.That(_game.Controller.Grid.TryGetPosition(c, out GridPosition cell), Is.True);
        return cell;
    }

    // What a mouse ray reports when it lands on the floor in the middle of a cell
    private void ClickGround(GridPosition cell)
    {
        RaycastHit hit = new RaycastHit();
        hit.point = _game.Space.ToWorldFloorCentre(cell) + Vector3.up * 0.001f;
        hit.normal = Vector3.up;
        Call(_input, "ClickGround", _hero, hit);
    }

    private void ClickCreature(Combatant target)
    {
        Call(_input, "ClickCreature", _hero, _game.ViewOf(target));
    }

    ///////////////////////////////////////////
    // ---------- Clicking the ground ---------- //
    ///////////////////////////////////////////

    [Test]
    public void ClickingAReachableSquare_MovesTheHero_AndItsBody()
    {
        HeroAndGoblin(new Vector3Int(8, 0, 8));

        ClickGround(P(1, 0, 4));

        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 4)));
        Assert.That(_game.ViewOf(_hero).transform.position, Is.EqualTo(_game.Space.ToWorldFloorCentre(P(1, 0, 4))).Using(Near));
        Assert.That(_hero.MovementRemaining, Is.EqualTo(15), "3 squares of 30 ft spent");
    }

    [Test]
    public void ClickingASquareTooFarAway_DoesNothing()
    {
        HeroAndGoblin(new Vector3Int(8, 0, 1));
        Vector3 bodyBefore = _game.ViewOf(_hero).transform.position;

        ClickGround(P(1, 0, 9));       // 8 squares, only 6 of movement

        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 1)));
        Assert.That(_game.ViewOf(_hero).transform.position, Is.EqualTo(bodyBefore).Using(Near));
        Assert.That(_hero.MovementRemaining, Is.EqualTo(30));
    }

    [Test]
    public void ClickingTheSquareYouAreOn_DoesNothing()
    {
        HeroAndGoblin(new Vector3Int(8, 0, 8));

        Assert.DoesNotThrow(() => ClickGround(P(1, 0, 1)));

        Assert.That(_hero.MovementRemaining, Is.EqualTo(30));
    }

    [Test]
    public void ClickingAnOccupiedSquare_DoesNotMoveOntoIt()
    {
        Combatant goblin = HeroAndGoblin(new Vector3Int(3, 0, 1));

        ClickGround(P(3, 0, 1));

        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 1)));
        Assert.That(Where(goblin), Is.EqualTo(P(3, 0, 1)));
    }

    [Test]
    public void ClickingTwice_SpendsMovementBothTimes_UntilItRunsOut()
    {
        HeroAndGoblin(new Vector3Int(8, 0, 8));

        ClickGround(P(1, 0, 5));       // 4 squares, 10 ft left
        ClickGround(P(1, 0, 7));       // 2 more, 0 ft left
        ClickGround(P(1, 0, 8));       // nothing left

        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 7)));
        Assert.That(_hero.MovementRemaining, Is.EqualTo(0));
    }

    ///////////////////////////////////////////////
    // ---------- Clicking a creature ---------- //
    ///////////////////////////////////////////////

    [Test]
    public void ClickingAnAdjacentEnemy_AttacksIt_WithoutMoving()
    {
        Combatant goblin = HeroAndGoblin(new Vector3Int(2, 0, 1));

        ClickCreature(goblin);

        Assert.That(_attacks.Count, Is.EqualTo(1));
        Assert.That(_attacks[0].Attacker, Is.SameAs(_hero));
        Assert.That(_attacks[0].Target, Is.SameAs(goblin));
        Assert.That(_hero.ActionAvailable, Is.False, "The attack used the action");
        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 1)));
    }

    [Test]
    public void ClickingAnEnemyInWalkingRange_WalksStraightUp_ThenAttacks()
    {
        // Goblin 4 squares east. The best square is (4, 0, 1): next to it, 3 steps, in a straight line.
        Combatant goblin = HeroAndGoblin(new Vector3Int(5, 0, 1));

        ClickCreature(goblin);

        Assert.That(Where(_hero), Is.EqualTo(P(4, 0, 1)));
        Assert.That(_game.ViewOf(_hero).transform.position, Is.EqualTo(_game.Space.ToWorldFloorCentre(P(4, 0, 1))).Using(Near));
        Assert.That(_hero.MovementRemaining, Is.EqualTo(15));
        Assert.That(_attacks.Count, Is.EqualTo(1));
        Assert.That(_attacks[0].Target, Is.SameAs(goblin));
    }

    [Test]
    public void ClickingAnEnemyTooFarToReach_NeitherMovesNorAttacks()
    {
        // 8 squares away diagonally: 6 squares of movement still leaves it 2 away
        Combatant goblin = HeroAndGoblin(new Vector3Int(9, 0, 9));

        ClickCreature(goblin);

        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 1)), "It shouldn't wander off if it can't attack");
        Assert.That(_hero.MovementRemaining, Is.EqualTo(30));
        Assert.That(_attacks, Is.Empty);
        Assert.That(_hero.ActionAvailable, Is.True);
    }

    [Test]
    public void ClickingYourself_DoesNothing()
    {
        HeroAndGoblin(new Vector3Int(8, 0, 8));

        Assert.DoesNotThrow(() => ClickCreature(_hero));

        Assert.That(_attacks, Is.Empty);
        Assert.That(_hero.ActionAvailable, Is.True);
    }

    [Test]
    public void ClickingAnAlly_NeitherMovesNorAttacks()
    {
        StartBattle(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
                    new Spawn(Def("Cleric"), Team.Heroes, new Vector3Int(5, 0, 1)),
                    new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 8)));
        Combatant ally = _hero.Creature.Name == "Hero" ? Other("Cleric") : Other("Hero");
        GridPosition before = Where(_hero);

        ClickCreature(ally);

        Assert.That(Where(_hero), Is.EqualTo(before));
        Assert.That(_attacks, Is.Empty);
    }

    [Test]
    public void ClickingAnEnemyAfterAttacking_DoesNotAttackAgain_OrMove()
    {
        Combatant goblin = HeroAndGoblin(new Vector3Int(2, 0, 1));
        ClickCreature(goblin);

        ClickCreature(goblin);

        Assert.That(_attacks.Count, Is.EqualTo(1));
        Assert.That(Where(_hero), Is.EqualTo(P(1, 0, 1)));
    }

    [Test]
    public void ApproachingAfterSomeMovementIsUsed_UsesOnlyWhatIsLeft()
    {
        // Walk 4 squares away first (10 ft left), then click a goblin 3 squares off: can't reach
        Combatant goblin = HeroAndGoblin(new Vector3Int(9, 0, 1));
        ClickGround(P(5, 0, 5));
        Assert.That(_hero.MovementRemaining, Is.EqualTo(10));

        ClickCreature(goblin);     // from (5, 0, 5) the goblin is 4 away; 2 squares of walking leaves it 2 away

        Assert.That(Where(_hero), Is.EqualTo(P(5, 0, 5)));
        Assert.That(_attacks, Is.Empty);
    }

    ///////////////////////////////////////////
    // ---------- Wiring and safety ---------- //
    ///////////////////////////////////////////

    [Test]
    public void Start_SubscribesToAttackReports_AndLogsThem()
    {
        Combatant goblin = HeroAndGoblin(new Vector3Int(2, 0, 1));

        LogAssert.Expect(LogType.Log, new Regex("^Hero rolled"));
        ClickCreature(goblin);
    }

    [Test]
    public void WithNoGameManager_StartAndUpdate_DoNotThrow()
    {
        GameObject go = new GameObject("Lonely Click Input");
        _made.Add(go);
        ClickInput input = go.AddComponent<ClickInput>();

        Assert.DoesNotThrow(() => Call(input, "Start"));
        Assert.DoesNotThrow(() => Call(input, "Update"));
        Assert.DoesNotThrow(() => Call(input, "OnDestroy"));
    }

    [Test]
    public void OnTheEnemysTurn_UpdateDoesNothing()
    {
        Combatant goblin = HeroAndGoblin(new Vector3Int(8, 0, 8));
        _game.Controller.EndTurn();
        Assume.That(_game.Controller.Turns.Current, Is.SameAs(goblin));

        Assert.DoesNotThrow(() => Call(_input, "Update"));

        Assert.That(_game.Controller.Turns.Current, Is.SameAs(goblin));
    }
}
