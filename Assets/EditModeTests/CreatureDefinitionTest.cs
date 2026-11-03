using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// CreatureDefinition is the data asset behind every creature. These tests build definitions in
// memory (no asset files), set their Inspector fields, and check the Core objects they make.
public class CreatureDefinitionTest
{
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

    private CreatureDefinition Blank()
    {
        CreatureDefinition definition = ScriptableObject.CreateInstance<CreatureDefinition>();
        _made.Add(definition);
        return definition;
    }

    // Replaces the attack list with the given entries
    private static void SetAttacks(CreatureDefinition definition, params (string name, Ability ability, int count, int sides, DamageType type, int range)[] attacks)
    {
        SetList(definition, "_attacks", attacks.Length, (entry, i) =>
        {
            entry.FindPropertyRelative("Name").stringValue = attacks[i].name;
            entry.FindPropertyRelative("AttackAbility").intValue = (int)attacks[i].ability;
            entry.FindPropertyRelative("DiceCount").intValue = attacks[i].count;
            entry.FindPropertyRelative("DiceSides").intValue = attacks[i].sides;
            entry.FindPropertyRelative("DamageKind").intValue = (int)attacks[i].type;
            entry.FindPropertyRelative("RangeSquares").intValue = attacks[i].range;
        });
    }

    private static void SetDamageList(CreatureDefinition definition, string field, params DamageType[] types)
    {
        SetList(definition, field, types.Length, (entry, i) => entry.intValue = (int)types[i]);
    }

    ////////////////////////////////////
    // ---------- Defaults ---------- //
    ////////////////////////////////////

    [Test]
    public void NewDefinition_MakesAnOrdinaryCreature()
    {
        Creature creature = Blank().CreateCreature("Commoner");

        Assert.That(creature.Name, Is.EqualTo("Commoner"));
        Assert.That(creature.GetScore(Ability.Strength), Is.EqualTo(10));
        Assert.That(creature.MaxHP, Is.EqualTo(10));
        Assert.That(creature.ArmourClass, Is.EqualTo(10));
        Assert.That(creature.Speed, Is.EqualTo(30));
        Assert.That(creature.ProficiencyBonus, Is.EqualTo(2));
        Assert.That(creature.AttacksPerAction, Is.EqualTo(1));
    }

    [Test]
    public void NewDefinition_HasNoAttacks_AndNoPrefab()
    {
        CreatureDefinition definition = Blank();

        Assert.That(definition.CreateAttacks(definition.CreateCreature("X")), Is.Empty);
        Assert.That(definition.ViewPrefab, Is.Null);
        Assert.That(definition.DisplayName, Is.EqualTo("New Creature"));
    }

    ////////////////////////////////////////////
    // ---------- CreateCreature ---------- //
    ////////////////////////////////////////////

    [Test]
    public void CreateCreature_CopiesEveryNumber()
    {
        CreatureDefinition definition = Blank();
        SetInt(definition, "_strength", 18);
        SetInt(definition, "_dexterity", 14);
        SetInt(definition, "_constitution", 16);
        SetInt(definition, "_intelligence", 7);
        SetInt(definition, "_wisdom", 12);
        SetInt(definition, "_charisma", 9);
        SetInt(definition, "_maxHP", 45);
        SetInt(definition, "_armourClass", 15);
        SetInt(definition, "_speed", 40);
        SetInt(definition, "_proficiencyBonus", 3);
        SetInt(definition, "_attacksPerAction", 2);

        Creature creature = definition.CreateCreature("Ogre");

        Assert.That(creature.GetScore(Ability.Strength), Is.EqualTo(18));
        Assert.That(creature.GetScore(Ability.Dexterity), Is.EqualTo(14));
        Assert.That(creature.GetScore(Ability.Constitution), Is.EqualTo(16));
        Assert.That(creature.GetScore(Ability.Intelligence), Is.EqualTo(7));
        Assert.That(creature.GetScore(Ability.Wisdom), Is.EqualTo(12));
        Assert.That(creature.GetScore(Ability.Charisma), Is.EqualTo(9));
        Assert.That(creature.MaxHP, Is.EqualTo(45));
        Assert.That(creature.CurrentHP, Is.EqualTo(45));
        Assert.That(creature.ArmourClass, Is.EqualTo(15));
        Assert.That(creature.Speed, Is.EqualTo(40));
        Assert.That(creature.ProficiencyBonus, Is.EqualTo(3));
        Assert.That(creature.AttacksPerAction, Is.EqualTo(2));
    }

