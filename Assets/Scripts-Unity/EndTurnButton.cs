using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // Ends the hero's turn when clicked. Only clickable when ending the turn is allowed:
    // on a hero's turn, while combat is on, and when nothing is still walking or swinging.
    public class EndTurnButton : MonoBehaviour
    {
        // TODO(guide): UI 9. OVERVIEW
        //
        //   In the editor: a Button (your art), with this component on it or anywhere else.
        //   A Button's "interactable" switches it on and off, and its Transition (Color Tint or
        //   Sprite Swap) shows the off look automatically.
        //
        //   Your toolbox:
        //     _button.onClick.AddListener(OnClicked)       run OnClicked when it's clicked
        //     _button.onClick.RemoveListener(OnClicked)
        //     _button.interactable = true / false
        //     _game.Controller.EndTurn()
        //
        //   Space still ends the turn too (that's in ClickInput, and it already waits for the
        //   director). The button and the key do the same thing.

        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private Button _button;

        private void Start()
        {
            // TODO(guide): UI 9a. If _button isn't null, _button.onClick.AddListener(OnClicked).
        }

        private void OnDestroy()
        {
            // TODO(guide): UI 9d. If _button isn't null, RemoveListener(OnClicked).
        }

        private void Update()
        {
            // TODO(guide): UI 9b. If _button isn't null: _button.interactable = CanEndTurn();
            //   Every frame is fine here: it's one bool, and it means the button can never be
            //   left switched on by mistake.
        }

        private void OnClicked()
        {
            // TODO(guide): UI 9c. Check again (if CanEndTurn()), then _game.Controller.EndTurn().
            //   Checking again matters: a click can land in the same frame the turn changes.
        }

        // True only when the player is allowed to end the turn right now
        private bool CanEndTurn()
        {
            // TODO(guide): UI 9e. Return false if any of these, otherwise true:
            //   _game or _game.Controller is null
            //   Turns.State isn't CombatState.Ongoing
            //   Turns.Current is null, or its Team isn't Team.Heroes
            //   _director isn't null and _director.IsBusy
            return false;    // placeholder until UI 9e is written
        }
    }
}
