using System.Collections.Generic;
using UnityEngine;
using DnDCombat.Core;
using Grid = DnDCombat.Core.Grid;

namespace DnDCombat.Unity
{
    public class GameManager : MonoBehaviour
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // The game manager builds every Core object once, in the right order, and hands
        //   them out to the other scripts. It makes no combat decisions.
        // Who fights is decided by the Spawns list in the Inspector. Each entry is a
        //   creature definition (an asset), a team and a starting square.
        // Awake builds everything. Start begins combat. They are separate so that scripts
        //   written later can subscribe to events in their own Start before anything happens.

        ///////////////////////////////////////////
        // ---------- Spawn entries ---------- //
        ///////////////////////////////////////////
        [System.Serializable]
        private struct SpawnEntry
        {
            public CreatureDefinition Definition;
            public Team Team;
            public Vector3Int Cell;
            public string Name;             // optional: empty uses the definition's name
        }

        ///////////////////////////////////////////////
        // ---------- Inspector settings ---------- //
        ///////////////////////////////////////////////
        [SerializeField] private int _seed = 0;                    // 0 means "pick a random one"
        [SerializeField] private ArenaBuilder _arena;
        [SerializeField] private List<SpawnEntry> _spawns = new List<SpawnEntry>();

        ////////////////////////////////////////////////
        // ---------- What others can read ---------- //
        ////////////////////////////////////////////////
        // Anyone can look, only this class can change them.
        // Controller stays null unless setup finished, so null means "not ready".
        public CombatController Controller { get; private set; }
        public GridSpace Space { get; private set; }
        public int Seed { get; private set; }
        public IReadOnlyList<Combatant> Combatants => _combatants;

        private readonly List<Combatant> _combatants = new List<Combatant>();
        private readonly Dictionary<Combatant, CombatantView> _views = new Dictionary<Combatant, CombatantView>();
        private readonly Dictionary<Combatant, List<AttackProfile>> _attacks = new Dictionary<Combatant, List<AttackProfile>>();

        // For naming: how many entries share a definition, and how many have been named so far
        private readonly Dictionary<CreatureDefinition, int> _nameTotals = new Dictionary<CreatureDefinition, int>();
        private readonly Dictionary<CreatureDefinition, int> _nameSeen = new Dictionary<CreatureDefinition, int>();

        // The body that belongs to a combatant, or null if there isn't one
        public CombatantView ViewOf(Combatant combatant)
        {
            if (combatant == null) return null;
            _views.TryGetValue(combatant, out CombatantView view);
            return view;
        }

        // A combatant's default attack (the first in its list). False if it has none.
        public bool TryGetAttack(Combatant combatant, out AttackProfile attack)
        {
            attack = default;
            if (combatant == null) return false;
            if (!_attacks.TryGetValue(combatant, out List<AttackProfile> list) || list.Count == 0) return false;

            attack = list[0];
            return true;
        }

        // Every attack a combatant has, for choosing between them later
        public IReadOnlyList<AttackProfile> AttacksOf(Combatant combatant)
        {
            if (combatant != null && _attacks.TryGetValue(combatant, out List<AttackProfile> list))
            {
                return list;
            }
            return new List<AttackProfile>();
        }

