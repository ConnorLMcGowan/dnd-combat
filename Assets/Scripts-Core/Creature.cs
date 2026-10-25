using System;
using System.Collections.Generic;

namespace DnDCombat.Core
{
    public enum Ability
    {
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma
    }
    /* HOW TO USE IN OTHER SCRIPTS
    Creature fighter = new Creature("Fighter", 16, 12, 14, 10, 10, 8);
    int strMod = fighter.GetModifier(Ability.Strength);   // 3
    */
    public class Creature
    {
        public string Name { get; private set; }
        public int MaxHP { get; private set; }        
        public int CurrentHP { get; private set; }
        public int ArmourClass { get; private set; }
        public int Speed { get; private set; }
        public int ProficiencyBonus { get; private set; }
        public int AttacksPerAction { get; private set; }
        public bool IsDefeated => CurrentHP <= 0;

        // The six scores live here, keyed by the Ability enum.
        // Private, so nothing outside Creature can change them.
        private readonly Dictionary<Ability, int> _scores = new Dictionary<Ability, int>();

        // Creature Constructor
        public Creature(string name, 
                        int strength = 10, int dexterity = 10, int constitution = 10, int intelligence = 10, int wisdom = 10, int charisma = 10, 
                        int maxHP = 10, int armourClass = 10, int speed = 30, int proficiencyBonus = 2, int attacksPerAction = 1)
        {
            Name = name;
            MaxHP = maxHP;
            CurrentHP = maxHP;
            ArmourClass = armourClass;
            Speed = speed;
            ProficiencyBonus = proficiencyBonus;
            AttacksPerAction = Math.Max(1, attacksPerAction);
            _scores[Ability.Strength] = strength;
            _scores[Ability.Dexterity] = dexterity;
            _scores[Ability.Constitution] = constitution;
            _scores[Ability.Intelligence] = intelligence;
            _scores[Ability.Wisdom] = wisdom;
            _scores[Ability.Charisma] = charisma;
        }

        public int GetScore(Ability ability)
        {
            return _scores[ability];
        }

        public int GetModifier(Ability ability)
        {
            return CalculateModifier(GetScore(ability));
        }

        private int CalculateModifier(int score)
        {
            float modifier = (score - 10) / 2.0f;
            return (int)Math.Floor(modifier);
        }
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            CurrentHP = Math.Max(CurrentHP - amount, 0);     
        }
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            int room = MaxHP - CurrentHP;

            if (amount > room)
                amount = room;

            CurrentHP = Math.Min(CurrentHP + amount, MaxHP);            
        }
    }
}