    [Test]
    public void CreateCreature_UsesTheNameItIsGiven_NotTheDisplayName()
    {
        // GameManager passes "Zombie 2" when there are several, so the given name must win
        CreatureDefinition definition = Blank();
        SetString(definition, "_displayName", "Zombie");

        Assert.That(definition.CreateCreature("Zombie 2").Name, Is.EqualTo("Zombie 2"));
    }

    [Test]
    public void CreateCreature_CopiesTheDamageRules()
    {
        CreatureDefinition definition = Blank();
        SetDamageList(definition, "_resistances", DamageType.Fire, DamageType.Cold);
        SetDamageList(definition, "_vulnerabilities", DamageType.Radiant);
        SetDamageList(definition, "_immunities", DamageType.Poison, DamageType.Necrotic);

        Creature creature = definition.CreateCreature("Wight");

        Assert.That(creature.IsResistantTo(DamageType.Fire), Is.True);
        Assert.That(creature.IsResistantTo(DamageType.Cold), Is.True);
        Assert.That(creature.IsResistantTo(DamageType.Slashing), Is.False);
        Assert.That(creature.IsVulnerableTo(DamageType.Radiant), Is.True);
        Assert.That(creature.IsImmuneTo(DamageType.Poison), Is.True);
        Assert.That(creature.IsImmuneTo(DamageType.Necrotic), Is.True);
        Assert.That(creature.IsImmuneTo(DamageType.Fire), Is.False);
    }

    [Test]
    public void CreateCreature_EachCallMakesASeparateCreature()
    {
        // Two Zombies from one asset must not share HP
        CreatureDefinition definition = Blank();
        SetInt(definition, "_maxHP", 20);

        Creature first = definition.CreateCreature("Zombie 1");
        Creature second = definition.CreateCreature("Zombie 2");
        first.TakeDamage(15);

        Assert.That(first, Is.Not.SameAs(second));
        Assert.That(second.CurrentHP, Is.EqualTo(20));
    }

    [Test]
    public void EditingTheAssetLater_DoesNotChangeACreatureAlreadyMade()
    {
        CreatureDefinition definition = Blank();
        SetDamageList(definition, "_resistances", DamageType.Fire);
        Creature creature = definition.CreateCreature("Imp");

        SetDamageList(definition, "_resistances");          // emptied in the Inspector mid-fight
        SetInt(definition, "_armourClass", 1);

        Assert.That(creature.IsResistantTo(DamageType.Fire), Is.True);
        Assert.That(creature.ArmourClass, Is.EqualTo(10));
    }

    //////////////////////////////////////////
    // ---------- CreateAttacks ---------- //
    //////////////////////////////////////////

    [Test]
    public void StrengthAttack_AddsModifierAndProficiencyToHit_AndModifierToDamage()
    {
        // Strength 16 is +3, proficiency +2: +5 to hit, 1d8 + 3
        CreatureDefinition definition = Blank();
        SetInt(definition, "_strength", 16);
        SetAttacks(definition, ("Longsword", Ability.Strength, 1, 8, DamageType.Slashing, 1));
        Creature creature = definition.CreateCreature("Fighter");

        AttackProfile attack = definition.CreateAttacks(creature)[0];

        Assert.That(attack.Name, Is.EqualTo("Longsword"));
        Assert.That(attack.AttackBonus, Is.EqualTo(5));
        Assert.That(attack.DamageBonus, Is.EqualTo(3));
        Assert.That(attack.DamageDiceCount, Is.EqualTo(1));
        Assert.That(attack.DamageSides, Is.EqualTo(8));
        Assert.That(attack.DamageType, Is.EqualTo(DamageType.Slashing));
        Assert.That(attack.RangeSquares, Is.EqualTo(1));
    }

    [Test]
    public void DexterityAttack_UsesDexterity_NotStrength()
    {
        // Strength 8 (-1) would give +1 to hit. Dexterity 16 (+3) gives +5.
        CreatureDefinition definition = Blank();
        SetInt(definition, "_strength", 8);
        SetInt(definition, "_dexterity", 16);
        SetAttacks(definition, ("Shortbow", Ability.Dexterity, 1, 6, DamageType.Piercing, 16));
        Creature creature = definition.CreateCreature("Archer");

        AttackProfile attack = definition.CreateAttacks(creature)[0];

        Assert.That(attack.AttackBonus, Is.EqualTo(5));
        Assert.That(attack.DamageBonus, Is.EqualTo(3));
        Assert.That(attack.RangeSquares, Is.EqualTo(16));
    }

