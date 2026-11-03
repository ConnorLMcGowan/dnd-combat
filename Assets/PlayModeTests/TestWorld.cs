// These tests set Inspector fields with the editor's own tools, so they only exist in the editor
#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DnDCombat.Core;
using DnDCombat.Unity;

// Builds a small, real battle for play mode tests: an arena, a GameManager with a hero and an
// enemy, and a camera looking straight down. Unity runs Awake, Start and LateUpdate itself.
//
// The trick for setting private Inspector fields before Awake runs: build everything under a
// parent that starts INACTIVE, fill the fields, then switch the parent on. Awake waits until then.
public class TestWorld
{
    public GameObject Root { get; }
    public GameManager Game { get; private set; }
    public ArenaBuilder Arena { get; private set; }
    public Camera Camera { get; private set; }
    public BodyDirector Director { get; private set; }

    private readonly List<Object> _assets = new List<Object>();
    private bool _animated;     // give each creature a model with a real Animator

    public TestWorld()
    {
        Root = new GameObject("Test World");
        Root.SetActive(false);
    }

    ///////////////////////////////////////////
    // ---------- Building the world ---------- //
    ///////////////////////////////////////////

    // A 15 x 15 arena with the given blocks, centred on the world origin
    public TestWorld WithArena(params Vector3Int[] blocks)
    {
        GameObject go = new GameObject("Arena");
        go.transform.SetParent(Root.transform);
        Arena = go.AddComponent<ArenaBuilder>();
        SetList(Arena, "_blocks", blocks.Length, (entry, i) =>
        {
            entry.FindPropertyRelative("Cell").vector3IntValue = blocks[i];
            entry.FindPropertyRelative("Prefab").objectReferenceValue = null;
            entry.FindPropertyRelative("Scale").floatValue = 0f;
            entry.FindPropertyRelative("DecorationOnly").boolValue = false;
        });
        return this;
    }

    // Call before WithFight: each creature gets a "Model" child with an Animator whose controller
    // has the same four parameters as Creature Base, so tests can read what the code set
    public TestWorld WithAnimatedBodies()
    {
        _animated = true;
        return this;
    }

    // A hero and an enemy (lots of HP, so a lucky hit can't end the fight mid-test)
    public TestWorld WithFight(Vector3Int heroCell, Vector3Int enemyCell, int enemyArmourClass = 10, int enemyHP = 999)
    {
        GameObject go = new GameObject("Game Manager");
        go.transform.SetParent(Root.transform);
        Game = go.AddComponent<GameManager>();
        SetObject(Game, "_arena", Arena);
        SetInt(Game, "_seed", 99);

        CreatureDefinition hero = MakeDefinition("Hero", 10, 999);
        CreatureDefinition enemy = MakeDefinition("Goblin", enemyArmourClass, enemyHP);
        SetList(Game, "_spawns", 2, (entry, i) =>
        {
            entry.FindPropertyRelative("Definition").objectReferenceValue = i == 0 ? hero : enemy;
            entry.FindPropertyRelative("Team").intValue = (int)(i == 0 ? Team.Heroes : Team.Enemies);
            entry.FindPropertyRelative("Cell").vector3IntValue = i == 0 ? heroCell : enemyCell;
            entry.FindPropertyRelative("Name").stringValue = "";
        });
        return this;
    }

    // A camera 20 up, looking straight down, seeing a bit more than the whole arena
    public TestWorld WithCamera()
    {
        GameObject go = new GameObject("Test Camera");
        go.transform.SetParent(Root.transform);
        go.transform.position = new Vector3(0f, 20f, 0f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Camera = go.AddComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = 9f;
        return this;
    }

    // The BodyDirector, playing the real timed show. Call after WithFight.
    // Faster than the game's 4 squares a second, so the tests don't take all day.
    public TestWorld WithDirector(float moveSpeed = 8f)
    {
        Director = Add<BodyDirector>("Body Director");
        SetObject(Director, "_game", Game);
        SetFloat(Director, "_moveSpeed", moveSpeed);
        SetFloat(Director, "_attackHitTime", 0.1f);
        SetFloat(Director, "_attackRecoverTime", 0.1f);
        return this;
    }

    // The turn driver, with short pauses. Call after WithDirector.
    public MonsterTurnDriver AddTurnDriver(float thinkDelay = 0.05f, float afterDelay = 0.05f)
    {
        MonsterTurnDriver driver = Add<MonsterTurnDriver>("Monster Turn Driver");
        SetObject(driver, "_game", Game);
        SetObject(driver, "_director", Director);
        SetFloat(driver, "_thinkDelay", thinkDelay);
        SetFloat(driver, "_afterDelay", afterDelay);
        return driver;
    }

    // Adds a component under the world, letting the caller fill its fields before Awake
    public T Add<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(Root.transform);
        return go.AddComponent<T>();
    }

    // Switches everything on, so Awake runs
    public void Activate()
    {
        Root.SetActive(true);
        Physics.SyncTransforms();
    }

    public void Destroy()
    {
        Object.Destroy(Root);
        foreach (Object asset in _assets)
        {
            if (asset != null) Object.Destroy(asset);
        }
    }

