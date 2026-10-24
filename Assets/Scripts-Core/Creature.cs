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

        // The six scores live here, keyed by the Ability enum.
        // Private, so nothing outside Creature can change them.
        private readonly Dictionary<Ability, int> _scores = new Dictionary<Ability, int>();

        // Creature Constructor
        public Creature(string name, int strength, int dexterity, int constitution,
                        int intelligence, int wisdom, int charisma)
        {
            Name = name;
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
    }
}