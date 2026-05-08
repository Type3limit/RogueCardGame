using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using RogueCardGame.Core.Cards;

namespace RogueCardGame.Tests;

public class RarityDebugTest
{
    [Fact]
    public void Should_deserialize_rare_rarity()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var result = JsonSerializer.Deserialize<CardRarity>("\"rare\"", opts);
        Assert.Equal(CardRarity.Rare, result);
    }

    [Fact]
    public void Should_deserialize_uncommon_rarity()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var result = JsonSerializer.Deserialize<CardRarity>("\"uncommon\"", opts);
        Assert.Equal(CardRarity.Uncommon, result);
    }
}
