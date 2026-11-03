using UnityEngine;

namespace DnDCombat.Unity
{
    // Shared helpers for the soft, glowing look of the movement overlay, the path line and the
    // destination glow.
    //
    // How the glow works:
    //   1. A see-through (transparent) shader. Line Material uses URP's Unlit shader set to
    //      Opaque, which ignores alpha completely, so nothing could ever fade. These materials
    //      use Sprites/Default instead: it blends by alpha and uses vertex colours.
    //   2. A texture that's white everywhere, with alpha shaped like light: a thin solid core
    //      and a long tail that fades away to nothing. That's the feathering.
    //   3. A material colour brighter than white (e.g. 1.5). The scene's Bloom (threshold 1)
    //      picks up anything brighter than 1 and spreads a soft halo around it.
    public static class SoftGlow
    {
        // Drawn in this order, so the path sits on the glow and the glow on the overlay
        public const int OverlayQueue = 3000;
        public const int GlowQueue = 3001;
        public const int PathQueue = 3002;

        //////////////////////////////////////
        // ---------- Textures ---------- //
        //////////////////////////////////////

        // For lines: fades across the width (V), the same all along the length (U).
        // Strips and LineRenderers map V from one edge (0) to the other (1).
        public static Texture2D MakeStripTexture(int size, float core)
        {
            Texture2D texture = new Texture2D(4, size, TextureFormat.RGBA32, false);
            texture.name = "Soft Strip";
            texture.wrapMode = TextureWrapMode.Clamp;

            float centre = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                // 0 on the centre line, 1 at either edge
                float d = Mathf.Abs(y - centre) / centre;
                float alpha = LightFalloff(d, core);
                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        // For a spot of light: fades out from the centre in every direction (a soft circle).
        public static Texture2D MakeSpotTexture(int size, float core)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Soft Spot";
            texture.wrapMode = TextureWrapMode.Clamp;

            float centre = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Straight-line distance, so the spot is round. Corners come out past 1,
                    // which is fully see-through.
                    float dx = x - centre;
                    float dy = y - centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / centre;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, LightFalloff(d, core)));
                }
            }

            texture.Apply();
            return texture;
        }

        // How solid a pixel is at distance d (0 = centre, 1 = edge), shaped like a glow:
        //   Inside the core (d <= core): fully solid. That's the crisp bright line in the middle.
        //   Outside it: t runs from 1 at the core's edge down to 0 at the outer edge, and
        //   squaring it makes the tail drop fast at first, then linger faintly. That long faint
        //   tail is what reads as feathering rather than a hard edge.
        // Worked numbers with core 0.2:
        //   d = 0.2 -> 1      d = 0.6 -> t = 0.5  -> 0.25      d = 0.8 -> t = 0.25 -> 0.06
        private static float LightFalloff(float d, float core)
        {
            if (d <= core) return 1f;
            if (d >= 1f) return 0f;

            float t = (1f - d) / (1f - core);
            return t * t;
        }

        //////////////////////////////////////
        // ---------- Materials ---------- //
        //////////////////////////////////////

        // A new transparent material with the soft texture and a brightness. Brightness above 1
        // is what Bloom turns into a halo. The fallback material is only used if Sprites/Default
        // can't be found (it's in the project's Always Included Shaders, so it should be).
        public static Material MakeMaterial(Material fallback, Texture2D texture, float brightness, int renderQueue)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material material = shader != null ? new Material(shader) : new Material(fallback);
            material.mainTexture = texture;
            material.color = Bright(Color.white, brightness);
            material.renderQueue = renderQueue;
            return material;
        }

        // The same colour with RGB scaled up, leaving alpha alone
        public static Color Bright(Color colour, float brightness)
        {
            return new Color(colour.r * brightness, colour.g * brightness, colour.b * brightness, colour.a);
        }

        // A gentle "breathing" multiplier between (1 - depth) and 1, once every 1 / speed seconds
        public static float Pulse(float speed, float depth)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * 2f * Mathf.PI);   // 0 to 1
            return 1f - depth * wave;
        }
    }
}
