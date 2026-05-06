using DndBuilder.Core;
using Godot;
using System;

// Builds the ability row used inside level-progression accordion rows (DnD5eClass, Species, DnD5eSubclass).
public static class LevelAbilityRow
{
    public static HBoxContainer Make(
        string abilityName,
        string initialFormula,
        Action onNavigate,
        Action<string> onFormulaSaved,
        Action onDelete,
        Action<Node> addPopupChild)
    {
        var row     = new HBoxContainer();
        var nameBtn = new Button
        {
            Text                    = abilityName,
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            SizeFlagsHorizontal     = Control.SizeFlags.ExpandFill,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        nameBtn.Pressed += () => onNavigate();

        string formula  = initialFormula;
        var usesBtn = new Button
        {
            Text              = DnD5eUsesFormula.FormatForDisplay(formula),
            CustomMinimumSize = new Vector2(80, 0),
            TooltipText       = "Click to edit usage scaling",
        };
        usesBtn.Pressed += () =>
        {
            var popup = new DnD5eUsageProgressionPopup();
            addPopupChild(popup);
            popup.Setup(abilityName, formula);
            popup.Saved += newFormula =>
            {
                formula      = newFormula;
                onFormulaSaved(newFormula);
                usesBtn.Text = DnD5eUsesFormula.FormatForDisplay(newFormula);
            };
            popup.PopupCentered();
        };

        var delBtn = new Button { Text = "×", Flat = true };
        delBtn.Pressed += () => onDelete();

        row.AddChild(nameBtn);
        row.AddChild(usesBtn);
        row.AddChild(delBtn);
        return row;
    }
}
