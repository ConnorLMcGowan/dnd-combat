using DnDCombat.Core;
using System.Collections.Generic;
using UnityEngine;


namespace DnDCombat.Unity
{
    public class MovementOverlay : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private BodyDirector _director;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _outlineColor = Color.white;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private float _outlineWidth = 0.1f;
        [SerializeField] private float _gridWidth = 0.03f;
        [SerializeField] private float _heightAboveFloor = 0.03f;   // stops the lines flickering against the floor

        [Header("Glow")]
        [SerializeField] private float _brightness = 1.5f;     // above 1 lets the scene's Bloom add a halo
        [SerializeField] private float _coreWidth = 0.2f;      // fraction of each line that's solid; the rest feathers away
        [SerializeField] private float _widthScale = 3f;       // lines are drawn wider to leave room for the feathered tail
        [SerializeField] private float _gridWidthScale = 5f;   // the same for the inner grid lines (wider, so they're easier to see)

        [Header("Inner feather")]
        [SerializeField] private float _innerFeatherLength = 0.6f;  // how far the soft light reaches in from the edge, in cells
        [SerializeField] private float _innerFeatherAlpha = 0.3f;   // how strong it is right at the edge (0 = off)

        // What is drawn right now, so we can tell when it needs to change
        private bool _shown;
        private Combatant _shownActor;
        private GridPosition _shownCell;
        private int _shownSquares;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _texture;

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

