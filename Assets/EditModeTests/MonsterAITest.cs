using NUnit.Framework;
using DnDCombat.Core;

public class MonsterAITest
{
    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // d20 of 14 plus the +4 bonus is 18, which beats the default AC of 10.
    // Damage is the roll plus 2, so a damage roll of 4 deals 6.
    private static readonly AttackProfile Shortsword = new AttackProfile("Shortsword", 4, 1, 6, 2, DamageType.Piercing);
    private static readonly AttackProfile Spear = new AttackProfile("Spear", 4, 1, 6, 2, DamageType.Piercing, 2);

    private static Combatant Make(string name, Team team, int maxHP = 10, int attacksPerAction = 1)
    {
        return new Combatant(new Creature(name, maxHP: maxHP, speed: 30, attacksPerAction: attacksPerAction), team);
    }

    // The first combatant listed rolls the first initiative number, and so on, so the highest
    // number decides who goes first. Anything queued after one number per combatant is used
    // by attacks: a d20, then a damage roll.
    private static CombatController Build(FakeDice dice, Grid grid, params Combatant[] combatants)
    {
        TurnManager turns = new TurnManager(dice, combatants);
        turns.StartCombat();
        return new CombatController(turns, grid, new AttackResolver(dice));
    }

    private static GridPosition Where(Grid grid, Combatant combatant)
    {
        grid.TryGetPosition(combatant, out GridPosition position);
        return position;
    }

    // A column of solid cells, heightCells tall, starting at the ground
    private static void Pillar(Grid grid, int x, int z, int heightCells)
    {
        for (int y = 0; y < heightCells; y++)
        {
            grid.SetSolid(P(x, y, z), true);
        }
    }

    /////////////////////////////////////
    // ---------- Targeting ---------- //
    /////////////////////////////////////

