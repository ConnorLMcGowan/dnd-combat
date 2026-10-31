using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;
using Grid = DnDCombat.Core.Grid;

public class CombatantViewTest
{
    // Float maths is never exact, so we compare "close enough" (within 0.001)
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.001f);

    // Everything the tests create, so TearDown can clean it all up
    private readonly List<GameObject> _spawned = new List<GameObject>();

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    private static Combatant MakeFighter()
    {
        return new Combatant(StatBlocks.Fighter(), Team.Heroes);
    }

    private CombatantView MakeView()
    {
        GameObject go = new GameObject("TestView");
        _spawned.Add(go);
        return go.AddComponent<CombatantView>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in _spawned)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        _spawned.Clear();
    }

    //////////////////////////////////
    // ---------- Binding ---------- //
    //////////////////////////////////

    [Test]
    public void Bind_StoresTheCombatant()
    {
        Combatant fighter = MakeFighter();
        CombatantView view = MakeView();

        view.Bind(fighter, new Grid(5, 2, 5), new GridSpace(Vector3.zero, 1f));

        Assert.That(view.Combatant, Is.SameAs(fighter));
    }

    ////////////////////////////////////////
    // ---------- Snapping works ---------- //
    ////////////////////////////////////////

    [Test]
    public void SnapToGrid_PutsTheViewAtTheCentreOfItsCell()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant fighter = MakeFighter();
        grid.Place(fighter, P(2, 0, 3));
        CombatantView view = MakeView();
        view.Bind(fighter, grid, new GridSpace(Vector3.zero, 1f));

        bool snapped = view.SnapToGrid();

        Assert.That(snapped, Is.True);
        Assert.That(view.transform.position, Is.EqualTo(new Vector3(2.5f, 0.5f, 3.5f)).Using(Near));
    }

    [Test]
    public void SnapToGrid_UsesTheGridSpacesOrigin()
    {
        // A 15 by 15 arena centred on the world: cell (7, 0, 7) is the middle one
        Grid grid = new Grid(15, 2, 15);
        Combatant fighter = MakeFighter();
        grid.Place(fighter, P(7, 0, 7));
        CombatantView view = MakeView();
        view.Bind(fighter, grid, new GridSpace(new Vector3(-7.5f, 0f, -7.5f), 1f));

        view.SnapToGrid();

        Assert.That(view.transform.position, Is.EqualTo(new Vector3(0f, 0.5f, 0f)).Using(Near));
    }

    [Test]
    public void SnapToGrid_UsesTheGridSpacesCellSize()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant fighter = MakeFighter();
        grid.Place(fighter, P(1, 0, 1));
        CombatantView view = MakeView();
        view.Bind(fighter, grid, new GridSpace(Vector3.zero, 2f));

        view.SnapToGrid();

        Assert.That(view.transform.position, Is.EqualTo(new Vector3(3f, 1f, 3f)).Using(Near));   // (1 + 0.5) * 2
    }

    [Test]
    public void SnapToGrid_FollowsTheCoreWhenTheCombatantMoves()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant fighter = MakeFighter();
        grid.Place(fighter, P(1, 0, 1));
        CombatantView view = MakeView();
        view.Bind(fighter, grid, new GridSpace(Vector3.zero, 1f));
        view.SnapToGrid();
        Assert.That(view.transform.position, Is.EqualTo(new Vector3(1.5f, 0.5f, 1.5f)).Using(Near));

        grid.MoveTo(fighter, P(3, 0, 2));
        view.SnapToGrid();

        Assert.That(view.transform.position, Is.EqualTo(new Vector3(3.5f, 0.5f, 2.5f)).Using(Near));
    }

    ///////////////////////////////////////////
    // ---------- Snapping refused ---------- //
    ///////////////////////////////////////////

    [Test]
    public void SnapToGrid_BeforeBind_ReturnsFalse_AndDoesNotThrow()
    {
        CombatantView view = MakeView();
        bool snapped = true;

        Assert.DoesNotThrow(() => snapped = view.SnapToGrid());

        Assert.That(snapped, Is.False);
    }

    [Test]
    public void SnapToGrid_NullCombatant_ReturnsFalse_AndDoesNotThrow()
    {
        CombatantView view = MakeView();
        view.Bind(null, new Grid(5, 2, 5), new GridSpace(Vector3.zero, 1f));
        bool snapped = true;

        Assert.DoesNotThrow(() => snapped = view.SnapToGrid());

        Assert.That(snapped, Is.False);
    }

    [Test]
    public void SnapToGrid_CombatantNotOnTheGrid_ReturnsFalse_AndTheViewDoesNotMove()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant fighter = MakeFighter();   // never placed
        CombatantView view = MakeView();
        view.transform.position = new Vector3(9f, 9f, 9f);
        view.Bind(fighter, grid, new GridSpace(Vector3.zero, 1f));

        bool snapped = view.SnapToGrid();

        Assert.That(snapped, Is.False);
        Assert.That(view.transform.position, Is.EqualTo(new Vector3(9f, 9f, 9f)).Using(Near));
    }

    [Test]
    public void SnapToGrid_AfterTheCombatantIsRemoved_ReturnsFalse_AndStaysWhereItWas()
    {
        Grid grid = new Grid(5, 2, 5);
        Combatant fighter = MakeFighter();
        grid.Place(fighter, P(2, 0, 2));
        CombatantView view = MakeView();
        view.Bind(fighter, grid, new GridSpace(Vector3.zero, 1f));
        view.SnapToGrid();

        grid.Remove(fighter);
        bool snapped = view.SnapToGrid();

        Assert.That(snapped, Is.False);
        Assert.That(view.transform.position, Is.EqualTo(new Vector3(2.5f, 0.5f, 2.5f)).Using(Near));
    }
}