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
        // TODO(guide): UI 1a. Add a second event here:
        //   public event Action<Combatant> TurnStarted;    // after a creature's turn begins, its resources refreshed
        //   Listeners can read Round and Current for the rest. The banner ("Round 2: Skeleton's
        //   turn"), the log and the initiative panel's highlight all hang off this.
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

        public TurnManager(Dice dice, Combatant fighter, Combatant skeleton)
        {
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
                CombatEnded?.Invoke(State);
            }            
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
                    // TODO(guide): UI 1b. Raise it here, after the resources are set and before
                    //   the return: TurnStarted?.Invoke(Current);
                    //   This is the only place a turn begins (both StartCombat and EndTurn come
                    //   through here), so one line covers the first turn and every one after.
                    //   Pitfall: don't raise it in StartCombat or EndTurn as well, or it fires twice.
                    //   Checkpoint: compiles, every test still passes.
                    return;
                }
                index++;
            }
        }
    }
}
