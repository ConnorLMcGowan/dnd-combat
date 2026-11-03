using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DnDCombat.Core;
using UnityEngine.EventSystems;

namespace DnDCombat.Unity
{
    // Shows the route the current hero would walk to the hovered square, with a soft glow on
    // the square they'd end up on.
    //
    // Hovering an enemy asks the Core the same question ClickInput asks,
    // range.BestApproach(enemyCell, reach), so the preview always matches what a click does:
    //   Already in reach          -> no line, your own square glows
    //   Reachable this turn       -> line to the square you'd attack from, glow on it
    //   Can't reach it this turn  -> nothing (a click would do nothing either)
    [RequireComponent(typeof(LineRenderer))]
    public class PathPreview : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private Camera _camera;               // empty uses the Main Camera
        [SerializeField] private BodyDirector _director;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _color = new Color(1f, 0.9f, 0.2f, 0.5f);
        [SerializeField] private float _width = 0.12f;
        [SerializeField] private float _heightAboveFloor = 0.06f;   // above the overlay's lines

        [Header("Soft line")]
        [SerializeField] private float _lineBrightness = 1.5f;  // above 1 lets the scene's Bloom add a halo
        [SerializeField] private float _lineCoreWidth = 0.2f;   // fraction of the line that's solid; the rest feathers away
        [SerializeField] private float _lineWidthScale = 3.5f;  // drawn wider to leave room for the feathered tail
        [SerializeField] private float _lineStartAlpha = 0.3f;  // faint at the hero, full strength at the destination

        [Header("Glow")]
        [SerializeField] private Color _glowColour = new Color(1f, 0.9f, 0.3f, 0.25f);
        [SerializeField] private float _glowBrightness = 2f;    // above 1 lets the scene's Bloom add a halo
        [SerializeField] private float _glowSize = 1.3f;        // fraction of a cell (over 1 lets the light spill softly past the edges)
        [SerializeField] private float _glowCore = 0f;          // fraction of the spot that's solid (0 = fades all the way from the centre)
        [SerializeField] private float _glowHeight = 0.045f;    // between the overlay lines (0.03) and the path line (0.06)
        [SerializeField] private float _glowPulseSpeed = 0.5f;  // breaths per second
        [SerializeField] private float _glowPulseDepth = 0.35f; // how far it dims at the bottom of each breath (0 = no pulse)

        private LineRenderer _line;
        private Material _lineSoftMaterial;
        private Texture2D _lineTexture;
        private GameObject _glow;
        private Material _glowMaterial;
        private Texture2D _glowTexture;

        // What the line currently shows, so we know when it needs to change
        private bool _hasKey;
        private Combatant _keyActor;
        private GridPosition _keyActorCell;
        private int _keyFeet;
        private GridPosition _keyHover;
        private Combatant _keyEnemy;      // null when hovering the ground

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = _width * _lineWidthScale;
            _line.startColor = new Color(_color.r, _color.g, _color.b, _color.a * _lineStartAlpha);
            _line.endColor = _color;

            // A see-through, feathered, extra-bright material (see SoftGlow). A LineRenderer
            // maps V across its width, so the strip texture fades both edges.
            _lineTexture = SoftGlow.MakeStripTexture(64, _lineCoreWidth);
            _lineSoftMaterial = SoftGlow.MakeMaterial(_lineMaterial, _lineTexture, _lineBrightness, SoftGlow.PathQueue);
            _line.sharedMaterial = _lineSoftMaterial;
            _line.textureMode = LineTextureMode.Stretch;
            _line.numCapVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
            if(_camera == null) _camera = Camera.main;

