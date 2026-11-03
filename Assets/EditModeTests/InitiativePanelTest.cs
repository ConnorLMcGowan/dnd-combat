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
using static UnityTestHelpers;

// The initiative panel with a real fight behind it: cards appear in turn order when combat
// starts, the highlight follows the turn, and HP updates as attacks land. The card template is
// built in code from plain UI pieces, so these never depend on your art.
public class InitiativePanelTest
{
    private readonly List<Object> _made = new List<Object>();

    private GameManager _game;
    private BodyDirector _director;
    private InitiativePanel _panel;
    private Transform _container;

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in _made)
        {
            if (o != null) Object.DestroyImmediate(o);
        }
        _made.Clear();
    }

    ///////////////////////////////////
    // ---------- Helpers ---------- //
    ///////////////////////////////////

    private T Keep<T>(T thing) where T : Object
    {
        _made.Add(thing);
        return thing;
    }

    private CreatureDefinition Def(string name, int maxHP = 999, int armourClass = 10)
    {
        GameObject body = Keep(GameObject.CreatePrimitive(PrimitiveType.Capsule));
        body.name = name + " Body";
        CreatureDefinition definition = Keep(MakeDefinition(name, body, maxHP: maxHP));
        SetInt(definition, "_armourClass", armourClass);
        return definition;
    }

    // A card template like your prefab: name, HP text, HP bar, portrait, current and defeated markers
    private InitiativeEntry MakeCardTemplate()
    {
        GameObject card = Keep(new GameObject("Test Card", typeof(RectTransform)));
        InitiativeEntry entry = card.AddComponent<InitiativeEntry>();
        SetObject(entry, "_nameText", Part<TextMeshProUGUI>(card, "Name"));
        SetObject(entry, "_hpText", Part<TextMeshProUGUI>(card, "HPText"));
        Image fill = Part<Image>(card, "HPBar");
        fill.type = Image.Type.Filled;
        SetObject(entry, "_hpFill", fill);
        SetObject(entry, "_portrait", Part<Image>(card, "CharacterPortrait"));
        SetObject(entry, "_currentMarker", Part<Image>(card, "Current").gameObject);
        SetObject(entry, "_defeatedMarker", Part<Image>(card, "Defeated").gameObject);
        return entry;
    }

    private static T Part<T>(GameObject card, string name) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(card.transform, false);
        return go.AddComponent<T>();
    }

    // The fight, the director (Instant) and the panel, all set up but combat not yet started
    private void Build(params Spawn[] spawns)
    {
        ArenaBuilder arena = Keep(new GameObject("Test Arena")).AddComponent<ArenaBuilder>();
        SetInt(arena, "_width", 10);
        SetInt(arena, "_height", 3);
        SetInt(arena, "_depth", 10);
        SetBlocks(arena, new Block[0]);

        _game = Keep(new GameObject("Test Game Manager")).AddComponent<GameManager>();
        SetObject(_game, "_arena", arena);
        SetInt(_game, "_seed", 5);
        SetSpawns(_game, spawns);
        Call(_game, "Awake");

        _director = Keep(new GameObject("Test Body Director")).AddComponent<BodyDirector>();
        SetObject(_director, "_game", _game);
        SetField(_director, "_instant", p => p.boolValue = true);
        Call(_director, "Start");

        _container = Keep(new GameObject("Test Panel", typeof(RectTransform))).transform;
        _panel = _container.gameObject.AddComponent<InitiativePanel>();
        SetObject(_panel, "_game", _game);
        SetObject(_panel, "_director", _director);
        SetObject(_panel, "_container", _container);
        SetObject(_panel, "_entryPrefab", MakeCardTemplate());
    }

    private void HeroAndGoblin(int goblinHP = 999, int goblinAC = 10)
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin", goblinHP, goblinAC), Team.Enemies, new Vector3Int(2, 0, 1)));
    }

    private void StartCombat() => Call(_game, "Start");

    private List<InitiativeEntry> Cards() => _container.GetComponentsInChildren<InitiativeEntry>().ToList();

    private InitiativeEntry CardOf(Combatant c) => Cards().Single(card => card.Combatant == c);

    private Combatant Named(string name) => _game.Combatants.Single(c => c.Creature.Name == name);

    private static bool IsCurrent(InitiativeEntry card) => card.transform.Find("Current").gameObject.activeSelf;
    private static bool IsDefeated(InitiativeEntry card) => card.transform.Find("Defeated").gameObject.activeSelf;
    private static string HPText(InitiativeEntry card) => card.transform.Find("HPText").GetComponent<TMP_Text>().text;

    ////////////////////////////////////////////
    // ---------- Building the cards ---------- //
    ////////////////////////////////////////////

    [Test]
    public void BeforeCombatStarts_ThereAreNoCards()
    {
        HeroAndGoblin();

        Call(_panel, "Start");

        Assert.That(Cards(), Is.Empty);
    }

    [Test]
    public void WhenCombatStarts_ThereIsOneCardPerCreature_InTurnOrder()
    {
        Build(new Spawn(Def("Hero"), Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(5, 0, 5)),
              new Spawn(Def("Orc"), Team.Enemies, new Vector3Int(8, 0, 8)));
        Call(_panel, "Start");

        StartCombat();

        List<Combatant> cardOrder = Cards().Select(card => card.Combatant).ToList();
        Assert.That(cardOrder, Is.EqualTo(_game.Controller.Turns.Order));
    }

    [Test]
    public void TheCards_ShowNamesAndFullHP()
    {
        HeroAndGoblin(goblinHP: 13);
        Call(_panel, "Start");

        StartCombat();

        InitiativeEntry goblin = CardOf(Named("Goblin"));
        Assert.That(goblin.transform.Find("Name").GetComponent<TMP_Text>().text, Is.EqualTo("Goblin"));
        Assert.That(HPText(goblin), Is.EqualTo("13 / 13"));
    }

    [Test]
    public void IfCombatAlreadyStarted_ThePanelCatchesUp()
    {
        // Scripts start in no fixed order: the GameManager may start combat before the panel subscribes
        HeroAndGoblin();
        StartCombat();

        Call(_panel, "Start");

        Assert.That(Cards().Count, Is.EqualTo(2));
        Assert.That(IsCurrent(CardOf(_game.Controller.Turns.Current)), Is.True);
    }

    [Test]
    public void CardsAreBuiltOnce_NotEveryTurn()
    {
        HeroAndGoblin();
        Call(_panel, "Start");
        StartCombat();

        for (int i = 0; i < 5; i++) _game.Controller.EndTurn();

        Assert.That(Cards().Count, Is.EqualTo(2));
    }

    ///////////////////////////////////////////
    // ---------- Whose turn ---------- //
    ///////////////////////////////////////////

    [Test]
    public void OnlyTheCurrentCreaturesCard_IsHighlighted()
    {
        HeroAndGoblin();
        Call(_panel, "Start");

        StartCombat();

        foreach (InitiativeEntry card in Cards())
        {
            Assert.That(IsCurrent(card), Is.EqualTo(card.Combatant == _game.Controller.Turns.Current), card.Combatant.Creature.Name);
        }
    }

    [Test]
    public void TheHighlight_MovesWithTheTurn()
    {
        HeroAndGoblin();
        Call(_panel, "Start");
        StartCombat();
        Combatant first = _game.Controller.Turns.Current;

        _game.Controller.EndTurn();

        Combatant second = _game.Controller.Turns.Current;
        Assert.That(IsCurrent(CardOf(second)), Is.True);
        Assert.That(IsCurrent(CardOf(first)), Is.False);
    }

    /////////////////////////////////
    // ---------- HP ---------- //
    /////////////////////////////////

    [Test]
    public void AnAttack_UpdatesTheTargetsHP_ToWhatTheReportSays()
    {
        HeroAndGoblin(goblinHP: 999);
        Call(_panel, "Start");
        StartCombat();
        UnityTestHelpers.PassToFirstHero(_game.Controller);
        Combatant hero = Named("Hero");
        Combatant goblin = Named("Goblin");
        int hpAfter = -1;
        _game.Controller.AttackResolved += report => hpAfter = report.TargetHPAfter;
        _game.TryGetAttack(hero, out AttackProfile club);

        _game.Controller.TryAttack(hero, goblin, club);

        Assert.That(HPText(CardOf(goblin)), Is.EqualTo($"{hpAfter} / 999"));
        Assert.That(HPText(CardOf(hero)), Is.EqualTo("999 / 999"), "Only the target's card changes");
    }

    [Test]
    public void AKillingBlow_MarksTheCardDefeated()
    {
        // AC 1, 1 HP: only a natural 1 could save it
        HeroAndGoblin(goblinHP: 1, goblinAC: 1);
        Call(_panel, "Start");
        StartCombat();
        UnityTestHelpers.PassToFirstHero(_game.Controller);
        Combatant goblin = Named("Goblin");
        _game.TryGetAttack(Named("Hero"), out AttackProfile club);

        bool killed = _game.Controller.TryAttack(Named("Hero"), goblin, club).Result.Hit;

        Assume.That(killed, Is.True, "This seed rolled a natural 1");
        Assert.That(IsDefeated(CardOf(goblin)), Is.True);
        Assert.That(HPText(CardOf(goblin)), Is.EqualTo("0 / 1"));
    }

    ///////////////////////////////////////
    // ---------- Portraits ---------- //
    ///////////////////////////////////////

    [Test]
    public void EachCard_GetsItsCreaturesPortrait()
    {
        CreatureDefinition heroDef = Def("Hero");
        Texture2D texture = Keep(new Texture2D(4, 4));
        Sprite face = Keep(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero));
        SetObject(heroDef, "_portrait", face);
        Build(new Spawn(heroDef, Team.Heroes, new Vector3Int(1, 0, 1)),
              new Spawn(Def("Goblin"), Team.Enemies, new Vector3Int(5, 0, 5)));
        Call(_panel, "Start");

        StartCombat();

        Image heroPortrait = CardOf(Named("Hero")).transform.Find("CharacterPortrait").GetComponent<Image>();
        Image goblinPortrait = CardOf(Named("Goblin")).transform.Find("CharacterPortrait").GetComponent<Image>();
        Assert.That(heroPortrait.sprite, Is.SameAs(face));
        Assert.That(goblinPortrait.sprite, Is.Not.SameAs(face), "The goblin has no portrait, so it keeps the placeholder");
    }

    ///////////////////////////////////////////
    // ---------- Wiring and safety ---------- //
    ///////////////////////////////////////////

    [Test]
    public void AfterOnDestroy_ItStopsListening()
    {
        HeroAndGoblin();
        Call(_panel, "Start");
        StartCombat();
        Combatant first = _game.Controller.Turns.Current;

        Call(_panel, "OnDestroy");
        _game.Controller.EndTurn();

        Assert.That(IsCurrent(CardOf(first)), Is.True, "The highlight shouldn't move once it's stopped listening");
    }

    [Test]
    public void WithEmptySlots_ItWarns_AndDoesNotThrow()
    {
        HeroAndGoblin();
        SetObject(_panel, "_entryPrefab", null);

        LogAssert.Expect(LogType.Warning, new Regex("InitiativePanel: fill in"));
        Assert.DoesNotThrow(() => Call(_panel, "Start"));
        Assert.DoesNotThrow(() => Call(_panel, "OnDestroy"));
    }

    [Test]
    public void WithNoGameManager_ItWarns_AndDoesNotThrow()
    {
        InitiativePanel lonely = Keep(new GameObject("Lonely Panel")).AddComponent<InitiativePanel>();

        LogAssert.Expect(LogType.Warning, new Regex("InitiativePanel: no ready GameManager"));
        Assert.DoesNotThrow(() => Call(lonely, "Start"));
        Assert.DoesNotThrow(() => Call(lonely, "OnDestroy"));
    }
}
