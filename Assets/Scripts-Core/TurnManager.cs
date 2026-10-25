using DnDCombat.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DnDCombat.Core
{
    public enum CombatState { Ongoing, HeroesWon, EnemiesWon }

    public class TurnManager
    {
        public event Action<CombatState> CombatEnded;
        private readonly IDice _dice;
        private readonly List<Combatant> _combatants;
        private List<Combatant> _order = new List<Combatant>();
        private int _currentIndex;

        public Combatant Current { get; private set; }
        public int Round { get; private set; }
        public CombatState State { get; private set; }
        public IReadOnlyList<Combatant> Order => _order;

        public TurnManager(IDice dice, IEnumerable<Combatant> combatants)
        {
            _dice = dice;
            _combatants = new List<Combatant>(combatants);
        }

        public void StartCombat()
        {
            State = CombatState.Ongoing;
            foreach (Combatant c in _combatants)
            {
                c.Initiative = _dice.RollD20() + c.Creature.GetModifier(Ability.Dexterity);
            }

            _order = _combatants
                .OrderByDescending(c => c.Initiative)
                .ThenByDescending(c => c.Creature.GetModifier(Ability.Dexterity))
                .ThenBy(c => c.Team)
                .ToList();

            Round = 1;
            Current = null;
            CheckCombatEnd();

            if (State == CombatState.Ongoing)
            {
                StartTurnFrom(0);
            }
        }
        public void EndTurn()
        {
            if (State != CombatState.Ongoing || Current == null) return;

            CheckCombatEnd();

            if (State != CombatState.Ongoing) return;

            StartTurnFrom(_currentIndex + 1);
        }

        public void CheckCombatEnd()
        {
            if (State != CombatState.Ongoing) return;

            bool heroesAlive = _order.Any(c => c.Team == Team.Heroes && !c.Creature.IsDefeated);
            bool enemiesAlive = Order.Any(c => c.Team == Team.Enemies && !c.Creature.IsDefeated);

            if (!heroesAlive) State = CombatState.EnemiesWon;
            else if (!enemiesAlive) State = CombatState.HeroesWon;
            else State = CombatState.Ongoing;

            if (State != CombatState.Ongoing)
            {
                Current = null;
            }
            CombatEnded?.Invoke(State);
        }

        private void StartTurnFrom(int startIndex)
        {
            int index = startIndex;

            for (int steps = 0; steps < _order.Count; steps++)
            {
                if (index >= _order.Count)
                {
                    index = 0;
                    Round++;
                }

                if (!_order[index].Creature.IsDefeated)
                {
                    _currentIndex = index;
                    Current = _order[index];
                    Current.MovementRemaining = Current.Creature.Speed;
                    Current.ActionAvailable = true;
                    Current.BonusActionAvailable = true;
                    Current.AttacksRemaining = 0;
                    return;
                }
                index++;
            }
        }
    }
}
