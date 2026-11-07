#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using DnDCombat.Core;
using DnDCombat.Unity;

// The HUD in a running fight: the combat log, the End Turn button, the turn banner and the
// victory screen. Every UI piece is plain and built in code, so none of this depends on your art.
//
// The hero starts at (7, 0, 7) and the goblin next to it at (8, 0, 7), so an attack is always in reach.
public class HudPlayTest
{
    private TestWorld _world;

    [TearDown]
    public void TearDown()
    {
        _world?.Destroy();
    }

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    // The fight and a director, not yet switched on. Add HUD pieces, then call Begin.
    private void Build(int goblinArmourClass = 10, int goblinHP = 999)
    {
        _world = new TestWorld().WithArena()
                                .WithFight(new Vector3Int(7, 0, 7), new Vector3Int(8, 0, 7), goblinArmourClass, goblinHP)
                                .WithDirector();
    }

    private IEnumerator Begin()
    {
        _world.Activate();
        yield return null;                 // Start runs: combat begins
        _world.PassToHero();
        yield return null;
    }

    private GameObject UI(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : _world.Root.transform, false);
        return go;
    }

    private TMP_Text Text(string name, Transform parent = null) => UI(name, parent).AddComponent<TextMeshProUGUI>();

    private CombatController Controller => _world.Game.Controller;

    private void HeroAttacks()
    {
        _world.Game.TryGetAttack(_world.Hero, out AttackProfile club);
        Assume.That(Controller.TryAttack(_world.Hero, _world.Enemy, club).Success, Is.True);
    }

    private IEnumerator WaitFor(System.Func<bool> condition, float timeout = 5f)
    {
        float giveUp = Time.time + timeout;
        while (!condition() && Time.time < giveUp) yield return null;
        Assert.That(condition(), Is.True, $"Still waiting after {timeout} s");
    }

    /////////////////////////////////////
    // ---------- Combat log ---------- //
    /////////////////////////////////////

    private Transform _logContent;

    private CombatLogView AddLog(int maxLines = 50)
    {
        CombatLogView log = _world.Add<CombatLogView>("Combat Log");
        _logContent = UI("Content", log.transform).transform;
        TMP_Text linePrefab = Text("Line Template");
        TestWorld.SetObject(log, "_game", _world.Game);
        TestWorld.SetObject(log, "_director", _world.Director);
        TestWorld.SetObject(log, "_content", _logContent);
        TestWorld.SetObject(log, "_linePrefab", linePrefab);
        TestWorld.SetInt(log, "_maxLines", maxLines);
        return log;
    }

    private List<string> LogLines() =>
        _logContent.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToList();

    [UnityTest]
    public IEnumerator Log_StartsWithTheFirstTurn()
    {
        Build();
        AddLog();

        yield return Begin();

        // The first line is round 1; PassToHero may have added the hero's turn after it
        Assert.That(LogLines()[0], Does.StartWith("Round 1: "));
        Assert.That(LogLines().Last(), Is.EqualTo("Round " + Controller.Turns.Round + ": Hero's turn."));
    }

    [UnityTest]
    public IEnumerator Log_AddsALineForEachNewTurn()
    {
        Build();
        AddLog();
        yield return Begin();
        int before = LogLines().Count;

        Controller.EndTurn();
        yield return null;

        Assert.That(LogLines().Count, Is.EqualTo(before + 1));
        Assert.That(LogLines().Last(), Is.EqualTo(CombatLogText.TurnStart(Controller.Turns.Current, Controller.Turns.Round)));
    }

    [UnityTest]
    public IEnumerator Log_AddsTheAttack_OnlyWhenTheBladeLands()
    {
        Build();
        AddLog();
        yield return Begin();
        int before = LogLines().Count;
        AttackReport landed = default;
        _world.Director.AttackLanded += report => landed = report;

        HeroAttacks();
        Assert.That(LogLines().Count, Is.EqualTo(before), "Logged before the swing even started");

        yield return _world.WaitUntilIdle();
        Assert.That(LogLines().Count, Is.EqualTo(before + 1));
        Assert.That(LogLines().Last(), Is.EqualTo(CombatLogText.Describe(landed)));
    }

    [UnityTest]
    public IEnumerator Log_KeepsOnlyTheNewestLines()
    {
        Build();
        CombatLogView log = AddLog(maxLines: 3);
        yield return Begin();

        log.AddLine("A");
        log.AddLine("B");
        log.AddLine("C");
        log.AddLine("D");
        yield return null;                 // Destroy finishes at the end of the frame

        Assert.That(LogLines(), Is.EqualTo(new[] { "B", "C", "D" }));
    }

    [UnityTest]
    public IEnumerator Log_WithEmptySlots_WarnsAndNothingBreaks()
    {
        Build();
        CombatLogView log = _world.Add<CombatLogView>("Empty Log");
        TestWorld.SetObject(log, "_game", _world.Game);

        LogAssert.Expect(LogType.Warning, new Regex("CombatLogView"));
        yield return Begin();
        Controller.EndTurn();
        yield return null;
        // Any exception, including from OnDestroy in TearDown, would fail the test
    }

    /////////////////////////////////////////
    // ---------- End Turn button ---------- //
    /////////////////////////////////////////

    private Button AddEndTurnButton()
    {
        EndTurnButton endTurn = _world.Add<EndTurnButton>("End Turn");
        Button button = UI("Button").AddComponent<Button>();
        TestWorld.SetObject(endTurn, "_game", _world.Game);
        TestWorld.SetObject(endTurn, "_director", _world.Director);
        TestWorld.SetObject(endTurn, "_button", button);
        return button;
    }

    [UnityTest]
    public IEnumerator EndTurn_IsOnDuringTheHerosTurn_AndClickingItEndsTheTurn()
    {
        Build();
        Button button = AddEndTurnButton();
        yield return Begin();

        Assert.That(button.interactable, Is.True);
        button.onClick.Invoke();

        Assert.That(Controller.Turns.Current, Is.SameAs(_world.Enemy));
    }

    [UnityTest]
    public IEnumerator EndTurn_IsOffDuringTheEnemysTurn_AndClickingDoesNothing()
    {
        Build();
        Button button = AddEndTurnButton();
        yield return Begin();
        Controller.EndTurn();
        yield return null;

        Assert.That(button.interactable, Is.False);
        button.onClick.Invoke();               // even a click that somehow gets through
        Assert.That(Controller.Turns.Current, Is.SameAs(_world.Enemy));
    }

    [UnityTest]
    public IEnumerator EndTurn_IsOffWhileTheHeroIsStillWalking()
    {
        Build();
        Button button = AddEndTurnButton();
        yield return Begin();

        Controller.TryMove(_world.Hero, new GridPosition(7, 0, 11));
        yield return null;
        Assume.That(_world.Director.IsBusy, Is.True);

        Assert.That(button.interactable, Is.False, "Off mid-walk");
        button.onClick.Invoke();
        Assert.That(Controller.Turns.Current, Is.SameAs(_world.Hero), "Ending the turn mid-walk");

        yield return _world.WaitUntilIdle();
        yield return null;
        Assert.That(button.interactable, Is.True, "Back on once the walk is done");
    }

    [UnityTest]
    public IEnumerator EndTurn_IsOffOnceCombatIsOver()
    {
        Build();
        Button button = AddEndTurnButton();
        yield return Begin();

        _world.Enemy.Creature.TakeDamage(9999);
        Controller.Turns.CheckCombatEnd();
        yield return null;

        Assert.That(button.interactable, Is.False);
    }

    //////////////////////////////////////
    // ---------- Turn banner ---------- //
    //////////////////////////////////////

    private CanvasGroup AddBanner(out TMP_Text text)
    {
        TurnBanner banner = _world.Add<TurnBanner>("Turn Banner");
        CanvasGroup group = banner.gameObject.AddComponent<CanvasGroup>();
        text = Text("Banner Text", banner.transform);
        TestWorld.SetObject(banner, "_game", _world.Game);
        TestWorld.SetObject(banner, "_text", text);
        TestWorld.SetObject(banner, "_group", group);
        TestWorld.SetFloat(banner, "_fadeIn", 0.05f);
        TestWorld.SetFloat(banner, "_hold", 0.2f);
        TestWorld.SetFloat(banner, "_fadeOut", 0.05f);
        return group;
    }

    [UnityTest]
    public IEnumerator Banner_SaysWhoseTurn()
    {
        Build();
        AddBanner(out TMP_Text text);
        yield return Begin();

        Assert.That(text.text, Is.EqualTo(CombatLogText.TurnStart(Controller.Turns.Current, Controller.Turns.Round)));
    }

    [UnityTest]
    public IEnumerator Banner_FadesIn_ReachesFull_ThenFadesAway()
    {
        Build();
        CanvasGroup group = AddBanner(out _);
        yield return Begin();
        Controller.EndTurn();                  // a fresh banner to watch from the start

        yield return WaitFor(() => group.alpha >= 1f, 1f);
        Assert.That(group.alpha, Is.EqualTo(1f), "Fully shown during the hold, not 0.9-something");

        yield return WaitFor(() => group.alpha <= 0f, 2f);
        Assert.That(group.alpha, Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator Banner_ANewTurn_ReplacesTheOldBanner()
    {
        Build();
        CanvasGroup group = AddBanner(out TMP_Text text);
        yield return Begin();

        Controller.EndTurn();
        yield return null;
        Controller.EndTurn();
        yield return null;

        Assert.That(text.text, Is.EqualTo(CombatLogText.TurnStart(Controller.Turns.Current, Controller.Turns.Round)));
        yield return WaitFor(() => group.alpha <= 0f, 2f);   // and it still finishes cleanly
    }

    [UnityTest]
    public IEnumerator Banner_WithEmptySlots_WarnsAndNothingBreaks()
    {
        Build();
        _world.Add<TurnBanner>("Empty Banner");

        LogAssert.Expect(LogType.Warning, new Regex("TurnBanner"));
        yield return Begin();
        Controller.EndTurn();
        yield return null;
    }

    /////////////////////////////////////////
    // ---------- Victory screen ---------- //
    /////////////////////////////////////////

    private CanvasGroup _victoryGroup;
    private TMP_Text _victoryTitle;
    private TMP_Text _victorySubtitle;
    private Button _nextLevel;

    private VictoryScreen AddVictoryScreen(float delay = 0.1f)
    {
        VictoryScreen screen = _world.Add<VictoryScreen>("Victory Screen");
        _victoryGroup = screen.gameObject.AddComponent<CanvasGroup>();
        _victoryTitle = Text("Title", screen.transform);
        _victorySubtitle = Text("Subtitle", screen.transform);
        _nextLevel = UI("Next Level", screen.transform).AddComponent<Button>();
        Button restart = UI("Restart", screen.transform).AddComponent<Button>();
        TestWorld.SetObject(screen, "_game", _world.Game);
        TestWorld.SetObject(screen, "_director", _world.Director);
        TestWorld.SetObject(screen, "_group", _victoryGroup);
        TestWorld.SetObject(screen, "_title", _victoryTitle);
        TestWorld.SetObject(screen, "_subtitle", _victorySubtitle);
        TestWorld.SetObject(screen, "_nextLevelButton", _nextLevel);
        TestWorld.SetObject(screen, "_restartButton", restart);
        TestWorld.SetFloat(screen, "_delay", delay);
        TestWorld.SetFloat(screen, "_fadeIn", 0.05f);
        return screen;
    }

    private bool VictoryShown => _victoryGroup.alpha >= 1f;

    [UnityTest]
    public IEnumerator Victory_IsHiddenDuringTheFight_AndDoesNotCatchClicks()
    {
        Build();
        AddVictoryScreen();
        yield return Begin();

        Assert.That(_victoryGroup.alpha, Is.EqualTo(0f));
        Assert.That(_victoryGroup.blocksRaycasts, Is.False, "A hidden panel must not eat clicks meant for the board");
        Assert.That(_victoryGroup.interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator Victory_WhenTheEnemiesFall()
    {
        Build();
        AddVictoryScreen();
        yield return Begin();

        _world.Enemy.Creature.TakeDamage(9999);
        Controller.Turns.CheckCombatEnd();
        yield return WaitFor(() => VictoryShown, 2f);

        Assert.That(_victoryTitle.text, Is.EqualTo("Victory!"));
        Assert.That(_victorySubtitle.text, Is.EqualTo("The heroes stand triumphant."));
        Assert.That(_nextLevel.gameObject.activeSelf, Is.True, "Next Level is offered after a win");
        Assert.That(_victoryGroup.blocksRaycasts, Is.True, "Shown, it blocks the board behind it");
        Assert.That(_victoryGroup.interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator Defeat_WhenTheHeroesFall_WithNoNextLevel()
    {
        Build();
        AddVictoryScreen();
        yield return Begin();

        _world.Hero.Creature.TakeDamage(9999);
        Controller.Turns.CheckCombatEnd();
        yield return WaitFor(() => VictoryShown, 2f);

        Assert.That(_victoryTitle.text, Is.EqualTo("Defeat"));
        Assert.That(_victorySubtitle.text, Is.EqualTo("The party has fallen."));
        Assert.That(_nextLevel.gameObject.activeSelf, Is.False, "No Next Level after a loss");
    }

    [UnityTest]
    public IEnumerator Victory_WaitsForTheKillingBlowToPlay_ThenTheDelay()
    {
        // AC 1 and 1 HP: only a natural 1 could save the goblin
        Build(goblinArmourClass: 1, goblinHP: 1);
        AddVictoryScreen(delay: 0.3f);
        yield return Begin();

        HeroAttacks();
        Assume.That(Controller.Turns.State, Is.EqualTo(CombatState.HeroesWon), "This seed rolled a natural 1");

        // The Core says it's over, but the swing and the fall are still playing
        while (_world.Director.IsBusy)
        {
            Assert.That(_victoryGroup.alpha, Is.EqualTo(0f), "Shown before the goblin even fell");
            yield return null;
        }
        Assert.That(_victoryGroup.alpha, Is.EqualTo(0f), "Shown with no pause after the fall");

        yield return WaitFor(() => VictoryShown, 2f);
    }

    [UnityTest]
    public IEnumerator Victory_AddsTheResultToTheLog()
    {
        Build();
        AddLog();
        VictoryScreen screen = AddVictoryScreen();
        TestWorld.SetObject(screen, "_log", _world.Root.GetComponentInChildren<CombatLogView>(true));
        yield return Begin();

        _world.Enemy.Creature.TakeDamage(9999);
        Controller.Turns.CheckCombatEnd();
        yield return WaitFor(() => VictoryShown, 2f);

        Assert.That(LogLines().Last(), Is.EqualTo("Victory!"));
    }

    [UnityTest]
    public IEnumerator Victory_AFightThatEndsBeforeItStarts_IsStillShown()
    {
        // The goblin is down before combat begins, so CombatEnded fires inside GameManager.Start,
        // possibly before the screen has subscribed. It must catch up either way.
        Build();
        AddVictoryScreen();
        _world.Activate();
        _world.Enemy.Creature.TakeDamage(9999);

        yield return WaitFor(() => VictoryShown, 2f);
        Assert.That(_victoryTitle.text, Is.EqualTo("Victory!"));
    }

    [UnityTest]
    public IEnumerator Victory_WithEmptySlots_WarnsAndNothingBreaks()
    {
        Build();
        _world.Add<VictoryScreen>("Empty Victory Screen");

        LogAssert.Expect(LogType.Warning, new Regex("VictoryScreen"));
        yield return Begin();
        _world.Enemy.Creature.TakeDamage(9999);
        Controller.Turns.CheckCombatEnd();
        yield return null;
    }
}
#endif
