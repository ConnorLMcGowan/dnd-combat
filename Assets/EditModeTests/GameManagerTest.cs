using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// GameManager is the composition root: it builds every Core object from the Inspector settings
// and hands them out. These tests build a small scene in code, run Awake and Start by hand
// (edit mode doesn't run them), and check what comes out.
public class GameManagerTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.01f);
    private readonly List<Object> _made = new List<Object>();

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

    // A stand-in for a creature model: a capsule, which comes with a collider
    private GameObject MakeBody(string name)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name;
        _made.Add(body);
        return body;
    }

    private CreatureDefinition MakeDef(string name, bool withAttack = true, GameObject body = null)
    {
        CreatureDefinition definition = MakeDefinition(name, body != null ? body : MakeBody(name + " Body"), withAttack: withAttack);
        _made.Add(definition);
        return definition;
    }

    private ArenaBuilder MakeArena(params Block[] blocks)
    {
        GameObject go = new GameObject("Test Arena");
        _made.Add(go);
        ArenaBuilder arena = go.AddComponent<ArenaBuilder>();
        SetInt(arena, "_width", 10);
        SetInt(arena, "_height", 3);
        SetInt(arena, "_depth", 10);
        SetFloat(arena, "_cellSize", 1f);
        SetBlocks(arena, blocks);
        return arena;
    }

    private GameManager MakeGame(ArenaBuilder arena, int seed, params Spawn[] spawns)
    {
        GameObject go = new GameObject("Test Game Manager");
        _made.Add(go);
        GameManager game = go.AddComponent<GameManager>();
        SetObject(game, "_arena", arena);
        SetInt(game, "_seed", seed);
        SetSpawns(game, spawns);
        return game;
    }

    // A ready-made fight: one hero against one enemy, set up but not started
    private GameManager MakeFight(out CreatureDefinition heroDef, out CreatureDefinition enemyDef, int seed = 1234)
    {
        heroDef = MakeDef("Hero");
        enemyDef = MakeDef("Goblin");
        GameManager game = MakeGame(MakeArena(), seed,
                                    new Spawn(heroDef, Team.Heroes, new Vector3Int(1, 0, 1)),
                                    new Spawn(enemyDef, Team.Enemies, new Vector3Int(8, 0, 8)));
        Call(game, "Awake");
        return game;
    }

    ///////////////////////////////////////////////
    // ---------- Setup that should fail ---------- //
    ///////////////////////////////////////////////
    // Each of these must say what's wrong and leave Controller null, which every other
    // script reads as "not ready". None may throw.

    [Test]
    public void NoArena_LogsAnError_AndIsNotReady()
    {
        GameManager game = MakeGame(null, 1, new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)));

        LogAssert.Expect(LogType.Error, "GameManager: the Arena slot is empty.");
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void NoSpawns_LogsAnError_AndIsNotReady()
    {
        GameManager game = MakeGame(MakeArena(), 1);

        LogAssert.Expect(LogType.Error, "GameManager: the Spawns list is empty, so nobody would fight.");
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void SpawnWithNoDefinition_LogsWhichOne_AndIsNotReady()
    {
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
                                    new Spawn(null, Team.Enemies, new Vector3Int(5, 0, 5)));

        LogAssert.Expect(LogType.Error, "GameManager: spawn 1 has no Definition.");
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void DefinitionWithNoViewPrefab_LogsAnError_AndIsNotReady()
    {
        CreatureDefinition ghost = MakeDef("Ghost");
        SetObject(ghost, "_viewPrefab", null);
        GameManager game = MakeGame(MakeArena(), 1, new Spawn(ghost, Team.Enemies, new Vector3Int(1, 0, 1)));

        LogAssert.Expect(LogType.Error, new Regex("Ghost.*has no View Prefab"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void SpawnOnABlock_LogsAnError_AndIsNotReady()
    {
        ArenaBuilder arena = MakeArena(new Block(new Vector3Int(3, 0, 3)));
        GameManager game = MakeGame(arena, 1, new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(3, 0, 3)));

        LogAssert.Expect(LogType.Error, new Regex(@"could not be placed at \(3, 0, 3\)"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void SpawnOutsideTheArena_LogsAnError_AndIsNotReady()
    {
        GameManager game = MakeGame(MakeArena(), 1, new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(40, 0, 1)));

        LogAssert.Expect(LogType.Error, new Regex(@"could not be placed at \(40, 0, 1\)"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void TwoSpawnsOnTheSameSquare_LogsAnError_AndIsNotReady()
    {
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(2, 0, 2)),
                                    new Spawn(MakeDef("Goblin"), Team.Enemies, new Vector3Int(2, 0, 2)));

        LogAssert.Expect(LogType.Error, new Regex(@"Spawn 1 .*could not be placed at \(2, 0, 2\)"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void SpawnFloatingInMidAir_LogsAnError_AndIsNotReady()
    {
        // Layer 1 with nothing under it isn't standable
        GameManager game = MakeGame(MakeArena(), 1, new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(2, 1, 2)));

        LogAssert.Expect(LogType.Error, new Regex(@"could not be placed at \(2, 1, 2\)"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Null);
    }

    [Test]
    public void StartAfterAFailedSetup_LogsAnError_AndDoesNotThrow()
    {
        GameManager game = MakeGame(MakeArena(), 1);
        LogAssert.Expect(LogType.Error, new Regex("Spawns list is empty"));
        Call(game, "Awake");

        LogAssert.Expect(LogType.Error, new Regex("setup did not finish"));
        Assert.DoesNotThrow(() => Call(game, "Start"));
    }

    /////////////////////////////////////////////
    // ---------- Setup that works ---------- //
    /////////////////////////////////////////////

    [Test]
    public void GoodSetup_IsReady_AndSharesTheArenasGridAndSpace()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.Controller, Is.Not.Null);
        Assert.That(game.Space, Is.Not.Null);
        Assert.That(game.Controller.Grid.Width, Is.EqualTo(10));
        Assert.That(game.Space.Origin, Is.EqualTo(new Vector3(-5f, 0f, -5f)).Using(Near));
    }

    [Test]
    public void GoodSetup_BuildsTheArenaOnce()
    {
        GameManager game = MakeFight(out _, out _);

        ArenaBuilder arena = Object.FindObjectsByType<ArenaBuilder>().Single(a => a.name == "Test Arena");
        int floors = arena.transform.Cast<Transform>().Count(t => t.name == "Floor");
        Assert.That(floors, Is.EqualTo(1));
    }

    [Test]
    public void EverySpawn_BecomesACombatant_InListOrder_OnTheRightTeam()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.Combatants.Count, Is.EqualTo(2));
        Assert.That(game.Combatants[0].Creature.Name, Is.EqualTo("Hero"));
        Assert.That(game.Combatants[0].Team, Is.EqualTo(Team.Heroes));
        Assert.That(game.Combatants[1].Creature.Name, Is.EqualTo("Goblin"));
        Assert.That(game.Combatants[1].Team, Is.EqualTo(Team.Enemies));
    }

    [Test]
    public void EverySpawn_IsPlacedOnItsSquare()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.Controller.Grid.TryGetPosition(game.Combatants[0], out GridPosition hero), Is.True);
        Assert.That(hero, Is.EqualTo(P(1, 0, 1)));
        Assert.That(game.Controller.Grid.TryGetPosition(game.Combatants[1], out GridPosition goblin), Is.True);
        Assert.That(goblin, Is.EqualTo(P(8, 0, 8)));
    }

    [Test]
    public void EverySpawn_GetsABody_BoundToIt_AndStandingOnItsSquare()
    {
        GameManager game = MakeFight(out _, out _);

        foreach (Combatant combatant in game.Combatants)
        {
            CombatantView view = game.ViewOf(combatant);
            Assert.That(view, Is.Not.Null, combatant.Creature.Name);
            Assert.That(view.Combatant, Is.SameAs(combatant));
            Assert.That(view.name, Is.EqualTo(combatant.Creature.Name));
            Assert.That(view.transform.parent, Is.SameAs(game.transform));

            game.Controller.Grid.TryGetPosition(combatant, out GridPosition cell);
            Assert.That(view.transform.position, Is.EqualTo(game.Space.ToWorldFloorCentre(cell)).Using(Near));
        }
    }

    [Test]
    public void Bodies_AreCopies_NotThePrefabItself()
    {
        GameObject body = MakeBody("Shared Body");
        CreatureDefinition zombie = MakeDef("Zombie", body: body);
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        CombatantView first = game.ViewOf(game.Combatants[0]);
        CombatantView second = game.ViewOf(game.Combatants[1]);
        Assert.That(first.gameObject, Is.Not.SameAs(body));
        Assert.That(first.gameObject, Is.Not.SameAs(second.gameObject));
        Assert.That(body.GetComponent<CombatantView>() == null, Is.True, "The prefab itself must not be changed");
    }

    [Test]
    public void PrefabThatAlreadyHasACombatantView_IsReused_NotGivenASecondOne()
    {
        GameObject body = MakeBody("Prepared Body");
        body.AddComponent<CombatantView>();
        CreatureDefinition knight = MakeDef("Knight", body: body);
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(knight, Team.Heroes, new Vector3Int(1, 0, 1)),
                                    new Spawn(MakeDef("Goblin"), Team.Enemies, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        Assert.That(game.ViewOf(game.Combatants[0]).GetComponents<CombatantView>().Length, Is.EqualTo(1));
    }

    ////////////////////////////////
    // ---------- Naming ---------- //
    ////////////////////////////////

    [Test]
    public void OneOfAKind_UsesThePlainName()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.Combatants.Select(c => c.Creature.Name), Is.EqualTo(new[] { "Hero", "Goblin" }));
    }

    [Test]
    public void SeveralOfAKind_AreNumberedInListOrder()
    {
        CreatureDefinition zombie = MakeDef("Zombie");
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(7, 0, 1)));
        Call(game, "Awake");

        Assert.That(game.Combatants.Select(c => c.Creature.Name),
                    Is.EqualTo(new[] { "Zombie 1", "Hero", "Zombie 2", "Zombie 3" }));
    }

    [Test]
    public void AGivenName_WinsOverTheNumbering()
    {
        CreatureDefinition zombie = MakeDef("Zombie");
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1), "Big Bob"),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(5, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        Assert.That(game.Combatants.Select(c => c.Creature.Name),
                    Is.EqualTo(new[] { "Zombie 1", "Big Bob", "Zombie 2", "Hero" }));
    }

    [Test]
    public void WhitespaceName_CountsAsNoName()
    {
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1), "   "),
                                    new Spawn(MakeDef("Goblin"), Team.Enemies, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        Assert.That(game.Combatants[0].Creature.Name, Is.EqualTo("Hero"));
    }

    [Test]
    public void RunningAwakeTwice_DoesNotKeepCountingNames()
    {
        // The name counts are cleared at the start of Awake, so a re-run starts from 1 again
        CreatureDefinition zombie = MakeDef("Zombie");
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");
        Call(game, "Awake");

        Assert.That(game.Combatants.Select(c => c.Creature.Name), Is.EqualTo(new[] { "Zombie 1", "Zombie 2", "Hero" }));
        Assert.That(game.Combatants.Count, Is.EqualTo(3));
    }

    //////////////////////////////////////////////
    // ---------- Attacks and lookups ---------- //
    //////////////////////////////////////////////

    [Test]
    public void TryGetAttack_GivesTheFirstAttack()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.TryGetAttack(game.Combatants[0], out AttackProfile attack), Is.True);
        Assert.That(attack.Name, Is.EqualTo("Club"));
        Assert.That(game.AttacksOf(game.Combatants[0]).Count, Is.EqualTo(1));
    }

    [Test]
    public void ACreatureWithNoAttacks_StillSpawns_ButTryGetAttackSaysNo()
    {
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
                                    new Spawn(MakeDef("Slime", withAttack: false), Team.Enemies, new Vector3Int(5, 0, 5)));

        LogAssert.Expect(LogType.Warning, new Regex("Slime.*has no attacks"));
        Call(game, "Awake");

        Assert.That(game.Controller, Is.Not.Null, "No attacks is a warning, not a failure");
        Assert.That(game.TryGetAttack(game.Combatants[1], out _), Is.False);
        Assert.That(game.AttacksOf(game.Combatants[1]), Is.Empty);
    }

    [Test]
    public void Lookups_ForNullOrAStranger_AreSafe()
    {
        GameManager game = MakeFight(out _, out _);
        Combatant stranger = new Combatant(new Creature("Stranger"), Team.Heroes);

        Assert.That(game.ViewOf(null), Is.Null);
        Assert.That(game.ViewOf(stranger), Is.Null);
        Assert.That(game.TryGetAttack(null, out _), Is.False);
        Assert.That(game.TryGetAttack(stranger, out _), Is.False);
        Assert.That(game.AttacksOf(null), Is.Empty);
        Assert.That(game.AttacksOf(stranger), Is.Empty);
    }

    [Test]
    public void EachCombatant_GetsItsOwnAttackList()
    {
        CreatureDefinition zombie = MakeDef("Zombie");
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        Assert.That(game.AttacksOf(game.Combatants[0]), Is.Not.SameAs(game.AttacksOf(game.Combatants[1])));
    }

    ////////////////////////////////
    // ---------- Seeds ---------- //
    ////////////////////////////////

    [Test]
    public void AFixedSeed_IsKept()
    {
        GameManager game = MakeFight(out _, out _, seed: 4242);

        Assert.That(game.Seed, Is.EqualTo(4242));
    }

    [Test]
    public void SeedZero_PicksARandomNonZeroSeed()
    {
        GameManager game = MakeFight(out _, out _, seed: 0);

        Assert.That(game.Seed, Is.Not.EqualTo(0));
    }

    [Test]
    public void TheSameSeed_GivesTheSameInitiativeOrder()
    {
        // The whole point of logging the seed: put it back in and the fight replays exactly
        List<string> OrderFor(int seed)
        {
            CreatureDefinition zombie = MakeDef("Zombie");
            GameManager game = MakeGame(MakeArena(), seed,
                                        new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
                                        new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 3)),
                                        new Spawn(zombie, Team.Enemies, new Vector3Int(5, 0, 5)),
                                        new Spawn(zombie, Team.Enemies, new Vector3Int(7, 0, 7)));
            Call(game, "Awake");
            Call(game, "Start");
            return game.Controller.Turns.Order.Select(c => $"{c.Creature.Name}:{c.Initiative}").ToList();
        }

        Assert.That(OrderFor(777), Is.EqualTo(OrderFor(777)));
    }

    ////////////////////////////////
    // ---------- Start ---------- //
    ////////////////////////////////

    [Test]
    public void Start_BeginsCombat()
    {
        GameManager game = MakeFight(out _, out _);

        Call(game, "Start");

        Assert.That(game.Controller.Turns.State, Is.EqualTo(CombatState.Ongoing));
        Assert.That(game.Controller.Turns.Current, Is.Not.Null);
        Assert.That(game.Controller.Turns.Round, Is.EqualTo(1));
        Assert.That(game.Controller.Turns.Order.Count, Is.EqualTo(2));
    }

    [Test]
    public void Awake_DoesNotStartCombat_SoOtherScriptsCanSubscribeFirst()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.Controller.Turns.Current, Is.Null);
    }

    [Test]
    public void HeroesWithNoEnemies_EndCombatStraightAway_WithoutAnError()
    {
        GameManager game = MakeGame(MakeArena(), 1, new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)));
        Call(game, "Awake");

        Call(game, "Start");

        Assert.That(game.Controller.Turns.State, Is.EqualTo(CombatState.HeroesWon));
        Assert.That(game.Controller.Turns.Current, Is.Null);
    }

    ///////////////////////////////////////////
    // ---------- DefinitionOf ---------- //
    ///////////////////////////////////////////

    [Test]
    public void DefinitionOf_GivesTheAssetEachCombatantWasMadeFrom()
    {
        GameManager game = MakeFight(out CreatureDefinition heroDef, out CreatureDefinition goblinDef);

        Assert.That(game.DefinitionOf(game.Combatants[0]), Is.SameAs(heroDef));
        Assert.That(game.DefinitionOf(game.Combatants[1]), Is.SameAs(goblinDef));
    }

    [Test]
    public void DefinitionOf_CreaturesFromTheSameAsset_ShareIt()
    {
        // Every Zombie shows the Zombie portrait
        CreatureDefinition zombie = MakeDef("Zombie");
        GameManager game = MakeGame(MakeArena(), 1,
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(1, 0, 1)),
                                    new Spawn(zombie, Team.Enemies, new Vector3Int(3, 0, 1)),
                                    new Spawn(MakeDef("Hero"), Team.Heroes, new Vector3Int(5, 0, 5)));
        Call(game, "Awake");

        Assert.That(game.DefinitionOf(game.Combatants[0]), Is.SameAs(zombie));
        Assert.That(game.DefinitionOf(game.Combatants[1]), Is.SameAs(zombie));
    }

    [Test]
    public void DefinitionOf_NullOrAStranger_IsNull()
    {
        GameManager game = MakeFight(out _, out _);

        Assert.That(game.DefinitionOf(null), Is.Null);
        Assert.That(game.DefinitionOf(new Combatant(new Creature("Stranger"), Team.Heroes)), Is.Null);
    }

    [Test]
    public void DefinitionOf_ForgetsLastTimesCombatants_WhenAwakeRunsAgain()
    {
        GameManager game = MakeFight(out _, out _);
        Combatant oldHero = game.Combatants[0];

        Call(game, "Awake");

        Assert.That(game.DefinitionOf(oldHero), Is.Null, "A combatant from the previous setup");
        Assert.That(game.DefinitionOf(game.Combatants[0]), Is.Not.Null);
    }
}