            CreateGlow();
        }

        private void OnDestroy()
        {
            // Made in code, so Unity won't clean them up for us
            if (_lineSoftMaterial != null) Destroy(_lineSoftMaterial);
            if (_lineTexture != null) Destroy(_lineTexture);
            if (_glowMaterial != null) Destroy(_glowMaterial);
            if (_glowTexture != null) Destroy(_glowTexture);
        }

        private void LateUpdate()
        {
            PulseGlow();

            bool hovering = TryGetHover(out Combatant actor, out GridPosition actorCell, out GridPosition hover, out Combatant enemy);
            if(!hovering)
            {
                Hide();
                return;
            }

            if (_hasKey && actor == _keyActor && actorCell == _keyActorCell && actor.MovementRemaining == _keyFeet
                && hover == _keyHover && enemy == _keyEnemy)
            {
                return;
            }

            _keyActor = actor;
            _keyActorCell = actorCell;
            _keyFeet = actor.MovementRemaining;
            _keyHover = hover;
            _keyEnemy = enemy;
            _hasKey = true;

            MovementRange range = _game.Controller.GetMovementRange(actor);

            // Where would we end up? The hovered square, or the square we'd attack an enemy from
            GridPosition destination = hover;
            if (enemy != null)
            {
                if (!TryGetApproach(actor, enemy, range, out destination))
                {
                    HideLine();
                    HideGlow();
                    return;
                }

                // Already in reach: nowhere to walk, so just glow the square we're standing on
                if (destination == actorCell)
                {
                    HideLine();
                    ShowGlow(destination);
                    return;
                }
            }

            List<GridPosition> path = range.PathTo(destination);
            if (path.Count == 0)
            {
                HideLine();
                HideGlow();
                return;
            }
            Draw(actorCell, path);
            ShowGlow(destination);
        }

        // What's under the mouse on a hero's turn. Ground gives enemy = null and the hovered
        // square. An enemy gives enemy = that creature and hover = its square.
        private bool TryGetHover(out Combatant actor, out GridPosition actorCell, out GridPosition hover, out Combatant enemy)
        {
            actor = null;
            actorCell = default;
            hover = default;
            enemy = null;

            if(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) // ignore the mouse while it's over the UI, so clicking End Turn (or the log) doesn't also click the floor behind it
                return false;
            if (_director != null && _director.IsBusy)
                return false;
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

            CombatantView view = hit.collider.GetComponentInParent<CombatantView>();
            if (view != null)
            {
                Combatant other = view.Combatant;

                // Yourself, an ally or a body: nothing to preview
                if (other == null || other == current)
                    return false;
                if (other.Team == current.Team)
                    return false;
                if (other.Creature.IsDefeated)
                    return false;
                if (!_game.Controller.Grid.TryGetPosition(other, out hover))
                    return false;

                enemy = other;
                actor = current;
                return true;
            }

            hover = _game.Space.ToGridOnSurface(hit.point, hit.normal);
            actor = current;
            return true;
        }

        // Where the actor would stand to attack this enemy, worked out the same way ClickInput
        // does it. False if they have no attack or can't get in reach this turn.
        private bool TryGetApproach(Combatant actor, Combatant enemy, MovementRange range, out GridPosition destination)
        {
            destination = default;

            if (!_game.TryGetAttack(actor, out AttackProfile attack))
                return false;
            if (!_game.Controller.Grid.TryGetPosition(enemy, out GridPosition enemyCell))
                return false;

            destination = range.BestApproach(enemyCell, attack.RangeSquares);
            return Core.Grid.DistanceInSquares(destination, enemyCell) <= attack.RangeSquares;
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
            HideGlow();
            _hasKey = false;
        }

        //////////////////////////////
        // ---------- Glow ---------- //
        //////////////////////////////

        // A flat square lying on the floor, built once. It starts hidden.
        private void CreateGlow()
        {
            _glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _glow.name = "Path Glow";
            _glow.transform.SetParent(transform, false);

            // Quads come with a collider. Left in, the mouse ray would hit the glow instead of
            // the floor or the enemy under it.
            Destroy(_glow.GetComponent<Collider>());

            // A quad faces -Z. Tipping it 90 degrees forward lays it flat, facing up.
            _glow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // A round, soft spot of light on its own copy of the material (see SoftGlow)
            _glowTexture = SoftGlow.MakeSpotTexture(64, _glowCore);
            _glowMaterial = SoftGlow.MakeMaterial(_lineMaterial, _glowTexture, 1f, SoftGlow.GlowQueue);
            _glowMaterial.color = SoftGlow.Bright(_glowColour, _glowBrightness);

            MeshRenderer glowRenderer = _glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = _glowMaterial;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            _glow.SetActive(false);
        }

        private void ShowGlow(GridPosition cell)
        {
            if (_glow == null) return;

            _glow.transform.position = _game.Space.ToWorldFloorCentre(cell) + Vector3.up * _glowHeight;
            _glow.transform.localScale = Vector3.one * (_game.Space.CellSize * _glowSize);
            _glow.SetActive(true);
        }

        private void HideGlow()
        {
            if (_glow == null) return;

            _glow.SetActive(false);
        }

        // Slowly brightens and dims the glow while it's showing, like it's breathing.
        // The shader multiplies colour by alpha, so the centre shines at Glow Brightness x the
        // Glow Colour's alpha. Over 1 gets a Bloom halo: with alpha 0.8 it swings between
        // 2 x 0.8 = 1.6 and 1.3 x 0.8 = 1.04, so the halo swells and fades with each breath.
        private void PulseGlow()
        {
            if (_glow == null || !_glow.activeSelf) return;

            float brightness = _glowBrightness * SoftGlow.Pulse(_glowPulseSpeed, _glowPulseDepth);
            _glowMaterial.color = SoftGlow.Bright(_glowColour, brightness);
        }

        private static Vector2 MouseScreenPosition()
        {
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }
    }
}
