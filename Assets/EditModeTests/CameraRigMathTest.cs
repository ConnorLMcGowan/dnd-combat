using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DnDCombat.Unity;

// The camera maths with real numbers. Every worked example in CameraRigMath's comments is
// checked here, plus the properties the controls rely on (orbiting keeps the grabbed point still,
// reading a camera and rebuilding it gives the same camera back).
public class CameraRigMathTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.01f);

    //////////////////////////////////////////////
    // ---------- Position and rotation ---------- //
    //////////////////////////////////////////////

    [Test]
    public void Position_At45Degrees_IsUpAndBackEqually()
    {
        Vector3 camera = CameraRigMath.Position(Vector3.zero, 0f, 45f, 10f);

        Assert.That(camera, Is.EqualTo(new Vector3(0f, 7.07f, -7.07f)).Using(Near));
    }

    [Test]
    public void Position_StraightDown_IsDirectlyAbove()
    {
        Assert.That(CameraRigMath.Position(new Vector3(3f, 0f, 4f), 0f, 90f, 10f),
                    Is.EqualTo(new Vector3(3f, 10f, 4f)).Using(Near));
    }

    [Test]
    public void Position_Yaw90_LooksAlongX_SoTheCameraSitsOnTheMinusXSide()
    {
        Assert.That(CameraRigMath.Position(Vector3.zero, 90f, 45f, 10f),
                    Is.EqualTo(new Vector3(-7.07f, 7.07f, 0f)).Using(Near));
    }

    [Test]
    public void TheCamera_AlwaysLooksAtTheFocus()
    {
        Vector3 focus = new Vector3(2f, 0f, -3f);
        float yaw = 37f, pitch = 28f, distance = 9f;

        Vector3 camera = CameraRigMath.Position(focus, yaw, pitch, distance);
        Vector3 looking = CameraRigMath.Rotation(yaw, pitch) * Vector3.forward;

        Assert.That(camera + looking * distance, Is.EqualTo(focus).Using(Near));
        Assert.That(Vector3.Distance(camera, focus), Is.EqualTo(distance).Within(0.01f));
    }

    [Test]
    public void Rotation_NeverRolls()
    {
        // The camera's right-hand side always stays level, whatever the yaw and pitch
        Quaternion rotation = CameraRigMath.Rotation(123f, 40f);

        Assert.That((rotation * Vector3.right).y, Is.EqualTo(0f).Within(0.0001f));
    }

    //////////////////////////////////////////
    // ---------- Reading a camera ---------- //
    //////////////////////////////////////////

    [Test]
    public void TryReadFrom_ThenPosition_GivesTheSameCameraBack()
    {
        // This is what makes "zoomed out" start exactly where you placed the camera
        Vector3 placed = new Vector3(4f, 12f, -9f);
        Quaternion turned = Quaternion.Euler(50f, 20f, 0f);

        bool read = CameraRigMath.TryReadFrom(placed, turned, 0f,
                                              out Vector3 focus, out float yaw, out float pitch, out float distance);

        Assert.That(read, Is.True);
        Assert.That(focus.y, Is.EqualTo(0f).Within(0.001f), "The focus is on the floor");
        Assert.That(yaw, Is.EqualTo(20f).Within(0.01f));
        Assert.That(pitch, Is.EqualTo(50f).Within(0.01f));
        Assert.That(CameraRigMath.Position(focus, yaw, pitch, distance), Is.EqualTo(placed).Using(Near));
    }

    [Test]
    public void TryReadFrom_ACameraLookingStraightDown()
    {
        bool read = CameraRigMath.TryReadFrom(new Vector3(0f, 20f, 0f), Quaternion.Euler(90f, 0f, 0f), 0f,
                                              out Vector3 focus, out _, out float pitch, out float distance);

        Assert.That(read, Is.True);
        Assert.That(focus, Is.EqualTo(Vector3.zero).Using(Near));
        Assert.That(pitch, Is.EqualTo(90f).Within(0.01f));
        Assert.That(distance, Is.EqualTo(20f).Within(0.01f));
    }

    [Test]
    public void TryReadFrom_ACameraLookingAtTheHorizon_Fails()
    {
        // It never meets the floor, so there's no focus to orbit around
        Assert.That(CameraRigMath.TryReadFrom(new Vector3(0f, 5f, 0f), Quaternion.Euler(0f, 0f, 0f), 0f,
                                              out _, out _, out _, out _), Is.False);
        Assert.That(CameraRigMath.TryReadFrom(new Vector3(0f, 5f, 0f), Quaternion.Euler(-30f, 0f, 0f), 0f,
                                              out _, out _, out _, out _), Is.False, "Looking up at the sky");
    }

    ///////////////////////////////////
    // ---------- Panning ---------- //
    ///////////////////////////////////

    [Test]
    public void PanDirection_W_IsIntoTheScreen_WhicheverWayTheCameraFaces()
    {
        Vector2 w = new Vector2(0f, 1f);

        Assert.That(CameraRigMath.PanDirection(0f, w), Is.EqualTo(Vector3.forward).Using(Near));
        Assert.That(CameraRigMath.PanDirection(90f, w), Is.EqualTo(Vector3.right).Using(Near));
        Assert.That(CameraRigMath.PanDirection(180f, w), Is.EqualTo(Vector3.back).Using(Near));
    }

    [Test]
    public void PanDirection_D_IsToTheCamerasRight()
    {
        Assert.That(CameraRigMath.PanDirection(0f, new Vector2(1f, 0f)), Is.EqualTo(Vector3.right).Using(Near));
        Assert.That(CameraRigMath.PanDirection(90f, new Vector2(1f, 0f)), Is.EqualTo(Vector3.back).Using(Near));
    }

    [Test]
    public void PanDirection_TwoKeysAtOnce_AreNoFasterThanOne()
    {
        Vector3 diagonal = CameraRigMath.PanDirection(0f, new Vector2(1f, 1f));

        Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(diagonal, Is.EqualTo(new Vector3(0.707f, 0f, 0.707f)).Using(Near));
    }

    [Test]
    public void PanDirection_NoKeys_IsNoMovement()
    {
        Assert.That(CameraRigMath.PanDirection(45f, Vector2.zero), Is.EqualTo(Vector3.zero).Using(Near));
    }

    [Test]
    public void PanDirection_IsAlwaysFlat()
    {
        Assert.That(CameraRigMath.PanDirection(33f, new Vector2(0.4f, -0.8f)).y, Is.EqualTo(0f));
    }

    [Test]
    public void ClampFocus_KeepsItInsideTheBox_AndLeavesTheHeightAlone()
    {
        Vector3 min = new Vector3(-10f, 0f, -10f);
        Vector3 max = new Vector3(10f, 0f, 10f);

        Assert.That(CameraRigMath.ClampFocus(new Vector3(15f, 2f, -30f), min, max), Is.EqualTo(new Vector3(10f, 2f, -10f)));
        Assert.That(CameraRigMath.ClampFocus(new Vector3(3f, 0f, 4f), min, max), Is.EqualTo(new Vector3(3f, 0f, 4f)));
    }

    ////////////////////////////////////
    // ---------- Orbiting ---------- //
    ////////////////////////////////////

    [Test]
    public void OrbitFocus_QuarterTurn_MatchesTheWorkedExample()
    {
        Assert.That(CameraRigMath.OrbitFocus(new Vector3(2f, 0f, 0f), Vector3.zero, 90f),
                    Is.EqualTo(new Vector3(0f, 0f, -2f)).Using(Near));
    }

    [Test]
    public void OrbitFocus_AroundItself_DoesNothing()
    {
        Vector3 focus = new Vector3(4f, 0f, 1f);

        Assert.That(CameraRigMath.OrbitFocus(focus, focus, 73f), Is.EqualTo(focus).Using(Near));
    }

    [Test]
    public void Orbiting_KeepsTheGrabbedPointAtTheSameSpotOnScreen()
    {
        // The heart of "swivel around the cursor": turn the focus AND the yaw by the same angle,
        // and the pivot sits in exactly the same place as seen from the camera
        Vector3 focus = new Vector3(0f, 0f, 0f);
        Vector3 pivot = new Vector3(3f, 0f, 2f);       // where the right-click landed
        float yaw = 10f, pitch = 40f, distance = 15f;

        Vector3 camBefore = CameraRigMath.Position(focus, yaw, pitch, distance);
        Vector3 seenBefore = Quaternion.Inverse(CameraRigMath.Rotation(yaw, pitch)) * (pivot - camBefore);

        float angle = 55f;
        Vector3 newFocus = CameraRigMath.OrbitFocus(focus, pivot, angle);
        float newYaw = yaw + angle;
        Vector3 camAfter = CameraRigMath.Position(newFocus, newYaw, pitch, distance);
        Vector3 seenAfter = Quaternion.Inverse(CameraRigMath.Rotation(newYaw, pitch)) * (pivot - camAfter);

        Assert.That(seenAfter, Is.EqualTo(seenBefore).Using(Near));
    }

    ////////////////////////////////
    // ---------- Zoom ---------- //
    ////////////////////////////////

    [Test]
    public void Zoom_HalfWay_MatchesTheWorkedExample()
    {
        Assert.That(CameraRigMath.ZoomDistance(0.5f, 4f, 20f), Is.EqualTo(12f).Within(0.001f));
        Assert.That(CameraRigMath.ZoomPitch(0.5f, 12f, 50f), Is.EqualTo(31f).Within(0.001f));
    }

    [Test]
    public void Zoom_FullyOutAndFullyIn_AreTheLimits()
    {
        Assert.That(CameraRigMath.ZoomDistance(1f, 4f, 20f), Is.EqualTo(20f));
        Assert.That(CameraRigMath.ZoomPitch(1f, 12f, 50f), Is.EqualTo(50f));
        Assert.That(CameraRigMath.ZoomDistance(0f, 4f, 20f), Is.EqualTo(4f));
        Assert.That(CameraRigMath.ZoomPitch(0f, 12f, 50f), Is.EqualTo(12f));
    }

    [Test]
    public void Zoom_PastTheLimits_StaysAtTheLimits()
    {
        Assert.That(CameraRigMath.ZoomDistance(1.7f, 4f, 20f), Is.EqualTo(20f));
        Assert.That(CameraRigMath.ZoomDistance(-0.3f, 4f, 20f), Is.EqualTo(4f));
        Assert.That(CameraRigMath.ZoomPitch(-1f, 12f, 50f), Is.EqualTo(12f));
    }

    [Test]
    public void Smooth_MatchesTheWorkedExample()
    {
        // Sharpness 10 over one 60 fps frame closes about 15% of the gap
        Assert.That(CameraRigMath.Smooth(0f, 1f, 10f, 0.016f), Is.EqualTo(0.148f).Within(0.002f));
    }

    [Test]
    public void Smooth_DoesNotDependOnTheFrameRate()
    {
        // Two half-frames land in the same place as one whole frame
        float once = CameraRigMath.Smooth(0f, 1f, 8f, 0.1f);
        float twice = CameraRigMath.Smooth(CameraRigMath.Smooth(0f, 1f, 8f, 0.05f), 1f, 8f, 0.05f);

        Assert.That(twice, Is.EqualTo(once).Within(0.0001f));
    }

    [Test]
    public void Smooth_NeverOvershoots()
    {
        Assert.That(CameraRigMath.Smooth(0f, 1f, 1000f, 1f), Is.LessThanOrEqualTo(1f));
    }

    ////////////////////////////////
    // ---------- Rays ---------- //
    ////////////////////////////////

    [Test]
    public void TryGroundPoint_StraightDown_MatchesTheWorkedExample()
    {
        bool hit = CameraRigMath.TryGroundPoint(new Ray(new Vector3(3f, 10f, 0f), Vector3.down), 0f, out Vector3 point);

        Assert.That(hit, Is.True);
        Assert.That(point, Is.EqualTo(new Vector3(3f, 0f, 0f)).Using(Near));
    }

    [Test]
    public void TryGroundPoint_AtAnAngle()
    {
        // Down 1 for every 1 across: from 4 up, it lands 4 along
        Ray ray = new Ray(new Vector3(0f, 4f, 0f), new Vector3(0f, -1f, 1f).normalized);

        Assert.That(CameraRigMath.TryGroundPoint(ray, 0f, out Vector3 point), Is.True);
        Assert.That(point, Is.EqualTo(new Vector3(0f, 0f, 4f)).Using(Near));
    }

    [Test]
    public void TryGroundPoint_UsesTheFloorHeight()
    {
        CameraRigMath.TryGroundPoint(new Ray(new Vector3(0f, 10f, 0f), Vector3.down), 2.5f, out Vector3 point);

        Assert.That(point.y, Is.EqualTo(2.5f).Within(0.001f));
    }

    [Test]
    public void TryGroundPoint_LevelUpwardsOrFromBelow_Misses()
    {
        Assert.That(CameraRigMath.TryGroundPoint(new Ray(new Vector3(0f, 5f, 0f), Vector3.forward), 0f, out _), Is.False, "Level");
        Assert.That(CameraRigMath.TryGroundPoint(new Ray(new Vector3(0f, 5f, 0f), Vector3.up), 0f, out _), Is.False, "Upwards");
        Assert.That(CameraRigMath.TryGroundPoint(new Ray(new Vector3(0f, -5f, 0f), Vector3.down), 0f, out _), Is.False, "Already below the floor");
    }
}
