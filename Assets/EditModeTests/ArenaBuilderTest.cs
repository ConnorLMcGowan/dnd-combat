using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// ArenaBuilder makes the Core grid and the visuals that match it. The rule being tested
// throughout: the Core is the truth, and the scene only ever copies it.
public class ArenaBuilderTest
{
    private static readonly Vector3EqualityComparer Near = new Vector3EqualityComparer(0.01f);
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

    private static GridPosition P(int x, int y, int z) => new GridPosition(x, y, z);

    // An arena with the given size and no blocks (the default list is replaced)
    private ArenaBuilder MakeArena(int width = 15, int height = 3, int depth = 15, float cellSize = 1f, params Block[] blocks)
    {
        GameObject go = new GameObject("Test Arena");
        _made.Add(go);
        ArenaBuilder arena = go.AddComponent<ArenaBuilder>();
        SetInt(arena, "_width", width);
        SetInt(arena, "_height", height);
        SetInt(arena, "_depth", depth);
        SetFloat(arena, "_cellSize", cellSize);
        SetBlocks(arena, blocks);
        return arena;
    }

    private static Transform Child(ArenaBuilder arena, string name)
    {
        Transform found = arena.transform.Cast<Transform>().FirstOrDefault(t => t.name == name);
        Assert.That(found, Is.Not.Null, $"No child called '{name}'");
        return found;
    }

    private static int CountChildrenStartingWith(ArenaBuilder arena, string prefix)
    {
        return arena.transform.Cast<Transform>().Count(t => t.name.StartsWith(prefix));
    }

    // A model whose pivot is nowhere near its bottom, like many Asset Store models
    private GameObject MakeOffCentreModel()
    {
        GameObject root = new GameObject("Offset Model");
        _made.Add(root);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(3f, 5f, -2f);
        body.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
        return root;
    }

    // One box around every renderer in an object
    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    ///////////////////////////////////////
    // ---------- Grid and space ---------- //
    ///////////////////////////////////////

    [Test]
    public void Build_MakesAGridOfTheInspectorSize()
    {
        ArenaBuilder arena = MakeArena(12, 4, 9);

        arena.Build();

        Assert.That(arena.Grid.Width, Is.EqualTo(12));
        Assert.That(arena.Grid.Height, Is.EqualTo(4));
        Assert.That(arena.Grid.Depth, Is.EqualTo(9));
    }

