using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DnDCombat.Unity;

// SoftGlow makes the feathered textures and see-through materials for the overlay, the path
// line and the destination glow. These tests read the textures' pixels back to check the
// shape of the fade, and check the materials are set up to actually show it.
public class SoftGlowTest
{
    private readonly List<Object> _made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in _made)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        _made.Clear();
    }

    private T Keep<T>(T thing) where T : Object
    {
        _made.Add(thing);
        return thing;
    }

    // Alpha of the strip texture at row y (the strip fades across its height)
    private static float StripAlpha(Texture2D texture, int y) => texture.GetPixel(0, y).a;

    //////////////////////////////////////////
    // ---------- Strip texture ---------- //
    //////////////////////////////////////////

    [Test]
    public void Strip_IsWhite_SoTheMaterialColourDecidesTheTint()
    {
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(64, 0.2f));

        Color middle = strip.GetPixel(0, 32);
        Assert.That(middle.r, Is.EqualTo(1f).Within(0.01f));
        Assert.That(middle.g, Is.EqualTo(1f).Within(0.01f));
        Assert.That(middle.b, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void Strip_IsSolidInTheMiddle_AndSeeThroughAtBothEdges()
    {
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(64, 0.2f));

        Assert.That(StripAlpha(strip, 31), Is.EqualTo(1f).Within(0.01f));
        Assert.That(StripAlpha(strip, 32), Is.EqualTo(1f).Within(0.01f));
        Assert.That(StripAlpha(strip, 0), Is.EqualTo(0f).Within(0.01f));
        Assert.That(StripAlpha(strip, 63), Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void Strip_IsTheSameOnBothSides()
    {
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(64, 0.2f));

        for (int y = 0; y < 32; y++)
        {
            Assert.That(StripAlpha(strip, y), Is.EqualTo(StripAlpha(strip, 63 - y)).Within(0.01f), $"row {y}");
        }
    }

    [Test]
    public void Strip_NeverGetsBrighter_GoingOutFromTheMiddle()
    {
        // A bump in the fade would show up as a stripe along every line
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(64, 0.2f));

        for (int y = 32; y < 63; y++)
        {
            Assert.That(StripAlpha(strip, y + 1), Is.LessThanOrEqualTo(StripAlpha(strip, y) + 0.001f), $"row {y + 1}");
        }
    }

    [Test]
    public void Strip_FollowsTheWorkedNumbers()
    {
        // With core 0.2, from the SoftGlow comments:
        //   d = 0.6 -> 0.25      d = 0.8 -> 0.06
        // With size 65 the centre is exactly row 32, so d = (row - 32) / 32
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(65, 0.2f));

        Assert.That(StripAlpha(strip, 32 + 19), Is.EqualTo(0.25f).Within(0.03f));   // d = 0.59
        Assert.That(StripAlpha(strip, 32 + 26), Is.EqualTo(0.06f).Within(0.02f));   // d = 0.81
    }

    [Test]
    public void Strip_BiggerCore_KeepsMoreOfTheWidthSolid()
    {
        Texture2D thin = Keep(SoftGlow.MakeStripTexture(64, 0.1f));
        Texture2D thick = Keep(SoftGlow.MakeStripTexture(64, 0.6f));

        // Row 45 is about 40% of the way out: inside the thick core, well into the thin one's fade
        Assert.That(StripAlpha(thick, 45), Is.EqualTo(1f).Within(0.01f));
        Assert.That(StripAlpha(thin, 45), Is.LessThan(0.6f));
    }

    [Test]
    public void Strip_ClampsAtItsEdges_SoNothingBleedsRound()
    {
        // Repeat would wrap the solid middle round onto the see-through edge
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(64, 0.2f));

        Assert.That(strip.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        Assert.That(strip.height, Is.EqualTo(64));
    }

    /////////////////////////////////////////
    // ---------- Spot texture ---------- //
    /////////////////////////////////////////

    [Test]
    public void Spot_IsBrightestInTheMiddle_AndGoneAtTheEdgesAndCorners()
    {
        Texture2D spot = Keep(SoftGlow.MakeSpotTexture(64, 0f));

        Assert.That(spot.GetPixel(32, 32).a, Is.GreaterThan(0.9f));
        Assert.That(spot.GetPixel(63, 32).a, Is.EqualTo(0f).Within(0.01f));   // edge
        Assert.That(spot.GetPixel(32, 0).a, Is.EqualTo(0f).Within(0.01f));    // edge
        Assert.That(spot.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f));     // corner
        Assert.That(spot.GetPixel(63, 63).a, Is.EqualTo(0f).Within(0.01f));   // corner
    }

    [Test]
    public void Spot_IsRound_NotSquare()
    {
        // Same distance from the centre in different directions gives the same alpha.
        // (A square glow would be brighter along the diagonal.)
        Texture2D spot = Keep(SoftGlow.MakeSpotTexture(65, 0f));

        float straight = spot.GetPixel(32 + 20, 32).a;      // 20 pixels right
        float diagonal = spot.GetPixel(32 + 14, 32 + 14).a; // 14 * 1.414 = 19.8 pixels away

        Assert.That(diagonal, Is.EqualTo(straight).Within(0.03f));
        Assert.That(spot.GetPixel(32 + 20, 32 + 20).a, Is.LessThan(straight));   // further out on the diagonal
    }

    [Test]
    public void Spot_IsSymmetric()
    {
        Texture2D spot = Keep(SoftGlow.MakeSpotTexture(64, 0f));

        for (int i = 0; i < 64; i += 7)
        {
            Assert.That(spot.GetPixel(i, 20).a, Is.EqualTo(spot.GetPixel(63 - i, 20).a).Within(0.01f));
            Assert.That(spot.GetPixel(20, i).a, Is.EqualTo(spot.GetPixel(20, 63 - i).a).Within(0.01f));
        }
    }

    ////////////////////////////////////
    // ---------- Materials ---------- //
    ////////////////////////////////////

    [Test]
    public void Material_UsesASeeThroughShader()
    {
        // The project's Line Material is URP Unlit set to Opaque, which ignores alpha.
        // That's what made the first glow look harsh, so this guards against it coming back.
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(32, 0.2f));
        Material material = Keep(SoftGlow.MakeMaterial(null, strip, 1.5f, SoftGlow.OverlayQueue));

        Assert.That(material.shader.name, Is.EqualTo("Sprites/Default"));
    }

    [Test]
    public void Material_HasTheTexture_TheQueue_AndTheBrightness()
    {
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(32, 0.2f));
        Material material = Keep(SoftGlow.MakeMaterial(null, strip, 1.5f, SoftGlow.PathQueue));

        Assert.That(material.mainTexture, Is.SameAs(strip));
        Assert.That(material.renderQueue, Is.EqualTo(SoftGlow.PathQueue));
        Assert.That(material.color.r, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(material.color.a, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void Material_IsANewCopy_SoTheProjectAssetIsNeverChanged()
    {
        Material original = Keep(new Material(Shader.Find("Sprites/Default")));
        original.color = Color.red;
        Texture2D strip = Keep(SoftGlow.MakeStripTexture(32, 0.2f));

        Material made = Keep(SoftGlow.MakeMaterial(original, strip, 2f, SoftGlow.GlowQueue));

        Assert.That(made, Is.Not.SameAs(original));
        Assert.That(original.color, Is.EqualTo(Color.red));
        Assert.That(original.mainTexture, Is.Null);
    }

    [Test]
    public void DrawOrder_IsOverlayThenGlowThenPath()
    {
        Assert.That(SoftGlow.OverlayQueue, Is.LessThan(SoftGlow.GlowQueue));
        Assert.That(SoftGlow.GlowQueue, Is.LessThan(SoftGlow.PathQueue));
        Assert.That(SoftGlow.OverlayQueue, Is.GreaterThanOrEqualTo(3000), "See-through things belong in the Transparent queue (3000+)");
    }

    /////////////////////////////////////////
    // ---------- Bright and Pulse ---------- //
    /////////////////////////////////////////

    [Test]
    public void Bright_ScalesTheColour_ButNotTheAlpha()
    {
        Color result = SoftGlow.Bright(new Color(1f, 0.5f, 0.2f, 0.4f), 2f);

        Assert.That(result.r, Is.EqualTo(2f).Within(0.001f));
        Assert.That(result.g, Is.EqualTo(1f).Within(0.001f));
        Assert.That(result.b, Is.EqualTo(0.4f).Within(0.001f));
        Assert.That(result.a, Is.EqualTo(0.4f).Within(0.001f));
    }

    [Test]
    public void Pulse_StaysBetweenOneMinusDepthAndOne()
    {
        // Pulse reads the clock, so just check it's in range right now, for several depths
        foreach (float depth in new[] { 0f, 0.35f, 1f })
        {
            float pulse = SoftGlow.Pulse(0.5f, depth);
            Assert.That(pulse, Is.InRange(1f - depth - 0.0001f, 1.0001f), $"depth {depth}");
        }
    }

    [Test]
    public void Pulse_WithNoDepth_IsAlwaysOne()
    {
        Assert.That(SoftGlow.Pulse(3f, 0f), Is.EqualTo(1f).Within(0.0001f));
    }
}
