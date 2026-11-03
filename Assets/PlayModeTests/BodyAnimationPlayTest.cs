#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;

// The BodyDirector's real, timed show in a running scene: walking along the path, turning,
// swinging, and the Animator parameters it sets along the way. Moves at 8 squares a second
// (twice the game's speed) so the tests stay quick.
//
// The arena is 15 x 15 and centred, so world x and z = cell - 7 (cell (7, 0, 7) is at (0, 0, 0)).
public class BodyAnimationPlayTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.02f);

    private TestWorld _world;

    [TearDown]
    public void TearDown()
    {
        _world?.Destroy();
    }

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // The hero at (7, 0, 7), the goblin wherever the test wants, a director, and the hero's turn
    private IEnumerator StartWorld(Vector3Int goblinCell, int goblinArmourClass = 10, params Vector3Int[] blocks)
    {
        _world = new TestWorld().WithArena(blocks).WithAnimatedBodies()
                                .WithFight(new Vector3Int(7, 0, 7), goblinCell, goblinArmourClass)
                                .WithDirector();
        _world.Activate();

        yield return null;                 // Start runs: combat begins, everyone faces the nearest enemy
        _world.PassToHero();
        yield return null;
    }

    private CombatController Controller => _world.Game.Controller;
    private Transform Body(Combatant c) => _world.Game.ViewOf(c).transform;
    private Vector3 Floor(GridPosition cell) => _world.Game.Space.ToWorldFloorCentre(cell);

    ///////////////////////////////////
    // ---------- Walking ---------- //
    ///////////////////////////////////

    [UnityTest]
    public IEnumerator AMove_TakesTime_AndTheDirectorIsBusyUntilItEnds()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        Combatant hero = _world.Hero;

        Controller.TryMove(hero, P(7, 0, 11));
        Assert.That(_world.Director.IsBusy, Is.True, "Busy as soon as the move is reported");
        yield return null;

        Assert.That(Body(hero).position, Is.Not.EqualTo(Floor(P(7, 0, 11))).Using(Near), "Not there yet after one frame");
        yield return _world.WaitUntilIdle();
        Assert.That(Body(hero).position, Is.EqualTo(Floor(P(7, 0, 11))).Using(Near), "Ends exactly on the square");
    }

    [UnityTest]
    public IEnumerator AWalk_TakesAboutAsLongAsItsSquaresNeed()
    {
        // 4 squares at 8 a second is 0.5 s. Frames don't land exactly, so allow some slack.
        yield return StartWorld(new Vector3Int(14, 0, 14));
        float started = Time.time;

        Controller.TryMove(_world.Hero, P(7, 0, 11));
        yield return _world.WaitUntilIdle();

        Assert.That(Time.time - started, Is.InRange(0.4f, 1.2f));
    }

    [UnityTest]
    public IEnumerator AStraightWalk_StaysOnTheLine()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        Combatant hero = _world.Hero;

        Controller.TryMove(hero, P(7, 0, 11));
        while (_world.Director.IsBusy)
        {
            Assert.That(Body(hero).position.x, Is.EqualTo(0f).Within(0.01f), "Wandered sideways");
            Assert.That(Body(hero).position.y, Is.EqualTo(0f).Within(0.01f), "Left the floor");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator AWalk_FollowsThePath_AroundABlock_NotStraightThroughIt()
    {
        // A block at (7, 0, 9) sits right between the hero and (7, 0, 11)
        yield return StartWorld(new Vector3Int(14, 0, 14), 10, new Vector3Int(7, 0, 9));
        Combatant hero = _world.Hero;
        Vector3 block = Floor(P(7, 0, 9));

        Assume.That(Controller.TryMove(hero, P(7, 0, 11)).Success, Is.True);
        while (_world.Director.IsBusy)
        {
            Vector3 p = Body(hero).position;
            bool insideBlock = Mathf.Abs(p.x - block.x) < 0.4f && Mathf.Abs(p.z - block.z) < 0.4f;
            Assert.That(insideBlock, Is.False, $"Walked through the block at {p}");
            yield return null;
        }
        Assert.That(Body(hero).position, Is.EqualTo(Floor(P(7, 0, 11))).Using(Near));
    }

    [UnityTest]
    public IEnumerator AWalk_EndsFacingAlongItsLastStep()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        Combatant hero = _world.Hero;

        Controller.TryMove(hero, P(4, 0, 7));       // straight west
        yield return _world.WaitUntilIdle();

        Assert.That(Body(hero).forward, Is.EqualTo(Vector3.left).Using(Near));
        Assert.That(Body(hero).up, Is.EqualTo(Vector3.up).Using(Near), "Never tilts");
    }

    [UnityTest]
    public IEnumerator DuringAWalk_SpeedIsSet_AndItIsZeroAgainAfter()
    {
        yield return StartWorld(new Vector3Int(14, 0, 14));
        Combatant hero = _world.Hero;
        Animator animator = _world.AnimatorOf(hero);

        Controller.TryMove(hero, P(7, 0, 11));
        yield return null;
        yield return null;
        Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0f), "Running mid-walk");

        // Still running on the LAST square, not just the first (the run used to stop after one)
        while (_world.Director.IsBusy && Body(hero).position.z < Floor(P(7, 0, 10)).z + 0.2f)
        {
            yield return null;
        }
        if (_world.Director.IsBusy)
        {
            Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(0f), "Still running on the last square");
        }

        yield return _world.WaitUntilIdle();
        Assert.That(animator.GetFloat("Speed"), Is.EqualTo(0f), "Back to idle once it arrives");
    }

    ////////////////////////////////////
    // ---------- Attacking ---------- //
    ////////////////////////////////////

    [UnityTest]
    public IEnumerator AnAttack_TurnsTheAttacker_AndPlaysTheSwing()
    {
        yield return StartWorld(new Vector3Int(8, 0, 7));
        Combatant hero = _world.Hero;
        Body(hero).rotation = Quaternion.LookRotation(Vector3.left);      // facing away from the goblin
        _world.Game.TryGetAttack(hero, out AttackProfile club);

        Assume.That(Controller.TryAttack(hero, _world.Enemy, club).Success, Is.True);
        Assert.That(_world.Director.IsBusy, Is.True);
        yield return _world.WaitUntilIdle();

        Assert.That(Body(hero).forward, Is.EqualTo(Vector3.right).Using(Near), "Turned to face the goblin");
        Assert.That(_world.AnimatorOf(hero).GetBool("Attack"), Is.True, "The Attack trigger was set");
    }

    [UnityTest]
    public IEnumerator TheTarget_FlinchesOnAHit_AndNotOnAMiss()
    {
        yield return StartWorld(new Vector3Int(8, 0, 7));
        Combatant hero = _world.Hero;
        _world.Game.TryGetAttack(hero, out AttackProfile club);
        bool hit = false;
        Controller.AttackResolved += report => hit = report.Result.Hit;

        Controller.TryAttack(hero, _world.Enemy, club);
        yield return _world.WaitUntilIdle();

        // Whichever way the dice went, the reaction must match it
        Assert.That(_world.AnimatorOf(_world.Enemy).GetBool("Hit"), Is.EqualTo(hit));
        Assert.That(_world.AnimatorOf(_world.Enemy).GetBool("Dead"), Is.False, "999 HP: still standing");
    }

    [UnityTest]
    public IEnumerator AKillingBlow_SetsDead_NotHit()
    {
        // AC 1 and 1 HP: only a natural 1 could save the goblin
        yield return StartWorld(new Vector3Int(8, 0, 7), goblinArmourClass: 1);
        Combatant hero = _world.Hero;
        _world.Enemy.Creature.TakeDamage(_world.Enemy.Creature.MaxHP - 1);
        _world.Game.TryGetAttack(hero, out AttackProfile club);
        bool defeated = false;
        Controller.AttackResolved += report => defeated = report.TargetDefeated;

        Controller.TryAttack(hero, _world.Enemy, club);
        Assume.That(defeated, Is.True, "This seed rolled a natural 1");
        yield return _world.WaitUntilIdle();

        Animator goblin = _world.AnimatorOf(_world.Enemy);
        Assert.That(goblin.GetBool("Dead"), Is.True);
        Assert.That(goblin.GetBool("Hit"), Is.False, "Dying replaces the flinch");
    }

    [UnityTest]
    public IEnumerator AMoveThenAnAttack_PlayInOrder()
    {
        // Both are reported in the same instant, but the swing must wait for the walk
        yield return StartWorld(new Vector3Int(9, 0, 7));
        Combatant hero = _world.Hero;
        _world.Game.TryGetAttack(hero, out AttackProfile club);
        Animator animator = _world.AnimatorOf(hero);

        Controller.TryMove(hero, P(8, 0, 8));
        Assume.That(Controller.TryAttack(hero, _world.Enemy, club).Success, Is.True);

        bool swungEarly = false;
        while (Vector3.Distance(Body(hero).position, Floor(P(8, 0, 8))) > 0.05f && _world.Director.IsBusy)
        {
            swungEarly |= animator.GetBool("Attack");
            yield return null;
        }
        Assert.That(swungEarly, Is.False, "The swing started before the walk finished");

        yield return _world.WaitUntilIdle();
        Assert.That(animator.GetBool("Attack"), Is.True);
        Vector3 toGoblin = Floor(P(9, 0, 7)) - Floor(P(8, 0, 8));
        Assert.That(Body(hero).forward, Is.EqualTo(toGoblin.normalized).Using(Near), "Facing the goblin, not the way it walked");
    }

    ///////////////////////////////////////
    // ---------- Without models ---------- //
    ///////////////////////////////////////

    [UnityTest]
    public IEnumerator CreaturesWithNoAnimator_StillWalk_WithoutErrors()
    {
        // The grey capsules have no Animator at all. Any error logged here fails the test.
        _world = new TestWorld().WithArena().WithFight(new Vector3Int(7, 0, 7), new Vector3Int(14, 0, 14)).WithDirector();
        _world.Activate();
        yield return null;
        _world.PassToHero();

        _world.Game.Controller.TryMove(_world.Hero, P(7, 0, 9));
        yield return _world.WaitUntilIdle();

        Assert.That(Body(_world.Hero).position, Is.EqualTo(Floor(P(7, 0, 9))).Using(Near));
    }
}
#endif
