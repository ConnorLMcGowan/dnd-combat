#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;

// Everything that must wait while the BodyDirector is busy: the monster turn driver mustn't end
// a turn mid-walk, and the movement overlay hides until the body arrives.
public class WaitingForTheShowPlayTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.02f);

    private TestWorld _world;

    [TearDown]
    public void TearDown()
    {
        _world?.Destroy();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    //////////////////////////////////////////////
    // ---------- The monster turn driver ---------- //
    //////////////////////////////////////////////

    [UnityTest]
    public IEnumerator TheDriver_WaitsForTheWalkAndSwing_BeforeEndingTheTurn()
    {
        // The goblin starts 5 squares east of the hero, so on its turn it walks over and attacks
        _world = new TestWorld().WithArena().WithAnimatedBodies()
                                .WithFight(new Vector3Int(7, 0, 7), new Vector3Int(12, 0, 7))
                                .WithDirector();
        _world.AddTurnDriver();
        _world.Activate();
        yield return null;
        if (_world.Game.Controller.Turns.Current == _world.Hero)
        {
            _world.Game.Controller.EndTurn();       // make it the goblin's go
        }

        // Watch every frame until the turn comes back to the hero
        float giveUp = Time.time + 10f;
        while (_world.Game.Controller.Turns.Current != _world.Hero && Time.time < giveUp)
        {
            yield return null;
        }
        Assert.That(_world.Game.Controller.Turns.Current, Is.SameAs(_world.Hero), "The goblin's turn never ended");

        // At the moment the turn passed back, the show must already be over
        Assert.That(_world.Director.IsBusy, Is.False, "The turn ended while the goblin was still moving or swinging");
        GridPosition goblinCell = _world.Where(_world.Enemy);
        Assert.That(_world.Game.ViewOf(_world.Enemy).transform.position,
                    Is.EqualTo(_world.Game.Space.ToWorldFloorCentre(goblinCell)).Using(Near), "The body hadn't arrived");
        Assert.That(goblinCell, Is.Not.EqualTo(P(12, 0, 7)), "The goblin should have walked");
    }

    [UnityTest]
    public IEnumerator TheDriver_PlaysEveryEnemyTurn_AndHandsBackToTheHero_RoundAfterRound()
    {
        // Two full rounds with no player input apart from ending the hero's turn
        _world = new TestWorld().WithArena()
                                .WithFight(new Vector3Int(2, 0, 2), new Vector3Int(12, 0, 12))
                                .WithDirector();
        _world.AddTurnDriver();
        _world.Activate();
        yield return null;

        for (int heroTurns = 0; heroTurns < 2; heroTurns++)
        {
            float giveUp = Time.time + 10f;
            while (_world.Game.Controller.Turns.Current != _world.Hero && Time.time < giveUp)
            {
                yield return null;
            }
            Assert.That(_world.Game.Controller.Turns.Current, Is.SameAs(_world.Hero), $"Hero turn {heroTurns + 1} never came");
            _world.Game.Controller.EndTurn();
            yield return null;
        }
    }

    //////////////////////////////////////////
    // ---------- The movement overlay ---------- //
    //////////////////////////////////////////

    [UnityTest]
    public IEnumerator TheOverlay_HidesWhileTheBodyWalks_AndComesBackWhenItArrives()
    {
        _world = new TestWorld().WithArena()
                                .WithFight(new Vector3Int(7, 0, 7), new Vector3Int(14, 0, 14))
                                .WithDirector();
        MovementOverlay overlay = _world.Add<MovementOverlay>("Movement Overlay");
        TestWorld.SetObject(overlay, "_game", _world.Game);
        TestWorld.SetObject(overlay, "_director", _world.Director);
        _world.Activate();
        yield return null;
        _world.PassToHero();
        yield return null;
        Mesh mesh = overlay.transform.Find("Overlay Mesh").GetComponent<MeshFilter>().sharedMesh;
        Assume.That(mesh.vertexCount, Is.GreaterThan(0));

        _world.Game.Controller.TryMove(_world.Hero, P(7, 0, 10));     // 3 squares: 15 ft left after
        yield return null;
        Assert.That(mesh.vertexCount, Is.EqualTo(0), "Hidden while walking");

        yield return _world.WaitUntilIdle();
        yield return null;
        Assert.That(mesh.vertexCount, Is.GreaterThan(0), "Back once the body has arrived");
    }
}
#endif
