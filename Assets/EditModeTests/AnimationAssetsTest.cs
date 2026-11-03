using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using DnDCombat.Unity;

// Checks the animation ASSETS: the shared Creature Base controller and every creature's
// prefab. CombatantView and BodyDirector only ever set four parameter names, so if a controller
// is missing one, or set up so it fights the code, nothing fails loudly. These catch it here.
public class AnimationAssetsTest
{
    private const string BasePath = "Assets/Creature Base.controller";

    private static AnimatorController Base()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
        Assert.That(controller, Is.Not.Null, $"No Animator Controller at {BasePath}");
        return controller;
    }

    private static AnimatorStateMachine Machine() => Base().layers[0].stateMachine;

    private static AnimatorState State(string name)
    {
        AnimatorState state = Machine().states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        Assert.That(state, Is.Not.Null, $"Creature Base has no '{name}' state");
        return state;
    }

    // Every CreatureDefinition asset in the project
    private static IEnumerable<string> AllDefinitionPaths()
    {
        return AssetDatabase.FindAssets("t:CreatureDefinition")
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .OrderBy(path => path);
    }

    private static Animator AnimatorFor(string definitionPath)
    {
        CreatureDefinition definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath);
        Assume.That(definition != null && definition.ViewPrefab != null, "Covered by CreatureAssetsTest");
        Animator animator = definition.ViewPrefab.GetComponentInChildren<Animator>(true);
        Assert.That(animator != null, Is.True, $"{definition.ViewPrefab.name} has no Animator, so it would slide about without animating");
        return animator;
    }

    // The clip a creature actually plays for one of Creature Base's states, after its override
    private static AnimationClip ClipFor(Animator animator, string stateName)
    {
        AnimationClip original = State(stateName).motion as AnimationClip;
        Assert.That(original, Is.Not.Null, $"Creature Base's '{stateName}' state has no clip");

        if (animator.runtimeAnimatorController is AnimatorOverrideController overrides)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            foreach (KeyValuePair<AnimationClip, AnimationClip> pair in pairs)
            {
                if (pair.Key == original && pair.Value != null) return pair.Value;
            }
        }
        return original;
    }

    ////////////////////////////////////////////
    // ---------- The base controller ---------- //
    ////////////////////////////////////////////

    [TestCase("Speed", AnimatorControllerParameterType.Float)]
    [TestCase("Attack", AnimatorControllerParameterType.Trigger)]
    [TestCase("Hit", AnimatorControllerParameterType.Trigger)]
    [TestCase("Dead", AnimatorControllerParameterType.Bool)]
    public void CreatureBase_HasTheParameterTheCodeSets(string name, AnimatorControllerParameterType type)
    {
        // CombatantView sets these by name. A typo here means that animation silently never plays.
        AnimatorControllerParameter parameter = Base().parameters.FirstOrDefault(p => p.name == name);

        Assert.That(parameter, Is.Not.Null, $"Creature Base has no '{name}' parameter");
        Assert.That(parameter.type, Is.EqualTo(type), $"'{name}' is the wrong kind of parameter");
    }

    [Test]
    public void CreatureBase_HasEveryState_AndStartsInIdle()
    {
        foreach (string name in new[] { "Idle", "Move", "Attack", "Hit", "Dead" })
        {
            State(name);
        }
        Assert.That(Machine().defaultState.name, Is.EqualTo("Idle"));
    }

    [Test]
    public void TheDeathTransition_CannotRestartItself()
    {
        // Dead stays true, so with "Can Transition To Self" ticked, Any State -> Dead fires again
        // every frame and the death animation never gets past its start. That was the death loop.
        AnimatorStateTransition toDead = Machine().anyStateTransitions
            .FirstOrDefault(t => t.conditions.Any(c => c.parameter == "Dead"));

        Assert.That(toDead, Is.Not.Null, "No Any State transition uses the Dead parameter");
        Assert.That(toDead.canTransitionToSelf, Is.False,
                    "Untick Can Transition To Self on Any State -> Dead, then File > Save Project");
    }

    [Test]
    public void TheDeadState_HasNoWayOut()
    {
        // Once down, a creature stays down
        Assert.That(State("Dead").transitions, Is.Empty);
    }

    [Test]
    public void StartingAndStoppingTheRun_ReactsStraightAway()
    {
        // With Has Exit Time on, the run would keep going until the clip finished, so the
        // body would arrive and keep running on the spot
        foreach (AnimatorStateTransition t in State("Idle").transitions.Concat(State("Move").transitions))
        {
            if (t.conditions.Any(c => c.parameter == "Speed"))
            {
                Assert.That(t.hasExitTime, Is.False, $"{t.GetDisplayName(null)} should have Has Exit Time off");
            }
        }
    }

    [Test]
    public void IdleAndMove_AreLinkedBothWays_BySpeed()
    {
        Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Move")
                    && t.conditions.Any(c => c.parameter == "Speed")), Is.True, "Idle -> Move on Speed");
        Assert.That(State("Move").transitions.Any(t => t.destinationState == State("Idle")
                    && t.conditions.Any(c => c.parameter == "Speed")), Is.True, "Move -> Idle on Speed");
    }

    ////////////////////////////////////////////////
    // ---------- Every creature's prefab ---------- //
    ////////////////////////////////////////////////

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void Prefab_HasAnAnimator_WithRootMotionOff(string path)
    {
        // Root motion would move the body as well as the BodyDirector, so it would drift off its square
        Animator animator = AnimatorFor(path);

        Assert.That(animator.applyRootMotion, Is.False, "Untick Apply Root Motion on the model's Animator");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void Prefab_UsesCreatureBase_OrAnOverrideOfIt(string path)
    {
        // Overrides keep every creature on the same four parameters, so no creature needs new code
        RuntimeAnimatorController controller = AnimatorFor(path).runtimeAnimatorController;
        Assert.That(controller, Is.Not.Null, "The Animator has no controller");

        RuntimeAnimatorController underneath = controller is AnimatorOverrideController overrides
            ? overrides.runtimeAnimatorController
            : controller;
        Assert.That(underneath, Is.SameAs(Base()));
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void TheRunClip_Loops(string path)
    {
        // A run that doesn't loop plays once, then the body glides the rest of the way
        AnimationClip run = ClipFor(AnimatorFor(path), "Move");

        Assert.That(run.isLooping, Is.True, $"Tick Loop Time on {run.name}'s import settings");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void TheIdleClip_Loops(string path)
    {
        AnimationClip idle = ClipFor(AnimatorFor(path), "Idle");

        Assert.That(idle.isLooping, Is.True, $"Tick Loop Time on {idle.name}'s import settings");
    }

    [TestCaseSource(nameof(AllDefinitionPaths))]
    public void TheDeathClip_DoesNotLoop(string path)
    {
        AnimationClip death = ClipFor(AnimatorFor(path), "Dead");

        Assert.That(death.isLooping, Is.False, $"Untick Loop Time on {death.name}'s import settings");
    }
}
