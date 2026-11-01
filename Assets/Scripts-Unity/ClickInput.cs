using DnDCombat.Core;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

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

            Debug.Log("Clicked at " + MouseScreenPosition());
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
            _game.Controller.Grid.TryGetPosition(target, out GridPosition targetPos); // Get the target's position on the grid

            if (targetPos == null) // Check if the target position is null
            {
                Debug.Log("ERROR 1: Target position is null");
                return;
            }

            MovementRange range = _game.Controller.GetMovementRange(actor); // Get the movement range of the actor
            _game.Controller.Grid.TryGetPosition(actor, out GridPosition actorPos); // Get the actor's position on the grid

            GridPosition best = actorPos;
            foreach (GridPosition square in range.Destinations)
            {
                if (IsBetter(square, best, targetPos, attack.RangeSquares, range))
                {
                    best = square;
                }
            }
            if (Core.Grid.DistanceInSquares(best, targetPos) > attack.RangeSquares)
            {
                Debug.Log($"{target.Creature.Name} can't be reached this turn.");
                return;
            }

            // 2. Walk there, and put the body there too
            MoveResult move = _game.Controller.TryMove(actor, best);
            if (!move.Success)
            {
                Debug.Log("ERROR 2: " + move.Failure);
                return;
            }
            _game.ViewOf(actor).SnapToGrid();

            // 3. Attack from there
            AttackOutcome after = _game.Controller.TryAttack(actor, target, attack);
            if (!after.Success)
            {
                Debug.Log("ERROR 3: " + after.Failure);
            }
        }

        private static bool IsBetter(GridPosition a, GridPosition b, GridPosition targetPosition,
                                     int reach, MovementRange range)
        {
            bool aInReach = Core.Grid.DistanceInSquares(a, targetPosition) <= reach;
            bool bInReach = Core.Grid.DistanceInSquares(b, targetPosition) <= reach;

            if (aInReach && !bInReach)
            {
                return true; // a is in range and b is not
            }
            if (!aInReach && bInReach)
            {
                return false; // b is in range and a is not
            }
            if (!aInReach)
            {
                int aDistance = Core.Grid.DistanceInSquares(a, targetPosition);
                int bDistance = Core.Grid.DistanceInSquares(b, targetPosition);

                if (aDistance < bDistance) { return true; }
                if (aDistance > bDistance) { return false; }
            }

            int aSteps = range.StepsTo(a);
            int bSteps = range.StepsTo(b);

            if (aSteps < bSteps)
            {
                return true;     // a is cheaper to reach
            }
            if (aSteps > bSteps)
            {
                return false;    // a is harder to reach
            }

            if (a.X < b.X) { return true; }
            if (a.X > b.X) { return false; }
            if (a.Z < b.Z) { return true; }
            if (a.Z > b.Z) { return false; }

            return false;
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