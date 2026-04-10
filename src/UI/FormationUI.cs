using Godot;
using RogueCardGame.Core.Characters;
using RogueCardGame.Core.Combat;

namespace RogueCardGame.UI;

/// <summary>
/// Visual representation of the front/back row formation.
/// Shows player and enemy positions with switch-row controls.
/// </summary>
public partial class FormationUI : Control
{
    // Visual containers for each zone
    private Panel? _playerFrontZone;
    private Panel? _playerBackZone;
    private Panel? _enemyFrontZone;
    private Panel? _enemyBackZone;

    // Battlefield line
    private Panel? _battleLine;

    private CombatManager? _combat;

    public override void _Ready()
    {
        _playerFrontZone = GetNodeOrNull<Panel>("%PlayerFrontZone");
        _playerBackZone = GetNodeOrNull<Panel>("%PlayerBackZone");
        _enemyFrontZone = GetNodeOrNull<Panel>("%EnemyFrontZone");
        _enemyBackZone = GetNodeOrNull<Panel>("%EnemyBackZone");
        _battleLine = GetNodeOrNull<Panel>("%BattleLine");
    }

    public void BindCombat(CombatManager combat)
    {
        _combat = combat;
    }

    /// <summary>
    /// Refresh the formation display based on current combat state.
    /// </summary>
    public void RefreshFormation()
    {
        if (_combat == null) return;

        // Update zone highlights based on who's where
        UpdateZoneHighlight(_playerFrontZone,
            HasCombatantsInRow(_combat.Players, FormationRow.Front));
        UpdateZoneHighlight(_playerBackZone,
            HasCombatantsInRow(_combat.Players, FormationRow.Back));
        UpdateZoneHighlight(_enemyFrontZone,
            HasCombatantsInRow(_combat.Enemies, FormationRow.Front));
        UpdateZoneHighlight(_enemyBackZone,
            HasCombatantsInRow(_combat.Enemies, FormationRow.Back));
    }

    private bool HasCombatantsInRow<T>(List<T> combatants, FormationRow row) where T : Combatant
    {
        return combatants.Any(c =>
            c.IsAlive && _combat!.Formation.GetPosition(c.Id) == row);
    }

    private static void UpdateZoneHighlight(Panel? zone, bool hasUnits)
    {
        if (zone == null) return;

        var style = new StyleBoxFlat();
        style.BgColor = hasUnits
            ? new Color(0.2f, 0.3f, 0.5f, 0.3f)  // Active zone
            : new Color(0.1f, 0.1f, 0.15f, 0.2f); // Empty zone
        style.BorderColor = hasUnits
            ? new Color(0.4f, 0.6f, 0.9f, 0.5f)
            : new Color(0.2f, 0.2f, 0.3f, 0.3f);
        style.BorderWidthBottom = style.BorderWidthTop =
            style.BorderWidthLeft = style.BorderWidthRight = 1;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
            style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 4;

        zone.AddThemeStyleboxOverride("panel", style);
    }
}
