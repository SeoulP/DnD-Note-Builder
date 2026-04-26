using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;
using System;

public static class AliasChipsHelper
{
    public static void Reload(
        VBoxContainer container,
        DatabaseService db,
        string entityType,
        int entityId,
        int campaignId,
        Action onReload)
    {
        if (container == null) return;
        foreach (Node child in container.GetChildren()) child.QueueFree();

        var chipsRow = new HBoxContainer();
        chipsRow.AddThemeConstantOverride("separation", 4);
        container.AddChild(chipsRow);

        foreach (var alias in db.EntityAliases.GetForEntity(entityType, entityId))
        {
            int capturedId = alias.Id;
            var deleteHover = new StyleBoxFlat { BgColor = ThemeManager.DeleteHoverColor };
            deleteHover.SetCornerRadiusAll(4);
            // ContentMargin = 0 — MarginContainer below provides consistent padding in both states.

            var chip = new PanelContainer();
            // No panel override — inherits theme default, same as EntityRow at rest.
            var innerMargin = new MarginContainer();
            innerMargin.AddThemeConstantOverride("margin_left",   4);
            innerMargin.AddThemeConstantOverride("margin_right",  4);
            innerMargin.AddThemeConstantOverride("margin_top",    4);
            innerMargin.AddThemeConstantOverride("margin_bottom", 4);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);
            var lbl = new Label { Text = alias.Alias };
            lbl.AddThemeFontSizeOverride("font_size", 11);
            var rmBtn = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            rmBtn.AddThemeFontSizeOverride("font_size", 11);
            rmBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", deleteHover);
            rmBtn.MouseExited  += () => chip.RemoveThemeStyleboxOverride("panel");
            rmBtn.Pressed      += () => { db.EntityAliases.Delete(capturedId); onReload(); };
            row.AddChild(lbl); row.AddChild(rmBtn);
            innerMargin.AddChild(row);
            chip.AddChild(innerMargin); chipsRow.AddChild(chip);
        }

        var addInput = new LineEdit
        {
            PlaceholderText     = "+ alias",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(80, 0),
        };
        addInput.TextSubmitted += text =>
        {
            string t = text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            addInput.Text = "";
            db.EntityAliases.Add(new EntityAlias { CampaignId = campaignId, EntityType = entityType, EntityId = entityId, Alias = t });
            onReload();
        };
        container.AddChild(addInput);
    }
}
