using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eAncestryDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Pf2eAncestry       _ancestry;
    private ConfirmationDialog _confirmDialog;
    private bool               _loaded;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void HeritageAddedEventHandler(int ancestryId, int heritageId);
    [Signal] public delegate void FeatAddedEventHandler(int ancestryId, int featId);
    [Signal] public delegate void FeatDeletedEventHandler(int featId);

    private LineEdit      _nameInput;
    private IntInput      _hpInput;
    private OptionButton  _sizeInput;
    private IntInput      _speedInput;
    private TextEdit      _descInput;
    private VBoxContainer _heritagesContainer;
    private VBoxContainer _featsContainer;

    private List<Pf2eSize> _sizes = new();

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
            PlaceholderText     = "Ancestry Name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CaretBlink          = true,
        };
        _nameInput.AddThemeFontSizeOverride("font_size", 18);
        var deleteBtn = new Button { Icon = GD.Load<Texture2D>("res://Scenes/Icons/Trashcan.png"), Flat = true };
        nameRow.AddChild(_nameInput);
        nameRow.AddChild(deleteBtn);
        vbox.AddChild(nameRow);

        // ── Stat row ─────────────────────────────────────────────────────────
        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(statsRow);

        _hpInput = new IntInput { MinValue = 1, MaxValue = 20 };
        _hpInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        statsRow.AddChild(MakeRow("Base HP", _hpInput));

        _sizeInput = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        statsRow.AddChild(MakeRow("Size", _sizeInput));

        var speedBox = new HBoxContainer();
        speedBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        speedBox.AddThemeConstantOverride("separation", 3);
        _speedInput = new IntInput { MinValue = 5, MaxValue = 60 };
        _speedInput.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        speedBox.AddChild(_speedInput);
        speedBox.AddChild(new Label { Text = "ft", VerticalAlignment = VerticalAlignment.Center });
        statsRow.AddChild(MakeRow("Speed", speedBox));

        // ── Description ───────────────────────────────────────────────────────
        var fields = new VBoxContainer();
        fields.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fields.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(fields);

        fields.AddChild(new Label { Text = "Description" });
        _descInput = new TextEdit
        {
            PlaceholderText     = "Ancestry description...",
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            CaretBlink          = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 100),
        };
        fields.AddChild(_descInput);

        // ── Heritages section ─────────────────────────────────────────────────
        fields.AddChild(new HSeparator());
        var heritagesHeader = new HBoxContainer();
        heritagesHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heritagesHeader.AddThemeConstantOverride("separation", 0);
        var heritagesToggle = new Button
        {
            Text                    = "Heritages",
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        var addHeritageBtn = new Button { Text = "+", Flat = true, TooltipText = "Add Heritage", MouseDefaultCursorShape = CursorShape.PointingHand };
        heritagesHeader.AddChild(heritagesToggle);
        heritagesHeader.AddChild(addHeritageBtn);
        fields.AddChild(heritagesHeader);
        var heritagesInset = new MarginContainer();
        heritagesInset.AddThemeConstantOverride("margin_left", 8);
        _heritagesContainer = new VBoxContainer();
        _heritagesContainer.AddThemeConstantOverride("separation", 2);
        heritagesInset.AddChild(_heritagesContainer);
        fields.AddChild(heritagesInset);
        UiHelpers.WireSectionToggle(heritagesToggle, heritagesInset, startCollapsed: false);

        // ── Ancestry Feats section ────────────────────────────────────────────
        fields.AddChild(new HSeparator());
        var featsHeader = new HBoxContainer();
        featsHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        featsHeader.AddThemeConstantOverride("separation", 0);
        var featsToggle = new Button
        {
            Text                    = "Ancestry Feats",
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        var addFeatBtn = new Button { Text = "+", Flat = true, TooltipText = "Add Ancestry Feat", MouseDefaultCursorShape = CursorShape.PointingHand };
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
            EmitSignal(SignalName.NameChanged, "pf2e_ancestry", _ancestry?.Id ?? 0,
                string.IsNullOrEmpty(name) ? "New Ancestry" : name);
        };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Ancestry"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _hpInput.ValueChanged    += _ => { if (_loaded) Save(); };
        _sizeInput.ItemSelected  += _ => { if (_loaded) Save(); };
        _speedInput.ValueChanged += _ => { if (_loaded) Save(); };
        _descInput.TextChanged   += () => { if (_loaded) Save(); };
        addHeritageBtn.Pressed   += AddHeritage;
        addFeatBtn.Pressed       += AddFeat;

        _confirmDialog = DialogHelper.Make("Delete Ancestry");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_ancestry", _ancestry?.Id ?? 0);
        deleteBtn.Pressed        += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_ancestry?.Name}\"? This cannot be undone.");
    }

    public void Load(Pf2eAncestry ancestry)
    {
        _loaded   = false;
        _ancestry = ancestry;

        _nameInput.Text = ancestry.Name;
        _descInput.Text = ancestry.Description;

        _sizes = _db.Pf2eSizes.GetAll();
        _sizeInput.Clear();
        foreach (var s in _sizes)
            _sizeInput.AddItem(s.Name);
        int sidx = _sizes.FindIndex(s => s.Id == ancestry.SizeId);
        _sizeInput.Selected = sidx >= 0 ? sidx : 0;

        _hpInput.Value    = ancestry.BaseHp;
        _speedInput.Value = ancestry.SpeedFeet;

        _loaded = true;
        LoadHeritages();
        LoadFeats();
    }

    private void Save()
    {
        if (_ancestry == null || !_loaded) return;
        _ancestry.Name        = _nameInput.Text;
        _ancestry.Description = _descInput.Text;
        _ancestry.BaseHp      = _hpInput.Value;
        _ancestry.SpeedFeet   = _speedInput.Value;
        if (_sizes.Count > 0 && _sizeInput.Selected >= 0 && _sizeInput.Selected < _sizes.Count)
            _ancestry.SizeId = _sizes[_sizeInput.Selected].Id;
        _db.Pf2eAncestries.Edit(_ancestry);
    }

    private void LoadHeritages()
    {
        foreach (Node child in _heritagesContainer.GetChildren()) child.QueueFree();
        if (_ancestry == null) return;

        var heritages = _db.Pf2eHeritages.GetAll(_ancestry.CampaignId)
                           .FindAll(h => h.AncestryId == _ancestry.Id);

        if (heritages.Count == 0)
        {
            var empty = new Label { Text = "No heritages." };
            empty.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.45f));
            _heritagesContainer.AddChild(empty);
            return;
        }

        foreach (var heritage in heritages)
        {
            int capId = heritage.Id;
            var row = new EntityRow
            {
                Text            = heritage.Name,
                Description     = heritage.Description,
                ShowDescription = !string.IsNullOrEmpty(heritage.Description),
                ShowDelete      = true,
            };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo, "pf2e_heritage", capId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateTo, "pf2e_heritage", capId);
            row.DeletePressed         += () => { _db.Pf2eHeritages.Delete(capId); LoadHeritages(); };
            _heritagesContainer.AddChild(row);
        }
    }

    private void LoadFeats()
    {
        foreach (Node child in _featsContainer.GetChildren()) child.QueueFree();
        if (_ancestry == null) return;

        var feats = _db.Pf2eFeats.GetAll(_ancestry.CampaignId)
                       .FindAll(f => f.AncestryId == _ancestry.Id);

        if (feats.Count == 0)
        {
            var empty = new Label { Text = "No ancestry feats." };
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
                var ftype = _db.Pf2eFeatTypes.GetAll(_ancestry.CampaignId).Find(ft => ft.Id == f.FeatTypeId);
                if (ftype?.Name == "Ancestry" && f.ClassId == null)
                    { _db.Pf2eFeats.Delete(capId); EmitSignal(SignalName.FeatDeleted, capId); }
                else
                    { f.AncestryId = null; _db.Pf2eFeats.Edit(f); }
                LoadFeats();
            };
            _featsContainer.AddChild(row);
        }
    }

    private void AddHeritage()
    {
        if (_ancestry == null) return;
        var existing  = _db.Pf2eHeritages.GetAll(_ancestry.CampaignId);
        var takenNames = new System.Collections.Generic.HashSet<string>(existing.ConvertAll(h => h.Name));
        string name = "New Heritage";
        if (takenNames.Contains(name)) { int i = 2; while (takenNames.Contains($"New Heritage ({i})")) i++; name = $"New Heritage ({i})"; }
        var h = new DndBuilder.Core.Models.Pf2eHeritage
        {
            CampaignId = _ancestry.CampaignId,
            AncestryId = _ancestry.Id,
            Name       = name,
        };
        int newId = _db.Pf2eHeritages.Add(h);
        LoadHeritages();
        EmitSignal(SignalName.HeritageAdded, _ancestry.Id, newId);
        EmitSignal(SignalName.NavigateTo, "pf2e_heritage", newId);
    }

    private void AddFeat()
    {
        if (_ancestry == null) return;
        var featTypes    = _db.Pf2eFeatTypes.GetAll(_ancestry.CampaignId);
        int featTypeId   = featTypes.Find(ft => ft.Name == "Ancestry")?.Id ?? (featTypes.Count > 0 ? featTypes[0].Id : 1);
        var actionCosts  = _db.Pf2eActionCosts.GetAll();
        int actionCostId = actionCosts.Find(ac => ac.Name == "None")?.Id ?? (actionCosts.Count > 0 ? actionCosts[0].Id : 1);
        var takenNames   = new System.Collections.Generic.HashSet<string>(_db.Pf2eFeats.GetAll(_ancestry.CampaignId).ConvertAll(f => f.Name));
        string name = "New Feat";
        if (takenNames.Contains(name)) { int i = 2; while (takenNames.Contains($"New Feat ({i})")) i++; name = $"New Feat ({i})"; }
        var f = new DndBuilder.Core.Models.Pf2eFeat
        {
            CampaignId    = _ancestry.CampaignId,
            AncestryId    = _ancestry.Id,
            Name          = name,
            FeatTypeId    = featTypeId,
            ActionCostId  = actionCostId,
            LevelRequired = 1,
        };
        int newId = _db.Pf2eFeats.Add(f);
        LoadFeats();
        EmitSignal(SignalName.FeatAdded, _ancestry.Id, newId);
        EmitSignal(SignalName.NavigateTo, "pf2e_feat", newId);
    }

    private static HBoxContainer MakeRow(string labelText, Control input)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var lbl = new Label
        {
            Text              = labelText,
            CustomMinimumSize = new Vector2(70, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(lbl);
        row.AddChild(input);
        return row;
    }
}
