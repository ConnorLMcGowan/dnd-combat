using DnDCombat.Core;

namespace DnDCombat.Core
{
    public class Combatant
    {
        public Creature Creature { get; }
        public Team Team { get; }
        public int Initiative { get; set; }          // set when combat starts

        // Per-turn resources, reset at the start of each of this combatant's turns
        public int MovementRemaining { get; set; }   // in feet
        public bool ActionAvailable { get; set; }
        public bool BonusActionAvailable { get; set; }

        public Combatant(Creature creature, Team team)
        {
            Creature = creature;
            Team = team;
        }
    }
}