    // The body template lives under the inactive root so it never shows up as a creature itself
    private CreatureDefinition MakeDefinition(string name, int armourClass, int maxHP)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = name + " Body";
        body.transform.SetParent(Root.transform);
        body.transform.position = new Vector3(0f, -100f, 0f);   // well out of the way of mouse rays

        if (_animated)
        {
            // Like a real prefab: the Animator is on a model under the body, not the body itself
            GameObject model = new GameObject("Model");
            model.transform.SetParent(body.transform, false);
            Animator animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = MakeController();
        }

        CreatureDefinition definition = ScriptableObject.CreateInstance<CreatureDefinition>();
        _assets.Add(definition);
        SetString(definition, "_displayName", name);
        SetObject(definition, "_viewPrefab", body);
        SetInt(definition, "_maxHP", maxHP);
        SetInt(definition, "_armourClass", armourClass);
        SetList(definition, "_attacks", 1, (entry, i) =>
        {
            entry.FindPropertyRelative("Name").stringValue = "Club";
            entry.FindPropertyRelative("AttackAbility").intValue = (int)Ability.Strength;
            entry.FindPropertyRelative("DiceCount").intValue = 1;
            entry.FindPropertyRelative("DiceSides").intValue = 4;
            entry.FindPropertyRelative("DamageKind").intValue = (int)DamageType.Bludgeoning;
            entry.FindPropertyRelative("RangeSquares").intValue = 1;
        });
        return definition;
    }

    // A bare controller with Creature Base's four parameters and one empty state. With no
    // transitions, nothing ever uses up a trigger, so a test can see that it was set.
    private UnityEditor.Animations.AnimatorController MakeController()
    {
        UnityEditor.Animations.AnimatorController controller = new UnityEditor.Animations.AnimatorController();
        controller.name = "Test Creature Controller";
        controller.AddLayer("Base Layer");
        controller.layers[0].stateMachine.AddState("Idle");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        _assets.Add(controller);
        return controller;
    }

    ///////////////////////////////////////
    // ---------- During a test ---------- //
    ///////////////////////////////////////

    // Waits (up to a time limit) until the BodyDirector has finished showing everything
    public System.Collections.IEnumerator WaitUntilIdle(float timeout = 5f)
    {
        float giveUp = Time.time + timeout;
        while (Director != null && Director.IsBusy && Time.time < giveUp)
        {
            yield return null;
        }
        Assert.That(Director == null || !Director.IsBusy, Is.True, $"Still busy after {timeout} s");
    }

    // The Animator on a creature's model (only with WithAnimatedBodies)
    public Animator AnimatorOf(Combatant c)
    {
        Animator animator = Game.ViewOf(c).GetComponentInChildren<Animator>();
        Assert.That(animator != null, Is.True, "Use WithAnimatedBodies() to give creatures an Animator");
        return animator;
    }

    public Combatant Hero => Find(Team.Heroes);
    public Combatant Enemy => Find(Team.Enemies);

    private Combatant Find(Team team)
    {
        foreach (Combatant c in Game.Combatants)
        {
            if (c.Team == team) return c;
        }
        return null;
    }

    // Ends turns until it's the hero's go, so tests don't depend on who won initiative
    public void PassToHero()
    {
        for (int i = 0; i < 10 && Game.Controller.Turns.Current != Hero; i++)
        {
            Game.Controller.EndTurn();
        }
        Assert.That(Game.Controller.Turns.Current, Is.SameAs(Hero), "Couldn't get to the hero's turn");
    }

    public GridPosition Where(Combatant c)
    {
        Game.Controller.Grid.TryGetPosition(c, out GridPosition cell);
        return cell;
    }

    // Where on screen a world point appears, for aiming the mouse
    public Vector2 ScreenPointOf(Vector3 world)
    {
        Vector3 screen = Camera.WorldToScreenPoint(world);
        return new Vector2(screen.x, screen.y);
    }

    ///////////////////////////////////////////
    // ---------- Inspector fields ---------- //
    ///////////////////////////////////////////
    // Play mode tests still run inside the editor, so the Inspector's own tools are available

    public static void SetObject(Object target, string field, Object value) => Edit(target, field, p => p.objectReferenceValue = value);
    public static void SetInt(Object target, string field, int value) => Edit(target, field, p => p.intValue = value);
    public static void SetFloat(Object target, string field, float value) => Edit(target, field, p => p.floatValue = value);
    public static void SetString(Object target, string field, string value) => Edit(target, field, p => p.stringValue = value);

    private static void Edit(Object target, string field, System.Action<SerializedProperty> set)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        Assert.That(property, Is.Not.Null, $"{target.GetType().Name} has no Inspector field '{field}'. Was it renamed?");
        set(property);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetList(Object target, string field, int count, System.Action<SerializedProperty, int> fill)
    {
        Edit(target, field, list =>
        {
            list.arraySize = count;
            for (int i = 0; i < count; i++) fill(list.GetArrayElementAtIndex(i), i);
        });
    }
}
#endif
