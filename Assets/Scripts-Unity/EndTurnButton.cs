using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    public class EndTurnButton : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private Button _button;

        private void Start()
        {            
            if(_button != null)
            {
                _button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDestroy()
        {            
            if(_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        private void Update()
        {
            if(_button != null)
            {
                _button.interactable = CanEndTurn();
            }
        }

        private void OnClicked()
        {           
            if(CanEndTurn())
            {
                _game.Controller.EndTurn();
            }
        }

        // True only when the player is allowed to end the turn right now
        private bool CanEndTurn()
        {
            if(_game == null || _game.Controller == null)
            {
                return false;
            }
            if(_game.Controller.Turns.State != CombatState.Ongoing)
            {
                return false;
            }
            if(_game.Controller.Turns.Current == null || _game.Controller.Turns.Current.Team != Team.Heroes)
            {
                return false;
            }
            if(_director != null && _director.IsBusy)
            {
                return false;
            }
            return true;
        }
    }
}
