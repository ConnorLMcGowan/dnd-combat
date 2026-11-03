using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DnDCombat.Core;
using DnDCombat.Unity;

// Checks the REAL creature assets in the project (Fighter.asset, Skeleton.asset and any added
// later), so a typo in the Inspector is caught here instead of halfway through a fight.
// Every test that takes a path runs once per creature asset, so new creatures are covered
// automatically.
public class CreatureAssetsTest
{
    private static readonly int[] RealDice = { 4, 6, 8, 10, 12, 20 };

    // Every CreatureDefinition asset in the project
    private static IEnumerable<string> AllDefinitionPaths()
    {
        return AssetDatabase.FindAssets("t:CreatureDefinition")
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .OrderBy(path => path);
    }

    private static CreatureDefinition Load(string path)
    {
        CreatureDefinition definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path);
        Assert.That(definition, Is.Not.Null, $"Couldn't load {path}");
        return definition;
    }

    private static CreatureDefinition FindByName(string displayName)
    {
        CreatureDefinition found = AllDefinitionPaths().Select(Load).FirstOrDefault(d => d.DisplayName == displayName);
        Assert.That(found, Is.Not.Null, $"No CreatureDefinition asset has the display name '{displayName}'");
        return found;
    }

    private static void AssertSameAttack(AttackProfile actual, AttackProfile expected)
    {
        string who = expected.Name;
        Assert.That(actual.AttackBonus, Is.EqualTo(expected.AttackBonus), $"{who}: attack bonus");
        Assert.That(actual.DamageDiceCount, Is.EqualTo(expected.DamageDiceCount), $"{who}: number of dice");
        Assert.That(actual.DamageSides, Is.EqualTo(expected.DamageSides), $"{who}: dice sides");
        Assert.That(actual.DamageBonus, Is.EqualTo(expected.DamageBonus), $"{who}: damage bonus");
        Assert.That(actual.DamageType, Is.EqualTo(expected.DamageType), $"{who}: damage type");
        Assert.That(actual.RangeSquares, Is.EqualTo(expected.RangeSquares), $"{who}: range");
    }

    private static void AssertSameCreature(Creature actual, Creature expected)
    {
        foreach (Ability ability in System.Enum.GetValues(typeof(Ability)))
        {
            Assert.That(actual.GetScore(ability), Is.EqualTo(expected.GetScore(ability)), ability.ToString());
        }
        Assert.That(actual.MaxHP, Is.EqualTo(expected.MaxHP), "Max HP");
        Assert.That(actual.ArmourClass, Is.EqualTo(expected.ArmourClass), "Armour Class");
        Assert.That(actual.Speed, Is.EqualTo(expected.Speed), "Speed");
        Assert.That(actual.ProficiencyBonus, Is.EqualTo(expected.ProficiencyBonus), "Proficiency");
        Assert.That(actual.AttacksPerAction, Is.EqualTo(expected.AttacksPerAction), "Attacks per action");
        foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
        {
            Assert.That(actual.IsResistantTo(type), Is.EqualTo(expected.IsResistantTo(type)), $"resistant to {type}");
            Assert.That(actual.IsVulnerableTo(type), Is.EqualTo(expected.IsVulnerableTo(type)), $"vulnerable to {type}");
            Assert.That(actual.IsImmuneTo(type), Is.EqualTo(expected.IsImmuneTo(type)), $"immune to {type}");
        }
    }

    ////////////////////////////////////////////////
    // ---------- The assets exist at all ---------- //
    ////////////////////////////////////////////////

    [Test]
    public void TheProjectHasCreatureAssets()
    {
        Assert.That(AllDefinitionPaths(), Is.Not.Empty);
    }

    [Test]
    public void DisplayNames_AreUnique()
    {
        // Two assets both called "Skeleton" would make the combat log ambiguous
        List<string> names = AllDefinitionPaths().Select(p => Load(p).DisplayName).ToList();

        Assert.That(names, Is.Unique);
    }

    ////////////////////////////////////////////////////
    // ---------- Every creature asset is usable ---------- //
    ////////////////////////////////////////////////////

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void HasADisplayName(string path)
    {
        string name = Load(path).DisplayName;

        Assert.That(string.IsNullOrWhiteSpace(name), Is.False);
        Assert.That(name, Is.Not.EqualTo("New Creature"), "Still has the default name");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void HasAViewPrefab_ThatCanBeSeen(string path)
    {
        GameObject prefab = Load(path).ViewPrefab;

        Assert.That(prefab, Is.Not.Null, "GameManager refuses to spawn a creature without a View Prefab");
        Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, "The prefab has nothing to draw");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void ViewPrefab_HasACollider_SoItCanBeClickedAndHovered(string path)
    {
        // Asset Store models don't come with colliders. Without one, clicking this creature
        // clicks the floor behind it instead, so it can never be attacked.
        GameObject prefab = Load(path).ViewPrefab;
        Assume.That(prefab, Is.Not.Null);

        Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Not.Empty,
                    $"Add a Capsule Collider to {prefab.name}");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void HasSensibleNumbers(string path)
    {
        Creature creature = Load(path).CreateCreature("Test");

        Assert.That(creature.MaxHP, Is.GreaterThan(0), "Max HP");
        Assert.That(creature.ArmourClass, Is.InRange(1, 30), "Armour Class");
        Assert.That(creature.Speed, Is.GreaterThanOrEqualTo(Pathfinder.FeetPerSquare), "Speed under 5 ft can never move a square");
        Assert.That(creature.Speed % Pathfinder.FeetPerSquare, Is.EqualTo(0), "Speed should be whole squares (a multiple of 5)");
        Assert.That(creature.ProficiencyBonus, Is.InRange(2, 9), "Proficiency");
        foreach (Ability ability in System.Enum.GetValues(typeof(Ability)))
        {
            Assert.That(creature.GetScore(ability), Is.InRange(1, 30), ability.ToString());
        }
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void HasAtLeastOneUsableAttack(string path)
    {
        CreatureDefinition definition = Load(path);
        List<AttackProfile> attacks = definition.CreateAttacks(definition.CreateCreature("Test"));

        Assert.That(attacks, Is.Not.Empty, "With no attacks it can only walk about");
        foreach (AttackProfile attack in attacks)
        {
            Assert.That(string.IsNullOrWhiteSpace(attack.Name), Is.False, "An attack has no name");
            Assert.That(attack.DamageDiceCount, Is.GreaterThanOrEqualTo(1), $"{attack.Name}: number of dice");
            Assert.That(RealDice, Does.Contain(attack.DamageSides), $"{attack.Name}: d{attack.DamageSides} isn't a real die");
            Assert.That(attack.RangeSquares, Is.GreaterThanOrEqualTo(1), $"{attack.Name}: range");
        }
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void IsNotBothResistantAndImmune_ToTheSameDamage(string path)
    {
        // Immunity already wins, so having both is almost certainly a typo
        Creature creature = Load(path).CreateCreature("Test");

        foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
        {
            Assert.That(creature.IsResistantTo(type) && creature.IsImmuneTo(type), Is.False, type.ToString());
        }
    }
    
}