    [Test]
    public void Build_CentresTheArenaOnTheWorldOrigin()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15);

        arena.Build();

        Assert.That(arena.Space.Origin, Is.EqualTo(new Vector3(-7.5f, 0f, -7.5f)).Using(Near));
        Assert.That(arena.Space.ToWorld(P(7, 0, 7)), Is.EqualTo(new Vector3(0f, 0.5f, 0f)).Using(Near));
    }

    [Test]
    public void Build_UsesTheCellSize()
    {
        ArenaBuilder arena = MakeArena(10, 3, 6, 2f);

        arena.Build();

        Assert.That(arena.Space.CellSize, Is.EqualTo(2f));
        Assert.That(arena.Space.Origin, Is.EqualTo(new Vector3(-10f, 0f, -6f)).Using(Near));   // half of 20 and 12
    }

    [Test]
    public void Build_WithNoBlocks_LeavesEveryGroundCellStandable()
    {
        ArenaBuilder arena = MakeArena(5, 2, 5);

        arena.Build();

        for (int x = 0; x < 5; x++)
            for (int z = 0; z < 5; z++)
                Assert.That(arena.Grid.IsStandable(P(x, 0, z)), Is.True, $"({x}, 0, {z})");
    }

    /////////////////////////////////
    // ---------- Floor ---------- //
    /////////////////////////////////

    [Test]
    public void Floor_CoversTheWholeGrid_WithItsTopAtGroundLevel()
    {
        ArenaBuilder arena = MakeArena(15, 3, 10);

        arena.Build();

        Bounds floor = Child(arena, "Floor").GetComponent<Renderer>().bounds;
        Assert.That(floor.max.y, Is.EqualTo(0f).Within(0.001f), "The top of the floor should be y = 0, where layer 0 stands");
        Assert.That(floor.min.x, Is.EqualTo(arena.Space.Origin.x).Within(0.001f));
        Assert.That(floor.max.x, Is.EqualTo(arena.Space.Origin.x + 15f).Within(0.001f));
        Assert.That(floor.min.z, Is.EqualTo(arena.Space.Origin.z).Within(0.001f));
        Assert.That(floor.max.z, Is.EqualTo(arena.Space.Origin.z + 10f).Within(0.001f));
    }

    [Test]
    public void Floor_HasACollider_SoClicksAndHoversHitIt()
    {
        ArenaBuilder arena = MakeArena();

        arena.Build();

        Assert.That(Child(arena, "Floor").GetComponent<Collider>(), Is.Not.Null);
    }

    [Test]
    public void Floor_UsesTheFloorMaterial_WhenOneIsGiven()
    {
        ArenaBuilder arena = MakeArena();
        Material grass = new Material(Shader.Find("Sprites/Default"));
        _made.Add(grass);
        SetObject(arena, "_floorMaterial", grass);

        arena.Build();

        Assert.That(Child(arena, "Floor").GetComponent<Renderer>().sharedMaterial, Is.SameAs(grass));
    }

    //////////////////////////////////
    // ---------- Blocks ---------- //
    //////////////////////////////////

    [Test]
    public void Block_MakesTheCoreCellSolid()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6)));

        arena.Build();

        Assert.That(arena.Grid.IsSolid(P(4, 0, 6)), Is.True);
        Assert.That(arena.Grid.IsStandable(P(4, 0, 6)), Is.False);
        Assert.That(arena.Grid.IsStandable(P(4, 1, 6)), Is.True, "You can stand on top of a block");
    }

    [Test]
    public void Block_SpawnsANamedAnchor_AtTheCellsCentre()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6)));

        arena.Build();

        Transform anchor = Child(arena, "Block (4, 0, 6)");
        Assert.That(anchor.position, Is.EqualTo(arena.Space.ToWorld(P(4, 0, 6))).Using(Near));
    }

    [Test]
    public void BlockWithoutAPrefab_IsAOneCellGreyCube_WithACollider()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6)));

        arena.Build();

        Transform cube = Child(arena, "Block (4, 0, 6)").Find("Cube");
        Assert.That(cube, Is.Not.Null);
        Bounds bounds = cube.GetComponent<Renderer>().bounds;
        Assert.That(bounds.size, Is.EqualTo(Vector3.one).Using(Near));
        Assert.That(bounds.center, Is.EqualTo(arena.Space.ToWorld(P(4, 0, 6))).Using(Near));
        Assert.That(cube.GetComponent<Collider>(), Is.Not.Null, "Without a collider, clicks go straight through blocks");
    }

    [Test]
    public void BlockCube_FillsTheCell_AtABiggerCellSize()
    {
        ArenaBuilder arena = MakeArena(6, 3, 6, 2f, new Block(new Vector3Int(1, 0, 1)));

        arena.Build();

        Bounds bounds = Child(arena, "Block (1, 0, 1)").Find("Cube").GetComponent<Renderer>().bounds;
        Assert.That(bounds.size, Is.EqualTo(new Vector3(2f, 2f, 2f)).Using(Near));
    }

    [Test]
    public void BlockCube_UsesTheBlockMaterial_WhenOneIsGiven()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6)));
        Material stone = new Material(Shader.Find("Sprites/Default"));
        _made.Add(stone);
        SetObject(arena, "_blockMaterial", stone);

        arena.Build();

        Assert.That(Child(arena, "Block (4, 0, 6)").Find("Cube").GetComponent<Renderer>().sharedMaterial, Is.SameAs(stone));
    }

    [Test]
    public void StackedBlocks_MakeATallPillar()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f,
                                       new Block(new Vector3Int(9, 0, 3)),
                                       new Block(new Vector3Int(9, 1, 3)));

        arena.Build();

        Assert.That(arena.Grid.IsSolid(P(9, 0, 3)), Is.True);
        Assert.That(arena.Grid.IsSolid(P(9, 1, 3)), Is.True);
        Assert.That(arena.Grid.IsStandable(P(9, 2, 3)), Is.True);
        Assert.That(CountChildrenStartingWith(arena, "Block "), Is.EqualTo(2));
    }

    [Test]
    public void DecorationOnly_IsSpawned_ButTheCoreCellStaysOpen()
    {
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), decorationOnly: true));

        arena.Build();

        Assert.That(arena.Grid.IsSolid(P(4, 0, 6)), Is.False);
        Assert.That(arena.Grid.IsStandable(P(4, 0, 6)), Is.True);
        Assert.That(CountChildrenStartingWith(arena, "Block "), Is.EqualTo(1));
    }

    [Test]
    public void BlockOutsideTheGrid_IsSkippedWithAWarning_AndNothingIsSpawned()
    {
        ArenaBuilder arena = MakeArena(5, 2, 5, 1f,
                                       new Block(new Vector3Int(9, 0, 9)),
                                       new Block(new Vector3Int(-1, 0, 2)),
                                       new Block(new Vector3Int(2, 0, 2)));

        LogAssert.Expect(LogType.Warning, new Regex(@"\(9, 0, 9\).*outside the grid"));
        LogAssert.Expect(LogType.Warning, new Regex(@"\(-1, 0, 2\).*outside the grid"));
        arena.Build();

        Assert.That(CountChildrenStartingWith(arena, "Block "), Is.EqualTo(1), "Only the in-bounds block");
        Assert.That(arena.Grid.IsSolid(P(2, 0, 2)), Is.True, "One bad entry mustn't stop the rest");
    }

    ///////////////////////////////////////////
    // ---------- Prefab blocks ---------- //
    ///////////////////////////////////////////

    [Test]
    public void PrefabBlock_IsSpawnedUnderTheAnchor_InsteadOfACube()
    {
        GameObject model = MakeOffCentreModel();
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), model));

        arena.Build();

        Transform anchor = Child(arena, "Block (4, 0, 6)");
        Assert.That(anchor.Find("Cube"), Is.Null);
        Assert.That(anchor.Find(model.name), Is.Not.Null);
    }

    [Test]
    public void PrefabBlock_SitsOnTheCellFloor_WhateverItsPivot()
    {
        // The model's body is 3 across, 5 up and 2 back from its pivot. Fitting should put the
        // bottom-centre of what you can see on the middle of the cell's floor anyway.
        GameObject model = MakeOffCentreModel();
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), model));

        arena.Build();

        GameObject visual = Child(arena, "Block (4, 0, 6)").Find(model.name).gameObject;
        Bounds bounds = BoundsOf(visual);
        Vector3 floorCentre = arena.Space.ToWorldFloorCentre(P(4, 0, 6));
        Assert.That(bounds.min.y, Is.EqualTo(floorCentre.y).Within(0.01f));
        Assert.That(bounds.center.x, Is.EqualTo(floorCentre.x).Within(0.01f));
        Assert.That(bounds.center.z, Is.EqualTo(floorCentre.z).Within(0.01f));
    }

    [Test]
    public void PrefabBlock_Scale_ResizesTheModel_ThenStillSitsOnTheFloor()
    {
        GameObject model = MakeOffCentreModel();
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), model, scale: 2f));

        arena.Build();

        GameObject visual = Child(arena, "Block (4, 0, 6)").Find(model.name).gameObject;
        Bounds bounds = BoundsOf(visual);
        Assert.That(bounds.size.y, Is.EqualTo(4f).Within(0.01f), "The 2-high body doubled");
        Assert.That(bounds.min.y, Is.EqualTo(arena.Space.ToWorldFloorCentre(P(4, 0, 6)).y).Within(0.01f));
    }

    [Test]
    public void PrefabBlock_ZeroScale_LeavesTheSizeAlone()
    {
        GameObject model = MakeOffCentreModel();
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), model, scale: 0f));

        arena.Build();

        GameObject visual = Child(arena, "Block (4, 0, 6)").Find(model.name).gameObject;
        Assert.That(BoundsOf(visual).size.y, Is.EqualTo(2f).Within(0.01f));
    }

    [Test]
    public void PrefabBlock_DoesNotChangeTheOriginalPrefab()
    {
        GameObject model = MakeOffCentreModel();
        Vector3 before = model.transform.position;
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), model, scale: 3f));

        arena.Build();

        Assert.That(model.transform.position, Is.EqualTo(before).Using(Near));
        Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one).Using(Near));
    }

    [Test]
    public void PrefabWithNothingVisible_IsStillSpawned_WithoutAnError()
    {
        GameObject empty = new GameObject("Empty Marker");
        _made.Add(empty);
        ArenaBuilder arena = MakeArena(15, 3, 15, 1f, new Block(new Vector3Int(4, 0, 6), empty));

        Assert.DoesNotThrow(() => arena.Build());
        Assert.That(Child(arena, "Block (4, 0, 6)").Find("Empty Marker"), Is.Not.Null);
    }
}
