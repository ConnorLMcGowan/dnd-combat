using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;
using Grid = DnDCombat.Core.Grid;

// BodyDirector keeps the bodies in step with the Core: everyone faces the nearest enemy at the
// start, bodies snap and turn when they move, and attackers turn to face their target.
// The arena is 10 x 10, so world x and z = cell - 4.5 (cell (1, 0, 1) is at (-3.5, 0, -3.5)).
public class BodyDirectorTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.01f);
    private readonly List<Object> _made = new List<Object>();

    private GameManager _game;
    private BodyDirector _director;

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in _made)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        _made.Clear();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private CreatureDefinition Def(string name)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name + " Body";
        _made.Add(body);
        CreatureDefinition definition = MakeDefinition(name, body, maxHP: 999);
        _made.Add(definition);
        return definition;
    }

    // Builds the fight and the director, but doesn't start combat: Start on the director only
    // subscribes and sets the starting facing
    private void Build(params Spawn[] spawns)
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
        SetInt(_game, "_seed", 5);
        SetSpawns(_game, spawns);
        Call(_game, "Awake");

        GameObject directorGo = new GameObject("Test Body Director");
        _made.Add(directorGo);
        _director = directorGo.AddComponent<BodyDirector>();
        SetObject(_director, "_game", _game);
        // Edit mode can't play coroutines, so every job jumps straight to its end result
        SetField(_director, "_instant", p => p.boolValue = true);
    }

    private void StartDirector() => Call(_director, "Start");

    // Starts combat and makes it the given combatant's turn
    private void TurnOf(Combatant who)
    {
        Call(_game, "Start");
        for (int i = 0; i < 10 && _game.Controller.Turns.Current != who; i++)
        {
            _game.Controller.EndTurn();
        }
        Assume.That(_game.Controller.Turns.Current, Is.SameAs(who));
    }

    private Combatant Named(string name)
    {
        foreach (Combatant c in _game.Combatants)
        {
            if (c.Creature.Name == name) return c;
        }
        Assert.Fail($"No combatant called {name}");
        return null;
    }

    private Vector3 Front(Combatant c) => _game.ViewOf(c).transform.forward;

    private static Vector3 Flat(float x, float z) => new Vector3(x, 0f, z).normalized;

    ///////////////////////////////////////////
    // ---------- Facing at the start ---------- //
    ///////////////////////////////////////////

    [Test]
    public void AtTheStart_TwoFoesFaceEachOther()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 1)));

        StartDirector();

        Assert.That(Front(Named("Hero")), Is.EqualTo(Vector3.right).Using(Near));
        Assert.That(Front(Named("Goblin")), Is.EqualTo(Vector3.left).Using(Near));
    }

    [Test]
    public void AtTheStart_EachFacesItsNearestEnemy_NotJustTheFirst()
    {
        // Goblin 1 is 7 squares east. Goblin 2 is 3 squares north, so it's the nearer one.
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(1, 0, 4)));

        StartDirector();

        Assert.That(Front(Named("Hero")), Is.EqualTo(Vector3.forward).Using(Near));
    }

    [Test]
    public void AtTheStart_AlliesAreIgnored()
    {
        // The cleric is right next to the hero, but it's not an enemy
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Cleric"), Team.Heroes, new Vector3Int(1, 0, 2)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 1)));

        StartDirector();

        Assert.That(Front(Named("Hero")), Is.EqualTo(Vector3.right).Using(Near));
    }

    [Test]
    public void AtTheStart_DefeatedEnemiesAreIgnored()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 1)),
              new Spawn(Def("Corpse"), Team.Enemies, new Vector3Int(1, 0, 3)));
        Named("Corpse").Creature.TakeDamage(9999);

        StartDirector();

        Assert.That(Front(Named("Hero")), Is.EqualTo(Vector3.right).Using(Near));
    }

    [Test]
    public void AtTheStart_WithNoEnemies_FacesTheMiddleOfTheArena()
    {
        // The hero at (-3.5, 0, -3.5), the arena's middle at (0, 0, 0): facing north-east
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)));

        StartDirector();

        Assert.That(Front(Named("Hero")), Is.EqualTo(Flat(1f, 1f)).Using(Near));
    }

    [Test]
    public void AtTheStart_FacingNeverTilts()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(6, 0, 5)));

        StartDirector();

        Assert.That(_game.ViewOf(Named("Hero")).transform.up, Is.EqualTo(Vector3.up).Using(Near));
    }

    //////////////////////////////////////
    // ---------- Moving ---------- //
    //////////////////////////////////////

    [Test]
    public void AMove_SnapsTheBodyToItsNewSquare()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 8)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);

        _game.Controller.TryMove(hero, P(1, 0, 4));

        Assert.That(_game.ViewOf(hero).transform.position, Is.EqualTo(_game.Space.ToWorldFloorCentre(P(1, 0, 4))).Using(Near));
    }

    [Test]
    public void AMove_TurnsTheBodyAlongItsLastStep()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 8)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);

        _game.Controller.TryMove(hero, P(1, 0, 4));      // straight north

        Assert.That(Front(hero), Is.EqualTo(Vector3.forward).Using(Near));
    }

    [Test]
    public void ADiagonalMove_FacesDiagonally()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(9, 0, 9)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);

        _game.Controller.TryMove(hero, P(3, 0, 3));      // two steps south-west

        Assert.That(Front(hero), Is.EqualTo(Flat(-1f, -1f)).Using(Near));
    }

    [Test]
    public void ARefusedMove_LeavesTheBodyAlone()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 1)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);
        Vector3 before = _game.ViewOf(hero).transform.position;

        _game.Controller.TryMove(hero, P(1, 0, 9));      // too far

        Assert.That(_game.ViewOf(hero).transform.position, Is.EqualTo(before).Using(Near));
        Assert.That(Front(hero), Is.EqualTo(Vector3.right).Using(Near), "Still facing the goblin");
    }

    ////////////////////////////////////////
    // ---------- Attacking ---------- //
    ////////////////////////////////////////

    [Test]
    public void AnAttack_TurnsTheAttackerToFaceItsTarget()
    {
        // Both goblins are next to the hero. At the start it faces Goblin 1 (east): a tie goes
        // to the first in the list. Then it attacks Goblin 2 (north), so it must turn.
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(2, 0, 1)),
              new Spawn(Def("Goblin 2"), Team.Enemies, new Vector3Int(1, 0, 2)));
        StartDirector();
        Combatant hero = Named("Hero");
        Combatant north = Named("Goblin 2");
        Assume.That(Front(hero), Is.EqualTo(Vector3.right).Using(Near));
        TurnOf(hero);
        _game.TryGetAttack(hero, out AttackProfile club);

        Assume.That(_game.Controller.TryAttack(hero, north, club).Success, Is.True);

        Assert.That(Front(hero), Is.EqualTo(Vector3.forward).Using(Near));
    }

    [Test]
    public void AMonsterThatWalksThenAttacks_EndsUpFacingItsTarget_NotTheWayItWalked()
    {
        // The goblin walks north-west towards the hero, then attacks it from the side.
        // The attack comes after the move, so the attack's facing wins.
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(2, 0, 5)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(6, 0, 2)));
        StartDirector();
        Combatant hero = Named("Hero");
        Combatant goblin = Named("Goblin");
        TurnOf(goblin);
        _game.TryGetAttack(goblin, out AttackProfile club);

        MonsterAI.TakeTurn(_game.Controller, goblin, club);

        Vector3 toHero = _game.ViewOf(hero).transform.position - _game.ViewOf(goblin).transform.position;
        toHero.y = 0f;
        Assume.That(Grid.DistanceInSquares(Where(goblin), Where(hero)), Is.LessThanOrEqualTo(1), "The goblin should have reached the hero");
        Assert.That(Front(goblin), Is.EqualTo(toHero.normalized).Using(Near));
        Assert.That(_game.ViewOf(goblin).transform.position, Is.EqualTo(_game.Space.ToWorldFloorCentre(Where(goblin))).Using(Near));
    }

    private GridPosition Where(Combatant c)
    {
        _game.Controller.Grid.TryGetPosition(c, out GridPosition cell);
        return cell;
    }

    ///////////////////////////////////////////
    // ---------- Wiring and safety ---------- //
    ///////////////////////////////////////////

    [Test]
    public void AfterOnDestroy_ItStopsListening()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 8)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);
        Vector3 before = _game.ViewOf(hero).transform.position;

        Call(_director, "OnDestroy");
        _game.Controller.TryMove(hero, P(1, 0, 4));

        Assert.That(_game.ViewOf(hero).transform.position, Is.EqualTo(before).Using(Near));
    }

    [Test]
    public void WithNoGameManager_ItWarns_AndDoesNotThrow()
    {
        GameObject go = new GameObject("Lonely Body Director");
        _made.Add(go);
        BodyDirector director = go.AddComponent<BodyDirector>();

        LogAssert.Expect(LogType.Warning, new Regex("BodyDirector: no ready GameManager"));
        Assert.DoesNotThrow(() => Call(director, "Start"));
        Assert.DoesNotThrow(() => Call(director, "OnDestroy"));
    }

    ////////////////////////////////////////
    // ---------- Instant mode ---------- //
    ////////////////////////////////////////
    // Instant mode is how every edit mode test sees the bodies, so it gets checked itself.
    // The real, timed show is tested in play mode (BodyAnimationPlayTest).

    [Test]
    public void InInstantMode_TheDirectorIsNeverBusy()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(3, 0, 1)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);
        _game.TryGetAttack(hero, out AttackProfile club);

        _game.Controller.TryMove(hero, P(2, 0, 2));
        Assert.That(_director.IsBusy, Is.False, "After a move");

        _game.Controller.TryAttack(hero, Named("Goblin"), club);
        Assert.That(_director.IsBusy, Is.False, "After an attack");
    }

    [Test]
    public void InInstantMode_AMoveThenAnAttack_EndsInTheRightPlace_FacingTheTarget()
    {
        // Walking east-north-east, then attacking the goblin to the south: the attack's facing wins
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(4, 0, 2)));
        StartDirector();
        Combatant hero = Named("Hero");
        TurnOf(hero);
        _game.TryGetAttack(hero, out AttackProfile club);

        _game.Controller.TryMove(hero, P(4, 0, 3));
        Assume.That(_game.Controller.TryAttack(hero, Named("Goblin"), club).Success, Is.True);

        Assert.That(_game.ViewOf(hero).transform.position, Is.EqualTo(_game.Space.ToWorldFloorCentre(P(4, 0, 3))).Using(Near));
        Assert.That(Front(hero), Is.EqualTo(Vector3.back).Using(Near));
    }

    [Test]
    public void IsBusy_IsFalseBeforeAnythingHappens()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(8, 0, 8)));

        StartDirector();

        Assert.That(_director.IsBusy, Is.False);
    }
}
