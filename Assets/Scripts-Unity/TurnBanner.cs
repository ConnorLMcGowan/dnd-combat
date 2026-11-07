using System.Collections;
using TMPro;
using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // EG "Round 2: Skeleton's turn." Fades in when a turn starts, holds, then fades out.
    public class TurnBanner : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private float _fadeIn = 0.2f;
        [SerializeField] private float _hold = 1.0f;
        [SerializeField] private float _fadeOut = 0.4f;

        private CombatController _subscribedTo;

        private void Start()
        {
            if(_group != null)
            {
                _group.alpha = 0;
            }
            if(_game == null || _game.Controller == null || _text == null || _group == null)
            {
                Debug.LogWarning("TurnBanner is missing a required reference.");
                return;
            }
            _subscribedTo = _game.Controller;
            _subscribedTo.Turns.TurnStarted += OnTurnStarted;
            if (_subscribedTo.Turns.Current != null)
            {
                OnTurnStarted(_subscribedTo.Turns.Current);
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe
            if(_subscribedTo != null)
            {
                _subscribedTo.Turns.TurnStarted -= OnTurnStarted;
            }
        }

        private void OnTurnStarted(Combatant current)
        {
            _text.text = CombatLogText.TurnStart(current, _subscribedTo.Turns.Round);
            StopAllCoroutines();
            StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            for (float t = 0; t < _fadeIn; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Clamp01(t / _fadeIn);
                yield return null;
            }
            _group.alpha = 1;      // land exactly on fully shown
            yield return new WaitForSeconds(_hold);
            for (float t = 0; t < _fadeOut; t += Time.deltaTime)
            {
                _group.alpha = 1 - Mathf.Clamp01(t / _fadeOut);
                yield return null;
            }
            _group.alpha = 0;
        }
    }
}
