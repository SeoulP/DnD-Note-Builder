using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eFeatDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Pf2eFeat           _feat;
    private ConfirmationDialog _confirmDialog;
    private bool               _loaded;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    private LineEdit      _nameInput;
    private OptionButton  _featTypeInput;
    private OptionButton  _classInput;
    private OptionButton  _ancestryInput;
    private IntInput      _levelInput;
    private OptionButton  _actionCostInput;
    private LineEdit      _triggerInput;
    private TextEdit      _prerequisitesInput;
    private TextEdit      _descInput;

    private List<Pf2eFeatType>   _featTypes   = new();
    private List<Pf2eClass>      _classes     = new();
    private List<Pf2eAncestry>   _ancestries  = new();
    private List<Pf2eActionCost> _actionCosts = new();

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GrowHorizontal       = GrowDirection.Both;
        GrowVertical         = GrowDirection.Both;
        HorizontalScrollMode = ScrollMode.Disabled;

        var margin = new MarginContainer();
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.SizeFlagsVertical   = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        // ── Name row ──────────────────────────────────────────────────────────
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 4);
        _nameInput = new LineEdit
        {
            PlaceholderText     = "Feat Name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CaretBlink          = true,
        };
        _nameInput.AddThemeFontSizeOverride("font_size", 18);
        var deleteBtn = new Button { Icon = GD.Load<Texture2D>("res://Scenes/Icons/Trashcan.png"), Flat = true };
        nameRow.AddChild(_nameInput);
        nameRow.AddChild(deleteBtn);
        vbox.AddChild(nameRow);

        // ── Fields ────────────────────────────────────────────────────────────
        var fields = new VBoxContainer();
        fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fields.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(fields);

        // Row 1: Feat Type + Level Required
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 12);
        fields.AddChild(row1);

        _featTypeInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row1.AddChild(MakeRow("Type", _featTypeInput, 50));

        _levelInput = new IntInput { MinValue = 1, MaxValue = 20 };
        _levelInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row1.AddChild(MakeRow("Level", _levelInput, 50));

        _actionCostInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row1.AddChild(MakeRow("Cost", _actionCostInput, 40));

        // Row 2: Class + Ancestry (optional links)
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 12);
        fields.AddChild(row2);

        _classInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row2.AddChild(MakeRow("Class", _classInput, 50));

        _ancestryInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row2.AddChild(MakeRow("Ancestry", _ancestryInput, 60));

        // Trigger
        _triggerInput = new LineEdit
        {
            PlaceholderText     = "Trigger condition...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CaretBlink          = true,
        };
        fields.AddChild(MakeRow("Trigger", _triggerInput, 60));

        // Prerequisites
        fields.AddChild(new Label { Text = "Prerequisites" });
        _prerequisitesInput = new TextEdit
        {
            PlaceholderText     = "Prerequisites...",
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            CaretBlink          = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 48),
        };
        fields.AddChild(_prerequisitesInput);

        // Description
        fields.AddChild(new Label { Text = "Description" });
        _descInput = new TextEdit
        {
            PlaceholderText     = "Feat description...",
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            CaretBlink          = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 120),
        };
        fields.AddChild(_descInput);

        // ── Wire events ───────────────────────────────────────────────────────
        _nameInput.TextChanged  += name =>
        {
            if (!_loaded) return;
            Save();
            EmitSignal(SignalName.NameChanged, "pf2e_feat", _feat?.Id ?? 0,
                string.IsNullOrEmpty(name) ? "New Feat" : name);
        };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Feat"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _featTypeInput.ItemSelected   += _ => { if (_loaded) Save(); };
        _levelInput.ValueChanged      += _ => { if (_loaded) Save(); };
        _actionCostInput.ItemSelected += _ => { if (_loaded) Save(); };
        _classInput.ItemSelected      += _ => { if (_loaded) Save(); };
        _ancestryInput.ItemSelected   += _ => { if (_loaded) Save(); };
        _triggerInput.TextChanged     += _ => { if (_loaded) Save(); };
        _prerequisitesInput.TextChanged += () => { if (_loaded) Save(); };
        _descInput.TextChanged          += () => { if (_loaded) Save(); };

        _confirmDialog = DialogHelper.Make("Delete Feat");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_feat", _feat?.Id ?? 0);
        deleteBtn.Pressed        += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_feat?.Name}\"? This cannot be undone.");
    }

    public void Load(Pf2eFeat feat)
    {
        _loaded = false;
        _feat   = feat;

        _nameInput.Text = feat.Name;

        // Feat types
        _featTypeInput.Clear();
        _featTypes = _db.Pf2eFeatTypes.GetAll(feat.CampaignId);
        foreach (var ft in _featTypes)
            _featTypeInput.AddItem(ft.Name);
        int ftIdx = _featTypes.FindIndex(ft => ft.Id == feat.FeatTypeId);
        _featTypeInput.Selected = ftIdx >= 0 ? ftIdx : 0;

        // Action costs
        _actionCostInput.Clear();
        _actionCosts = _db.Pf2eActionCosts.GetAll();
        foreach (var ac in _actionCosts)
            _actionCostInput.AddItem(ac.Name);
        int acIdx = _actionCosts.FindIndex(ac => ac.Id == feat.ActionCostId);
        _actionCostInput.Selected = acIdx >= 0 ? acIdx : 0;

        // Classes (nullable) — first item is "(Any)"
        _classInput.Clear();
        _classInput.AddItem("(Any)");
        _classes = _db.Pf2eClasses.GetAll(feat.CampaignId);
        foreach (var c in _classes)
            _classInput.AddItem(c.Name);
        if (feat.ClassId.HasValue)
        {
            int cIdx = _classes.FindIndex(c => c.Id == feat.ClassId.Value);
            _classInput.Selected = cIdx >= 0 ? cIdx + 1 : 0;
        }
        else _classInput.Selected = 0;

        // Ancestries (nullable) — first item is "(Any)"
        _ancestryInput.Clear();
        _ancestryInput.AddItem("(Any)");
        _ancestries = _db.Pf2eAncestries.GetAll(feat.CampaignId);
        foreach (var a in _ancestries)
            _ancestryInput.AddItem(a.Name);
        if (feat.AncestryId.HasValue)
        {
            int aIdx = _ancestries.FindIndex(a => a.Id == feat.AncestryId.Value);
            _ancestryInput.Selected = aIdx >= 0 ? aIdx + 1 : 0;
        }
        else _ancestryInput.Selected = 0;

        _levelInput.Value           = feat.LevelRequired;
        _triggerInput.Text          = feat.Trigger;
        _prerequisitesInput.Text    = feat.Prerequisites;
        _descInput.Text             = feat.Description;

        _loaded = true;
    }

    private void Save()
    {
        if (_feat == null || !_loaded) return;
        _feat.Name          = _nameInput.Text;
        _feat.LevelRequired = _levelInput.Value;
        _feat.Trigger       = _triggerInput.Text;
        _feat.Prerequisites = _prerequisitesInput.Text;
        _feat.Description   = _descInput.Text;

        if (_featTypes.Count > 0 && _featTypeInput.Selected >= 0 && _featTypeInput.Selected < _featTypes.Count)
            _feat.FeatTypeId = _featTypes[_featTypeInput.Selected].Id;

        if (_actionCosts.Count > 0 && _actionCostInput.Selected >= 0 && _actionCostInput.Selected < _actionCosts.Count)
            _feat.ActionCostId = _actionCosts[_actionCostInput.Selected].Id;

        // Class: index 0 = (Any) = null; index N+1 = _classes[N]
        int cSel = _classInput.Selected;
        _feat.ClassId = cSel > 0 && cSel - 1 < _classes.Count ? (int?)_classes[cSel - 1].Id : null;

        // Ancestry: index 0 = (Any) = null; index N+1 = _ancestries[N]
        int aSel = _ancestryInput.Selected;
        _feat.AncestryId = aSel > 0 && aSel - 1 < _ancestries.Count ? (int?)_ancestries[aSel - 1].Id : null;

        _db.Pf2eFeats.Edit(_feat);
    }

    private static HBoxContainer MakeRow(string labelText, Control input, float labelWidth = 80)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var lbl = new Label
        {
            Text              = labelText,
            CustomMinimumSize = new Vector2(labelWidth, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(lbl);
        row.AddChild(input);
        return row;
    }
}
