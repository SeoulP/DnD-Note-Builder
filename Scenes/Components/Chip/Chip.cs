using DndBuilder.Core;
using Godot;
using System;

// Static factory for badge/chip UI nodes used across the app.
public static class Chip
{
    // Alias chip: inherits panel theme, 4px margins, label + hover-delete × button. No confirm dialog.
    public static Control MakeAlias(string text, Action onDelete)
    {
        var deleteHover = new StyleBoxFlat { BgColor = ThemeManager.DeleteHoverColor };
        deleteHover.SetCornerRadiusAll(4);

        var chip  = new PanelContainer();
        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left",   4);
        inner.AddThemeConstantOverride("margin_right",  4);
        inner.AddThemeConstantOverride("margin_top",    4);
        inner.AddThemeConstantOverride("margin_bottom", 4);
        var hrow = new HBoxContainer(); hrow.AddThemeConstantOverride("separation", 2);
        var lbl  = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        var rmBtn = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        rmBtn.AddThemeFontSizeOverride("font_size", 11);
        rmBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", deleteHover);
        rmBtn.MouseExited  += () => chip.RemoveThemeStyleboxOverride("panel");
        rmBtn.Pressed      += () => onDelete();
        hrow.AddChild(lbl); hrow.AddChild(rmBtn);
        inner.AddChild(hrow);
        chip.AddChild(inner);
        return chip;
    }

    // Compact chip: text-width only (no ExpandFill), optional tooltip, confirm dialog before delete.
    public static Control MakeCompact(string text, Action onDelete, string tooltip = null)
    {
        var deleteHover = new StyleBoxFlat { BgColor = ThemeManager.DeleteHoverColor };
        deleteHover.SetCornerRadiusAll(4);

        var chip = new PanelContainer();
        if (!string.IsNullOrEmpty(tooltip)) chip.TooltipText = tooltip;

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left",   4);
        inner.AddThemeConstantOverride("margin_right",  4);
        inner.AddThemeConstantOverride("margin_top",    3);
        inner.AddThemeConstantOverride("margin_bottom", 3);

        var hrow  = new HBoxContainer(); hrow.AddThemeConstantOverride("separation", 2);
        var lbl   = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        var rmBtn = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        rmBtn.AddThemeFontSizeOverride("font_size", 11);
        rmBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", deleteHover);
        rmBtn.MouseExited  += () => chip.RemoveThemeStyleboxOverride("panel");

        var confirmDlg = DialogHelper.Make(text: "Remove this trait? This cannot be undone.");
        confirmDlg.Confirmed += () => { onDelete(); chip.QueueFree(); };
        chip.AddChild(confirmDlg);
        rmBtn.Pressed += () => DialogHelper.Show(confirmDlg);

        hrow.AddChild(lbl); hrow.AddChild(rmBtn);
        inner.AddChild(hrow);
        chip.AddChild(inner);
        return chip;
    }

    // Read-only pill: blue background, optional tooltip. No delete.
    public static Control MakePill(string text, string tooltip = null)
    {
        var pill = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Stop };
        pill.AddThemeFontSizeOverride("font_size", 11);
        pill.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor              = new Color(0.24f, 0.31f, 0.50f),
            ContentMarginLeft    = 6, ContentMarginRight  = 6,
            ContentMarginTop     = 2, ContentMarginBottom = 2,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight    = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        if (!string.IsNullOrEmpty(tooltip)) pill.TooltipText = tooltip;
        return pill;
    }
}
