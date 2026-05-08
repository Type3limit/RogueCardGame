using RogueCardGame.Core;
using RogueCardGame.Core.Cards;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;
using RogueCardGame.Core.Combat.Actions;

namespace RogueCardGame.Tests;

public class EffectTrackingTests
{
    static EffectTrackingTests()
    {
        BalanceConfig.LoadFromFile("../../data/balance/balance.json");
    }

    [Fact]
    public void DealDamageAction_should_record_actual_hp_damage_in_current_effect_context()
    {
        var source = PlayerCharacter.CreateSymbiote("Source");
        var target = CreateEnemy("Target", 50);
        target.Block = 4;

        var formation = new FormationSystem();
        formation.SetPosition(source.Id, FormationRow.Front);
        formation.SetPosition(target.Id, FormationRow.Front);

        var context = CreateContext(source, target, formation, new AggroSystem());
        var manager = new ActionManager { CurrentEffectContext = context };

        var action = new DealDamageAction(source, [target], 10, CardRange.Melee, formation, new AggroSystem());
        manager.AddToBottom(action);
        manager.ProcessAll();

        Assert.Equal(6, context.LastDamageDealt);
        Assert.Equal(6, context.TotalDamageDealtThisCard);
        Assert.Equal(44, target.CurrentHp);
        Assert.Equal(0, target.Block);
    }

    [Fact]
    public void SelfDamageAction_should_record_actual_hp_loss_in_current_effect_context()
    {
        var source = PlayerCharacter.CreateSymbiote("Source");
        source.CurrentHp = 7;
        var context = CreateContext(source, source, new FormationSystem(), new AggroSystem());
        var manager = new ActionManager { CurrentEffectContext = context };

        var action = new SelfDamageAction(source, 10);
        manager.AddToBottom(action);
        manager.ProcessAll();

        Assert.Equal(6, context.LastSelfHpLoss);
        Assert.Equal(6, context.TotalSelfHpLossThisCard);
        Assert.Equal(1, source.CurrentHp);
    }

    [Fact]
    public void LifestealEffect_should_heal_from_last_recorded_damage_only()
    {
        var source = PlayerCharacter.CreateSymbiote("Source");
        source.CurrentHp = 40;
        var target = CreateEnemy("Target", 100);
        target.CurrentHp = 30; // pre-existing damage should not affect heal

        var actions = new ActionManager();
        var context = CreateContext(source, target, new FormationSystem(), new AggroSystem(), actions);
        context.LastDamageDealt = 12;
        context.TotalDamageDealtThisCard = 20;

        var effect = new LifestealEffect(50);
        effect.Execute(context);
        actions.ProcessAll();

        Assert.Equal(46, source.CurrentHp);
    }

    [Fact]
    public void LifestealEffect_should_not_heal_when_last_damage_is_zero()
    {
        var source = PlayerCharacter.CreateSymbiote("Source");
        source.CurrentHp = 40;
        var target = CreateEnemy("Target", 100);

        var actions = new ActionManager();
        var context = CreateContext(source, target, new FormationSystem(), new AggroSystem(), actions);
        context.LastDamageDealt = 0;

        var effect = new LifestealEffect(50);
        effect.Execute(context);
        actions.ProcessAll();

        Assert.Equal(40, source.CurrentHp);
    }

    [Fact]
    public void DamageFromHpLostEffect_should_use_tracked_self_hp_loss_for_damage()
    {
        var source = PlayerCharacter.CreateSymbiote("Source");
        source.CurrentHp = 30;
        source.MaxHp = 75;
        var target = CreateEnemy("Target", 50);

        var formation = new FormationSystem();
        formation.SetPosition(source.Id, FormationRow.Front);
        formation.SetPosition(target.Id, FormationRow.Front);

        var actions = new ActionManager();
        var context = CreateContext(source, target, formation, new AggroSystem(), actions);
        context.TotalSelfHpLossThisCard = 9;

        var effect = new DamageFromHpLostEffect(3);
        effect.Execute(context);
        actions.ProcessAll();

        // hpLost=9, multiplier=3 → damage=27; target 50-27=23
        Assert.Equal(23, target.CurrentHp);
    }

    private static CardEffectContext CreateContext(
        Combatant source,
        Combatant target,
        FormationSystem formation,
        AggroSystem aggro,
        ActionManager? actions = null)
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
            LastDamageDealt = 0,
            TotalDamageDealtThisCard = 0,
            LastSelfHpLoss = 0,
            TotalSelfHpLossThisCard = 0
        };
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
