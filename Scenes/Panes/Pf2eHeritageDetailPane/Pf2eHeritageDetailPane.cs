using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eHeritageDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Pf2eHeritage       _heritage;
    private ConfirmationDialog _confirmDialog;
    private bool               _loaded;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    private LineEdit _nameInput;
    private TextEdit _descInput;

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
            PlaceholderText     = "Heritage Name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CaretBlink          = true,
        };
        _nameInput.AddThemeFontSizeOverride("font_size", 18);
        var deleteBtn = new Button { Icon = GD.Load<Texture2D>("res://Scenes/Icons/Trashcan.png"), Flat = true };
        nameRow.AddChild(_nameInput);
        nameRow.AddChild(deleteBtn);
        vbox.AddChild(nameRow);

        // ── Description ───────────────────────────────────────────────────────
        var fields = new VBoxContainer();
        fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fields.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(fields);

        fields.AddChild(new Label { Text = "Description" });
        _descInput = new TextEdit
        {
            PlaceholderText     = "Heritage description...",
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
            EmitSignal(SignalName.NameChanged, "pf2e_heritage", _heritage?.Id ?? 0,
                string.IsNullOrEmpty(name) ? "New Heritage" : name);
        };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Heritage"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _descInput.TextChanged  += () => { if (_loaded) Save(); };

        _confirmDialog = DialogHelper.Make("Delete Heritage");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_heritage", _heritage?.Id ?? 0);
        deleteBtn.Pressed        += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_heritage?.Name}\"? This cannot be undone.");
    }

    public void Load(Pf2eHeritage heritage)
    {
        _loaded   = false;
        _heritage = heritage;

        _nameInput.Text = heritage.Name;
        _descInput.Text = heritage.Description;

        _loaded = true;
    }

    private void Save()
    {
        if (_heritage == null || !_loaded) return;
        _heritage.Name        = _nameInput.Text;
        _heritage.Description = _descInput.Text;
        _db.Pf2eHeritages.Edit(_heritage);
    }
}
