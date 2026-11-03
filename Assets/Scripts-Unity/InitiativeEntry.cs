using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DnDCombat.Core;

namespace DnDCombat.Unity
{
    // One creature's card in the initiative panel: its name, its HP, and whether it's their turn.
    // Put this on the root of your entry prefab and drag your own text and images into the slots.
    // It only ever changes what's in the slots, so the look is entirely up to you.
    public class InitiativeEntry : MonoBehaviour
    {


        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _portrait;             // the creature's picture (CharacterPortrait)
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private Image _hpFill;               // Image Type: Filled
        [SerializeField] private GameObject _currentMarker;   // on during this creature's turn
        [SerializeField] private GameObject _defeatedMarker;  // optional: on once it's defeated

        public Combatant Combatant { get; private set; }

        // Sets the card up for one creature, at full health and not their turn
        public void Show(Combatant combatant)
        {
            Combatant = combatant;
            if (_nameText != null)
            {
                _nameText.text = combatant.Creature.Name;
            }
            SetHP(combatant.Creature.CurrentHP, combatant.Creature.MaxHP);
            SetCurrent(false);
            SetDefeated(combatant.Creature.IsDefeated);
        }

        // Shows the creature's picture. A creature with no portrait keeps whatever the prefab's
        // CharacterPortrait image shows by default (your placeholder art, say).
        // Tip: tick "Preserve Aspect" on the CharacterPortrait Image, so a picture that isn't square
        // isn't stretched to fit the frame.
        public void SetPortrait(Sprite portrait)
        {
            if (_portrait == null || portrait == null)
            {
                return;
            }
            _portrait.sprite = portrait;
        }

        // Each slot is checked on its own, so a card with only the text, or only the bar, still works
        public void SetHP(int current, int max)
        {
            if (_hpText != null)
            {
                _hpText.text = $"{current} / {max}";
            }

            if (_hpFill != null)
            {
                // A broken creature with no max HP shows an empty bar, instead of dividing by zero.
                // (float) matters: 9 / 13 with two ints is 0, so the bar would look empty until full.
                _hpFill.fillAmount = max > 0 ? current / (float)max : 0f;
            }
        }

        public void SetCurrent(bool isCurrent)
        {
            // Only show the current marker when it's this creature's turn.
            if (_currentMarker != null)
            {
                _currentMarker.SetActive(isCurrent);
            }
        }

        public void SetDefeated(bool isDefeated)
        {
            // Only show the defeated marker character is defeated.
            if (_defeatedMarker != null)
            {
                _defeatedMarker.SetActive(isDefeated);
            }
        }
    }
}
