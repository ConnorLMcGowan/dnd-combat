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

        public void Bind(Combatant combatant, Grid grid, GridSpace space)
        {
            Combatant = combatant;
            _grid = grid;
            _space = space;
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
                transform.position = _space.ToWorld(position);
            }
            return true;
        }
    }
}