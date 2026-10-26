namespace DnDCombat.Core
{
    public readonly struct DamageAdjustment
    {
        public int Amount { get; }
        public DamageEffect Effect { get; }

        public DamageAdjustment(int amount, DamageEffect effect)
        {
            Amount = amount;
            Effect = effect;
        }
    }

    // The one place that knows how resistance, vulnerability and immunity change damage.
    // Attacks use it now; spells and traps can call it later.
    public static class DamageRules
    {
        /////////////////////////////////////
        // ---------- Adjusting ---------- //
        /////////////////////////////////////

        // SRD order: immunity first, then resistance (halve, round down), then vulnerability (double)
        public static DamageAdjustment Adjust(Creature target, DamageType type, int rolled)
        {
            int amount = rolled > 0 ? rolled : 0;

            if (target == null)
            {
                return new DamageAdjustment(amount, DamageEffect.Normal);
            }
            if (target.IsImmuneTo(type))
            {
                return new DamageAdjustment(0, DamageEffect.Immune);
            }

            bool resistant = target.IsResistantTo(type);
            bool vulnerable = target.IsVulnerableTo(type);

            if (resistant) { amount = amount / 2; }
            if (vulnerable) { amount = amount * 2; }

            // Having both cancels out as far as the label goes
            DamageEffect effect = DamageEffect.Normal;
            if (resistant && !vulnerable) { effect = DamageEffect.Resisted; }
            if (vulnerable && !resistant) { effect = DamageEffect.Vulnerable; }

            return new DamageAdjustment(amount, effect);
        }
    }
}