    [Test]
    public void AdjacentHero_IsAttacked_WithoutMoving()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(4));
        Assert.That(Where(grid, skeleton), Is.EqualTo(P(7, 0, 7)));
        Assert.That(dice.Remaining, Is.EqualTo(0));
    }

    [Test]
    public void TwoHeroes_TheNearerOneIsAttacked()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant near = Make("Near", Team.Heroes);
        Combatant far = Make("Far", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(near, P(8, 0, 7));
        grid.Place(far, P(11, 0, 7));
        FakeDice dice = new FakeDice(20, 10, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, near, far);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(near.Creature.CurrentHP, Is.EqualTo(4));
        Assert.That(far.Creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void EqualDistance_TheWeakerHeroIsAttacked_AndCombatContinues()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant sturdy = Make("Sturdy", Team.Heroes, maxHP: 10);
        Combatant weak = Make("Weak", Team.Heroes, maxHP: 5);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(sturdy, P(8, 0, 7));
        grid.Place(weak, P(6, 0, 7));
        FakeDice dice = new FakeDice(20, 10, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, sturdy, weak);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(weak.Creature.CurrentHP, Is.EqualTo(0));        // 5 - 6, stops at 0
        Assert.That(sturdy.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(controller.Turns.State, Is.EqualTo(CombatState.Ongoing));
    }

    [Test]
    public void FullTie_TheFirstInTurnOrderIsAttacked()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant first = Make("First", Team.Heroes);
        Combatant second = Make("Second", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(first, P(8, 0, 7));       // same distance, same HP
        grid.Place(second, P(6, 0, 7));
        FakeDice dice = new FakeDice(20, 12, 8, 14, 4);   // "First" rolls higher, so it is earlier in the order
        CombatController controller = Build(dice, grid, skeleton, first, second);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(first.Creature.CurrentHP, Is.EqualTo(4));
        Assert.That(second.Creature.CurrentHP, Is.EqualTo(10));
    }

    [Test]
    public void DefeatedHero_IsSkipped()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant fallen = Make("Fallen", Team.Heroes);
        Combatant alive = Make("Alive", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(fallen, P(8, 0, 7));      // nearer, but down
        grid.Place(alive, P(6, 0, 7));
        fallen.Creature.TakeDamage(10);
        FakeDice dice = new FakeDice(20, 10, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, fallen, alive);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(alive.Creature.CurrentHP, Is.EqualTo(4));
    }

    [Test]
    public void HeroNotOnTheGrid_IsNotTargeted_AndNothingHappens()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));    // the hero is in the fight but was never placed
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, skeleton, Shortsword));

        Assert.That(Where(grid, skeleton), Is.EqualTo(P(7, 0, 7)));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    //////////////////////////////////
    // ---------- Moving ---------- //
    //////////////////////////////////

    [Test]
    public void FarHero_SkeletonWalksItsFullSpeed_AndDoesNotAttack()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(3, 0, 7));
        grid.Place(hero, P(11, 0, 7));                      // 8 squares away
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        GridPosition now = Where(grid, skeleton);
        Assert.That(Grid.DistanceInSquares(P(3, 0, 7), now), Is.EqualTo(6));     // 30 ft, no more
        Assert.That(Grid.DistanceInSquares(now, P(11, 0, 7)), Is.EqualTo(2));    // 6 squares closer
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(2));                              // no attack dice used
    }

    [Test]
    public void NearbyHero_SkeletonWalksInAndAttacksInTheSameTurn()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(11, 0, 7));                      // 4 squares away
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        GridPosition now = Where(grid, skeleton);
        Assert.That(Grid.DistanceInSquares(P(7, 0, 7), now), Is.EqualTo(3));     // only as far as needed
        Assert.That(Grid.DistanceInSquares(now, P(11, 0, 7)), Is.EqualTo(1));    // adjacent
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(4));
    }

    [Test]
    public void SpearUser_StopsAtTheEdgeOfItsReach_ThenAttacks()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(10, 0, 7));                      // 3 squares away
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Spear);

        GridPosition now = Where(grid, skeleton);
        Assert.That(Grid.DistanceInSquares(P(7, 0, 7), now), Is.EqualTo(1));     // one step, then stops
        Assert.That(Grid.DistanceInSquares(now, P(10, 0, 7)), Is.EqualTo(2));    // exactly its reach
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(4));
    }

    [Test]
    public void SpearUser_AlreadyInReach_AttacksWithoutMoving()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(9, 0, 7));                       // 2 squares away
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Spear);

        Assert.That(Where(grid, skeleton), Is.EqualTo(P(7, 0, 7)));
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(4));
    }

    [Test]
    public void PartialMovement_UsesOnlyWhatIsLeft()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(3, 0, 7));
        grid.Place(hero, P(11, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);
        skeleton.MovementRemaining = 10;                    // two squares left

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        GridPosition now = Where(grid, skeleton);
        Assert.That(Grid.DistanceInSquares(P(3, 0, 7), now), Is.EqualTo(2));
        Assert.That(Grid.DistanceInSquares(now, P(11, 0, 7)), Is.EqualTo(6));
    }

    [Test]
    public void NoMovementLeft_StaysPut_AndDoesNotAttackFromAfar()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(3, 0, 7));
        grid.Place(hero, P(11, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);
        skeleton.MovementRemaining = 0;

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(Where(grid, skeleton), Is.EqualTo(P(3, 0, 7)));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    /////////////////////////////////////////////
    // ---------- Multiple attacks ---------- //
    /////////////////////////////////////////////

    [Test]
    public void TwoAttackMonster_AttacksTwiceAndNoMore()
    {
        Combatant ogre = Make("Ogre", Team.Enemies, attacksPerAction: 2);
        Combatant hero = Make("Hero", Team.Heroes, maxHP: 20);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(ogre, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 3, 14, 3);
        CombatController controller = Build(dice, grid, ogre, hero);

        MonsterAI.TakeTurn(controller, ogre, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));                    // 5 + 5 damage
        Assert.That(dice.Remaining, Is.EqualTo(0));
    }

    [Test]
    public void TwoAttackMonster_WalksInThenAttacksTwice()
    {
        Combatant ogre = Make("Ogre", Team.Enemies, attacksPerAction: 2);
        Combatant hero = Make("Hero", Team.Heroes, maxHP: 20);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(ogre, P(7, 0, 7));
        grid.Place(hero, P(10, 0, 7));                      // 3 squares away
        FakeDice dice = new FakeDice(20, 5, 14, 3, 14, 3);
        CombatController controller = Build(dice, grid, ogre, hero);

        MonsterAI.TakeTurn(controller, ogre, Shortsword);

        Assert.That(Grid.DistanceInSquares(Where(grid, ogre), P(10, 0, 7)), Is.EqualTo(1));
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(0));
    }

    ///////////////////////////////////////
    // ---------- Ending combat ---------- //
    ///////////////////////////////////////

    [Test]
    public void KillingTheLastHero_EndsCombat_AndStopsTheSecondAttack()
    {
        Combatant ogre = Make("Ogre", Team.Enemies, attacksPerAction: 2);
        Combatant hero = Make("Hero", Team.Heroes, maxHP: 5);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(ogre, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 6, 14, 6);
        CombatController controller = Build(dice, grid, ogre, hero);

        MonsterAI.TakeTurn(controller, ogre, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(0));
        Assert.That(controller.Turns.State, Is.EqualTo(CombatState.EnemiesWon));
        Assert.That(controller.Turns.Current, Is.Null);
        Assert.That(dice.Remaining, Is.EqualTo(2));                              // the second attack never rolled
    }

    [Test]
    public void TakeTurn_DoesNotEndTheTurn()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        CombatController controller = Build(new FakeDice(20, 5, 14, 4), grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(controller.Turns.Current, Is.SameAs(skeleton));
    }

    [Test]
    public void CallingTwiceInOneTurn_DoesNotAttackTwice()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);
        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(4));                     // only one attack landed
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    /////////////////////////////////////
    // ---------- Obstacles ---------- //
    /////////////////////////////////////

    [Test]
    public void WalledInSkeleton_DoesNotMove_AndDoesNotThrow()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(5, 3, 5);
        grid.Place(skeleton, P(2, 0, 2));
        grid.Place(hero, P(4, 0, 4));
        for (int x = 1; x <= 3; x++)
        {
            for (int z = 1; z <= 3; z++)
            {
                if (x == 2 && z == 2) continue;
                Pillar(grid, x, z, 2);
            }
        }
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, skeleton, Shortsword));

        Assert.That(Where(grid, skeleton), Is.EqualTo(P(2, 0, 2)));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void WallBetween_SkeletonStopsAtTheWall_AndDoesNotHang()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(9, 3, 3);
        for (int z = 0; z < 3; z++) Pillar(grid, 4, z, 2);       // a full-width wall, two blocks high
        grid.Place(skeleton, P(0, 0, 1));
        grid.Place(hero, P(8, 0, 1));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, skeleton, Shortsword));

        // Known limit of this simple AI: it walks as close as it can in a straight line and waits there
        Assert.That(Where(grid, skeleton).X, Is.EqualTo(3));
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void HeroOnATallLedge_SkeletonGetsAsCloseAsItCan_WithoutAttacking()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(9, 5, 5);
        Pillar(grid, 5, 2, 2);                                   // a ledge two blocks high
        grid.Place(skeleton, P(1, 0, 2));
        grid.Place(hero, P(5, 2, 2));                            // standing on top of it
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        // Two squares away is the closest it can get: the ledge is too high to climb or to hit across
        Assert.That(Grid.DistanceInSquares(Where(grid, skeleton), P(5, 2, 2)), Is.EqualTo(2));
        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    ///////////////////////////////////
    // ---------- Guards ---------- //
    ///////////////////////////////////

    [Test]
    public void NotItsTurn_DoesNothing()
    {
        Combatant hero = Make("Hero", Team.Heroes);
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(hero, P(7, 0, 7));
        grid.Place(skeleton, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);              // the hero rolls highest, so it goes first
        CombatController controller = Build(dice, grid, hero, skeleton);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(Where(grid, skeleton), Is.EqualTo(P(8, 0, 7)));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void NullArguments_DoNothing_AndDoNotThrow()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(null, skeleton, Shortsword));
        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, null, Shortsword));

        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void DefeatedMonster_DoesNothing()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);
        skeleton.Creature.TakeDamage(10);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void UnplacedMonster_DoesNothing()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);
        grid.Remove(skeleton);

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, skeleton, Shortsword));

        Assert.That(hero.Creature.CurrentHP, Is.EqualTo(10));
        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    [Test]
    public void AfterCombatHasEnded_DoesNothing()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(7, 0, 7));
        grid.Place(hero, P(8, 0, 7));
        FakeDice dice = new FakeDice(20, 5, 14, 4);
        CombatController controller = Build(dice, grid, skeleton, hero);
        hero.Creature.TakeDamage(10);
        controller.Turns.CheckCombatEnd();                      // the fight is over: nobody has a turn

        Assert.DoesNotThrow(() => MonsterAI.TakeTurn(controller, skeleton, Shortsword));

        Assert.That(dice.Remaining, Is.EqualTo(2));
    }

    ///////////////////////////////////////////
    // ---------- Repeatability ---------- //
    ///////////////////////////////////////////

    private static GridPosition RunTheFarHeroScenario()
    {
        Combatant skeleton = Make("Skeleton", Team.Enemies);
        Combatant hero = Make("Hero", Team.Heroes);
        Grid grid = new Grid(15, 2, 15);
        grid.Place(skeleton, P(3, 0, 7));
        grid.Place(hero, P(11, 0, 7));
        CombatController controller = Build(new FakeDice(20, 5, 14, 4), grid, skeleton, hero);

        MonsterAI.TakeTurn(controller, skeleton, Shortsword);

        return Where(grid, skeleton);
    }

    [Test]
    public void TiedSquares_ResolveToTheLowestXThenLowestZ()
    {
        // Several squares are equally close and equally far to walk to: x = 9 with z from 5 to 9.
        // The final tie-break picks the lowest Z.
        Assert.That(RunTheFarHeroScenario(), Is.EqualTo(P(9, 0, 5)));
    }

    [Test]
    public void SameScenarioTwice_GivesTheSameResult()
    {
        Assert.That(RunTheFarHeroScenario(), Is.EqualTo(RunTheFarHeroScenario()));
    }
}