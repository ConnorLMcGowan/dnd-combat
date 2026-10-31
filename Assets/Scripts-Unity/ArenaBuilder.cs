using System.Collections.Generic;
using UnityEngine;
using DnDCombat.Core;
using Grid = DnDCombat.Core.Grid;

namespace DnDCombat.Unity
{
    public class ArenaBuilder : MonoBehaviour
    {
        //////////////////////////////////////////
        // ---------- How this works ---------- //
        //////////////////////////////////////////
        // Build() makes the Core's Grid, then spawns visuals so the scene matches it.
        //   The Core is the truth: the visuals only copy it.
        // Each block is a small empty "anchor" at the cell's centre, with the visual under it.
        //   The visual is either a prefab (which keeps its own materials and textures)
        //   or a plain grey cube when no prefab is given.

        /////////////////////////////////////////////
        // ---------- Block entries ---------- //
        /////////////////////////////////////////////
        [System.Serializable]
        private struct PlacedBlock
        {
            public Vector3Int Cell;
            public GameObject Prefab;       // empty means "plain grey cube"
            public float Scale;             // 0 means "leave the size alone"
            public bool DecorationOnly;     // true: looks only, the Core cell stays open
        }

        ///////////////////////////////////////////////
        // ---------- Inspector settings ---------- //
        ///////////////////////////////////////////////
        [SerializeField] private int _width = 15;       // X
        [SerializeField] private int _height = 3;       // Y
        [SerializeField] private int _depth = 15;       // Z
        [SerializeField] private float _cellSize = 1f;

        // Blocks to place. Keep the fighter and skeleton start squares clear,
        //   and avoid a wall right across the arena (the AI can get stuck behind one).
        [SerializeField]
        private List<PlacedBlock> _blocks = new List<PlacedBlock>
        {
            new PlacedBlock { Cell = new Vector3Int(7, 0, 7) },
            new PlacedBlock { Cell = new Vector3Int(5, 0, 4) },
            new PlacedBlock { Cell = new Vector3Int(9, 0, 10) },
            new PlacedBlock { Cell = new Vector3Int(9, 0, 3) },
            new PlacedBlock { Cell = new Vector3Int(9, 1, 3) }     // sits on top of the block below it (a tall pillar)
        };

        // Optional: materials so the floor and plain cubes are easy to tell apart
        [SerializeField] private Material _floorMaterial;
        [SerializeField] private Material _blockMaterial;

        public Grid Grid { get; private set; }
        public GridSpace Space { get; private set; }

        public void Build()
        {
            Grid = new Grid(_width, _height, _depth);
            float originX = -Grid.Width * _cellSize / 2f;
            float originY = 0;
            float originZ = -Grid.Depth * _cellSize / 2f;
            Space = new GridSpace(new Vector3(originX, originY, originZ), _cellSize);
            SpawnFloor();
            MarkAndSpawnBlocks();
        }
        private void SpawnFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(transform);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(Grid.Width * Space.CellSize, 1, Grid.Depth * Space.CellSize);
            floor.transform.position = new Vector3(Space.Origin.x + Grid.Width * Space.CellSize / 2f,
                                                   Space.Origin.y - 0.5f,
                                                   Space.Origin.z + Grid.Depth * Space.CellSize / 2f);
            if (_floorMaterial != null)
            {
                floor.GetComponent<Renderer>().sharedMaterial = _floorMaterial;
            }
        }

        private void MarkAndSpawnBlocks()
        {
            foreach (PlacedBlock block in _blocks)
            {
                GridPosition cell = new GridPosition(block.Cell.x, block.Cell.y, block.Cell.z);

                if (!Grid.InBounds(cell))
                {
                    Debug.LogWarning($"Cell {cell} is outside the grid, so no block was spawned");
                    continue;
                }

                // Only solid blocks are told to the Core. Decorations are looks only.
                if (!block.DecorationOnly && !Grid.SetSolid(cell, true))
                {
                    Debug.LogWarning($"Could not make cell {cell} solid, so no block was spawned");
                    continue;
                }

                GameObject anchor = new GameObject($"Block {cell}");
                anchor.transform.SetParent(transform);
                anchor.transform.position = Space.ToWorld(cell);

                if (block.Prefab != null)
                {
                    SpawnPrefab(block, anchor.transform, cell);
                }
                else
                {
                    SpawnCube(anchor.transform);
                }
            }
        }

        ////////////////////////////////////////
        // ---------- Spawn a cube ---------- //
        ////////////////////////////////////////
        private void SpawnCube(Transform anchor)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube";
            cube.transform.SetParent(anchor);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * Space.CellSize;
            if (_blockMaterial != null)
            {
                cube.GetComponent<Renderer>().sharedMaterial = _blockMaterial;
            }
        }

        /////////////////////////////////////////////
        // ---------- Spawn the prefab ---------- //
        /////////////////////////////////////////////
        private void SpawnPrefab(PlacedBlock block, Transform anchor, GridPosition cell)
        {
            GameObject visual = Instantiate(block.Prefab, anchor);
            visual.name = block.Prefab.name;

            // Scale first, because the fitting measures the model at its final size
            float scale = block.Scale > 0f ? block.Scale : 1f;
            visual.transform.localScale *= scale;

            FitToCell(visual, cell);
        }

        ///////////////////////////////////////////
        // ---------- Fit to the cell ---------- //
        ///////////////////////////////////////////
        // Moves the model so its bottom-centre sits on the middle of the cell's floor,
        //   whatever pivot the model was made with.
        private void FitToCell(GameObject visual, GridPosition cell)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;     // nothing visible to measure
            }

            // Combine every part's box into one box around the whole model
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 bottomCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 floorCentre = Space.ToWorld(cell) - Vector3.up * (Space.CellSize / 2f);

            visual.transform.position += floorCentre - bottomCentre;
        }
    }
}