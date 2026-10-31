using UnityEngine;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    public class GridSpace
    {
        public Vector3 Origin { get; }
        public float CellSize { get; }

        public GridSpace(Vector3 origin, float cellSize = 1f)
        {
            if (!(cellSize > 0f)) cellSize = 1f; // If the cell size is smaller than 0, make it 1. This avoids dividing by 0
            Origin = origin;
            CellSize = cellSize;
        }
        public Vector3 ToWorld(GridPosition position)
        {
            float x = Origin.x + (position.X + 0.5f) * CellSize;
            float y = Origin.y + (position.Y + 0.5f) * CellSize;
            float z = Origin.z + (position.Z + 0.5f) * CellSize;

            return new Vector3(x, y, z);
        }

        public GridPosition ToGrid(Vector3 world)
        {
            Vector3 local = world - Origin;

            int x = Mathf.FloorToInt(local.x / CellSize);
            int y = Mathf.FloorToInt(local.y / CellSize);
            int z = Mathf.FloorToInt(local.z / CellSize);
            return new GridPosition(x, y, z);
        }
    }
}