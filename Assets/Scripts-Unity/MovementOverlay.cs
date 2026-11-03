using DnDCombat.Core;
using System.Collections.Generic;
using UnityEngine;


namespace DnDCombat.Unity
{
    public class MovementOverlay : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _outlineColor = Color.white;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private float _outlineWidth = 0.1f;
        [SerializeField] private float _gridWidth = 0.03f;
        [SerializeField] private float _heightAboveFloor = 0.03f;   // stops the lines flickering against the floor

        // What is drawn right now, so we can tell when it needs to change
        private bool _shown;
        private Combatant _shownActor;
        private GridPosition _shownCell;
        private int _shownSquares;
        private Mesh _mesh;

        private void Awake()
        {
            GameObject container = new GameObject("Overlay Lines");
            container.transform.SetParent(transform);
            GameObject go = new GameObject("Overlay Mesh");
            go.transform.SetParent(transform);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            _mesh = new Mesh();
            _mesh.name = "Movement Overlay";
            filter.sharedMesh = _mesh;
            meshRenderer.sharedMaterial = _lineMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            bool want = TryGetTarget(out Combatant actor, out GridPosition cell, out int squares);
            if(!want && !_shown) return;
            if(want && _shown && actor == _shownActor && cell == _shownCell && squares == _shownSquares) return;
            else
            {
                Clear();
                _shown = false;
            }
            if (want)
            {
                Build(actor, cell, squares);
                _shownActor = actor;
                _shownCell = cell;
                _shownSquares = squares;
                _shown = true;
            }            
        }
        private bool TryGetTarget(out Combatant actor, out GridPosition cell, out int squares)
        {
            actor = null;
            cell = default;
            squares = 0;

            if (_game == null || _game.Controller == null || _game.Space == null)
            {
                return false;
            }

            if(_game.Controller.Turns.Current == null)
            {
                return false;
            }

            Combatant current = _game.Controller.Turns.Current;

            if (current.Team != Team.Heroes)
            {
                return false;
            }
            if(!_game.Controller.Grid.TryGetPosition(current, out cell))
            {
                return false;
            }

            squares = current.MovementRemaining / Pathfinder.FeetPerSquare;

            if(squares <= 0)
            {                
                return false;
            }
            else
            {
                actor = current;
                return true;
            }
        }

        private void Clear()
        {
            _vertices.Clear();
            _colours.Clear();
            _triangles.Clear();
            _mesh.Clear();
        }

        private void Build(Combatant actor, GridPosition cell, int squares)
        {
            // ---------- 1. Every square the hero can reach, plus the one they stand on ---------- //
            MovementRange range = _game.Controller.GetMovementRange(actor);

            List<GridPosition> cells = new List<GridPosition> { cell };
            foreach (GridPosition destination in range.Destinations)
            {
                cells.Add(destination);
            }

            // ---------- 2. Which (X, Z) columns belong to the region ---------- //
            HashSet<Vector2Int> columns = new HashSet<Vector2Int>();
            foreach (GridPosition c in cells)
            {
                columns.Add(new Vector2Int(c.X, c.Z));
            }

            // ---------- 3. The four sides of every cell ---------- //
            Vector3 lift = Vector3.up * _heightAboveFloor;
            float s = _game.Space.CellSize;

            foreach (GridPosition c in cells)
            {
                Vector3 corner = _game.Space.ToWorldCorner(c) + lift;

                // -X side: bold if nothing reachable beyond it, otherwise left for the neighbour to draw
                if (!columns.Contains(new Vector2Int(c.X - 1, c.Z)))
                {
                    AddStrip(corner, corner + new Vector3(0f, 0f, s), _outlineWidth, _outlineColor);
                }

                // +X side: bold if nothing reachable beyond it, otherwise faint
                Vector3 plusX = corner + new Vector3(s, 0f, 0f);
                if (!columns.Contains(new Vector2Int(c.X + 1, c.Z)))
                {
                    AddStrip(plusX, plusX + new Vector3(0f, 0f, s), _outlineWidth, _outlineColor);
                }
                else
                {
                    AddStrip(plusX, plusX + new Vector3(0f, 0f, s), _gridWidth, _gridColor);
                }

                // -Z side
                if (!columns.Contains(new Vector2Int(c.X, c.Z - 1)))
                {
                    AddStrip(corner, corner + new Vector3(s, 0f, 0f), _outlineWidth, _outlineColor);
                }

                // +Z side
                Vector3 plusZ = corner + new Vector3(0f, 0f, s);
                if (!columns.Contains(new Vector2Int(c.X, c.Z + 1)))
                {
                    AddStrip(plusZ, plusZ + new Vector3(s, 0f, 0f), _outlineWidth, _outlineColor);
                }
                else
                {
                    AddStrip(plusZ, plusZ + new Vector3(s, 0f, 0f), _gridWidth, _gridColor);
                }
            }

            // ---------- 4. Hand everything to the mesh ---------- //
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateNormals();
        }

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        private void AddStrip(Vector3 from, Vector3 to, float width, Color colour)
        {
            Vector3 direction = (to - from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * (width / 2f);   // sideways, along the floor
            Vector3 extend = direction * (width / 2f);                            // pokes past each end so corners meet

            from -= extend;
            to += extend;

            int start = _vertices.Count;
            _vertices.Add(from - side);
            _vertices.Add(from + side);
            _vertices.Add(to + side);
            _vertices.Add(to - side);
            for (int i = 0; i < 4; i++)
            {
                _colours.Add(colour);
            }

            // Two triangles make the rectangle (wound so the top faces the camera above)
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 1);
            _triangles.Add(start); _triangles.Add(start + 3); _triangles.Add(start + 2);
        }
    }
}