using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // "Victory!" or "Defeat", shown a moment after the last blow lands, with a Restart button.
    public class VictoryScreen : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private CanvasGroup _group;           // on the full-screen panel
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _subtitle;           // optional
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _nextLevelButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private CombatLogView _log;           // optional: adds a final line to the log

        [Header("Words")]
        [SerializeField] private string _victoryTitle = "Victory!";
        [SerializeField] private string _victorySubtitle = "The heroes stand triumphant.";
        [SerializeField] private string _defeatTitle = "Defeat";
        [SerializeField] private string _defeatSubtitle = "The party has fallen.";

        [Header("Timing")]
        [SerializeField] private float _delay = 1.5f;          // after the last animation ends
        [SerializeField] private float _fadeIn = 0.5f;

        private CombatController _subscribedTo;

        private void Start()
        {
            SetShown(false);
            if(_game == null || _game.Controller == null || _group == null)
            {
                Debug.LogWarning("VictoryScreen is missing a required reference.");
                return;
            }
            if(_restartButton != null)
            {
                _restartButton.onClick.AddListener(Restart);
            }
            
            _subscribedTo = _game.Controller;
            _game.Controller.Turns.CombatEnded += OnCombatEnded;

            if (_subscribedTo.Turns.State != CombatState.Ongoing)
            {
                OnCombatEnded(_subscribedTo.Turns.State);
            }
        }

        private void OnDestroy()
        {
            if(_subscribedTo != null)
            {
                _subscribedTo.Turns.CombatEnded -= OnCombatEnded;
            }
            if(_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(Restart);
            }
        }

        private void OnCombatEnded(CombatState result)
        {
            StopAllCoroutines();
            StartCoroutine(Reveal(result));
        }

        private IEnumerator Reveal(CombatState result)
        {
            if(_director != null)
            {
                yield return new WaitUntil(() => !_director.IsBusy);
            }
            yield return new WaitForSeconds(_delay);
            if(_title != null)
            {
                _title.text = result == CombatState.HeroesWon ? _victoryTitle : _defeatTitle;
            }
            if(_subtitle != null)
            {
                _subtitle.text = result == CombatState.HeroesWon ? _victorySubtitle : _defeatSubtitle;
            }
            if(_nextLevelButton != null)
            {
                _nextLevelButton.gameObject.SetActive(result == CombatState.HeroesWon); // only show the next level button if the heroes won
            }
            if (_log != null)
            {
                _log.AddLine(result == CombatState.HeroesWon ? _victoryTitle : _defeatTitle);
            }
            SetShown(true);
            _group.alpha = 0;
            for (float t = 0; t < _fadeIn; t += Time.deltaTime)
            {
                _group.alpha = Mathf.Clamp01(t / _fadeIn);
                yield return null;
            }
            _group.alpha = 1;
        }

        // Hides or shows the whole panel, including whether it catches clicks
        private void SetShown(bool shown)
        {
            if (_group == null) return;

            _group.alpha = shown ? 1f : 0f;
            _group.blocksRaycasts = shown;
            _group.interactable = shown;
        }

        // Starts the whole fight again from the top
        private void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