        ///////////////////////////////////
        // ---------- Awake ---------- //
        ///////////////////////////////////
        private void Awake()
        {
            if (_arena == null)
            {
                Debug.LogError("GameManager: the Arena slot is empty.");
                return;
            }
            if (_spawns.Count == 0)
            {
                Debug.LogError("GameManager: the Spawns list is empty, so nobody would fight.");
                return;
            }

            // Seed and dice. One Dice, shared by the TurnManager and the resolver.
            Seed = _seed != 0 ? _seed : Random.Range(1, int.MaxValue);
            Debug.Log($"GameManager: seed {Seed}");
            Dice dice = new Dice(Seed);

            // The arena makes the Core grid and the grid-to-world converter
            _arena.Build();
            Grid grid = _arena.Grid;
            Space = _arena.Space;

            // Count how many entries share each definition, so duplicates can be numbered
            _nameTotals.Clear();
            _nameSeen.Clear();
            foreach (SpawnEntry entry in _spawns)
            {
                if (entry.Definition == null) continue;
                _nameTotals.TryGetValue(entry.Definition, out int count);
                _nameTotals[entry.Definition] = count + 1;
            }

            // Everyone in the spawn list: create, place, give a body
            _combatants.Clear();
            _views.Clear();
            _attacks.Clear();
            for (int i = 0; i < _spawns.Count; i++)
            {
                if (!SpawnCombatant(_spawns[i], i, grid))
                {
                    return;     // the reason is already logged
                }
            }

            // Turn manager, resolver and controller
            TurnManager turns = new TurnManager(dice, _combatants);
            AttackResolver resolver = new AttackResolver(dice);
            Controller = new CombatController(turns, grid, resolver);
        }

        ///////////////////////////////////
        // ---------- Start ---------- //
        ///////////////////////////////////
        private void Start()
        {
            if (Controller == null)
            {
                Debug.LogError("GameManager: setup did not finish (see the error above), so combat was not started.");
                return;
            }

            Controller.Turns.StartCombat();

            Combatant first = Controller.Turns.Current;
            if (first != null)
            {
                Debug.Log($"GameManager: combat started, {first.Creature.Name} goes first.");
            }
            else
            {
                Debug.Log($"GameManager: combat ended immediately ({Controller.Turns.State}).");
            }
        }

        //////////////////////////////////////////////////
        // ---------- Spawning one combatant ---------- //
        //////////////////////////////////////////////////
        // Returns false (after logging why) if the entry could not be set up.
        private bool SpawnCombatant(SpawnEntry entry, int index, Grid grid)
        {
            if (entry.Definition == null)
            {
                Debug.LogError($"GameManager: spawn {index} has no Definition.");
                return false;
            }

            string label = $"Spawn {index} ({entry.Definition.DisplayName}, {entry.Team})";

            if (entry.Definition.ViewPrefab == null)
            {
                Debug.LogError($"GameManager: {label}: its definition has no View Prefab.");
                return false;
            }

            Creature creature = entry.Definition.CreateCreature(NameFor(entry));
            List<AttackProfile> attacks = entry.Definition.CreateAttacks(creature);
            if (attacks.Count == 0)
            {
                Debug.LogWarning($"GameManager: {label} has no attacks, so it can only move.");
            }

            Combatant combatant = new Combatant(creature, entry.Team);

            GridPosition cell = new GridPosition(entry.Cell.x, entry.Cell.y, entry.Cell.z);
            if (!grid.Place(combatant, cell))
            {
                Debug.LogError($"GameManager: {label} could not be placed at {cell}. Is it inside the grid, on open ground, not on a block and not on another creature's square?");
                return false;
            }

            // The body: a copy of the prefab, linked to the combatant and put on its square
            GameObject body = Instantiate(entry.Definition.ViewPrefab, transform);
            body.name = creature.Name;
            CombatantView view = body.GetComponent<CombatantView>();
            if (view == null)
            {
                view = body.AddComponent<CombatantView>();
            }
            view.Bind(combatant, grid, Space);
            view.SnapToGrid();

            _combatants.Add(combatant);
            _views[combatant] = view;
            _attacks[combatant] = attacks;
            return true;
        }

        //////////////////////////////////////////
        // ---------- Naming ---------- //
        //////////////////////////////////////////
        // Own name if given. Otherwise the definition's name, numbered when several
        //   entries share it ("Zombie 1", "Zombie 2"), so the combat log can tell them apart.
        private string NameFor(SpawnEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name;
            }

            string baseName = entry.Definition.DisplayName;
            if (_nameTotals[entry.Definition] <= 1)
            {
                return baseName;
            }

            _nameSeen.TryGetValue(entry.Definition, out int seen);
            seen++;
            _nameSeen[entry.Definition] = seen;
            return $"{baseName} {seen}";
        }
    }
}