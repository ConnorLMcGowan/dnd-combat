using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DnDCombat.Core;
using DnDCombat.Unity;
using Object = UnityEngine.Object;

// Shared tools for testing the Scripts-Unity MonoBehaviours in edit mode.
//
// Two problems these solve:
//   1. Inspector fields are private ([SerializeField] private ...). Tests set them the same way
//      the Inspector does, through a SerializedObject, so no game code has to change.
//   2. In edit mode Unity doesn't call Awake, Start or LateUpdate by itself. Call() runs them
//      by name. If a method is renamed, the test fails with a clear message saying which one.
public static class UnityTestHelpers
{
    ////////////////////////////////////////////
    // ---------- Inspector fields ---------- //
    ////////////////////////////////////////////

    // Changes one Inspector field, exactly as typing into the Inspector would
    public static void SetField(Object target, string field, Action<SerializedProperty> set)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        Assert.That(property, Is.Not.Null, $"{target.GetType().Name} has no Inspector field '{field}'. Was it renamed?");
        set(property);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetInt(Object target, string field, int value) => SetField(target, field, p => p.intValue = value);
    public static void SetFloat(Object target, string field, float value) => SetField(target, field, p => p.floatValue = value);
    public static void SetString(Object target, string field, string value) => SetField(target, field, p => p.stringValue = value);
    public static void SetObject(Object target, string field, Object value) => SetField(target, field, p => p.objectReferenceValue = value);

    // Replaces a whole Inspector list. fill is called once per element with its index.
    public static void SetList(Object target, string field, int count, Action<SerializedProperty, int> fill)
    {
        SetField(target, field, list =>
        {
            list.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                fill(list.GetArrayElementAtIndex(i), i);
            }
        });
    }

    // Reads an Inspector field back (for checking a scene's saved settings)
    public static SerializedProperty GetField(Object target, string field)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(field);
        Assert.That(property, Is.Not.Null, $"{target.GetType().Name} has no Inspector field '{field}'. Was it renamed?");
        return property;
    }

    /////////////////////////////////////////////
    // ---------- Private methods ---------- //
    /////////////////////////////////////////////

    // Runs a private method such as Awake, Start or LateUpdate, and returns what it returns
    public static object Call(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(info, Is.Not.Null, $"{target.GetType().Name} has no method '{method}'. Was it renamed?");
        try
        {
            return info.Invoke(target, args);
        }
        catch (TargetInvocationException e)
        {
            // Show the real error, not the reflection wrapper around it
            throw e.InnerException ?? e;
        }
    }

    ///////////////////////////////////////////////
    // ---------- Building test objects ---------- //
    ///////////////////////////////////////////////

    // A creature definition built in memory, with one Strength melee attack by default
    public static CreatureDefinition MakeDefinition(string name, GameObject viewPrefab,
                                                    int dexterity = 10, int maxHP = 20, int speed = 30,
                                                    bool withAttack = true)
    {
        CreatureDefinition definition = ScriptableObject.CreateInstance<CreatureDefinition>();
        definition.name = name;
        SetString(definition, "_displayName", name);
        SetObject(definition, "_viewPrefab", viewPrefab);
        SetInt(definition, "_dexterity", dexterity);
        SetInt(definition, "_maxHP", maxHP);
        SetInt(definition, "_speed", speed);
        SetList(definition, "_attacks", withAttack ? 1 : 0, (entry, i) =>
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

    // One row of the GameManager's Spawns list
    public struct Spawn
    {
        public CreatureDefinition Definition;
        public Team Team;
        public Vector3Int Cell;
        public string Name;

        public Spawn(CreatureDefinition definition, Team team, Vector3Int cell, string name = "")
        {
            Definition = definition; Team = team; Cell = cell; Name = name;
        }
    }

    public static void SetSpawns(GameManager game, IList<Spawn> spawns)
    {
        SetList(game, "_spawns", spawns.Count, (entry, i) =>
        {
            entry.FindPropertyRelative("Definition").objectReferenceValue = spawns[i].Definition;
            entry.FindPropertyRelative("Team").intValue = (int)spawns[i].Team;
            entry.FindPropertyRelative("Cell").vector3IntValue = spawns[i].Cell;
            entry.FindPropertyRelative("Name").stringValue = spawns[i].Name ?? "";
        });
    }

    // One row of the ArenaBuilder's Blocks list
    public struct Block
    {
        public Vector3Int Cell;
        public GameObject Prefab;
        public float Scale;
        public bool DecorationOnly;

        public Block(Vector3Int cell, GameObject prefab = null, float scale = 0f, bool decorationOnly = false)
        {
            Cell = cell; Prefab = prefab; Scale = scale; DecorationOnly = decorationOnly;
        }
    }

    public static void SetBlocks(ArenaBuilder arena, IList<Block> blocks)
    {
        SetList(arena, "_blocks", blocks.Count, (entry, i) =>
        {
            entry.FindPropertyRelative("Cell").vector3IntValue = blocks[i].Cell;
            entry.FindPropertyRelative("Prefab").objectReferenceValue = blocks[i].Prefab;
            entry.FindPropertyRelative("Scale").floatValue = blocks[i].Scale;
            entry.FindPropertyRelative("DecorationOnly").boolValue = blocks[i].DecorationOnly;
        });
    }

    // Ends turns until it's a hero's go, so tests don't depend on who won initiative
    public static void PassToFirstHero(CombatController controller)
    {
        for (int i = 0; i < 20 && controller.Turns.Current != null && controller.Turns.Current.Team != Team.Heroes; i++)
        {
            controller.EndTurn();
        }
        Assert.That(controller.Turns.Current, Is.Not.Null, "Combat ended before a hero got a turn");
        Assert.That(controller.Turns.Current.Team, Is.EqualTo(Team.Heroes));
    }
}
