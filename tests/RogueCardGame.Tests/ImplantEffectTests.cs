using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Actions;
using RogueCardGame.Core.Combat.Powers;
using RogueCardGame.Core.Deck;
using RogueCardGame.Core.Implants;

namespace RogueCardGame.Tests;

public class ImplantEffectTests
{
    static ImplantEffectTests()
    {
        BalanceConfig.LoadFromFile("../../data/balance/balance.json");
    }

    [Fact]
    public void StartCombat_should_draw_bonus_cards_from_resonance_threshold_when_implant_is_equipped()
    {
        var player = PlayerCharacter.CreatePsion("Psion");
        var enemy = CreateEnemy("Enemy", 40);
        var deckCards = Enumerable.Range(0, 20).Select(i => CreateCard($"card_{i}")).ToList();
        var implants = CreateImplantManager(
            CreateCoreImplant(
                "psion_resonance_core",
                ("resonanceDecayHalf", 1),
                ("resonanceDrawBonus", 5)));

        var combat = new CombatManager(123);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = deckCards
        }, implants: implants);

        player.Powers.ApplyPower(new ResonancePower { Amount = 10 }, player);
        combat.StartCombat();

        Assert.Equal(7, combat.PlayerDecks[player.Id].Hand.Count);
    }

    [Fact]
    public void ResonancePower_should_decay_every_other_turn_when_half_decay_implant_is_active()
    {
        var player = PlayerCharacter.CreatePsion("Psion");
        player.Powers.ImplantBonusProvider = effectType => effectType == "resonanceDecayHalf" ? 1 : 0;
        player.Powers.ApplyPower(new ResonancePower { Amount = 4 }, player);

        player.OnTurnEnd();
        Assert.Equal(3, player.Powers.GetStacks(CommonPowerIds.Resonance));

        player.OnTurnEnd();
        Assert.Equal(3, player.Powers.GetStacks(CommonPowerIds.Resonance));

        player.OnTurnEnd();
        Assert.Equal(2, player.Powers.GetStacks(CommonPowerIds.Resonance));
    }

    [Fact]
    public void ProtocolStackPower_should_clamp_to_expanded_max_stacks_from_implant()
    {
        var player = PlayerCharacter.CreateNetrunner("Runner");
        player.Powers.ImplantBonusProvider = effectType => effectType == "protocolStackSize" ? 2 : 0;

        player.Powers.ApplyPower(new ProtocolStackPower { Amount = 11 }, player);
        player.Powers.ApplyPower(new ProtocolStackPower { Amount = 5 }, player);

        var power = Assert.IsType<ProtocolStackPower>(player.Powers.GetPower(CommonPowerIds.ProtocolStack));
        Assert.Equal(12, power.Amount);
        Assert.Equal(12, power.MaxStacks);
    }

    [Fact]
    public void DoubleProtocolEffect_should_respect_implant_expanded_max_stacks()
    {
        var player = PlayerCharacter.CreateNetrunner("Runner");
        player.Powers.ImplantBonusProvider = effectType => effectType == "protocolStackSize" ? 2 : 0;
        player.Powers.ApplyPower(new ProtocolStackPower { Amount = 7 }, player);

        var context = CreateContext(player, player, new FormationSystem(), new AggroSystem());
        var effect = new DoubleProtocolEffect(20);

        effect.Execute(context);

        Assert.Equal(12, player.Powers.GetStacks(CommonPowerIds.ProtocolStack));
    }

    [Fact]
    public void HackEffect_should_scale_hack_progress_with_implant_bonus()
    {
        var source = PlayerCharacter.CreateNetrunner("Runner");
        var target = CreateEnemy("Enemy", 40);
        var actions = new ActionManager();
        var context = CreateContext(
            source,
            target,
            new FormationSystem(),
            new AggroSystem(),
            actions,
            effectType => effectType == "hackSpeedBonus" ? 50 : 0);

        var effect = new HackEffect(20);
        effect.Execute(context);
        actions.ProcessAll();

        Assert.Equal(30, target.Powers.GetStacks(CommonPowerIds.Hacked));
    }

    [Fact]
    public void SelfDamageEffects_should_halve_self_damage_when_symbiote_implant_is_active()
    {
        var source = PlayerCharacter.CreateSymbiote("Symbiote");
        source.CurrentHp = 20;

        var flatActions = new ActionManager();
        var flatContext = CreateContext(
            source,
            source,
            new FormationSystem(),
            new AggroSystem(),
            flatActions,
            effectType => effectType == "erosionSelfDamageHalf" ? 1 : 0);
        new SelfDamageEffect(5).Execute(flatContext);
        flatActions.ProcessAll();
        Assert.Equal(18, source.CurrentHp);

        source.CurrentHp = 20;
        var percentActions = new ActionManager();
        var percentContext = CreateContext(
            source,
            source,
            new FormationSystem(),
            new AggroSystem(),
            percentActions,
            effectType => effectType == "erosionSelfDamageHalf" ? 1 : 0);
        new SelfDamagePercentEffect(50).Execute(percentContext);
        percentActions.ProcessAll();
        Assert.Equal(15, source.CurrentHp);
    }

    [Fact]
    public void DamageFromHpLostEffect_should_gain_symbiote_bonus_damage_from_implant()
    {
        var source = PlayerCharacter.CreateSymbiote("Symbiote");
        var target = CreateEnemy("Enemy", 50);
        var formation = new FormationSystem();
        formation.SetPosition(source.Id, FormationRow.Front);
        formation.SetPosition(target.Id, FormationRow.Front);

        var actions = new ActionManager();
        var context = CreateContext(
            source,
            target,
            formation,
            new AggroSystem(),
            actions,
            effectType => effectType == "erosionBonusDamage" ? 25 : 0);
        context.TotalSelfHpLossThisCard = 6;

        new DamageFromHpLostEffect(3).Execute(context);
        actions.ProcessAll();

        // hpLost=6, multiplier=3 → damage=18; 25% erosion bonus → ceil(18*1.25)=23; target 50-23=27
        Assert.Equal(27, target.CurrentHp);
    }

    [Fact]
    public void BattlefieldInterface_should_make_row_switch_free_and_discount_next_card()
    {
        var player = PlayerCharacter.CreateVanguard("Vanguard");
        var enemy = CreateEnemy("Enemy", 40);
        var firstCard = CreateCard("first_skill");
        var secondCard = CreateCard("second_skill");
        var implants = CreateImplantManager(
            CreateImplant(ImplantSlot.Neural, "battlefield_interface", ("battlefieldInterface", 1)));

        var combat = new CombatManager(404);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = [firstCard, secondCard]
        }, implants: implants);
        combat.StartCombat();

        int energyBeforeSwitch = player.CurrentEnergy;
        Assert.True(combat.TrySwitchRow(player));
        Assert.Equal(energyBeforeSwitch, player.CurrentEnergy);

        var hand = combat.PlayerDecks[player.Id].Hand;
        Assert.True(combat.TryPlayCard(player, hand.First(c => c.Data.Id == "first_skill")));
        Assert.Equal(energyBeforeSwitch, player.CurrentEnergy);

        Assert.True(combat.TryPlayCard(player, hand.First(c => c.Data.Id == "second_skill")));
        Assert.Equal(energyBeforeSwitch - 1, player.CurrentEnergy);
    }

    [Fact]
    public void PrecognitionModule_should_trigger_scry_on_even_player_turn_and_reorder_draw_pile()
    {
        var player = PlayerCharacter.CreatePsion("Psion");
        var enemy = CreateEnemy("Enemy", 40);
        var deckCards = Enumerable.Range(0, 12).Select(i => CreateCard($"card_{i}")).ToList();
        var implants = CreateImplantManager(
            CreateImplant(ImplantSlot.Neural, "precognition_module", ("precognitionModule", 1)));

        var combat = new CombatManager(818);
        combat.Initialize([player], [enemy], new Dictionary<int, List<Card>>
        {
            [player.Id] = deckCards
        }, implants: implants);

        Card? chosen = null;
        int triggerCount = 0;
        combat.OnScryTriggered += (actor, deck, peeked, keepCount) =>
        {
            triggerCount++;
            Assert.Equal(player, actor);
            Assert.Equal(1, keepCount);
            Assert.Equal(2, peeked.Count);
            chosen = peeked[1];
            combat.CompleteScry(actor, deck, peeked, chosen);
        };

        combat.StartCombat();
        Assert.Equal(0, triggerCount);

        combat.EndPlayerTurn();

        Assert.Equal(1, triggerCount);
        Assert.NotNull(chosen);

        var drawn = combat.PlayerDecks[player.Id].Draw(1);
        Assert.Same(chosen, Assert.Single(drawn));
    }

    private static CardEffectContext CreateContext(
        Combatant source,
        Combatant target,
        FormationSystem formation,
        AggroSystem aggro,
        ActionManager? actions = null,
        Func<string, int>? implantBonusProvider = null)
    {
        return new CardEffectContext
        {
            Source = source,
            Target = target,
            AllTargets = [target],
            Formation = formation,
            Aggro = aggro,
            Actions = actions,
            Range = CardRange.Melee,
            ImplantBonusProvider = implantBonusProvider,
            LastDamageDealt = 0,
            TotalDamageDealtThisCard = 0,
            LastSelfHpLoss = 0,
            TotalSelfHpLossThisCard = 0
        };
    }

    private static ImplantManager CreateImplantManager(params ImplantData[] implants)
    {
        var manager = new ImplantManager();
        foreach (var implant in implants)
            manager.Equip(implant);
        return manager;
    }

    private static ImplantData CreateCoreImplant(string id, params (string type, int value)[] effects)
    {
        return CreateImplant(ImplantSlot.Core, id, effects);
    }

    private static ImplantData CreateImplant(
        ImplantSlot slot,
        string id,
        params (string type, int value)[] effects)
    {
        return new ImplantData
        {
            Id = id,
            Name = id,
            Description = id,
            Slot = slot,
            Rarity = ImplantRarity.Rare,
            Effects = effects.Select(effect => new ImplantEffectData
            {
                Type = effect.type,
                Value = effect.value
            }).ToList()
        };
    }

    private static Card CreateCard(string id)
    {
        return new Card(new CardData
        {
            Id = id,
            Name = id,
            Class = CardClass.Neutral,
            Rarity = CardRarity.Common,
            Type = CardType.Skill,
            Cost = 1,
            Description = id
        });
    }

    private static Enemy CreateEnemy(string name, int maxHp)
    {
        return new Enemy(new EnemyData
        {
            Id = name.ToLowerInvariant(),
            Name = name,
            MaxHp = maxHp,
            PreferredRow = FormationRow.Front,
            IntentPatterns = []
        });
    }
}
