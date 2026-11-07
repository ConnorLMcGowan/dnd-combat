using UnityEngine;

namespace DnDCombat.Unity
{
    // The world-space maths behind the camera controls, kept apart from input so every formula
    // can be tested with plain numbers.
    //
    // The camera is described by four numbers instead of a free position:
    //   focus     the point on the floor the camera looks at
    //   yaw       which way round it faces, in degrees (0 = looking along +Z, 90 = along +X)
    //   pitch     how steeply it looks down, in degrees (0 = level, 90 = straight down)
    //   distance  how far back from the focus it sits
    // From those, the camera's position and rotation are worked out fresh every frame, so it can
    // never drift, roll sideways or end up looking at the sky.
    public static class CameraRigMath
    {
        //////////////////////////////////////////////////////
        // ---------- The four numbers -> the camera ---------- //
        //////////////////////////////////////////////////////

        // Turn first around Y (yaw), then tip down around the camera's own X (pitch). No roll, ever.
        public static Quaternion Rotation(float yaw, float pitch)
        {
            return Quaternion.Euler(pitch, yaw, 0f);
        }

        // Start at the focus and step backwards along the view direction by the distance.
        // Worked example: focus (0, 0, 0), yaw 0, pitch 45, distance 10.
        //   The view direction is (0, -0.707, 0.707): forward and down at 45 degrees.
        //   Stepping back 10 along it: (0, 0, 0) - (0, -7.07, 7.07) = (0, 7.07, -7.07).
        public static Vector3 Position(Vector3 focus, float yaw, float pitch, float distance)
        {
            return focus - Rotation(yaw, pitch) * Vector3.forward * distance;
        }

        // The reverse: reads an existing camera into the four numbers, by finding where its view
        // hits the floor. Used once at the start, so the controls begin from the camera exactly as
        // you placed it in the scene. False if the camera isn't looking down at the floor at all.
        public static bool TryReadFrom(Vector3 cameraPosition, Quaternion cameraRotation, float groundY,
                                       out Vector3 focus, out float yaw, out float pitch, out float distance)
        {
            focus = default;
            yaw = 0f;
            pitch = 0f;
            distance = 0f;

            Vector3 forward = cameraRotation * Vector3.forward;
            if (!TryGroundPoint(new Ray(cameraPosition, forward), groundY, out focus))
            {
                return false;
            }

            distance = Vector3.Distance(cameraPosition, focus);
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            // Down against sideways, not Asin of the down part alone: near straight down, Asin turns
            // the tiniest float rounding into a visible error (0.02 degrees), and Atan2 doesn't
            float sideways = new Vector2(forward.x, forward.z).magnitude;
            pitch = Mathf.Atan2(-forward.y, sideways) * Mathf.Rad2Deg;
            return true;
        }

        //////////////////////////////////////
        // ---------- Panning ---------- //
        //////////////////////////////////////

        // Turns WASD into a direction across the floor, relative to the way the camera faces.
        // input.x is A/D (left -1, right +1), input.y is S/W (back -1, forward +1).
        // At yaw 0, W is +Z. At yaw 90, W is +X. Pressing two keys at once is no faster than one.
        public static Vector3 PanDirection(float yaw, Vector2 input)
        {
            Vector3 flat = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            return flat.sqrMagnitude > 1f ? flat.normalized : flat;
        }

        // Keeps the focus inside a box on the floor (the arena, plus a margin), so you can't fly off.
        // Height is left alone.
        public static Vector3 ClampFocus(Vector3 focus, Vector3 min, Vector3 max)
        {
            return new Vector3(Mathf.Clamp(focus.x, min.x, max.x),
                               focus.y,
                               Mathf.Clamp(focus.z, min.z, max.z));
        }

        //////////////////////////////////////
        // ---------- Orbiting ---------- //
        //////////////////////////////////////

        // Swings the focus around a pivot on the floor by an angle (degrees, around Y).
        // Turn the yaw by the same angle and the whole camera swings around the pivot as one
        // piece: the pivot stays at the same spot on screen and the world turns around it.
        // Worked example: focus (2, 0, 0) around pivot (0, 0, 0) by 90 degrees ends at (0, 0, -2).
        public static Vector3 OrbitFocus(Vector3 focus, Vector3 pivot, float angle)
        {
            return pivot + Quaternion.Euler(0f, angle, 0f) * (focus - pivot);
        }

        //////////////////////////////////
        // ---------- Zoom ---------- //
        //////////////////////////////////

        // One zoom value drives both distance and pitch, so zooming in also tilts towards level.
        //   zoom 1 = fully out (the camera as you placed it), zoom 0 = fully in.
        // Worked example: distance 4 to 20, pitch 12 to 50, zoom 0.5 gives distance 12, pitch 31.
        public static float ZoomDistance(float zoom, float minDistance, float maxDistance)
        {
            return Mathf.Lerp(minDistance, maxDistance, Mathf.Clamp01(zoom));
        }

        public static float ZoomPitch(float zoom, float minPitch, float maxPitch)
        {
            return Mathf.Lerp(minPitch, maxPitch, Mathf.Clamp01(zoom));
        }

        // Eases a value towards a target, the same fraction of the way every second whatever the
        // frame rate. Higher sharpness is snappier.
        // Worked example: sharpness 10 over a 0.016 s frame moves about 15% of the remaining way.
        public static float Smooth(float current, float target, float sharpness, float deltaTime)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * deltaTime));
        }

        /////////////////////////////////////
        // ---------- Rays ---------- //
        /////////////////////////////////////

        // Where a ray meets the flat floor at height groundY. False if it never does
        // (pointing level, or upwards).
        // Worked example: a ray from (3, 10, 0) going straight down hits (3, 0, 0) on a floor at 0.
        public static bool TryGroundPoint(Ray ray, float groundY, out Vector3 point)
        {
            point = default;
            if (ray.direction.y > -0.0001f)
            {
                return false;
            }

            float t = (groundY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
            {
                return false;
            }

            point = ray.origin + ray.direction * t;
            return true;
        }
    }
}
