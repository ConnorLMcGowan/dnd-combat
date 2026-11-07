using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // The scrolling combat log: a line when each turn starts, and a line for every attack as its attack lands. Old lines are removed past a limit, so it never grows forever.
    public class CombatLogView : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private Transform _content;      // the Scroll View's Content
        [SerializeField] private TMP_Text _linePrefab;
        [SerializeField] private int _maxLines = 50;

        private readonly Queue<TMP_Text> _lines = new Queue<TMP_Text>();
        private CombatController _subscribedTo;

        private void Start()
        {
            if(_game == null || _game.Controller == null || _director == null || _content == null || _linePrefab == null)
            {
                Debug.LogWarning("CombatLogView is missing a required reference.");
                return;
            }

            _subscribedTo = _game.Controller;
            _subscribedTo.Turns.TurnStarted += OnTurnStarted;
            _director.AttackLanded += OnAttackLanded;

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
            }
            if (_director != null)
            {
                _director.AttackLanded -= OnAttackLanded;
            }
        }

        private void OnTurnStarted(Combatant current)
        {
            AddLine(CombatLogText.TurnStart(current, _subscribedTo.Turns.Round));
        }

        private void OnAttackLanded(AttackReport report)
        {
            AddLine(CombatLogText.Describe(report));
        }

        // Adds a line at the bottom, removes the oldest past the limit, and scrolls down
        public void AddLine(string message)
        {
            TMP_Text line = Instantiate(_linePrefab, _content);
            line.text = message;
            _lines.Enqueue(line);

            while (_lines.Count > _maxLines)
            {
                Destroy(_lines.Dequeue().gameObject);
            }
            if(_scroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
