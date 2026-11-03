#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;

// The path preview in a real running scene, with a pretend mouse.
// InputTestFixture swaps the real mouse for a test one, so a test can say "the mouse is
// here" and the game sees exactly that. Every check is what the player would see: is the
// line showing, where does it go, and where is the glow.
public class PathPreviewPlayTest : InputTestFixture
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.02f);

    private TestWorld _world;
    private PathPreview _preview;
    private LineRenderer _line;
    private Mouse _mouse;

    public override void Setup()
    {
        base.Setup();
        _mouse = InputSystem.AddDevice<Mouse>();
        _mouse.MakeCurrent();
    }

    public override void TearDown()
    {
        _world?.Destroy();
        _world = null;
        base.TearDown();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // The hero in the middle of the arena (7, 0, 7) unless a test says otherwise,
    // the goblin wherever the test wants it
    private IEnumerator StartWorld(Vector3Int goblinCell, Vector3Int? heroCell = null, bool withDirector = false)
    {
        _world = new TestWorld().WithArena().WithFight(heroCell ?? new Vector3Int(7, 0, 7), goblinCell).WithCamera();
        if (withDirector) _world.WithDirector();
        _preview = _world.Add<PathPreview>("Path Preview");
        TestWorld.SetObject(_preview, "_game", _world.Game);
        if (withDirector) TestWorld.SetObject(_preview, "_director", _world.Director);
        TestWorld.SetObject(_preview, "_camera", _world.Camera);
        TestWorld.SetFloat(_preview, "_glowPulseDepth", 0f);     // keep the glow steady while testing
        _line = _preview.GetComponent<LineRenderer>();
        _world.Activate();

        yield return null;                 // Start runs: combat begins
        _world.PassToHero();
        Physics.SyncTransforms();          // so mouse rays see the bodies on their squares
        yield return null;
    }

    // Moves the test mouse over a point in the world, then lets a frame run
    private IEnumerator HoverOver(Vector3 world)
    {
        Set(_mouse.position, _world.ScreenPointOf(world));
        yield return null;
        yield return null;
    }

    private IEnumerator HoverOverCell(GridPosition cell)
    {
        yield return HoverOver(_world.Game.Space.ToWorldFloorCentre(cell));
    }

    private IEnumerator HoverOverCreature(Combatant combatant)
    {
        yield return HoverOver(_world.Game.ViewOf(combatant).transform.position);
    }

    private GameObject Glow()
    {
        Transform glow = _preview.transform.Find("Path Glow");
        Assert.That(glow, Is.Not.Null, "PathPreview should make a 'Path Glow' child in Awake");
        return glow.gameObject;
    }

    // The middle of a cell's floor, flattened, for comparing where things are from above
    private Vector3 Flat(GridPosition cell)
    {
        Vector3 p = _world.Game.Space.ToWorldFloorCentre(cell);
        return new Vector3(p.x, 0f, p.z);
    }

    private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

    private Vector3 LineEnd() => Flat(_line.GetPosition(_line.positionCount - 1));

    ///////////////////////////////////////////
    // ---------- Hovering the ground ---------- //
    ///////////////////////////////////////////

    [UnityTest]
    public IEnumerator HoveringAReachableSquare_DrawsTheLineFromTheHeroToIt()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(7, 0, 10));

        Assert.That(_line.enabled, Is.True);
        Assert.That(_line.positionCount, Is.EqualTo(4), "The hero's square plus 3 steps");
        Assert.That(Flat(_line.GetPosition(0)), Is.EqualTo(Flat(P(7, 0, 7))).Using(Near));
        Assert.That(LineEnd(), Is.EqualTo(Flat(P(7, 0, 10))).Using(Near));
    }

    [UnityTest]
    public IEnumerator HoveringStraightAhead_DrawsAStraightLine()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(7, 0, 12));

        for (int i = 0; i < _line.positionCount; i++)
        {
            Assert.That(_line.GetPosition(i).x, Is.EqualTo(_line.GetPosition(0).x).Within(0.01f), $"point {i} wandered sideways");
        }
    }

    [UnityTest]
    public IEnumerator TheLine_FloatsJustAboveTheFloor()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(9, 0, 9));

        for (int i = 0; i < _line.positionCount; i++)
        {
            Assert.That(_line.GetPosition(i).y, Is.InRange(0.01f, 0.2f), $"point {i}");
        }
    }

    [UnityTest]
    public IEnumerator HoveringAReachableSquare_GlowsUnderIt()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(9, 0, 5));

        Assert.That(Glow().activeSelf, Is.True);
        Assert.That(Flat(Glow().transform.position), Is.EqualTo(Flat(P(9, 0, 5))).Using(Near));
    }

    [UnityTest]
    public IEnumerator TheGlow_SitsBetweenTheOverlayAndTheLine()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(9, 0, 5));

        float glowHeight = Glow().transform.position.y;
        Assert.That(glowHeight, Is.GreaterThan(0.03f), "Above the overlay lines");
        Assert.That(glowHeight, Is.LessThan(_line.GetPosition(0).y), "Below the path line");
    }

    [UnityTest]
    public IEnumerator HoveringASquareTooFarAway_ShowsNothing()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCell(P(0, 0, 0));       // 7 squares, only 6 of movement

        Assert.That(_line.enabled, Is.False);
        Assert.That(Glow().activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator MovingTheMouseOffTheArena_HidesEverything()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        yield return HoverOverCell(P(7, 0, 10));
        Assume.That(_line.enabled, Is.True);

        Set(_mouse.position, new Vector2(1f, 1f));    // the bottom-left pixel: past the floor's edge
        yield return null;
        yield return null;

        Assert.That(_line.enabled, Is.False);
        Assert.That(Glow().activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator HoveringBackOntoTheSameSquare_ShowsTheLineAgain()
    {
        // The old "unchanged" check used to keep the line hidden in exactly this case
        yield return StartWorld(new Vector3Int(14, 0, 14));
        yield return HoverOverCell(P(7, 0, 10));
        Set(_mouse.position, new Vector2(1f, 1f));
        yield return null;
        yield return null;

        yield return HoverOverCell(P(7, 0, 10));

        Assert.That(_line.enabled, Is.True);
        Assert.That(Glow().activeSelf, Is.True);
    }

    ////////////////////////////////////////////
    // ---------- Hovering a creature ---------- //
    ////////////////////////////////////////////

    [UnityTest]
    public IEnumerator HoveringAnEnemyInWalkingRange_ShowsTheWalkToTheSquareInFrontOfIt()
    {
        // Goblin 4 squares north. Clicking would walk to (7, 0, 10) and attack from there.
        yield return StartWorld(new Vector3Int(7, 0, 11));

        yield return HoverOverCreature(_world.Enemy);

        Assert.That(_line.enabled, Is.True);
        Assert.That(LineEnd(), Is.EqualTo(Flat(P(7, 0, 10))).Using(Near));
        Assert.That(Glow().activeSelf, Is.True);
        Assert.That(Flat(Glow().transform.position), Is.EqualTo(Flat(P(7, 0, 10))).Using(Near));
    }

    [UnityTest]
    public IEnumerator HoveringAnAdjacentEnemy_ShowsNoLine_AndGlowsUnderTheHero()
    {
        yield return StartWorld(new Vector3Int(8, 0, 7));

        yield return HoverOverCreature(_world.Enemy);

        Assert.That(_line.enabled, Is.False, "Nowhere to walk");
        Assert.That(Glow().activeSelf, Is.True, "You'd attack from where you stand");
        Assert.That(Flat(Glow().transform.position), Is.EqualTo(Flat(P(7, 0, 7))).Using(Near));
    }

    [UnityTest]
    public IEnumerator HoveringAnEnemyTooFarToReach_ShowsNothing()
    {
        // Hero in one corner, goblin in the opposite one: 14 away, and 6 squares of walking
        // still leaves it 8 away
        yield return StartWorld(new Vector3Int(14, 0, 14), new Vector3Int(0, 0, 0));

        yield return HoverOverCreature(_world.Enemy);

        Assert.That(_line.enabled, Is.False);
        Assert.That(Glow().activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator HoveringTheHeroItself_ShowsNothing()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        yield return HoverOverCreature(_world.Hero);

        Assert.That(_line.enabled, Is.False);
        Assert.That(Glow().activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator ThePreview_MatchesWhereAClickWouldGo()
    {
        // Hover, remember the end of the line, then do what a click does and compare
        yield return StartWorld(new Vector3Int(10, 0, 11));
        yield return HoverOverCreature(_world.Enemy);
        Assume.That(_line.enabled, Is.True);
        Vector3 previewEnd = LineEnd();

        MovementRange range = _world.Game.Controller.GetMovementRange(_world.Hero);
        _world.Game.TryGetAttack(_world.Hero, out AttackProfile attack);
        GridPosition clickWouldGo = range.BestApproach(_world.Where(_world.Enemy), attack.RangeSquares);

        Assert.That(previewEnd, Is.EqualTo(Flat(clickWouldGo)).Using(Near));
    }

    ////////////////////////////////////////
    // ---------- Turns and safety ---------- //
    ////////////////////////////////////////

    [UnityTest]
    public IEnumerator OnTheEnemysTurn_NothingShows()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        _world.Game.Controller.EndTurn();
        Assume.That(_world.Game.Controller.Turns.Current, Is.SameAs(_world.Enemy));

        yield return HoverOverCell(P(7, 0, 10));

        Assert.That(_line.enabled, Is.False);
        Assert.That(Glow().activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator AfterTheHeroMoves_TheLineStartsFromItsNewSquare()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        Assume.That(_world.Game.Controller.TryMove(_world.Hero, P(7, 0, 9)).Success, Is.True);
        _world.Game.ViewOf(_world.Hero).SnapToGrid();
        Physics.SyncTransforms();

        yield return HoverOverCell(P(9, 0, 9));

        Assert.That(_line.enabled, Is.True);
        Assert.That(Flat(_line.GetPosition(0)), Is.EqualTo(Flat(P(7, 0, 9))).Using(Near));
        Assert.That(_line.positionCount, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator TheGlow_HasNoCollider_SoItNeverBlocksTheMouse()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        // In the editor a missing component comes back as a "fake null" object, so compare with == null
        Assert.That(Glow().GetComponent<Collider>() == null, Is.True);
    }

    [UnityTest]
    public IEnumerator TheLineAndGlow_UseSeeThroughMaterials_InTheRightOrder()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));

        Material line = _line.sharedMaterial;
        Material glow = Glow().GetComponent<MeshRenderer>().sharedMaterial;
        Assert.That(line.shader.name, Is.EqualTo("Sprites/Default"));
        Assert.That(glow.shader.name, Is.EqualTo("Sprites/Default"));
        Assert.That(line.renderQueue, Is.EqualTo(SoftGlow.PathQueue));
        Assert.That(glow.renderQueue, Is.EqualTo(SoftGlow.GlowQueue));
    }

    [UnityTest]
    public IEnumerator WithNoGameManager_NothingShows_AndNothingBreaks()
    {
        _world = new TestWorld().WithCamera();
        _preview = _world.Add<PathPreview>("Lonely Preview");
        TestWorld.SetObject(_preview, "_camera", _world.Camera);
        _line = _preview.GetComponent<LineRenderer>();
        _world.Activate();

        yield return null;
        Set(_mouse.position, new Vector2(100f, 100f));
        yield return null;

        Assert.That(_line.enabled, Is.False);
    }

    ///////////////////////////////////////////////
    // ---------- While bodies are moving ---------- //
    ///////////////////////////////////////////////

    [UnityTest]
    public IEnumerator WhileTheHeroWalks_ThePreviewHides_ThenComesBackFromItsNewSquare()
    {
        // While walking, the Core already has the hero at its destination but the body is still
        // on the way, so a preview drawn now would start from the wrong place
        yield return StartWorld(new Vector3Int(14, 0, 14), null, withDirector: true);
        yield return HoverOverCell(P(9, 0, 10));
        Assume.That(_line.enabled, Is.True);

        _world.Game.Controller.TryMove(_world.Hero, P(7, 0, 9));
        yield return null;
        yield return null;
        Assert.That(_line.enabled, Is.False, "Hidden mid-walk");
        Assert.That(Glow().activeSelf, Is.False);

        yield return _world.WaitUntilIdle();
        Physics.SyncTransforms();
        yield return null;
        yield return null;
        Assert.That(_line.enabled, Is.True, "Back after the walk");
        Assert.That(Flat(_line.GetPosition(0)), Is.EqualTo(Flat(P(7, 0, 9))).Using(Near), "Starting from the new square");
    }
}
#endif
