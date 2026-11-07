using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DnDCombat.Unity
{
    // Camera controls: WASD pans, hold right-click and drag to swivel around the point under the
    // cursor, scroll to zoom (in = closer and nearly level, out = the camera as you placed it).
    // Put it on the Main Camera.
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] private GameManager _game;              // for the arena's size, to keep the camera over it

        [Header("Panning")]
        [SerializeField] private float _panSpeed = 1.0f;         // per second, times the distance: quick zoomed out, gentle zoomed in
        [SerializeField] private float _boundsMargin = 2f;       // how far past the arena edge the focus may go

        [Header("Orbiting")]
        [SerializeField] private float _orbitSpeed = 0.25f;      // degrees per pixel of mouse movement

        [Header("Zoom")]
        [SerializeField] private float _minDistance = 4f;        // fully zoomed in
        [SerializeField] private float _minPitch = 12f;          // fully zoomed in: nearly level with the characters
        [SerializeField] private float _zoomStep = 0.1f;         // how much one scroll notch zooms (0 to 1 is the whole range)
        [SerializeField] private float _zoomSharpness = 10f;     // how quickly the zoom eases to where you scrolled

        // Fully zoomed out: read from the camera as you placed it, in Start
        private float _maxDistance;
        private float _maxPitch;

        // The four numbers (pitch and distance come from _zoom)
        private Vector3 _focus;
        private float _yaw;
        private float _zoom = 1f;         // where it is now: 0 = fully in, 1 = fully out
        private float _targetZoom = 1f;   // where the scroll wheel wants it

        // Orbiting
        private bool _orbiting;
        private Vector3 _pivot;
        private Camera _camera;           // this object's Camera, for turning the cursor into a ray

        // The floor the camera focuses on, and the box it stays over
        private float _groundY;
        private Vector3 _boundsMin;
        private Vector3 _boundsMax;
        private bool _ready;              // false if the camera couldn't be read, so nothing moves

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            _groundY = _game != null && _game.Space != null ? _game.Space.Origin.y : 0f;
            if (!CameraRigMath.TryReadFrom(transform.position, transform.rotation, _groundY,
                    out _focus, out _yaw, out _maxPitch, out _maxDistance))
            {
                Debug.LogWarning("CameraRig: the camera must look down at the floor.");
                return;
            }

            if (_minDistance >= _maxDistance)
            {
                Debug.LogWarning("CameraRig: the camera is closer than the minimum zoom.");
                _minDistance = _maxDistance * 0.5f;
            }
            if (_game != null && _game.Space != null && _game.Controller != null)
            {
                Vector3 size = new Vector3(_game.Controller.Grid.Width, 0, _game.Controller.Grid.Depth) * _game.Space.CellSize;
                Vector3 margin = new Vector3(_boundsMargin, 0, _boundsMargin);
                _boundsMin = _game.Space.Origin - margin;
                _boundsMax = _game.Space.Origin + size + margin;
            }
            else
            {
                _boundsMin = new Vector3(-1000f, 0f, -1000f);
                _boundsMax = new Vector3(1000f, 0f, 1000f);
            }
            _ready = true;
        }

        private void LateUpdate()
        {
            // LateUpdate, so the camera moves after everything else has moved this frame
            if (!_ready) return;
            float dt = Time.deltaTime;

            Pan(dt);
            Orbit();
            Zoom(dt);
            Apply();
        }

        ///////////////////////////////////
        // ---------- Panning ---------- //
        ///////////////////////////////////
        // WASD slides the focus across the floor, relative to the way the camera faces.
        // Speed scales with distance, so it feels the same on screen at any zoom:
        // zoomed out (distance 15.7) W for 1 s slides 15.7 metres, zoomed in (distance 4) only 4.
        private void Pan(float dt)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (input == Vector2.zero) return;

            float distance = CameraRigMath.ZoomDistance(_zoom, _minDistance, _maxDistance);
            _focus += CameraRigMath.PanDirection(_yaw, input) * _panSpeed * distance * dt;
        }

        ////////////////////////////////////
        // ---------- Orbiting ---------- //
        ////////////////////////////////////
        // Hold right-click and drag sideways to swivel around the floor point you grabbed.
        // 200 pixels right at 0.25 degrees per pixel is a 50 degree swivel.
        private void Orbit()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Grab the pivot ONCE, on the press. Re-reading it every frame would make it slide
            // under the cursor as the world turns, and the swivel would wobble.
            if (mouse.rightButton.wasPressedThisFrame && !PointerOverUI())
            {
                Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
                _orbiting = CameraRigMath.TryGroundPoint(ray, _groundY, out _pivot);
            }

            if (_orbiting && mouse.rightButton.isPressed)
            {
                // Focus and yaw turn together, always: turning only one would slide the camera
                // instead of swivelling it
                float angle = mouse.delta.ReadValue().x * _orbitSpeed;
                _focus = CameraRigMath.OrbitFocus(_focus, _pivot, angle);
                _yaw += angle;
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                _orbiting = false;
            }
        }

        ////////////////////////////////
        // ---------- Zoom ---------- //
        ////////////////////////////////
        // Each scroll notch moves the zoom one step (0.1 means 10 notches from fully out to fully in),
        // then the actual zoom eases towards it
        private void Zoom(float dt)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && !PointerOverUI())     // over the UI, scrolling is for the combat log
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    // Sign, not the raw value: one notch is 120 on Windows and 1 elsewhere.
                    // Minus, because scrolling up (positive) zooms IN, towards 0.
                    _targetZoom = Mathf.Clamp01(_targetZoom - Mathf.Sign(scroll) * _zoomStep);
                }
            }

            _zoom = CameraRigMath.Smooth(_zoom, _targetZoom, _zoomSharpness, dt);
        }

        //////////////////////////////////////////////
        // ---------- Placing the camera ---------- //
        //////////////////////////////////////////////
        // Keep the focus over the arena, then rebuild the camera from the four numbers
        private void Apply()
        {
            _focus = CameraRigMath.ClampFocus(_focus, _boundsMin, _boundsMax);

            float distance = CameraRigMath.ZoomDistance(_zoom, _minDistance, _maxDistance);
            float pitch = CameraRigMath.ZoomPitch(_zoom, _minPitch, _maxPitch);
            transform.position = CameraRigMath.Position(_focus, _yaw, pitch, distance);
            transform.rotation = CameraRigMath.Rotation(_yaw, pitch);
        }

        // True while the mouse is over the HUD, so the camera leaves those clicks and scrolls alone
        private static bool PointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
