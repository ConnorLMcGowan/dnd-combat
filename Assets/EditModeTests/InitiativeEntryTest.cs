using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;
using DnDCombat.Unity;
using static UnityTestHelpers;

// One initiative card, built in code from plain UI pieces (no art): the slots get filled
// correctly, and a card with some slots left empty still works.
public class InitiativeEntryTest
{
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

    private TMP_Text _name;
    private TMP_Text _hp;
    private Image _fill;
    private Image _portrait;
    private GameObject _current;
    private GameObject _defeated;

    // A card with every slot filled, like the one in your prefab
    private InitiativeEntry FullCard()
    {
        InitiativeEntry entry = EmptyCard();
        _name = Child<TextMeshProUGUI>(entry, "Name");
        _hp = Child<TextMeshProUGUI>(entry, "HPText");
        _fill = Child<Image>(entry, "HPBar");
        _fill.type = Image.Type.Filled;
        _portrait = Child<Image>(entry, "CharacterPortrait");
        _current = Child<Image>(entry, "Current").gameObject;
        _defeated = Child<Image>(entry, "Defeated").gameObject;

        SetObject(entry, "_nameText", _name);
        SetObject(entry, "_hpText", _hp);
        SetObject(entry, "_hpFill", _fill);
        SetObject(entry, "_portrait", _portrait);
        SetObject(entry, "_currentMarker", _current);
        SetObject(entry, "_defeatedMarker", _defeated);
        return entry;
    }

    // A card with no slots filled at all
    private InitiativeEntry EmptyCard()
    {
        GameObject go = new GameObject("Test Card", typeof(RectTransform));
        _made.Add(go);
        return go.AddComponent<InitiativeEntry>();
    }

    private static T Child<T>(InitiativeEntry entry, string name) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(entry.transform, false);
        return go.AddComponent<T>();
    }

    private Sprite MakeSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        _made.Add(texture);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        _made.Add(sprite);
        return sprite;
    }

    private static Combatant Goblin(int maxHP = 13) =>
        new Combatant(new Creature("Goblin", maxHP: maxHP), Team.Enemies);

    /////////////////////////////////
    // ---------- Show ---------- //
    /////////////////////////////////

    [Test]
    public void Show_FillsInTheNameAndFullHP_NotCurrent_NotDefeated()
    {
        InitiativeEntry entry = FullCard();
        Combatant goblin = Goblin();

        entry.Show(goblin);

        Assert.That(entry.Combatant, Is.SameAs(goblin));
        Assert.That(_name.text, Is.EqualTo("Goblin"));
        Assert.That(_hp.text, Is.EqualTo("13 / 13"));
        Assert.That(_fill.fillAmount, Is.EqualTo(1f).Within(0.001f));
        Assert.That(_current.activeSelf, Is.False);
        Assert.That(_defeated.activeSelf, Is.False);
    }

    [Test]
    public void Show_ACreatureAlreadyDown_IsShownDefeated()
    {
        InitiativeEntry entry = FullCard();
        Combatant goblin = Goblin();
        goblin.Creature.TakeDamage(99);

        entry.Show(goblin);

        Assert.That(_hp.text, Is.EqualTo("0 / 13"));
        Assert.That(_fill.fillAmount, Is.EqualTo(0f).Within(0.001f));
        Assert.That(_defeated.activeSelf, Is.True);
    }

    ////////////////////////////////
    // ---------- HP ---------- //
    ////////////////////////////////

    [Test]
    public void SetHP_ShowsTheNumbers_AndFillsTheBarByTheFraction()
    {
        InitiativeEntry entry = FullCard();

        entry.SetHP(9, 13);

        Assert.That(_hp.text, Is.EqualTo("9 / 13"));
        Assert.That(_fill.fillAmount, Is.EqualTo(9f / 13f).Within(0.001f), "Not 0: that would be integer division");
    }

    [Test]
    public void SetHP_WithNoMaxHP_ShowsAnEmptyBar_WithoutDividingByZero()
    {
        InitiativeEntry entry = FullCard();
        _fill.fillAmount = 1f;

        Assert.DoesNotThrow(() => entry.SetHP(0, 0));

        Assert.That(_fill.fillAmount, Is.EqualTo(0f));
        Assert.That(_hp.text, Is.EqualTo("0 / 0"));
    }

    [Test]
    public void SetHP_WorksWithOnlyAText_OrOnlyABar()
    {
        InitiativeEntry textOnly = EmptyCard();
        TMP_Text text = Child<TextMeshProUGUI>(textOnly, "HPText");
        SetObject(textOnly, "_hpText", text);
        InitiativeEntry barOnly = EmptyCard();
        Image bar = Child<Image>(barOnly, "HPBar");
        SetObject(barOnly, "_hpFill", bar);

        textOnly.SetHP(5, 10);
        barOnly.SetHP(5, 10);

        Assert.That(text.text, Is.EqualTo("5 / 10"));
        Assert.That(bar.fillAmount, Is.EqualTo(0.5f).Within(0.001f));
    }

    ///////////////////////////////////////////////
    // ---------- Current and defeated ---------- //
    ///////////////////////////////////////////////

    [Test]
    public void SetCurrent_SwitchesTheMarkerOnAndOff()
    {
        InitiativeEntry entry = FullCard();

        entry.SetCurrent(true);
        Assert.That(_current.activeSelf, Is.True);

        entry.SetCurrent(false);
        Assert.That(_current.activeSelf, Is.False);
    }

    [Test]
    public void SetDefeated_SwitchesTheMarkerOn()
    {
        InitiativeEntry entry = FullCard();

        entry.SetDefeated(true);

        Assert.That(_defeated.activeSelf, Is.True);
    }

    ///////////////////////////////////////
    // ---------- Portrait ---------- //
    ///////////////////////////////////////

    [Test]
    public void SetPortrait_PutsThePictureInTheFrame()
    {
        InitiativeEntry entry = FullCard();
        Sprite face = MakeSprite();

        entry.SetPortrait(face);

        Assert.That(_portrait.sprite, Is.SameAs(face));
    }

    [Test]
    public void SetPortrait_WithNoPicture_KeepsThePlaceholder()
    {
        // A creature whose asset has no portrait keeps whatever the prefab shows by default
        InitiativeEntry entry = FullCard();
        Sprite placeholder = MakeSprite();
        _portrait.sprite = placeholder;

        entry.SetPortrait(null);

        Assert.That(_portrait.sprite, Is.SameAs(placeholder));
    }

    ///////////////////////////////////////////
    // ---------- Empty slots ---------- //
    ///////////////////////////////////////////

    [Test]
    public void ACardWithEverySlotEmpty_NeverThrows()
    {
        // Your card can leave out anything (no defeated marker, no portrait) and still work
        InitiativeEntry entry = EmptyCard();

        Assert.DoesNotThrow(() =>
        {
            entry.Show(Goblin());
            entry.SetHP(3, 13);
            entry.SetCurrent(true);
            entry.SetDefeated(true);
            entry.SetPortrait(MakeSprite());
        });
    }
}
