using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eClassDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Pf2eClass          _class;
    private ConfirmationDialog _confirmDialog;
    private bool               _loaded;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void FeatAddedEventHandler(int classId, int featId);
    [Signal] public delegate void FeatDeletedEventHandler(int featId);

    private LineEdit      _nameInput;
    private OptionButton  _keyAbilityInput;
    private IntInput      _hpInput;
    private TextEdit      _descInput;
    private VBoxContainer _featsContainer;

    private List<Pf2eAbilityScore> _abilityScores = new();

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
            PlaceholderText     = "Class Name",
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

        _keyAbilityInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        fields.AddChild(MakeRow("Key Ability", _keyAbilityInput));

        _hpInput = new IntInput { MinValue = 1, MaxValue = 24 };
        _hpInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fields.AddChild(MakeRow("HP / Level", _hpInput));

        fields.AddChild(new Label { Text = "Description" });
        _descInput = new TextEdit
        {
            PlaceholderText   = "Class description...",
            WrapMode          = TextEdit.LineWrappingMode.Boundary,
            CaretBlink        = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 100),
        };
        fields.AddChild(_descInput);

        // ── Class Feats section ───────────────────────────────────────────────
        fields.AddChild(new HSeparator());
        var featsHeader = new HBoxContainer();
        featsHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        featsHeader.AddThemeConstantOverride("separation", 0);
        var featsToggle = new Button
        {
            Text                    = "Class Feats",
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        var addFeatBtn = new Button { Text = "+", Flat = true, TooltipText = "Add Class Feat", MouseDefaultCursorShape = CursorShape.PointingHand };
        featsHeader.AddChild(featsToggle);
        featsHeader.AddChild(addFeatBtn);
        fields.AddChild(featsHeader);
        var featsInset = new MarginContainer();
        featsInset.AddThemeConstantOverride("margin_left", 8);
        _featsContainer = new VBoxContainer();
        _featsContainer.AddThemeConstantOverride("separation", 2);
        featsInset.AddChild(_featsContainer);
        fields.AddChild(featsInset);
        UiHelpers.WireSectionToggle(featsToggle, featsInset, startCollapsed: false);

        // ── Wire events ───────────────────────────────────────────────────────
        _nameInput.TextChanged  += name =>
        {
            if (!_loaded) return;
            Save();
            EmitSignal(SignalName.NameChanged, "pf2e_class", _class?.Id ?? 0,
                string.IsNullOrEmpty(name) ? "New Class" : name);
        };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Class"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _keyAbilityInput.ItemSelected += _ => { if (_loaded) Save(); };
        _hpInput.ValueChanged         += _ => { if (_loaded) Save(); };
        _descInput.TextChanged        += () => { if (_loaded) Save(); };
        addFeatBtn.Pressed            += AddFeat;

        _confirmDialog = DialogHelper.Make("Delete Class");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_class", _class?.Id ?? 0);
        deleteBtn.Pressed        += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_class?.Name}\"? This cannot be undone.");
    }

    public void Load(Pf2eClass cls)
    {
        _loaded = false;
        _class  = cls;

        _nameInput.Text = cls.Name;
        _descInput.Text = cls.Description;

        _keyAbilityInput.Clear();
        _abilityScores = _db.Pf2eAbilityScores.GetAll();
        foreach (var s in _abilityScores)
            _keyAbilityInput.AddItem(s.Name);
        int aidx = _abilityScores.FindIndex(s => s.Id == cls.KeyAbilityScoreId);
        _keyAbilityInput.Selected = aidx >= 0 ? aidx : 0;

        _hpInput.Value = cls.HpPerLevel;

        _loaded = true;
        LoadFeats();
    }

    private void Save()
    {
        if (_class == null || !_loaded) return;
        _class.Name        = _nameInput.Text;
        _class.Description = _descInput.Text;
        _class.HpPerLevel  = _hpInput.Value;
        if (_abilityScores.Count > 0 && _keyAbilityInput.Selected >= 0 && _keyAbilityInput.Selected < _abilityScores.Count)
            _class.KeyAbilityScoreId = _abilityScores[_keyAbilityInput.Selected].Id;
        _db.Pf2eClasses.Edit(_class);
    }

    private void LoadFeats()
    {
        foreach (Node child in _featsContainer.GetChildren()) child.QueueFree();
        if (_class == null) return;

        var feats = _db.Pf2eFeats.GetAll(_class.CampaignId)
                       .FindAll(f => f.ClassId == _class.Id);

        if (feats.Count == 0)
        {
            var empty = new Label { Text = "No class feats." };
            empty.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.45f));
            _featsContainer.AddChild(empty);
            return;
        }

        foreach (var feat in feats)
        {
            int capId = feat.Id;
            var row = new EntityRow
            {
                Text            = feat.Name,
                Description     = $"Lvl {feat.LevelRequired}  •  {feat.Description}",
                ShowDescription = !string.IsNullOrEmpty(feat.Description),
                ShowDelete      = true,
            };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo, "pf2e_feat", capId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateTo, "pf2e_feat", capId);
            row.DeletePressed         += () =>
            {
                var f = _db.Pf2eFeats.Get(capId);
                if (f == null) { LoadFeats(); return; }
                var ftype = _db.Pf2eFeatTypes.GetAll(_class.CampaignId).Find(ft => ft.Id == f.FeatTypeId);
                if (ftype?.Name == "Class" && f.AncestryId == null)
                    { _db.Pf2eFeats.Delete(capId); EmitSignal(SignalName.FeatDeleted, capId); }
                else
                    { f.ClassId = null; _db.Pf2eFeats.Edit(f); }
                LoadFeats();
            };
            _featsContainer.AddChild(row);
        }
    }

    private void AddFeat()
    {
        if (_class == null) return;
        var featTypes    = _db.Pf2eFeatTypes.GetAll(_class.CampaignId);
        int featTypeId   = featTypes.Find(ft => ft.Name == "Class")?.Id ?? (featTypes.Count > 0 ? featTypes[0].Id : 1);
        var actionCosts  = _db.Pf2eActionCosts.GetAll();
        int actionCostId = actionCosts.Find(ac => ac.Name == "None")?.Id ?? (actionCosts.Count > 0 ? actionCosts[0].Id : 1);
        var takenNames   = new System.Collections.Generic.HashSet<string>(_db.Pf2eFeats.GetAll(_class.CampaignId).ConvertAll(f => f.Name));
        string name = "New Feat";
        if (takenNames.Contains(name)) { int i = 2; while (takenNames.Contains($"New Feat ({i})")) i++; name = $"New Feat ({i})"; }
        var f = new DndBuilder.Core.Models.Pf2eFeat
        {
            CampaignId    = _class.CampaignId,
            ClassId       = _class.Id,
            Name          = name,
            FeatTypeId    = featTypeId,
            ActionCostId  = actionCostId,
            LevelRequired = 1,
        };
        int newId = _db.Pf2eFeats.Add(f);
        LoadFeats();
        EmitSignal(SignalName.FeatAdded, _class.Id, newId);
        EmitSignal(SignalName.NavigateTo, "pf2e_feat", newId);
    }

    private static HBoxContainer MakeRow(string labelText, Control input)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var lbl = new Label
        {
            Text              = labelText,
            CustomMinimumSize = new Vector2(90, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(lbl);
        row.AddChild(input);
        return row;
    }
}
