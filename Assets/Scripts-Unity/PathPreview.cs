using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    [RequireComponent(typeof(LineRenderer))]
    public class PathPreview : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private Camera _camera;               // empty uses the Main Camera
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _color = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private float _width = 0.12f;
        [SerializeField] private float _heightAboveFloor = 0.06f;   // above the overlay's lines

        private LineRenderer _line;

        // What the line currently shows, so we know when it needs to change
        private bool _hasKey;
        private Combatant _keyActor;
        private GridPosition _keyActorCell;
        private int _keyFeet;
        private GridPosition _keyHover;
        private Vector2 _lastMouse;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = _width;
            _line.startColor = _color;
            _line.endColor = _color;
            _line.sharedMaterial = _lineMaterial;
            _line.numCapVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
            if(_camera == null) _camera = Camera.main;
        }

        private void LateUpdate()
        {
            Vector2 mousePos = MouseScreenPosition();
            if (mousePos != _lastMouse)
            {
                _lastMouse = mousePos;
            }

            bool hovering = TryGetHover(out Combatant actor, out GridPosition actorCell, out GridPosition hover);
            if(!hovering)
            {
                Hide();
                return;
            }

            if (_hasKey && actor == _keyActor && actorCell == _keyActorCell && actor.MovementRemaining == _keyFeet && hover == _keyHover)
            {
                return;
            }
            
            _keyActor = actor;
            _keyActorCell = actorCell;
            _keyFeet = actor.MovementRemaining;
            _keyHover = hover;
            _hasKey = true;

            MovementRange range = _game.Controller.GetMovementRange(actor);
            List<GridPosition> path = range.PathTo(hover);
            if (path.Count == 0)
            {
                HideLine();
                return;
            }
            Draw(actorCell, path);
        }

        private bool TryGetHover(out Combatant actor, out GridPosition actorCell, out GridPosition hover)
        {
            actor = null;
            actorCell = default;
            hover = default;

            if (_game == null || _game.Controller == null || _game.Space == null || _camera == null)
                return false;

            Combatant current = _game.Controller.Turns.Current;
            if (current == null)
                return false;
            if (current.Team != Team.Heroes)
                return false;
            if (!_game.Controller.Grid.TryGetPosition(current, out actorCell))
                return false;

            if (!Physics.Raycast(_camera.ScreenPointToRay(MouseScreenPosition()), out RaycastHit hit))
                return false;

            // A creature means an attack, not a walk
            if (hit.collider.GetComponentInParent<CombatantView>() != null)
                return false;

            hover = _game.Space.ToGridOnSurface(hit.point, hit.normal);
            actor = current;
            return true;
        }

        private void Draw(GridPosition actorCell, List<GridPosition> path)
        {
            _line.positionCount = path.Count + 1;
            _line.SetPosition(0, FloorPoint(actorCell));

            for (int i = 0; i < path.Count; i++)
            {
                _line.SetPosition(i + 1, FloorPoint(path[i]));
            }

            _line.enabled = true;
        }

        private Vector3 FloorPoint(GridPosition cell)
        {
            return _game.Space.ToWorldFloorCentre(cell) + Vector3.up * _heightAboveFloor;
        }

        private void HideLine()
        {
            _line.enabled = false;
        }

        private void Hide()
        {
            HideLine();
            _hasKey = false;
        }

        private static Vector2 MouseScreenPosition()
        {
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }
    }
}