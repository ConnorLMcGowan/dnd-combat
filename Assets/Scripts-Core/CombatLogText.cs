namespace DnDCombat.Core
{
    // Turns what happened into the sentences the combat log shows. Plain text in, plain text
    // out, so every wording (crit, resisted, defeated and so on) can be tested without Unity.
    public static class CombatLogText
    {
        // TODO(guide): UI 3. OVERVIEW. Do UI 2 (Attack in the AttackReport) first: Describe needs it.
        //
        //   How this works: Describe builds one sentence in three parts, adding to a string
        //   as it goes ("text += ..."). Some outcomes end the sentence early (a miss has no damage).
        //     1. Who, whom, with what:   "Fighter attacks Skeleton with Flail: "
        //     2. The roll:               "9 + 5 = 14 vs AC 14. Hit!"
        //     3. The damage and result:  " 7 bludgeoning, doubled (vulnerable) = 14. Skeleton is defeated."
        //
        //   Your toolbox:
        //     report.Attacker.Creature.Name, report.Target.Creature.Name, report.Attack.Name
        //     report.Result.D20Roll, .AttackTotal, .Hit, .IsCrit, .IsCriticalMiss
        //     report.Result.RolledDamage (before the target's rules), .Damage (after), .Effect
        //     report.Target.Creature.ArmourClass, report.TargetHPAfter, report.TargetDefeated
        //     report.Attack.DamageType.ToString().ToLower()      "Bludgeoning" -> "bludgeoning"
        //     Math.Abs(number)                                   (add "using System;" at the top)
        //     $"{a} attacks {b}"                                 builds a string with values in it
        //
        //   Worked examples (Fighter's Flail: +5 to hit, 1d8 + 3 bludgeoning. Skeleton: AC 14,
        //   13 HP, vulnerable to bludgeoning):
        //     d20 shows 9, 7 damage:
        //       "Fighter attacks Skeleton with Flail: 9 + 5 = 14 vs AC 14. Hit! 7 bludgeoning, doubled (vulnerable) = 14. Skeleton is defeated."
        //     d20 shows 4:
        //       "Fighter attacks Skeleton with Flail: 4 + 5 = 9 vs AC 14. Miss."
        //     d20 shows 1:
        //       "Fighter attacks Skeleton with Flail: natural 1. Critical miss."
        //     d20 shows 20, 13 damage:
        //       "Fighter attacks Skeleton with Flail: natural 20! Critical hit! 13 bludgeoning, doubled (vulnerable) = 26. Skeleton is defeated."
        //     A resistant target, 9 rolled:   "... Hit! 9 slashing, halved (resistant) = 4. Ghoul has 18 HP left."
        //     An immune target, 6 rolled:     "... Hit! 6 poison, but Skeleton is immune. Skeleton has 13 HP left."
        //     No effect, 8 rolled:            "... Hit! 8 slashing. Skeleton has 5 HP left."
        //     A NEGATIVE bonus (Strength 8):  "... 6 - 1 = 5 vs AC 12. Miss."

        public static string Describe(AttackReport report)
        {
            // TODO(guide): UI 3a to 3d.
            //   a. string target = report.Target.Creature.Name;
            //      string text = $"{attacker} attacks {target} with {weapon}: ";
            //   b. The roll. Check in this order, because a natural 1 or 20 ignores the maths:
            //        IsCriticalMiss -> text += "natural 1. Critical miss.";  return text;
            //        IsCrit         -> text += "natural 20! Critical hit!";
            //        otherwise      -> int bonus = AttackTotal - D20Roll;
            //                          a plus or minus sign depending on bonus, then Math.Abs(bonus),
            //                          so it reads "9 + 5" or "6 - 1", never "6 + -1".
            //                          text += $"{d20} {sign} {Math.Abs(bonus)} = {total} vs AC {ac}.";
            //                          if (!Hit) { text += " Miss."; return text; }
            //                          text += " Hit!";
            //   c. The damage, by report.Result.Effect (use a switch or if / else if):
            //        Normal      " {Damage} {type}."
            //        Vulnerable  " {RolledDamage} {type}, doubled (vulnerable) = {Damage}."
            //        Resisted    " {RolledDamage} {type}, halved (resistant) = {Damage}."
            //        Immune      " {RolledDamage} {type}, but {target} is immune."
            //   d. The result: TargetDefeated -> " {target} is defeated."
            //                  otherwise      -> " {target} has {TargetHPAfter} HP left."
            //      Then return text.
            //
            //   Pitfalls:
            //     Keep the spaces at the START of each added part (" Hit!"), never the end, so
            //       no sentence ends in a stray space. The tests compare exact strings.
            //     A crit still uses the Effect, so "doubled (vulnerable)" can follow "Critical hit!".
            //   Checkpoint: compiles. Then the tests (I can write them) check every example above.
            return string.Empty;    // placeholder until UI 3 is written
        }

        // "Round 2: Skeleton's turn."
        public static string TurnStart(Combatant who, int round)
        {
            // TODO(guide): UI 3e. One line: return $"Round {round}: {name}'s turn.";
            return string.Empty;    // placeholder until UI 3e is written
        }
    }
}
