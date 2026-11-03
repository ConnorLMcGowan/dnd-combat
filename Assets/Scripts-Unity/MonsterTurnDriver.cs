using System.Collections;
using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    public class MonsterTurnDriver : MonoBehaviour
    {
        ///////////////////////////////////////////////
        // ---------- Inspector settings ---------- //
        ///////////////////////////////////////////////
        [SerializeField] private GameManager _game;
        [SerializeField] private float _thinkDelay = 0.5f;     // pause before acting, so you can see whose turn it is
        [SerializeField] private float _afterDelay = 0.6f;     // pause after acting, so you can see what happened
        [SerializeField] private BodyDirector _director;

        private bool _busy;     // true while an AI turn is playing

        ///////////////////////////////////////
        // ---------- Every frame ---------- //
        ///////////////////////////////////////
        private void Update()
        {
 
            if (_busy)
                return;
            if(_game == null || _game.Controller == null)
                return;
            Combatant current = _game.Controller.Turns.Current;
            if(current == null)
                return;
            if (_game.Controller.Turns.State != CombatState.Ongoing)
                return;
            if(!IsAIControlled(current))
                return;
            StartCoroutine(PlayTurn(current));
        }

        ////////////////////////////////////////////
        // ---------- Playing one turn ---------- //
        ////////////////////////////////////////////
        private IEnumerator PlayTurn(Combatant monster)
        {
            _busy = true;

            try
            {
                yield return new WaitForSeconds(_thinkDelay);
                if(_game.Controller.Turns.Current != monster || _game.Controller.Turns.State != CombatState.Ongoing)
                    yield break;
                if(_game.TryGetAttack(monster, out AttackProfile attack))
                {
                    MonsterAI.TakeTurn(_game.Controller, monster, attack);   // the BodyDirector moves and turns the body as it goes
                }

                if(_director != null)
                {
                    yield return new WaitUntil(() => !_director.IsBusy);
                }

                yield return new WaitForSeconds(_afterDelay);
                if(_game.Controller.Turns.Current == monster && _game.Controller.Turns.State == CombatState.Ongoing)
                {
                    _game.Controller.EndTurn();
                }
            }
            finally
            {
                _busy = false;
            }
        }
        private static bool IsAIControlled(Combatant combatant)
        {
            return combatant.Team != Team.Heroes;
        }
    }
}
