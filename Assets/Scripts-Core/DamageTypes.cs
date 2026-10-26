namespace DnDCombat.Core
{
    public enum DamageType
    {
        Acid, Bludgeoning, Cold, Fire, Force, Lightning, Necrotic,
        Piercing, Poison, Psychic, Radiant, Slashing, Thunder
    }

    // What the target's damage rules did to an attack, so the UI can say it
    public enum DamageEffect { Normal, Resisted, Vulnerable, Immune }
}
