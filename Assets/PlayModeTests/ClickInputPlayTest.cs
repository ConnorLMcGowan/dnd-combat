#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using DnDCombat.Core;
using DnDCombat.Unity;

// ClickInput in a running scene with a pretend mouse: real clicks, real raycasts. Its rules are
// tested in edit mode (ClickInputTest). This checks the part only a running scene can: clicks
// made while a body is still walking are ignored.
public class ClickInputPlayTest : InputTestFixture
{
    private TestWorld _world;
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

    private IEnumerator StartWorld()
    {
        _world = new TestWorld().WithArena()
                                .WithFight(new Vector3Int(7, 0, 7), new Vector3Int(14, 0, 14))
                                .WithCamera()
                                .WithDirector(moveSpeed: 4f);
        ClickInput input = _world.Add<ClickInput>("Click Input");
        TestWorld.SetObject(input, "_game", _world.Game);
        TestWorld.SetObject(input, "_camera", _world.Camera);
        TestWorld.SetObject(input, "_director", _world.Director);
        _world.Activate();

        yield return null;
        _world.PassToHero();
        Physics.SyncTransforms();
        yield return null;
    }

    // Points the mouse at a square and clicks. The press is QUEUED, so the game sees it during
    // the next frame's Update, exactly as it would see a real click.
    private IEnumerator Click(GridPosition cell)
    {
        Set(_mouse.position, _world.ScreenPointOf(_world.Game.Space.ToWorldFloorCentre(cell)));
        Press(_mouse.leftButton, queueEventOnly: true);
        yield return null;
        Release(_mouse.leftButton, queueEventOnly: true);
        yield return null;
    }

    [UnityTest]
    public IEnumerator AClick_MovesTheHero()
    {
        // Checks the pretend clicks work at all, so the next test's "nothing happened" means something
        yield return StartWorld();

        yield return Click(P(7, 0, 9));
        yield return _world.WaitUntilIdle();

        Assert.That(_world.Where(_world.Hero), Is.EqualTo(P(7, 0, 9)));
        Assert.That(_world.Hero.MovementRemaining, Is.EqualTo(20));
    }

    [UnityTest]
    public IEnumerator AClickWhileWalking_IsIgnored()
    {
        // 4 squares at 4 per second takes a full second, plenty of time to click again mid-walk
        yield return StartWorld();
        yield return Click(P(7, 0, 11));
        Assume.That(_world.Director.IsBusy, Is.True, "The first click should have started a walk");

        yield return Click(P(9, 0, 11));
        yield return _world.WaitUntilIdle();

        Assert.That(_world.Where(_world.Hero), Is.EqualTo(P(7, 0, 11)), "The second click moved the hero");
        Assert.That(_world.Hero.MovementRemaining, Is.EqualTo(10), "Only the first walk was paid for");
    }

    [UnityTest]
    public IEnumerator AfterTheWalk_ClicksWorkAgain()
    {
        yield return StartWorld();
        yield return Click(P(7, 0, 9));
        yield return _world.WaitUntilIdle();
        Physics.SyncTransforms();

        yield return Click(P(9, 0, 9));
        yield return _world.WaitUntilIdle();

        Assert.That(_world.Where(_world.Hero), Is.EqualTo(P(9, 0, 9)));
        Assert.That(_world.Hero.MovementRemaining, Is.EqualTo(10));
    }
}
#endif
