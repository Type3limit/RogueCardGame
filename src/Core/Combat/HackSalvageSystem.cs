namespace RogueCardGame.Core.Combat;

/// <summary>
/// Tracks hack progress on enemies and manages the Hack & Salvage system.
/// Integrates with CombatManager and the Parts currency.
/// </summary>
public class HackSystem
{
    /// <summary>Parts currency earned from combat/hacking.</summary>
    public int Parts { get; set; }

    /// <summary>Hack progress bonus from implants/relics.</summary>
    public int HackBonusPerAction { get; set; }

    public event Action<int, int>? OnPartsGained; // (amount, total)
    public event Action<int>? OnEnemyHacked; // enemyId
    public event Action<int, int, int>? OnHackProgress; // (enemyId, current, threshold)

    /// <summary>
    /// Add hack progress to an enemy.
    /// </summary>
    public bool AddProgress(Characters.Enemy enemy, int amount)
    {
        if (!enemy.CanBeHacked()) return false;

        int totalAmount = amount + HackBonusPerAction;
        enemy.StatusEffects.Apply(Characters.StatusType.HackProgress, totalAmount);

        int current = enemy.StatusEffects.GetStacks(Characters.StatusType.HackProgress);
        OnHackProgress?.Invoke(enemy.Id, current, enemy.Data.HackThreshold);

        if (enemy.TryHack())
        {
            OnEnemyHacked?.Invoke(enemy.Id);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Award Parts when an enemy is defeated.
    /// </summary>
    public void AwardParts(Characters.Enemy enemy, bool wasHacked)
    {
        int baseParts = enemy.Data.IsBoss ? 30 : enemy.Data.IsElite ? 15 : 5;
        int bonus = wasHacked ? baseParts / 2 : 0; // +50% parts for hacked enemies
        int total = baseParts + bonus;

        Parts += total;
        OnPartsGained?.Invoke(total, Parts);
    }

    /// <summary>
    /// Spend parts for card modification or implant crafting.
    /// </summary>
    public bool TrySpendParts(int amount)
    {
        if (Parts < amount) return false;
        Parts -= amount;
        return true;
    }
}

/// <summary>
/// Card modification options at Mod Stations.
/// </summary>
public enum ModificationType
{
    UpgradeCard,       // Standard upgrade (free with Parts discount)
    AddEffect,         // Add a secondary effect to a card
    ReduceCost,        // Permanently reduce card cost by 1
    ConvertRange,      // Change melee ↔ ranged
    AddLinkProperty,   // Make a card into a Link card
    RemoveCard         // Remove a card from deck
}

/// <summary>
/// Manages the Salvage/Modification station functionality.
/// </summary>
public class SalvageSystem
{
    /// <summary>Parts cost for each modification type.</summary>
    public static readonly Dictionary<ModificationType, int> ModCosts = new()
    {
        [ModificationType.UpgradeCard] = 15,
        [ModificationType.AddEffect] = 25,
        [ModificationType.ReduceCost] = 30,
        [ModificationType.ConvertRange] = 20,
        [ModificationType.AddLinkProperty] = 35,
        [ModificationType.RemoveCard] = 10,
    };

    public bool CanAfford(HackSystem hackSystem, ModificationType mod) =>
        hackSystem.Parts >= ModCosts.GetValueOrDefault(mod, int.MaxValue);

    /// <summary>
    /// Apply a modification to a card.
    /// </summary>
    public bool ApplyModification(
        HackSystem hackSystem,
        Cards.Card card,
        ModificationType mod)
    {
        int cost = ModCosts.GetValueOrDefault(mod, int.MaxValue);
        if (!hackSystem.TrySpendParts(cost)) return false;

        switch (mod)
        {
            case ModificationType.UpgradeCard:
                card.Upgrade();
                break;

            case ModificationType.ReduceCost:
                card.CurrentCost = Math.Max(0, card.CurrentCost - 1);
                break;

            // Other modifications would require a more complex card mutation system
            // For now, these are placeholders
            case ModificationType.AddEffect:
            case ModificationType.ConvertRange:
            case ModificationType.AddLinkProperty:
                break;
        }

        return true;
    }
}
