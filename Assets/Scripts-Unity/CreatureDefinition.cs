using System.Collections.Generic;
using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // One kind of creature, as a data asset: a Fighter, a Skeleton, a Zombie, a Dragon.
    // Create these in the Project window (Assets > Create > DnD > Creature Definition).
    [CreateAssetMenu(menuName = "DnD/Creature Definition", fileName = "NewCreature")]
    public class CreatureDefinition : ScriptableObject
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // Pure data, plus two methods that turn it into Core objects.
        // Nothing here knows about teams or squares: the spawn list decides those.

        ////////////////////////////////////////
        // ---------- Attack entry ---------- //
        ////////////////////////////////////////
        [System.Serializable]
        public class AttackEntry
        {
            public string Name = "Attack";
            public Ability AttackAbility = Ability.Strength;    // adds to the hit and the damage
            public int DiceCount = 1;
            public int DiceSides = 6;
            public DamageType DamageKind = DamageType.Slashing;
            public int RangeSquares = 1;                        // 1 = melee, 2 = 10 ft reach, 6 = 30 ft
        }

        ///////////////////////////////////////////////
        // ---------- Inspector settings ---------- //
        ///////////////////////////////////////////////
        [Header("Identity")]
        [SerializeField] private string _displayName = "New Creature";
        [SerializeField] private GameObject _viewPrefab;

        [Header("Ability scores")]
        [SerializeField] private int _strength = 10;
        [SerializeField] private int _dexterity = 10;
        [SerializeField] private int _constitution = 10;
        [SerializeField] private int _intelligence = 10;
        [SerializeField] private int _wisdom = 10;
        [SerializeField] private int _charisma = 10;

        [Header("Combat numbers")]
        [SerializeField] private int _maxHP = 10;
        [SerializeField] private int _armourClass = 10;
        [SerializeField] private int _speed = 30;
        [SerializeField] private int _proficiencyBonus = 2;
        [SerializeField] private int _attacksPerAction = 1;

        [Header("Damage rules")]
        [SerializeField] private List<DamageType> _resistances = new List<DamageType>();
        [SerializeField] private List<DamageType> _vulnerabilities = new List<DamageType>();
        [SerializeField] private List<DamageType> _immunities = new List<DamageType>();

        [Header("Attacks (the first one is the default)")]
        [SerializeField] private List<AttackEntry> _attacks = new List<AttackEntry>();

        public string DisplayName => _displayName;
        public GameObject ViewPrefab => _viewPrefab;

        ///////////////////////////////////////////////
        // ---------- Build the Core creature ---------- //
        ///////////////////////////////////////////////
        public Creature CreateCreature(string name)
        {
            return new Creature(name,
                strength: _strength, dexterity: _dexterity, constitution: _constitution,
                intelligence: _intelligence, wisdom: _wisdom, charisma: _charisma,
                maxHP: _maxHP, armourClass: _armourClass, speed: _speed,
                proficiencyBonus: _proficiencyBonus, attacksPerAction: _attacksPerAction,
                resistances: _resistances,
                vulnerabilities: _vulnerabilities,
                immunities: _immunities);
        }

        ////////////////////////////////////////////////
        // ---------- Build the Core attacks ---------- //
        ////////////////////////////////////////////////
        // Bonuses come from the creature itself: ability modifier (and proficiency to hit).
        // Assumes the creature is proficient with all of its attacks.
        public List<AttackProfile> CreateAttacks(Creature creature)
        {
            List<AttackProfile> result = new List<AttackProfile>();

            foreach (AttackEntry entry in _attacks)
            {
                int modifier = creature.GetModifier(entry.AttackAbility);
                int range = entry.RangeSquares > 0 ? entry.RangeSquares : 1;

                result.Add(new AttackProfile(entry.Name,
                                             modifier + creature.ProficiencyBonus,
                                             entry.DiceCount, entry.DiceSides, modifier,
                                             entry.DamageKind, range));
            }

            return result;
        }
    }
}