using System;
using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public partial class SpeciesDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Species            _species;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void SubspeciesAddedEventHandler(int speciesId, int subspeciesId);

    [Export] private LineEdit         _nameInput;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes        _notes;
    [Export] private VBoxContainer    _subspeciesContainer;
    [Export] private Button           _addSubspeciesButton;
    [Export] private VBoxContainer    _abilitiesContainer;
    [Export] private TypeOptionButton _addAbilityButton;
    [Export] private Button           _levelsToggle;
    [Export] private Control          _levelsInset;
    [Export] private Button           _initLevelsButton;
    [Export] private VBoxContainer    _levelsContainer;
    [Export] private Button           _deleteButton;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "species", _species?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Species" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Species"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _descInput.TextChanged  += () => Save();
        _notes.TextChanged      += () => Save();
        _notes.NavigateTo       += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _addSubspeciesButton.Pressed += AddSubspecies;

        WireSectionToggle(_levelsToggle, _levelsInset);
        _initLevelsButton.Pressed += () =>
        {
            if (_species == null) return;
            _db.Species.InitializeLevels(_species.Id);
            LoadLevelProgression();
        };

        _confirmDialog = DialogHelper.Make("Delete Species");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "species", _species?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_species?.Name}\"? All subspecies will also be deleted. This cannot be undone.");
    }

    public void Load(Species species)
    {
        _species = species;
        _nameInput.Text = species.Name;
        _descInput.Text = species.Description;
        _notes.Text = species.Notes;
        _imageCarousel.Setup(EntityType.Species, species.Id, _db);
        LoadSubspecies();
        LoadAbilities();
        SetupAddAbilityButton();
        LoadLevelProgression();
    }

    private void Save()
    {
        if (_species == null) return;
        _species.Name        = _nameInput.Text;
        _species.Description = _descInput.Text;
        _species.Notes       = _notes.Text;
        _db.Species.Edit(_species);
    }

    private void LoadSubspecies()
    {
        foreach (Node child in _subspeciesContainer.GetChildren())
            if (child != _addSubspeciesButton) child.QueueFree();

        foreach (var sub in _db.Subspecies.GetAllForSpecies(_species.Id))
        {
            int subId = sub.Id;
            var row = new EntityRow
            {
                Text            = sub.Name,
                Description     = sub.Description,
                ShowDescription = true,
            };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "subspecies", subId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "subspecies", subId);
            row.DeletePressed   += () =>
            {
                _db.Subspecies.Delete(subId);
                LoadSubspecies();
            };
            _subspeciesContainer.AddChild(row);
        }
    }

    private void AddSubspecies()
    {
        if (_species == null) return;
        var sub = new Subspecies { CampaignId = _species.CampaignId, SpeciesId = _species.Id, Name = "New Subspecies" };
        int newId = _db.Subspecies.Add(sub);
        LoadSubspecies();
        EmitSignal(SignalName.SubspeciesAdded, _species.Id, newId);
        EmitSignal(SignalName.NavigateTo, "subspecies", newId);
    }

    private void LoadAbilities()
    {
        foreach (Node child in _abilitiesContainer.GetChildren())
            if (child != _addAbilityButton) child.QueueFree();

        var abilityIds = _db.Abilities.GetAbilityIdsForSpecies(_species.Id);
        foreach (int abilId in abilityIds)
        {
            var ability = _db.Abilities.Get(abilId);
            if (ability == null) continue;
            int capId = abilId;

            var row = new EntityRow { Text = ability.Name };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", capId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", capId);
            row.DeletePressed         += () =>
            {
                _db.Abilities.RemoveSpeciesAbility(_species.Id, capId);
                LoadAbilities();
                SetupAddAbilityButton();
            };
            _abilitiesContainer.AddChild(row);
        }
    }

    private void SetupAddAbilityButton()
    {
        if (_species == null) return;
        var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForSpecies(_species.Id));
        _addAbilityButton.NoneText = "(Add ability...)";
        _addAbilityButton.Setup(
            () => _db.Abilities.GetAll(_species.CampaignId)
                    .FindAll(a => !linkedIds.Contains(a.Id))
                    .ConvertAll(a => (a.Id, a.Name)),
            null, null);
        _addAbilityButton.SelectById(null);
        _addAbilityButton.TypeSelected -= OnAddAbility;
        _addAbilityButton.TypeSelected += OnAddAbility;
    }

    private void OnAddAbility(int id)
    {
        if (id < 0) return;
        _db.Abilities.AddSpeciesAbility(_species.Id, id);
        LoadAbilities();
        SetupAddAbilityButton();
    }

    // ── Level Progression ─────────────────────────────────────────────────────

    private void LoadLevelProgression()
    {
        foreach (Node child in _levelsContainer.GetChildren())
            child.QueueFree();

        var levels = _db.Species.GetLevelsForSpecies(_species.Id);
        _initLevelsButton.Visible = levels.Count == 0;

        foreach (var lvl in levels)
            _levelsContainer.AddChild(BuildLevelRow(lvl));
    }

    private Control BuildLevelRow(SpeciesLevel lvl)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var content = new VBoxContainer { Visible = false };
        content.AddThemeConstantOverride("separation", 4);

        var headerRow = new HBoxContainer();
        var header = new Button
        {
            Text                    = "▶  " + FormatLevelHeader(lvl),
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
        };

        var abilityRows   = new VBoxContainer();
        abilityRows.AddThemeConstantOverride("separation", 2);

        var addAbilityBtn = new TypeOptionButton();

        var usesMap = new Dictionary<int, string>();
        foreach (var seg in lvl.ClassData.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = seg.Split(':', 2);
            if (p.Length == 2 && int.TryParse(p[0].Trim(), out int uid) && uid > 0)
                usesMap[uid] = p[1].Trim();
        }

        void SaveUsesMap()
        {
            lvl.ClassData = usesMap.Count > 0
                ? string.Join(",", usesMap.Select(kv => $"{kv.Key}:{kv.Value}"))
                : "";
            _db.Species.SaveLevel(lvl);
        }

        void Refresh()
        {
            foreach (Node child in abilityRows.GetChildren()) child.QueueFree();

            var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForSpeciesLevel(lvl.Id));

            foreach (var key in usesMap.Keys.Except(linkedIds).ToList()) usesMap.Remove(key);

            foreach (int abilId in linkedIds)
            {
                var ability = _db.Abilities.Get(abilId);
                if (ability == null) continue;
                int capId = abilId;

                if (!usesMap.ContainsKey(capId)) usesMap[capId] = "--";

                var row     = new HBoxContainer();
                var nameBtn = new Button
                {
                    Text                    = ability.Name,
                    Flat                    = true,
                    Alignment               = HorizontalAlignment.Left,
                    SizeFlagsHorizontal     = SizeFlags.ExpandFill,
                    MouseDefaultCursorShape = CursorShape.PointingHand,
                };
                nameBtn.Pressed += () => EmitSignal(SignalName.NavigateTo, "ability", capId);

                var usesBtn = new Button
                {
                    Text              = UsesLabel(usesMap.GetValueOrDefault(capId, "--")),
                    CustomMinimumSize = new Vector2(80, 0),
                    TooltipText       = "Click to edit usage scaling",
                };
                usesBtn.Pressed += () =>
                {
                    var popup = new UsageProgressionPopup();
                    AddChild(popup);
                    popup.Setup(ability.Name, usesMap.GetValueOrDefault(capId, "--"));
                    popup.Saved += formula =>
                    {
                        usesMap[capId] = formula;
                        SaveUsesMap();
                        usesBtn.Text = UsesFormula.FormatForDisplay(formula);
                    };
                    popup.PopupCentered();
                };

                var delBtn = new Button { Text = "×", Flat = true };
                delBtn.Pressed += () =>
                {
                    _db.Abilities.RemoveSpeciesLevelAbility(lvl.Id, capId);
                    usesMap.Remove(capId);
                    SaveUsesMap();
                    Refresh();
                };

                row.AddChild(nameBtn);
                row.AddChild(usesBtn);
                row.AddChild(delBtn);
                abilityRows.AddChild(row);
            }

            addAbilityBtn.NoneText = "(Add ability...)";
            addAbilityBtn.Setup(
                () => _db.Abilities.GetAll(_species.CampaignId)
                        .FindAll(a => !linkedIds.Contains(a.Id))
                        .ConvertAll(a => (a.Id, a.Name)),
                null, null);
            addAbilityBtn.SelectById(null);

            header.Text = (content.Visible ? "▼  " : "▶  ") + FormatLevelHeader(lvl);
        }

        addAbilityBtn.TypeSelected += id =>
        {
            if (id < 0) return;
            _db.Abilities.AddSpeciesLevelAbility(lvl.Id, id);
            usesMap[id] = "--";
            SaveUsesMap();
            Refresh();
        };

        headerRow.AddChild(header);

        Refresh();

        content.AddChild(abilityRows);
        content.AddChild(addAbilityBtn);

        header.Pressed += () =>
        {
            content.Visible = !content.Visible;
            header.Text     = (content.Visible ? "▼  " : "▶  ") + FormatLevelHeader(lvl);
        };

        box.AddChild(headerRow);
        box.AddChild(content);
        return box;
    }

    private static string FormatLevelHeader(SpeciesLevel lvl) => $"Level {lvl.Level,2}";

    private static string UsesLabel(string val) => UsesFormula.FormatForDisplay(val);

    private static void WireSectionToggle(Button toggle, Control content, bool startCollapsed = false)
    {
        content.Visible = !startCollapsed;
        string label    = toggle.Text;
        toggle.Text     = (startCollapsed ? "▶  " : "▼  ") + label;
        toggle.Pressed += () =>
        {
            content.Visible = !content.Visible;
            toggle.Text     = (content.Visible ? "▼  " : "▶  ") + label;
        };
    }
}
