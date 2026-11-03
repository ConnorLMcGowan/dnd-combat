using DnDCombat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DnDCombat.Unity
{
    public class ClickInput : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private Camera _camera;       // empty uses the Main Camera

        private void Start()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            if (_game == null || _game.Controller == null)
            {
                Debug.Log("Game manager or Game Controller is null");
                return;
            }
            _game.Controller.AttackResolved += LogAttack;
        }

        private void OnDestroy()
        {
            if (_game == null || _game.Controller == null)
            {
                Debug.Log("Game manager or Game Controller is null");
                return;
            }
            _game.Controller.AttackResolved -= LogAttack;
        }

        private void Update()
        {
            if(_game == null || _game.Controller == null)
                return;
            Combatant actor = _game.Controller.Turns.Current;
            if (actor == null)
                return;
            if (actor.Team != Team.Heroes)
                return;
            if (EndTurnPressed())
            {
                _game.Controller.EndTurn();
                return;
            }
            if (!ClickedThisFrame())
                return;
            Ray ray = _camera.ScreenPointToRay(MouseScreenPosition());
            if (!Physics.Raycast(ray, out RaycastHit hit))
                return;

            CombatantView view = hit.collider.GetComponentInParent<CombatantView>();
            Debug.Log($"Hit '{hit.collider.name}' at {hit.point}, found a view: {view != null}");
            if (view != null)
            {
                ClickCreature(actor, view);

            }
            else
            {
                ClickGround(actor, hit);
            }
        }

        private void ClickCreature(Combatant actor, CombatantView view)
        {
            if (actor == view.Combatant)
                return;
            _game.TryGetAttack(actor, out AttackProfile attack);

            AttackOutcome outcome = _game.Controller.TryAttack(actor, view.Combatant, attack);

            if (outcome.Failure == AttackFailure.OutOfReach)
            {
                Debug.Log("Target is out of reach, attempting to approach and attack.");
                ApproachAndAttack(actor, view.Combatant, attack);
                return;
            }

            if (!outcome.Success)
            {
                Debug.Log(outcome.Failure);
            }
            if(outcome.Failure != AttackFailure.None)
            {
                Debug.Log(outcome.Failure);
            }
        }
        private void ApproachAndAttack(Combatant actor, Combatant target, AttackProfile attack)
        {
            if(!_game.Controller.Grid.TryGetPosition(target, out GridPosition targetPos))
            {
                return;
            }

            MovementRange range = _game.Controller.GetMovementRange(actor); // Get the movement range of the actor

            GridPosition best = range.BestApproach(targetPos, attack.RangeSquares);

            if (Core.Grid.DistanceInSquares(best, targetPos) > attack.RangeSquares)
            {
                Debug.Log($"{target.Creature.Name} can't be reached this turn.");
                return;
            }

            // Walk there, and put the body there too
            MoveResult move = _game.Controller.TryMove(actor, best);
            if (!move.Success)
            {
                Debug.Log("ERROR 2: " + move.Failure);
                return;
            }
            _game.ViewOf(actor).SnapToGrid();

            // Attack from there
            AttackOutcome after = _game.Controller.TryAttack(actor, target, attack);
            if (!after.Success)
            {
                Debug.Log("ERROR 3: " + after.Failure);
            }
        }       
        private void ClickGround(Combatant actor, RaycastHit hit)
        {
            GridPosition cell = _game.Space.ToGrid(hit.point);

            MoveResult tryMove = _game.Controller.TryMove(actor, cell);
            if (tryMove.Failure != MoveFailure.None)
            {
                Debug.Log("ERROR: " + tryMove.Failure);
                return;
            }
            else if (tryMove.Success == true)
            {
                _game.ViewOf(actor).SnapToGrid();
            }
        }

        private void LogAttack(AttackReport report)
        {
            Debug.Log($"{report.Attacker.Creature.Name} rolled {report.Result.AttackTotal}, which hit {report.Target.Creature.Name} for {report.Result.Damage} (critical hit: {report.Result.IsCrit}, rolled {report.Result.RolledDamage}). {report.Target.Creature.Name} has {report.TargetHPAfter} HP Left.");
            if (report.TargetDefeated == true)
            {
                Debug.Log($"{report.Target.Creature.Name} was defeated.");
            }
        }

        private static bool ClickedThisFrame()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
        private static Vector2 MouseScreenPosition() 
        { 
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }
        private static bool EndTurnPressed() // temporary until the End Turn button exists
        { 
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }     
    }
}