    [Test]
    public void NegativeModifier_LowersBothHitAndDamage()
    {
        // Strength 6 is -2: +0 to hit (-2 + 2), damage bonus -2
        CreatureDefinition definition = Blank();
        SetInt(definition, "_strength", 6);
        SetAttacks(definition, ("Bite", Ability.Strength, 1, 4, DamageType.Piercing, 1));
        Creature creature = definition.CreateCreature("Rat");

        AttackProfile attack = definition.CreateAttacks(creature)[0];

        Assert.That(attack.AttackBonus, Is.EqualTo(0));
        Assert.That(attack.DamageBonus, Is.EqualTo(-2));
    }

    [Test]
    public void HigherProficiency_RaisesTheAttackBonusOnly()
    {
        CreatureDefinition definition = Blank();
        SetInt(definition, "_strength", 16);
        SetInt(definition, "_proficiencyBonus", 4);
        SetAttacks(definition, ("Greataxe", Ability.Strength, 1, 12, DamageType.Slashing, 1));
        Creature creature = definition.CreateCreature("Veteran");

        AttackProfile attack = definition.CreateAttacks(creature)[0];

        Assert.That(attack.AttackBonus, Is.EqualTo(7));    // 3 + 4
        Assert.That(attack.DamageBonus, Is.EqualTo(3));
    }

    [Test]
    public void SeveralAttacks_KeepTheirInspectorOrder()
    {
        // The first is the default attack, so the order matters
        CreatureDefinition definition = Blank();
        SetAttacks(definition,
            ("Flail", Ability.Strength, 1, 8, DamageType.Bludgeoning, 1),
            ("Greatsword", Ability.Strength, 2, 6, DamageType.Slashing, 1),
            ("Halberd", Ability.Strength, 1, 10, DamageType.Slashing, 2));
        Creature creature = definition.CreateCreature("Fighter");

        List<AttackProfile> attacks = definition.CreateAttacks(creature);

        Assert.That(attacks.Count, Is.EqualTo(3));
        Assert.That(attacks[0].Name, Is.EqualTo("Flail"));
        Assert.That(attacks[1].Name, Is.EqualTo("Greatsword"));
        Assert.That(attacks[1].DamageDiceCount, Is.EqualTo(2));
        Assert.That(attacks[2].Name, Is.EqualTo("Halberd"));
        Assert.That(attacks[2].RangeSquares, Is.EqualTo(2));
    }

    [TestCase(0)]
    [TestCase(-3)]
    public void ZeroOrNegativeRange_IsTreatedAsMelee(int range)
    {
        // A range of 0 would mean "can't reach anyone", which is never what was meant
        CreatureDefinition definition = Blank();
        SetAttacks(definition, ("Claw", Ability.Strength, 1, 6, DamageType.Slashing, range));
        Creature creature = definition.CreateCreature("Beast");

        Assert.That(definition.CreateAttacks(creature)[0].RangeSquares, Is.EqualTo(1));
    }

    [Test]
    public void CreateAttacks_UsesTheCreatureItIsGiven()
    {
        // Bonuses come from the creature passed in, so a different creature gives different bonuses
        CreatureDefinition definition = Blank();
        SetAttacks(definition, ("Club", Ability.Strength, 1, 4, DamageType.Bludgeoning, 1));
        Creature strong = new Creature("Strong", strength: 20, proficiencyBonus: 2);

        AttackProfile attack = definition.CreateAttacks(strong)[0];

        Assert.That(attack.AttackBonus, Is.EqualTo(7));    // +5 and +2
        Assert.That(attack.DamageBonus, Is.EqualTo(5));
    }

    [Test]
    public void CreateAttacks_GivesANewListEachTime()
    {
        // GameManager keeps one list per combatant, so they must not share
        CreatureDefinition definition = Blank();
        SetAttacks(definition, ("Club", Ability.Strength, 1, 4, DamageType.Bludgeoning, 1));
        Creature creature = definition.CreateCreature("A");

        List<AttackProfile> first = definition.CreateAttacks(creature);
        List<AttackProfile> second = definition.CreateAttacks(creature);
        first.Clear();

        Assert.That(second.Count, Is.EqualTo(1));
    }
}
