#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;
using DnDCombat.Unity;

// The camera controls in a running scene with a pretend keyboard and mouse. The maths itself is
// tested in edit mode (CameraRigMathTest). These check the controls drive it the right way round.
//
// The camera starts at (0, 12, -10) tipped down 50 degrees, over a 15 x 15 arena centred on (0, 0, 0).
public class CameraRigPlayTest : InputTestFixture
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.05f);
    private static readonly Vector3 StartPosition = new Vector3(0f, 12f, -10f);
    private static readonly Quaternion StartRotation = Quaternion.Euler(50f, 0f, 0f);

    private TestWorld _world;
    private CameraRig _rig;
    private Camera _camera;
    private Keyboard _keyboard;
    private Mouse _mouse;

    public override void Setup()
    {
        base.Setup();
        _keyboard = InputSystem.AddDevice<Keyboard>();
        _mouse = InputSystem.AddDevice<Mouse>();
        _keyboard.MakeCurrent();
        _mouse.MakeCurrent();
    }

    public override void TearDown()
    {
        _world?.Destroy();
        _world = null;
        base.TearDown();
    }

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private IEnumerator StartWorld(float minDistance = 4f, float minPitch = 12f)
    {
        _world = new TestWorld().WithArena().WithFight(new Vector3Int(7, 0, 7), new Vector3Int(14, 0, 14));
        _rig = _world.Add<CameraRig>("Test Camera Rig");        // brings a Camera with it
        _camera = _rig.GetComponent<Camera>();
        _rig.transform.position = StartPosition;
        _rig.transform.rotation = StartRotation;
        TestWorld.SetObject(_rig, "_game", _world.Game);
        TestWorld.SetFloat(_rig, "_minDistance", minDistance);
        TestWorld.SetFloat(_rig, "_minPitch", minPitch);
        TestWorld.SetFloat(_rig, "_zoomSharpness", 1000f);      // no easing, so each frame shows the result straight away
        _world.Activate();
        yield return null;
        yield return null;
    }

    // Where the camera is looking on the floor right now
    private Vector3 Focus()
    {
        Assert.That(CameraRigMath.TryReadFrom(_rig.transform.position, _rig.transform.rotation, 0f,
                                              out Vector3 focus, out _, out _, out _), Is.True);
        return focus;
    }

    private float Pitch() => _rig.transform.eulerAngles.x;
    private float DistanceToFocus() => Vector3.Distance(_rig.transform.position, Focus());

    private IEnumerator Hold(Key key, float seconds)
    {
        Press(_keyboard[key]);
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
        Release(_keyboard[key]);
        yield return null;
    }

    private IEnumerator ScrollNotches(int notches, float perNotch = 120f)
    {
        for (int i = 0; i < Mathf.Abs(notches); i++)
        {
            Set(_mouse.scroll, new Vector2(0f, perNotch * Mathf.Sign(notches)), queueEventOnly: true);
            yield return null;
            yield return null;
        }
        yield return null;
    }

    ///////////////////////////////////
    // ---------- Start ---------- //
    ///////////////////////////////////

    [UnityTest]
    public IEnumerator AtTheStart_TheCameraStaysExactlyWhereYouPlacedIt()
    {
        // Reading the camera into its four numbers and rebuilding it must change nothing
        yield return StartWorld();

        Assert.That(_rig.transform.position, Is.EqualTo(StartPosition).Using(Near));
        Assert.That(Quaternion.Angle(_rig.transform.rotation, StartRotation), Is.LessThan(0.1f));
    }

    ///////////////////////////////////
    // ---------- Panning ---------- //
    ///////////////////////////////////

    [UnityTest]
    public IEnumerator W_MovesTheViewForward_AtTheSameHeight()
    {
        yield return StartWorld();
        Vector3 before = _rig.transform.position;

        yield return Hold(Key.W, 0.2f);

        Vector3 moved = _rig.transform.position - before;
        Assert.That(moved.z, Is.GreaterThan(0.5f), "Forward is into the screen (+Z here)");
        Assert.That(moved.x, Is.EqualTo(0f).Within(0.01f));
        Assert.That(moved.y, Is.EqualTo(0f).Within(0.01f), "Panning never changes the height");
    }

    [UnityTest]
    public IEnumerator S_A_And_D_MoveTheOtherWays()
    {
        yield return StartWorld();

        Vector3 before = _rig.transform.position;
        yield return Hold(Key.S, 0.2f);
        Assert.That(_rig.transform.position.z, Is.LessThan(before.z), "S is backwards");

        before = _rig.transform.position;
        yield return Hold(Key.D, 0.2f);
        Assert.That(_rig.transform.position.x, Is.GreaterThan(before.x), "D is to the right");

        before = _rig.transform.position;
        yield return Hold(Key.A, 0.2f);
        Assert.That(_rig.transform.position.x, Is.LessThan(before.x), "A is to the left");
    }

    [UnityTest]
    public IEnumerator Panning_StopsAtTheEdgeOfTheArena()
    {
        // The arena reaches z = 7.5, plus a 2 metre margin
        yield return StartWorld();

        yield return Hold(Key.W, 3f);

        Assert.That(Focus().z, Is.LessThanOrEqualTo(9.5f + 0.05f));
        Assert.That(Focus().z, Is.GreaterThan(9f), "It should have reached the edge in 3 seconds");
    }

    ////////////////////////////////
    // ---------- Zoom ---------- //
    ////////////////////////////////

    [UnityTest]
    public IEnumerator ScrollingUp_ZoomsIn_CloserAndLower()
    {
        yield return StartWorld();
        float distanceBefore = DistanceToFocus();
        float pitchBefore = Pitch();

        yield return ScrollNotches(3);

        Assert.That(DistanceToFocus(), Is.LessThan(distanceBefore - 0.5f));
        Assert.That(Pitch(), Is.LessThan(pitchBefore - 1f), "Zooming in also tilts towards level");
    }

    [UnityTest]
    public IEnumerator FullyZoomedIn_StopsAtTheMinimums()
    {
        yield return StartWorld(minDistance: 4f, minPitch: 12f);

        yield return ScrollNotches(15);        // more than enough (10 notches is the whole range)

        Assert.That(DistanceToFocus(), Is.EqualTo(4f).Within(0.1f));
        Assert.That(Pitch(), Is.EqualTo(12f).Within(0.5f));
    }

    [UnityTest]
    public IEnumerator FullyZoomedOut_IsTheCameraYouPlaced()
    {
        yield return StartWorld();
        yield return ScrollNotches(5);

        yield return ScrollNotches(-15);       // back out, and then some

        Assert.That(_rig.transform.position, Is.EqualTo(StartPosition).Using(Near));
    }

    [UnityTest]
    public IEnumerator EveryNotch_IsTheSameSize_WhateverTheMouseReports()
    {
        // Windows reports 120 per notch, other setups 1. Both must zoom the same amount.
        yield return StartWorld();
        yield return ScrollNotches(2, perNotch: 120f);
        float bigNotches = DistanceToFocus();

        yield return ScrollNotches(-15);
        yield return ScrollNotches(2, perNotch: 1f);
        float smallNotches = DistanceToFocus();

        Assert.That(smallNotches, Is.EqualTo(bigNotches).Within(0.1f));
    }

    ////////////////////////////////////
    // ---------- Orbiting ---------- //
    ////////////////////////////////////

    [UnityTest]
    public IEnumerator RightDrag_SwivelsAroundTheGrabbedPoint()
    {
        yield return StartWorld();
        // Grab a floor point off to one side of the screen, not the middle
        Vector2 grabScreen = new Vector2(_camera.pixelWidth * 0.7f, _camera.pixelHeight * 0.4f);
        Assume.That(CameraRigMath.TryGroundPoint(_camera.ScreenPointToRay(grabScreen), 0f, out Vector3 grabbed), Is.True);
        float yawBefore = _rig.transform.eulerAngles.y;

        Set(_mouse.position, grabScreen);
        Press(_mouse.rightButton, queueEventOnly: true);
        yield return null;
        for (int i = 0; i < 5; i++)
        {
            Set(_mouse.delta, new Vector2(40f, 0f), queueEventOnly: true);
            yield return null;
        }
        Release(_mouse.rightButton, queueEventOnly: true);
        yield return null;

        // 200 pixels at 0.25 degrees per pixel is a 50 degree swivel
        Assert.That(Mathf.DeltaAngle(yawBefore, _rig.transform.eulerAngles.y), Is.EqualTo(50f).Within(1f));
        // ...and the grabbed point is still under the same spot on screen
        Vector3 grabbedOnScreen = _camera.WorldToScreenPoint(grabbed);
        Assert.That(new Vector2(grabbedOnScreen.x, grabbedOnScreen.y), Is.EqualTo(grabScreen).Using(new Vector2EqualityComparer(2f)));
    }

    [UnityTest]
    public IEnumerator RightDrag_NeverChangesTheTilt()
    {
        yield return StartWorld();
        float pitchBefore = Pitch();

        Set(_mouse.position, new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight * 0.5f));
        Press(_mouse.rightButton, queueEventOnly: true);
        yield return null;
        for (int i = 0; i < 5; i++)
        {
            Set(_mouse.delta, new Vector2(30f, 50f), queueEventOnly: true);     // up and sideways
            yield return null;
        }
        Release(_mouse.rightButton, queueEventOnly: true);
        yield return null;

        Assert.That(Pitch(), Is.EqualTo(pitchBefore).Within(0.1f), "Vertical drag does nothing");
    }

    [UnityTest]
    public IEnumerator MovingTheMouse_WithoutRightClick_DoesNothing()
    {
        yield return StartWorld();

        for (int i = 0; i < 5; i++)
        {
            Set(_mouse.delta, new Vector2(40f, 0f), queueEventOnly: true);
            yield return null;
        }

        Assert.That(_rig.transform.position, Is.EqualTo(StartPosition).Using(Near));
    }

    ////////////////////////////////////
    // ---------- Over the HUD ---------- //
    ////////////////////////////////////

    [UnityTest]
    public IEnumerator ScrollingOverTheHUD_DoesNotZoom()
    {
        // Scrolling the combat log mustn't zoom the camera. A full-screen UI panel stands in for it.
        yield return StartWorld();
        GameObject eventSystem = new GameObject("Test Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(_world.Root.transform);
        Canvas canvas = new GameObject("Test Canvas", typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(_world.Root.transform);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;

        Set(_mouse.position, new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight * 0.5f));
        yield return null;
        yield return null;
        Assume.That(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(), Is.True,
                    "The test's pretend mouse isn't registering over the UI on this setup");
        float before = DistanceToFocus();

        yield return ScrollNotches(3);

        Assert.That(DistanceToFocus(), Is.EqualTo(before).Within(0.01f));
    }
}
#endif
