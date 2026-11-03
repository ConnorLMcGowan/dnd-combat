using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // The scrolling combat log: a line when each turn starts, and a line for every attack as its
    // blade lands. Old lines are removed past a limit, so it never grows forever.
    public class CombatLogView : MonoBehaviour
    {
        // TODO(guide): UI 8. OVERVIEW
        //
        //   In the editor: GameObject > UI > Scroll View. Inside it, select "Content" and add a
        //   Vertical Layout Group (tick Control Child Size: Height, untick Child Force Expand:
        //   Height) and a Content Size Fitter (Vertical Fit: Preferred Size). Then make a line
        //   prefab: one TMP text, styled how you like, with word wrapping on.
        //   The layout group stacks the lines, and the size fitter grows Content to fit them,
        //   which is what lets the Scroll View scroll.
        //
        //   Your toolbox:
        //     CombatLogText.Describe(report), CombatLogText.TurnStart(who, round)
        //     Instantiate(_linePrefab, _content)          a new line at the bottom
        //     Queue<TMP_Text> _lines                      oldest first, so the oldest is easy to remove
        //     Destroy(line.gameObject)
        //     Canvas.ForceUpdateCanvases()                makes the layout catch up straight away
        //     _scroll.verticalNormalizedPosition = 0      scrolls to the bottom (1 is the top)

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
            // TODO(guide): UI 8a.
            //   a. Missing _game, _game.Controller, _director, _content or _linePrefab: warn and return.
            //   b. Subscribe Turns.TurnStarted += OnTurnStarted and _director.AttackLanded += OnAttackLanded.
            //   c. Catch up if a turn already started (as in InitiativePanel).
        }

        private void OnDestroy()
        {
            // TODO(guide): UI 8e. Unsubscribe both, with the usual null guards.
        }

        private void OnTurnStarted(Combatant current)
        {
            // TODO(guide): UI 8b. AddLine(CombatLogText.TurnStart(current, _subscribedTo.Turns.Round));
        }

        private void OnAttackLanded(AttackReport report)
        {
            // TODO(guide): UI 8c. AddLine(CombatLogText.Describe(report));
        }

        // Adds a line at the bottom, removes the oldest past the limit, and scrolls down
        public void AddLine(string message)
        {
            // TODO(guide): UI 8d.
            //   a. TMP_Text line = Instantiate(_linePrefab, _content);  line.text = message;
            //      _lines.Enqueue(line);
            //   b. while (_lines.Count > _maxLines): Destroy(_lines.Dequeue().gameObject);
            //   c. If _scroll isn't null: Canvas.ForceUpdateCanvases(); then
            //      _scroll.verticalNormalizedPosition = 0f;
            //   Worked example with _maxLines 3: lines A, B, C, then D arrives. 4 > 3, so A is
            //   removed, leaving B, C, D.
            //   Pitfall: without ForceUpdateCanvases, Content hasn't grown yet when you scroll, so
            //   it scrolls to the bottom of the OLD size and the newest line sits just out of view.
            //   It's public so the victory screen (or anything else) can add lines later.
        }
    }
}
