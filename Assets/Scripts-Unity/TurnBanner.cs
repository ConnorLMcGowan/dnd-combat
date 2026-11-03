using System.Collections;
using TMPro;
using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // "Round 2: Skeleton's turn." Fades in when a turn starts, holds, then fades out.
    public class TurnBanner : MonoBehaviour
    {
        // TODO(guide): UI 7. OVERVIEW
        //
        //   In the editor: a panel (your background art) with a TMP text inside it, and a
        //   CanvasGroup on the panel. A CanvasGroup's Alpha fades everything inside it at once
        //   (0 = invisible, 1 = fully shown). Untick its "Blocks Raycasts" so the banner never
        //   eats clicks meant for the board.
        //
        //   Your toolbox:
        //     CombatLogText.TurnStart(who, round)       the sentence (already tested)
        //     _game.Controller.Turns.Round              the round number
        //     _group.alpha                              0 to 1
        //     Mathf.Clamp01(t / duration)               0 to 1 over a duration
        //     StopAllCoroutines(), StartCoroutine(...)
        //
        //   Worked timing with the defaults: fade in 0.2 s, hold 1.0 s, fade out 0.4 s = 1.6 s.
        //   The Skeleton's think pause is 0.5 s, so the banner is still showing as it starts to
        //   act. That's fine: the banner doesn't block anything.

        [SerializeField] private GameManager _game;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _fadeIn = 0.2f;
        [SerializeField] private float _hold = 1.0f;
        [SerializeField] private float _fadeOut = 0.4f;

        private CombatController _subscribedTo;

        private void Start()
        {
            // TODO(guide): UI 7a.
            //   a. Start hidden: if _group isn't null, _group.alpha = 0.
            //   b. If _game, _game.Controller, _text or _group is missing: Debug.LogWarning and return.
            //   c. _subscribedTo = _game.Controller; subscribe Turns.TurnStarted += OnTurnStarted.
            //   d. Catch up if a turn already started (same as InitiativePanel step 6a c).
        }

        private void OnDestroy()
        {
            // TODO(guide): UI 7e. Unsubscribe, if _subscribedTo isn't null.
        }

        private void OnTurnStarted(Combatant current)
        {
            // TODO(guide): UI 7b.
            //   a. _text.text = CombatLogText.TurnStart(current, _subscribedTo.Turns.Round);
            //   b. StopAllCoroutines();  (a new turn interrupts an old banner instead of fighting it)
            //   c. StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            // TODO(guide): UI 7c.
            //   a. Fade in:  for (float t = 0; t < _fadeIn; t += Time.deltaTime)
            //                  { _group.alpha = Mathf.Clamp01(t / _fadeIn); yield return null; }
            //                _group.alpha = 1;
            //   b. yield return new WaitForSeconds(_hold);
            //   c. Fade out: the same loop, with alpha = 1 - Mathf.Clamp01(t / _fadeOut), then alpha = 0.
            //   Pitfall: a fade of 0 seconds must still work. The loop just doesn't run, and the
            //   line after it sets the final alpha.
            yield break;
        }
    }
}
