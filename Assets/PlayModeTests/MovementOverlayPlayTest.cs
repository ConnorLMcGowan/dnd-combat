#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DnDCombat.Core;
using DnDCombat.Unity;

// The movement overlay in a real running scene: it should appear on a hero's turn, cover
// exactly the squares the hero can reach, follow the hero, and vanish when it isn't wanted.
public class MovementOverlayPlayTest
{
    private TestWorld _world;
    private MovementOverlay _overlay;

    [TearDown]
    public void TearDown()
    {
        _world?.Destroy();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // The hero in the middle of the arena, the goblin in a far corner (out of its reach)
    private IEnumerator StartWorld(Vector3Int heroCell, params Vector3Int[] blocks)
    {
        _world = new TestWorld().WithArena(blocks).WithFight(heroCell, new Vector3Int(14, 0, 14));
        _overlay = _world.Add<MovementOverlay>("Movement Overlay");
        TestWorld.SetObject(_overlay, "_game", _world.Game);
        _world.Activate();

        yield return null;                 // Start runs: combat begins
        _world.PassToHero();
        yield return null;                 // LateUpdate draws for the hero
    }

    private Mesh OverlayMesh()
    {
        Transform child = _overlay.transform.Find("Overlay Mesh");
        Assert.That(child, Is.Not.Null, "The overlay should make an 'Overlay Mesh' child in Awake");
        return child.GetComponent<MeshFilter>().sharedMesh;
    }

    private Bounds OverlayBounds()
    {
        return _overlay.transform.Find("Overlay Mesh").GetComponent<MeshRenderer>().bounds;
    }

    /////////////////////////////////////
    // ---------- Appearing ---------- //
    /////////////////////////////////////

    [UnityTest]
    public IEnumerator OnTheHerosTurn_TheOverlayIsDrawn()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));

        Assert.That(OverlayMesh().vertexCount, Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator Overlay_UsesASeeThroughMaterial_InTheOverlayQueue()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));

        Material material = _overlay.transform.Find("Overlay Mesh").GetComponent<MeshRenderer>().sharedMaterial;
        Assert.That(material.shader.name, Is.EqualTo("Sprites/Default"));
        Assert.That(material.renderQueue, Is.EqualTo(SoftGlow.OverlayQueue));
    }

    [UnityTest]
    public IEnumerator Overlay_CoversExactlyTheReachableArea()
    {
        // 30 ft is 6 squares each way, so from (7, 0, 7) it reaches x and z from 1 to 13:
        // 13 cells across. The outline adds half its width on each side.
        yield return StartWorld(new Vector3Int(7, 0, 7));
        float halfOutline = 0.1f * 3f / 2f;    // outline width x width scale, halved

        Bounds bounds = OverlayBounds();
        Assert.That(bounds.size.x, Is.EqualTo(13f + 2f * halfOutline).Within(0.05f));
        Assert.That(bounds.size.z, Is.EqualTo(13f + 2f * halfOutline).Within(0.05f));
        Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.05f), "Centred on the hero");
        Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.05f));
    }

    [UnityTest]
    public IEnumerator Overlay_LiesJustAboveTheFloor()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));

        Bounds bounds = OverlayBounds();
        Assert.That(bounds.min.y, Is.GreaterThan(0f), "On the floor it would flicker");
        Assert.That(bounds.max.y, Is.LessThan(0.1f), "Floating well above the floor would look odd");
    }

    [UnityTest]
    public IEnumerator Overlay_IsClippedByTheArenaEdge()
    {
        // From a corner, only 7 cells each way exist (0 to 6)
        yield return StartWorld(new Vector3Int(0, 0, 0));

        Bounds bounds = OverlayBounds();
        Assert.That(bounds.size.x, Is.EqualTo(7f + 0.3f).Within(0.05f));
        Assert.That(bounds.min.x, Is.EqualTo(-7.5f - 0.15f).Within(0.05f));
    }

    ///////////////////////////////////////////
    // ---------- Following the hero ---------- //
    ///////////////////////////////////////////

    [UnityTest]
    public IEnumerator AfterTheHeroMoves_TheOverlayShrinksAndFollows()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));
        float before = OverlayBounds().size.x;

        MoveResult move = _world.Game.Controller.TryMove(_world.Hero, P(7, 0, 10));   // 3 squares north
        Assume.That(move.Success, Is.True);
        yield return null;

        // 15 ft left is 3 squares each way: 7 cells across, centred on (7, 0, 10) which is z = 3
        Bounds after = OverlayBounds();
        Assert.That(after.size.x, Is.LessThan(before));
        Assert.That(after.size.x, Is.EqualTo(7f + 0.3f).Within(0.05f));
        Assert.That(after.center.z, Is.EqualTo(3f).Within(0.05f));
    }

    [UnityTest]
    public IEnumerator WhenTheHeroHasNoMovementLeft_TheOverlayDisappears()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));

        Assume.That(_world.Game.Controller.TryMove(_world.Hero, P(7, 0, 13)).Success, Is.True);   // all 6 squares
        yield return null;

        Assert.That(OverlayMesh().vertexCount, Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator OnTheEnemysTurn_TheOverlayDisappears()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));

        _world.Game.Controller.EndTurn();
        Assume.That(_world.Game.Controller.Turns.Current, Is.SameAs(_world.Enemy));
        yield return null;

        Assert.That(OverlayMesh().vertexCount, Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator BackOnTheHerosTurn_TheOverlayComesBack_AtFullSize()
    {
        yield return StartWorld(new Vector3Int(7, 0, 7));
        Assume.That(_world.Game.Controller.TryMove(_world.Hero, P(7, 0, 13)).Success, Is.True);
        _world.Game.Controller.EndTurn();     // goblin's go
        yield return null;
        _world.Game.Controller.EndTurn();     // hero again, with fresh movement
        yield return null;

        // From (7, 0, 13) a full 6 squares reaches z 7 to 14 (the arena stops it there),
        // which is world z -0.5 to 7.5, so the middle is 3.5
        Assert.That(OverlayMesh().vertexCount, Is.GreaterThan(0));
        Assert.That(OverlayBounds().center.z, Is.EqualTo(3.5f).Within(0.05f), "Around the hero's new square");
    }

    ////////////////////////////////////////
    // ---------- Holes and safety ---------- //
    ////////////////////////////////////////

    [UnityTest]
    public IEnumerator ABlockNextToTheHero_AddsAHoleOutline()
    {
        // A block inside the reachable area means a second loop round it, so more geometry
        yield return StartWorld(new Vector3Int(7, 0, 7));
        int plain = OverlayMesh().vertexCount;
        _world.Destroy();

        yield return StartWorld(new Vector3Int(7, 0, 7), new Vector3Int(9, 0, 7));

        Assert.That(OverlayMesh().vertexCount, Is.GreaterThan(plain));
    }

    [UnityTest]
    public IEnumerator WithNoGameManager_NothingIsDrawn_AndNothingBreaks()
    {
        _world = new TestWorld();
        _overlay = _world.Add<MovementOverlay>("Lonely Overlay");
        _world.Activate();

        yield return null;
        yield return null;

        Assert.That(OverlayMesh().vertexCount, Is.EqualTo(0));
        // Any error or exception logged during those frames would fail this test by itself
    }
}
#endif
