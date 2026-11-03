using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// Checks the real game scene is wired up: every Inspector slot that the scripts need is filled,
// and the Spawns list describes a fight that can actually start. These catch the mistakes that
// compile fine but break the moment you press Play.
//
// The scene is only READ. If it's already open (with or without unsaved changes) the open copy
// is checked. Otherwise it's opened alongside and closed again afterwards.
public class SceneSetupTest
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private Scene _scene;
    private bool _openedByTest;

    [OneTimeSetUp]
    public void OpenScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.path == ScenePath && loaded.isLoaded)
            {
                _scene = loaded;
                return;
            }
        }

        _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        _openedByTest = true;
    }

    [OneTimeTearDown]
    public void CloseScene()
    {
        if (_openedByTest && _scene.IsValid())
        {
            EditorSceneManager.CloseScene(_scene, true);
        }
    }

    private List<T> FindInScene<T>() where T : Component
    {
        return _scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToList();
    }

    private T FindOne<T>() where T : Component
    {
        List<T> found = FindInScene<T>();
        Assert.That(found.Count, Is.EqualTo(1), $"Expected exactly one {typeof(T).Name} in {ScenePath}, found {found.Count}");
        return found[0];
    }

    private struct SpawnRow
    {
        public CreatureDefinition Definition;
        public Team Team;
        public Vector3Int Cell;
    }

    private List<SpawnRow> ReadSpawns(GameManager game)
    {
        SerializedProperty list = GetField(game, "_spawns");
        List<SpawnRow> rows = new List<SpawnRow>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            rows.Add(new SpawnRow
            {
                Definition = entry.FindPropertyRelative("Definition").objectReferenceValue as CreatureDefinition,
                Team = (Team)entry.FindPropertyRelative("Team").intValue,
                Cell = entry.FindPropertyRelative("Cell").vector3IntValue
            });
        }
        return rows;
    }

    ///////////////////////////////////////////
    // ---------- The scene exists ---------- //
    ///////////////////////////////////////////

    [Test]
    public void SceneIsInTheBuild()
    {
        Assert.That(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), Is.True,
                    "Add SampleScene to File > Build Profiles, or a build will start with no scene");
    }

    [Test]
    public void HasAMainCamera()
    {
        // ClickInput and PathPreview fall back to the Main Camera when their slot is empty
        Assert.That(FindInScene<Camera>().Any(c => c.CompareTag("MainCamera")), Is.True);
    }

    ////////////////////////////////////////
    // ---------- GameManager ---------- //
    ////////////////////////////////////////

    [Test]
    public void GameManager_HasAnArena()
    {
        GameManager game = FindOne<GameManager>();

        Assert.That(GetField(game, "_arena").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void EverySpawn_HasADefinition_WithABody()
    {
        List<SpawnRow> spawns = ReadSpawns(FindOne<GameManager>());

        Assert.That(spawns, Is.Not.Empty);
        for (int i = 0; i < spawns.Count; i++)
        {
            Assert.That(spawns[i].Definition, Is.Not.Null, $"Spawn {i} has no Definition");
            Assert.That(spawns[i].Definition.ViewPrefab, Is.Not.Null, $"Spawn {i} ({spawns[i].Definition.DisplayName}) has no View Prefab");
        }
    }

    [Test]
    public void TheFightHasBothSides()
    {
        // With only one side, combat ends the moment it starts
        List<SpawnRow> spawns = ReadSpawns(FindOne<GameManager>());

        Assert.That(spawns.Any(s => s.Team == Team.Heroes), Is.True, "No heroes in the Spawns list");
        Assert.That(spawns.Any(s => s.Team == Team.Enemies), Is.True, "No enemies in the Spawns list");
    }

    [Test]
    public void EverySpawn_StartsOnItsOwnFreeSquare_InTheRealArena()
    {
        // Builds a throwaway copy of the scene's arena, with its real blocks, and checks every
        // start square could actually be stood on. The scene's own arena is left alone.
        GameManager game = FindOne<GameManager>();
        ArenaBuilder sceneArena = GetField(game, "_arena").objectReferenceValue as ArenaBuilder;
        Assume.That(sceneArena, Is.Not.Null);

        GameObject copy = Object.Instantiate(sceneArena.gameObject);
        try
        {
            ArenaBuilder arena = copy.GetComponent<ArenaBuilder>();
            arena.Build();

            HashSet<Vector3Int> used = new HashSet<Vector3Int>();
            foreach (SpawnRow spawn in ReadSpawns(game))
            {
                GridPosition cell = new GridPosition(spawn.Cell.x, spawn.Cell.y, spawn.Cell.z);
                string who = spawn.Definition != null ? spawn.Definition.DisplayName : "?";
                Assert.That(arena.Grid.InBounds(cell), Is.True, $"{who} starts outside the arena at {cell}");
                Assert.That(arena.Grid.IsStandable(cell), Is.True, $"{who} starts inside a block or in mid-air at {cell}");
                Assert.That(used.Add(spawn.Cell), Is.True, $"Two creatures start on {cell}");
            }
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }

    [Test]
    public void EveryArenaBlock_IsInsideTheArena()
    {
        ArenaBuilder arena = FindOne<ArenaBuilder>();
        int width = GetField(arena, "_width").intValue;
        int height = GetField(arena, "_height").intValue;
        int depth = GetField(arena, "_depth").intValue;
        SerializedProperty blocks = GetField(arena, "_blocks");

        for (int i = 0; i < blocks.arraySize; i++)
        {
            Vector3Int cell = blocks.GetArrayElementAtIndex(i).FindPropertyRelative("Cell").vector3IntValue;
            bool inside = cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height && cell.z >= 0 && cell.z < depth;
            Assert.That(inside, Is.True, $"Block {i} at {cell} is outside the {width} x {height} x {depth} arena");
        }
    }

    [Test]
    public void Arena_HasAPositiveSizeAndCellSize()
    {
        ArenaBuilder arena = FindOne<ArenaBuilder>();

        Assert.That(GetField(arena, "_width").intValue, Is.GreaterThan(0));
        Assert.That(GetField(arena, "_height").intValue, Is.GreaterThan(1), "Height 1 leaves no room for blocks to stand on");
        Assert.That(GetField(arena, "_depth").intValue, Is.GreaterThan(0));
        Assert.That(GetField(arena, "_cellSize").floatValue, Is.GreaterThan(0f));
    }

    //////////////////////////////////////////////////
    // ---------- Everything points at the game ---------- //
    //////////////////////////////////////////////////

    [Test]
    public void ClickInput_IsLinkedToTheGameManager()
    {
        Assert.That(GetField(FindOne<ClickInput>(), "_game").objectReferenceValue, Is.SameAs(FindOne<GameManager>()));
    }

    [Test]
    public void BodyDirector_IsLinkedToTheGameManager()
    {
        // Without it, bodies never move or turn: the Core moves creatures, but nothing shows it
        Assert.That(GetField(FindOne<BodyDirector>(), "_game").objectReferenceValue, Is.SameAs(FindOne<GameManager>()));
    }

    [Test]
    public void MonsterTurnDriver_IsLinkedToTheGameManager()
    {
        // Without it, the game stalls the first time it's an enemy's turn
        Assert.That(GetField(FindOne<MonsterTurnDriver>(), "_game").objectReferenceValue, Is.SameAs(FindOne<GameManager>()));
    }

    [Test]
    public void MovementOverlay_IsLinkedToTheGameManager()
    {
        Assert.That(GetField(FindOne<MovementOverlay>(), "_game").objectReferenceValue, Is.SameAs(FindOne<GameManager>()));
    }

    [Test]
    public void PathPreview_IsLinkedToTheGameManager_AndHasALineRenderer()
    {
        PathPreview preview = FindOne<PathPreview>();

        Assert.That(GetField(preview, "_game").objectReferenceValue, Is.SameAs(FindOne<GameManager>()));
        Assert.That(preview.GetComponent<LineRenderer>() != null, Is.True);
    }

    [Test]
    public void GlowSettings_AreSensible()
    {
        // Catches a zero or negative slipping into the Inspector, which would hide the effect
        MovementOverlay overlay = FindOne<MovementOverlay>();
        Assert.That(GetField(overlay, "_outlineWidth").floatValue, Is.GreaterThan(0f), "Overlay outline width");
        Assert.That(GetField(overlay, "_widthScale").floatValue, Is.GreaterThan(0f), "Overlay width scale");
        Assert.That(GetField(overlay, "_coreWidth").floatValue, Is.InRange(0f, 1f), "Overlay core width");

        PathPreview preview = FindOne<PathPreview>();
        Assert.That(GetField(preview, "_width").floatValue, Is.GreaterThan(0f), "Path width");
        Assert.That(GetField(preview, "_glowSize").floatValue, Is.GreaterThan(0f), "Glow size");
        Assert.That(GetField(preview, "_lineCoreWidth").floatValue, Is.InRange(0f, 1f), "Path core width");
        Assert.That(GetField(preview, "_glowHeight").floatValue, Is.LessThan(GetField(preview, "_heightAboveFloor").floatValue),
                    "The glow should sit under the path line");
    }
}
