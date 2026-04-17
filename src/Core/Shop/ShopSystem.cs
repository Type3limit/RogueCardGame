using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Implants;
using RogueCardGame.Core.Potions;

namespace RogueCardGame.Core.Shop;

/// <summary>
/// A single item for sale in the shop.
/// </summary>
public class ShopItem
{
    public string Id { get; }
    public string Name { get; }
    public int Price { get; set; }
    public ShopItemType Type { get; }
    public bool IsSold { get; set; }

    // The actual item data (one of these is non-null)
    public CardData? CardData { get; init; }
    public PotionData? PotionData { get; init; }
    public ImplantData? ImplantData { get; init; }

    public ShopItem(string id, string name, int price, ShopItemType type)
    {
        Id = id;
        Name = name;
        Price = price;
        Type = type;
    }
}

public enum ShopItemType
{
    Card,
    Potion,
    Implant,
    CardRemoval
}

/// <summary>
/// Generates and manages a shop's inventory.
/// </summary>
public class ShopManager
{
    private readonly SeededRandom _random;
    public List<ShopItem> Items { get; } = [];
    public int CardRemovalCost { get; set; } = BalanceConfig.Current.GlobalBalance.CardRemovalCost;

    /// <summary>Price multiplier based on act number. Act 1 = 1.0x, Act 2 = 1.25x, Act 3 = 1.5x.</summary>
    public float ActPriceMultiplier { get; private set; } = 1f;

    public ShopManager(SeededRandom random)
    {
        _random = random;
    }

    /// <summary>
    /// Generate shop inventory for the current act/floor.
    /// Prices scale with act number.
    /// </summary>
    public void GenerateShop(
        CardDatabase cardDb,
        PotionDatabase potionDb,
        ImplantDatabase implantDb,
        CardClass playerClass,
        int actNumber)
    {
        Items.Clear();
        ActPriceMultiplier = 1f + (actNumber - 1) * 0.25f;

        // 5 cards (mix of class and colorless)
        GenerateCardItems(cardDb, playerClass, 5);

        // 2 potions
        GeneratePotionItems(potionDb, 2);

        // 1 implant
        GenerateImplantItems(implantDb, playerClass, 1);

        // Card removal service (scales with act)
        int removalCost = ScalePrice(CardRemovalCost);
        Items.Add(new ShopItem("card_removal", "移除卡牌", removalCost, ShopItemType.CardRemoval));
    }

    private int ScalePrice(int basePrice) => (int)(basePrice * ActPriceMultiplier);

    private void GenerateCardItems(CardDatabase cardDb, CardClass playerClass, int count)
    {
        var classCards = cardDb.GetCardsByClass(playerClass)
            .Where(c => c.Rarity != CardRarity.Starter)
            .ToList();
        var colorlessCards = cardDb.GetCardsByClass(CardClass.Colorless)
            .Where(c => c.Rarity != CardRarity.Starter)
            .ToList();

        var pool = classCards.Concat(colorlessCards).ToList();
        _random.Shuffle(pool);

        foreach (var card in pool.Take(count))
        {
            int price = ScalePrice(GetCardPrice(card.Rarity));
            Items.Add(new ShopItem(card.Id, card.Name, price, ShopItemType.Card)
            {
                CardData = card
            });
        }
    }

    private void GeneratePotionItems(PotionDatabase potionDb, int count)
    {
        var pool = potionDb.GetAll();
        _random.Shuffle(pool);

        foreach (var potion in pool.Take(count))
        {
            int price = ScalePrice(GetPotionPrice(potion.Rarity));
            Items.Add(new ShopItem(potion.Id, potion.Name, price, ShopItemType.Potion)
            {
                PotionData = potion
            });
        }
    }

    private void GenerateImplantItems(ImplantDatabase implantDb, CardClass playerClass, int count)
    {
        var pool = implantDb.GetForClass(playerClass);
        _random.Shuffle(pool);

        foreach (var implant in pool.Take(count))
        {
            int price = ScalePrice(GetImplantPrice(implant.Rarity));
            Items.Add(new ShopItem(implant.Id, implant.Name, price, ShopItemType.Implant)
            {
                ImplantData = implant
            });
        }
    }

    private static int GetCardPrice(CardRarity rarity)
    {
        var prices = BalanceConfig.Current.ShopPrices;
        return rarity switch
        {
            CardRarity.Common => prices.CardCommon,
            CardRarity.Uncommon => prices.CardUncommon,
            CardRarity.Rare => prices.CardRare,
            _ => prices.CardCommon
        };
    }

    private static int GetPotionPrice(PotionRarity rarity)
    {
        var prices = BalanceConfig.Current.ShopPrices;
        return rarity switch
        {
            PotionRarity.Common => prices.PotionCommon,
            PotionRarity.Uncommon => prices.PotionUncommon,
            PotionRarity.Rare => prices.PotionRare,
            _ => prices.PotionCommon
        };
    }

    private static int GetImplantPrice(ImplantRarity rarity)
    {
        var prices = BalanceConfig.Current.ShopPrices;
        return rarity switch
        {
            ImplantRarity.Common => prices.ImplantCommon,
            ImplantRarity.Uncommon => prices.ImplantUncommon,
            ImplantRarity.Rare => prices.ImplantRare,
            ImplantRarity.Legendary => prices.ImplantLegendary,
            _ => prices.ImplantCommon
        };
    }
}
