using System.Collections.Generic;
using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // The row of creature cards in turn order: whose turn it is, and everyone's HP.
    public class InitiativePanel : MonoBehaviour
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // The turn order only exists once combat has started (GameManager.Start rolls initiative),
        // so the cards are built the first time a turn starts, not in Awake. After that:
        //   TurnStarted   -> highlight the new current creature, un-highlight the rest
        //   AttackLanded  -> update the target's HP (from the report), grey it out if defeated
        // HP comes from the BodyDirector's AttackLanded, not the Core's AttackResolved, so the bar
        // drops as the blade lands, not before the swing.

        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private Transform _container;           // where the cards go (e.g. with a Horizontal Layout Group)
        [SerializeField] private InitiativeEntry _entryPrefab;   // your card prefab

        private readonly Dictionary<Combatant, InitiativeEntry> _entries = new Dictionary<Combatant, InitiativeEntry>();
        private CombatController _subscribedTo;    // remembered so OnDestroy unsubscribes from the same one
        private BodyDirector _listeningTo;         // the same, for the director

        ///////////////////////////////////
        // ---------- Start ---------- //
        ///////////////////////////////////
        private void Start()
        {
            if (_game == null || _game.Controller == null)
            {
                Debug.LogWarning("InitiativePanel: no ready GameManager, so no cards will show.");
                return;
            }
            if (_director == null || _container == null || _entryPrefab == null)
            {
                Debug.LogWarning("InitiativePanel: fill in the Director, Container and Entry Prefab slots.");
                return;
            }

            _subscribedTo = _game.Controller;
            _subscribedTo.Turns.TurnStarted += OnTurnStarted;
            _listeningTo = _director;
            _listeningTo.AttackLanded += OnAttackLanded;

            // Scripts start in no fixed order. If combat already began before we subscribed, we
            // missed the first TurnStarted, so catch up now.
            if (_subscribedTo.Turns.Current != null)
            {
                OnTurnStarted(_subscribedTo.Turns.Current);
            }
        }

        private void OnDestroy()
        {
            if (_subscribedTo != null)
            {
                _subscribedTo.Turns.TurnStarted -= OnTurnStarted;
                _subscribedTo = null;
            }
            if (_listeningTo != null)
            {
                _listeningTo.AttackLanded -= OnAttackLanded;
                _listeningTo = null;
            }
        }

        ///////////////////////////////////////////
        // ---------- Building the cards ---------- //
        ///////////////////////////////////////////
        // One card per creature, in turn order: the order you see is the order turns go in
        private void Build()
        {
            foreach (Combatant combatant in _subscribedTo.Turns.Order)
            {
                InitiativeEntry entry = Instantiate(_entryPrefab, _container);
                entry.name = $"{_entryPrefab.name} ({combatant.Creature.Name})";
                entry.Show(combatant);

                // The picture comes from the creature's asset, so every Zombie shares the Zombie's
                CreatureDefinition definition = _game.DefinitionOf(combatant);
                if (definition != null)
                {
                    entry.SetPortrait(definition.Portrait);
                }

                _entries[combatant] = entry;
            }
        }

        //////////////////////////////////////////////
        // ---------- Reacting to events ---------- //
        //////////////////////////////////////////////
        private void OnTurnStarted(Combatant current)
        {
            if (_entries.Count == 0)
            {
                Build();
            }

            foreach (KeyValuePair<Combatant, InitiativeEntry> pair in _entries)
            {
                pair.Value.SetCurrent(pair.Key == current);
            }
        }

        private void OnAttackLanded(AttackReport report)
        {
            if (!_entries.TryGetValue(report.Target, out InitiativeEntry entry))
            {
                return;
            }

            // TargetHPAfter, not the creature's HP now: several attacks can resolve before the
            // first is shown, and the bar should step down one hit at a time
            entry.SetHP(report.TargetHPAfter, report.Target.Creature.MaxHP);
            entry.SetDefeated(report.TargetDefeated);
        }
    }
}
