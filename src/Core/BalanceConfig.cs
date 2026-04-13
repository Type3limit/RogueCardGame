using System.Text.Json;
using System.Text.Json.Serialization;

namespace RogueCardGame.Core;

/// <summary>
/// JSON-driven balance configuration loaded from data/balance/balance.json.
/// All gameplay constants are externalized here for easy tuning.
/// </summary>
public sealed class BalanceConfig
{
    public GlobalBalanceDef GlobalBalance { get; init; } = new();
    public FormationBalanceDef Formation { get; init; } = new();
    public AggroBalanceDef Aggro { get; init; } = new();
    public ShopPricesDef ShopPrices { get; init; } = new();
    public MapGenerationDef MapGeneration { get; init; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Singleton instance. Loaded once on startup.
    /// Falls back to hardcoded defaults if file not found.
    /// </summary>
    public static BalanceConfig Current { get; private set; } = new();

    public static void LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return;

        string json = File.ReadAllText(path);
        Current = JsonSerializer.Deserialize<BalanceConfig>(json, JsonOptions) ?? new();
    }
}

public sealed class GlobalBalanceDef
{
    public int BaseDrawPerTurn { get; init; } = 5;
    public int BaseEnergyPerTurn { get; init; } = 3;
    public int MaxHandSize { get; init; } = 10;
    public int MaxPotionSlots { get; init; } = 3;
    public int StartingGold { get; init; } = 99;
    public int RestHealPercent { get; init; } = 30;
    public int EliteGoldReward { get; init; } = 50;
    public int BossGoldReward { get; init; } = 100;
    public int NormalGoldRewardMin { get; init; } = 15;
    public int NormalGoldRewardMax { get; init; } = 30;
    public float PotionDropChance { get; init; } = 0.4f;
    public int CardRemovalCost { get; init; } = 75;
}

public sealed class FormationBalanceDef
{
    public float BackRowFirstRangedBonus { get; init; } = 1.20f;
    public int FrontRowBlockBonus { get; init; } = 3;
    public int RowSwitchEnergyCost { get; init; } = 1;
}

public sealed class AggroBalanceDef
{
    public float AggroPerDamage { get; init; } = 1.0f;
    public float AggroPerHealing { get; init; } = 0.5f;
    public float AggroPerBuff { get; init; } = 2.0f;
    public float AggroDecayRate { get; init; } = 0.8f;
    public float TaunterBonusAggro { get; init; } = 100.0f;
}

public sealed class ShopPricesDef
{
    public int CardCommon { get; init; } = 50;
    public int CardUncommon { get; init; } = 75;
    public int CardRare { get; init; } = 150;
    public int PotionCommon { get; init; } = 50;
    public int PotionUncommon { get; init; } = 75;
    public int PotionRare { get; init; } = 100;
    public int ImplantCommon { get; init; } = 100;
    public int ImplantUncommon { get; init; } = 175;
    public int ImplantRare { get; init; } = 250;
    public int ImplantLegendary { get; init; } = 400;
}

public sealed class MapGenerationDef
{
    public int ActsTotal { get; init; } = 3;
    public int FloorsPerAct { get; init; } = 15;
    public int NodesPerRowMin { get; init; } = 2;
    public int NodesPerRowMax { get; init; } = 4;
    public float BranchChance { get; init; } = 0.4f;
    public float EventChance { get; init; } = 0.15f;
    public float EliteChanceBase { get; init; } = 0.2f;
    public float ShopChance { get; init; } = 0.10f;
    public float RestChance { get; init; } = 0.10f;
}
