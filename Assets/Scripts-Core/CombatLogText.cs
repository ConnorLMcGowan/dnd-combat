using System;

namespace DnDCombat.Core
{
    // Turns what happened into the sentences the combat log shows. Plain text in, plain text
    // out, so every wording (crit, resisted, defeated and so on) can be tested without Unity.
    public static class CombatLogText
    {      
        public static string Describe(AttackReport report)
        {
            string target = report.Target.Creature.Name;
            string attacker = report.Attacker.Creature.Name;
            string weapon = report.Attack.Name;
            string damageType = report.Attack.DamageType.ToString().ToLower();
            string text = $"{attacker} attacks {target} with {weapon}: ";
            // a. The roll. Check for critical miss, critical hit, or normal hit/miss. If it's a miss, return early.
            if (report.Result.IsCriticalMiss)
            {
                text += $"rolled a natural 1. Critical miss.";
                return text;
            }
            else if (report.Result.IsCrit)
            {
                text += $"rolled a natural 20! Critical hit!";
            }
            else
            {
                int bonus = report.Result.AttackTotal - report.Result.D20Roll;
                string sign = bonus >= 0 ? "+" : "-";
                text += $"{report.Result.D20Roll} {sign} {Math.Abs(bonus)} = {report.Result.AttackTotal} vs AC {report.Target.Creature.ArmourClass}.";
                if (!report.Result.Hit)
                {
                    text += " Miss.";
                    return text;
                }
                text += " Hit!";
            }

            // b. The damage. Show the rolled damage, the damage type, and if it was resisted, vulnerable, or immune.
            if (report.Result.Effect == DamageEffect.Normal)
            {
                text += $" {report.Result.Damage} {damageType}.";
            }
            else if (report.Result.Effect == DamageEffect.Vulnerable)
            {
                text += $" {report.Result.RolledDamage} {damageType}, doubled (vulnerable) = {report.Result.Damage}.";
            }
            else if (report.Result.Effect == DamageEffect.Resisted)
            {
                text += $" {report.Result.RolledDamage} {damageType}, halved (resistant) = {report.Result.Damage}.";
            }
            else if (report.Result.Effect == DamageEffect.Immune)
            {
                text += $" {report.Result.RolledDamage} {damageType}, but {target} is immune.";
            }
            // c. The result. If the target is defeated, say so. Otherwise, show its HP after the attack.
            if (report.TargetDefeated)
            {
                text += $" {target} is defeated.";
            }
            else
            {
                text += $" {target} has {report.TargetHPAfter} HP left.";
            }
            return text;
        }

        // "Round 2: Skeleton's turn."
        public static string TurnStart(Combatant who, int round)
        {
            return $"Round {round}: {who.Creature.Name}'s turn.";
        }
    }
}
