using UnityEngine;
using DnDCombat.Core;
using Grid = DnDCombat.Core.Grid;

namespace DnDCombat.Unity
{
    public class CombatantView : MonoBehaviour
    {
        public Combatant Combatant { get; private set; }

        private Grid _grid;
        private GridSpace _space;
        private Animator _animator;       

        public void Bind(Combatant combatant, Grid grid, GridSpace space)
        {
            Combatant = combatant;
            _grid = grid;
            _space = space;
            _animator = GetComponentInChildren<Animator>();
        }

        public bool SnapToGrid()
        {
            if(_grid == null || _space == null)
            {
                return false;
            }

            if(!_grid.TryGetPosition(Combatant, out GridPosition position))
            {
                return false;
            }
            else
            {
                transform.position = _space.ToWorldFloorCentre(position);   // feet on the floor, not the cell's middle
            }
            return true;
        }

        ////////////////////////////////////
        // ---------- Facing ---------- //
        ////////////////////////////////////

        // Turns to look at a point, around Y only, so it never tips forwards or back
        public void FaceTowards(Vector3 worldPoint)
        {
            FaceDirection(worldPoint - transform.position);
        }

        // Turns to face along a direction, around Y only. A direction with no sideways part
        // (straight up, or zero) has no facing, so the body is left as it was.
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        ////////////////////////////////////////
        // ---------- Animation ---------- //
        ////////////////////////////////////////
        public void SetMoveSpeed(float squaresPerSecond)
        {
            if (!HasParameter("Speed")) return;
            _animator.SetFloat("Speed", squaresPerSecond);
        }

        public void PlayAttack()
        {
            if(!HasParameter("Attack")) return;
            _animator.SetTrigger("Attack");
        }

        public void PlayHit()
        {
            if(!HasParameter("Hit")) return;
            _animator.SetTrigger("Hit");
        }

        public void SetDead(bool dead)
        {
            if(!HasParameter("Dead")) return;
            _animator.SetBool("Dead", dead);
        }

        private bool HasParameter(string parameterName)
        {
            if(_animator == null || _animator.runtimeAnimatorController == null)
            {
                return false;
            }
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