            // A see-through, feathered, extra-bright material (see SoftGlow)
            _texture = SoftGlow.MakeStripTexture(64, _coreWidth);
            _material = SoftGlow.MakeMaterial(_lineMaterial, _texture, _brightness, SoftGlow.OverlayQueue);
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void OnDestroy()
        {
            // Made in code, so Unity won't clean them up for us
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
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

            if(_director != null && _director.IsBusy)
                return false;
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
            _uvs.Clear();
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

            Vector3 lift = Vector3.up * _heightAboveFloor;
            float s = _game.Space.CellSize;

            // ---------- 3. The outside edge as closed loops (see OutlineTracer in the Core) ---------- //
            List<List<LoopCorner>> loops = new List<List<LoopCorner>>();
            foreach (List<GridPosition> corners in OutlineTracer.Trace(cells))
            {
                loops.Add(MakeLoop(corners, lift));
            }

            // ---------- 4. A soft light fading inward from the outside edge ---------- //
            // Added first, so everything else draws on top of it (a mesh draws in order).
            // Four bands, each fading a bit more, following (1 - f) squared: strong at the edge,
            // then a long faint tail. With alpha 0.3 (f = how far in, 0 = edge, 1 = full length):
            //   f = 0 -> 0.3      f = 0.25 -> 0.17      f = 0.5 -> 0.075      f = 0.75 -> 0.02      f = 1 -> 0
            if (_innerFeatherAlpha > 0f)
            {
                const int Bands = 4;
                float length = s * _innerFeatherLength;
                foreach (List<LoopCorner> loop in loops)
                {
                    for (int i = 0; i < Bands; i++)
                    {
                        float f0 = (float)i / Bands;
                        float f1 = (float)(i + 1) / Bands;

                        // Negative distances are inward. Every UV points at the solid middle of the
                        // texture; the fade comes from the vertex colours instead.
                        AddRing(loop, -length * f0, -length * f1, FeatherColour(f0), FeatherColour(f1), 0.5f, 0.5f);
                    }
                }
            }

            // ---------- 5. Faint grid lines between reachable cells ---------- //
            // Drawn before the outline, so the outline always sits on top. They also stop exactly
            // at their ends (no poking past), so they finish under the outline instead of
            // sticking out through it.
            float gridWidth = _gridWidth * _gridWidthScale;
            foreach (GridPosition c in cells)
            {
                Vector3 corner = _game.Space.ToWorldCorner(c) + lift;
                Vector3 plusX = corner + new Vector3(s, 0f, 0f);
                Vector3 plusZ = corner + new Vector3(0f, 0f, s);

                // Only the + sides, so each shared side is drawn once (the neighbour has the - side)
                if (columns.Contains(new Vector2Int(c.X + 1, c.Z)))
                {
                    AddStrip(plusX, plusX + new Vector3(0f, 0f, s), gridWidth, _gridColor);
                }
                if (columns.Contains(new Vector2Int(c.X, c.Z + 1)))
                {
                    AddStrip(plusZ, plusZ + new Vector3(s, 0f, 0f), gridWidth, _gridColor);
                }
            }

            // ---------- 6. The bold outline, one continuous band per loop ---------- //
            // Half the width outward, half inward. V runs 0 (outer edge) to 1 (inner edge), so
            // the soft texture feathers both sides. No piece overlaps another, so no bright bulbs.
            float halfWidth = _outlineWidth * _widthScale / 2f;
            foreach (List<LoopCorner> loop in loops)
            {
                AddRing(loop, halfWidth, -halfWidth, _outlineColor, _outlineColor, 0f, 1f);
            }

            // ---------- 7. Hand everything to the mesh ---------- //
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateNormals();
        }

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colours = new List<Color>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        // A straight line from one point to another. It stops exactly at its ends, so grid lines
        // finish under the outline instead of poking through it.
        private void AddStrip(Vector3 from, Vector3 to, float width, Color colour)
        {
            Vector3 direction = (to - from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * (width / 2f);   // sideways, along the floor

            int start = _vertices.Count;
            _vertices.Add(from - side);
            _vertices.Add(from + side);
            _vertices.Add(to + side);
            _vertices.Add(to - side);
            for (int i = 0; i < 4; i++)
            {
                _colours.Add(colour);
            }

            // V runs across the strip (0 on one edge, 1 on the other), so the soft texture fades
            // both edges. U runs along it, where the texture is the same all the way.
            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(0f, 1f));
            _uvs.Add(new Vector2(1f, 1f));
            _uvs.Add(new Vector2(1f, 0f));

            // Two triangles make the rectangle (wound so the top faces the camera above)
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 1);
            _triangles.Add(start); _triangles.Add(start + 3); _triangles.Add(start + 2);
        }

        //////////////////////////////////////////
        // ---------- Outline loops ---------- //
        //////////////////////////////////////////

        // One corner of an outline loop: where it is in the world, and which way is "out"
        private struct LoopCorner
        {
            public Vector3 Position;
            public Vector3 Out;     // away from the region. Length 1 on a straight, about 1.41 on a corner
        }

        // Works out the world position and "out" direction of every corner in a traced loop.
        //
        // The loop keeps the region on its left, so the right-hand side of each edge points out.
        // At a corner, "out" is the average of the two edges' outward directions, stretched so a
        // band offset along it stays the same width on both edges (a mitre, like a picture frame).
        // Worked example, a corner where an edge heading +X turns to head +Z:
        //   out of the +X edge is (0, 0, -1), out of the +Z edge is (1, 0, 0)
        //   average direction (0.71, 0, -0.71), and its dot with either is 0.71
        //   so Out = (0.71, 0, -0.71) / 0.71 = (1, 0, -1): one step out from BOTH edges at once
        private List<LoopCorner> MakeLoop(List<GridPosition> corners, Vector3 lift)
        {
            List<LoopCorner> loop = new List<LoopCorner>();
            int count = corners.Count;

            for (int i = 0; i < count; i++)
            {
                GridPosition previous = corners[(i - 1 + count) % count];
                GridPosition here = corners[i];
                GridPosition next = corners[(i + 1) % count];

                Vector3 outBefore = OutwardOf(previous, here);
                Vector3 outAfter = OutwardOf(here, next);
                Vector3 mitre = (outBefore + outAfter).normalized;

                LoopCorner corner = new LoopCorner();
                corner.Position = _game.Space.ToWorldCorner(here) + lift;
                corner.Out = mitre / Vector3.Dot(mitre, outBefore);
                loop.Add(corner);
            }

            return loop;
        }

        // The right-hand side of an edge, which is outside the region
        private static Vector3 OutwardOf(GridPosition from, GridPosition to)
        {
            Vector3 direction = new Vector3(to.X - from.X, 0f, to.Z - from.Z).normalized;
            return Vector3.Cross(Vector3.up, direction);
        }

        // A band that follows a whole loop, between two distances from its edge (positive is
        // outward, negative inward). Each side of the loop becomes one four-cornered piece, and
        // neighbouring pieces share their mitred corner exactly, so nothing overlaps.
        private void AddRing(List<LoopCorner> loop, float outerDistance, float innerDistance,
                             Color outerColour, Color innerColour, float outerV, float innerV)
        {
            int count = loop.Count;
            for (int i = 0; i < count; i++)
            {
                LoopCorner a = loop[i];
                LoopCorner b = loop[(i + 1) % count];

                int start = _vertices.Count;
                _vertices.Add(a.Position + a.Out * outerDistance);
                _vertices.Add(a.Position + a.Out * innerDistance);
                _vertices.Add(b.Position + b.Out * innerDistance);
                _vertices.Add(b.Position + b.Out * outerDistance);

                _colours.Add(outerColour);
                _colours.Add(innerColour);
                _colours.Add(innerColour);
                _colours.Add(outerColour);

                _uvs.Add(new Vector2(0f, outerV));
                _uvs.Add(new Vector2(0f, innerV));
                _uvs.Add(new Vector2(1f, innerV));
                _uvs.Add(new Vector2(1f, outerV));

                // The shader draws both sides, so the winding doesn't matter here
                _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
            }
        }

        // The outline colour, faded to the feather's strength at distance f (0 = edge, 1 = gone)
        private Color FeatherColour(float f)
        {
            float fade = (1f - f) * (1f - f);
            return new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, _outlineColor.a * _innerFeatherAlpha * fade);
        }
    }
}
