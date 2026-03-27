using System;
using DndBuilder.Core;
using Godot;

/// <summary>
/// Formula editor for ability uses in level progression rows.
/// Formula string format: "base|level|prof|attr"
///   base  = integer (0+)
///   level = "" | "full" | "half" | "ceil" | "double"
///   prof  = "" | "prof"
///   attr  = "" | "str" | "dex" | "con" | "int" | "wis" | "cha"
/// Special values: "--" = no uses specified, plain integer = flat base only (legacy/simple).
/// Usage: AddChild(popup), Setup(...), subscribe to Saved, PopupCentered().
/// Popup calls QueueFree() on save or cancel.
/// </summary>
public partial class UsageProgressionPopup : Window
{
    public event Action<string> Saved;

    private SpinBox      _baseSpin;
    private OptionButton _levelOption;
    private CheckBox     _profCheck;
    private OptionButton _attrOption;
    private Label        _previewLabel;

    private static readonly string[] LevelLabels = { "None", "× Full Level", "× Half Level (floor)", "× Half Level (ceil)", "× Double Level" };
    private static readonly string[] LevelKeys   = { "",     "full",         "half",                  "ceil",                "double" };
    private static readonly string[] AttrLabels  = { "None", "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
    private static readonly string[] AttrKeys    = { "",     "str",      "dex",       "con",          "int",          "wis",   "cha" };

    public override void _Ready()
    {
        Exclusive        = true;
        Unresizable      = true;
        Size             = new Vector2I(380, 310);
        CloseRequested  += QueueFree;

        var root = new MarginContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("margin_left",   12);
        root.AddThemeConstantOverride("margin_right",  12);
        root.AddThemeConstantOverride("margin_top",    12);
        root.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(root);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 10);
        root.AddChild(vbox);

        // ── Fields ───────────────────────────────────────────────────────────
        _baseSpin = new SpinBox { MinValue = 0, MaxValue = 999, Step = 1, Value = 0, CustomMinimumSize = new Vector2(80, 0) };
        _baseSpin.ValueChanged += _ => UpdatePreview();
        vbox.AddChild(MakeRow("Base Uses", _baseSpin));

        _levelOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var lbl in LevelLabels) _levelOption.AddItem(lbl);
        _levelOption.ItemSelected += _ => UpdatePreview();
        vbox.AddChild(MakeRow("+ Level Scale", _levelOption));

        _profCheck = new CheckBox { Text = "Add Proficiency Bonus" };
        _profCheck.Toggled += _ => UpdatePreview();
        vbox.AddChild(_profCheck);

        _attrOption = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var lbl in AttrLabels) _attrOption.AddItem(lbl);
        _attrOption.ItemSelected += _ => UpdatePreview();
        vbox.AddChild(MakeRow("+ Attr Modifier", _attrOption));

        // ── Preview ──────────────────────────────────────────────────────────
        vbox.AddChild(new HSeparator());

        _previewLabel = new Label { Text = "Formula: 0" };
        vbox.AddChild(_previewLabel);

        // ── Footer ───────────────────────────────────────────────────────────
        var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vbox.AddChild(spacer);

        vbox.AddChild(new HSeparator());

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(footer);

        var clearBtn      = new Button { Text = "Clear (no uses)" };
        var footerSpacer  = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var saveBtn       = new Button { Text = "Save" };
        var cancelBtn     = new Button { Text = "Cancel" };

        clearBtn.Pressed  += () => { Saved?.Invoke("--"); QueueFree(); };
        saveBtn.Pressed   += OnSave;
        cancelBtn.Pressed += QueueFree;

        footer.AddChild(clearBtn);
        footer.AddChild(footerSpacer);
        footer.AddChild(saveBtn);
        footer.AddChild(cancelBtn);
    }

    public void Setup(string abilityName, string currentFormula)
    {
        Title = $"Uses Formula — {abilityName}";
        ParseAndLoad(currentFormula);
        UpdatePreview();
    }

    // ── Formula parsing / building ────────────────────────────────────────────

    private void ParseAndLoad(string formula)
    {
        _baseSpin.Value          = 0;
        _levelOption.Selected    = 0;
        _profCheck.ButtonPressed = false;
        _attrOption.Selected     = 0;

        if (string.IsNullOrEmpty(formula) || formula == "--") return;

        // Legacy: plain integer → just set base
        if (int.TryParse(formula, out int flat)) { _baseSpin.Value = flat; return; }

        // Full formula: "base|level|prof|attr"
        var p = formula.Split('|');
        if (p.Length < 4) return;

        if (int.TryParse(p[0], out int b)) _baseSpin.Value = b;

        int li = Array.IndexOf(LevelKeys, p[1]);
        _levelOption.Selected = li >= 0 ? li : 0;

        _profCheck.ButtonPressed = p[2] == "prof";

        int ai = Array.IndexOf(AttrKeys, p[3]);
        _attrOption.Selected = ai >= 0 ? ai : 0;
    }

    private string BuildFormula()
    {
        int    baseVal  = (int)_baseSpin.Value;
        string levelKey = LevelKeys[_levelOption.Selected];
        string profPart = _profCheck.ButtonPressed ? "prof" : "";
        string attrKey  = AttrKeys[_attrOption.Selected];

        // All empty/zero → no uses
        if (baseVal == 0 && levelKey == "" && profPart == "" && attrKey == "")
            return "--";

        // No modifiers → just the number (backward-compat)
        if (levelKey == "" && profPart == "" && attrKey == "")
            return baseVal.ToString();

        return $"{baseVal}|{levelKey}|{profPart}|{attrKey}";
    }

    private void UpdatePreview() =>
        _previewLabel.Text = "Formula: " + FormatForDisplay(BuildFormula());

    // ── Statics — delegate to UsesFormula ────────────────────────────────────

    /// <summary>Human-readable summary of a formula string (used on the level row button).</summary>
    public static string FormatForDisplay(string formula) => UsesFormula.FormatForDisplay(formula);

    private void OnSave() { Saved?.Invoke(BuildFormula()); QueueFree(); }

    private static HBoxContainer MakeRow(string labelText, Control field)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var lbl = new Label
        {
            Text                = labelText,
            CustomMinimumSize   = new Vector2(120, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        row.AddChild(lbl);
        row.AddChild(field);
        return row;
    }
}
