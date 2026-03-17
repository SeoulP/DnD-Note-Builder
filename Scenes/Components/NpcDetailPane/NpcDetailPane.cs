using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class NpcDetailPane : ScrollContainer
{
    private DatabaseService         _db;
    private Npc                     _npc;
    private List<Species>           _allSpecies     = new();
    private List<NpcStatus>         _statuses       = new();
    private List<NpcRelationshipType> _relationships = new();
    private List<Faction>           _availableFactions = new();
    private ConfirmationDialog      _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private OptionButton  _speciesInput;
    [Export] private LineEdit      _occupationInput;
    [Export] private LineEdit      _genderInput;
    [Export] private OptionButton  _statusInput;
    [Export] private OptionButton  _relationshipInput;
    [Export] private OptionButton  _factionSelect;
    [Export] private Button        _addFactionButton;
    [Export] private VBoxContainer _factionRowsContainer;
    [Export] private Button        _deleteButton;
    [Export] private TextEdit      _descInput;
    [Export] private TextEdit      _notesInput;
    [Export] private RichTextLabel _notesRenderer;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged          += name => { Save(); EmitSignal(SignalName.NameChanged, "npc", _npc?.Id ?? 0, string.IsNullOrEmpty(name) ? "New NPC" : name); };
        _nameInput.FocusExited          += () => { if (_nameInput.Text == "") _nameInput.Text = "New NPC"; };
        _occupationInput.TextChanged    += _ => Save();
        _genderInput.TextChanged        += _ => Save();
        _speciesInput.ItemSelected      += _ => Save();
        _statusInput.ItemSelected       += _ => Save();
        _relationshipInput.ItemSelected += _ => Save();
        _descInput.TextChanged          += () => Save();
        _notesInput.TextChanged         += () => { Save(); RenderNotes(); };

        _notesRenderer.MetaClicked += OnMetaClicked;

        _confirmDialog = new ConfirmationDialog { Title = "Delete NPC" };
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "npc", _npc?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            _confirmDialog.DialogText = $"Delete \"{_npc?.Name}\"? This cannot be undone.";
            _confirmDialog.PopupCentered();
        };

        _factionSelect.ItemSelected += _ => _addFactionButton.Disabled = _factionSelect.GetSelectedId() == -1;
        _addFactionButton.Pressed += OnAddFactionPressed;
    }

    public void Load(Npc npc)
    {
        _npc = npc;

        // Populate species dropdown (campaign-specific)
        _allSpecies = _db.Species.GetAll(npc.CampaignId);
        _speciesInput.Clear();
        _speciesInput.AddItem("(unknown)");
        foreach (var s in _allSpecies) _speciesInput.AddItem(s.Name);

        // Populate status and relationship dropdowns from DB
        _statuses = _db.NpcStatuses.GetAll(npc.CampaignId);
        _statusInput.Clear();
        _statusInput.AddItem("— None —");
        foreach (var s in _statuses) _statusInput.AddItem(s.Name);

        _relationships = _db.NpcRelationshipTypes.GetAll(npc.CampaignId);
        _relationshipInput.Clear();
        _relationshipInput.AddItem("— None —");
        foreach (var r in _relationships) _relationshipInput.AddItem(r.Name);

        _nameInput.Text       = string.IsNullOrEmpty(npc.Name) ? "New NPC" : npc.Name;
        _occupationInput.Text = npc.Occupation;
        _genderInput.Text     = npc.Gender;

        int speciesIdx = npc.SpeciesId.HasValue ? _allSpecies.FindIndex(s => s.Id == npc.SpeciesId.Value) : -1;
        _speciesInput.Select(speciesIdx >= 0 ? speciesIdx + 1 : 0);

        int statusIdx = npc.StatusId.HasValue ? _statuses.FindIndex(s => s.Id == npc.StatusId.Value) : -1;
        _statusInput.Select(statusIdx >= 0 ? statusIdx + 1 : 0);

        int relIdx = npc.RelationshipTypeId.HasValue ? _relationships.FindIndex(r => r.Id == npc.RelationshipTypeId.Value) : -1;
        _relationshipInput.Select(relIdx >= 0 ? relIdx + 1 : 0);

        PopulateFactionDropdown();
        LoadFactionRows();

        _descInput.Text  = npc.Description;
        _notesInput.Text = npc.Notes;
        RenderNotes();
    }

    private void PopulateFactionDropdown()
    {
        var assignedIds = new HashSet<int>(_npc.FactionIds);
        _availableFactions = _db.Factions.GetAll(_npc.CampaignId)
            .FindAll(f => !assignedIds.Contains(f.Id));

        _factionSelect.Clear();
        _factionSelect.AddItem("Pick a faction", -1);
        foreach (var f in _availableFactions) _factionSelect.AddItem(f.Name, f.Id);

        bool hasAny = _availableFactions.Count > 0;
        _factionSelect.Disabled      = !hasAny;
        _addFactionButton.Disabled   = true;
    }

    private void LoadFactionRows()
    {
        foreach (Node child in _factionRowsContainer.GetChildren())
            child.QueueFree();

        var assignedIds = new HashSet<int>(_npc.FactionIds);
        var allFactions = _db.Factions.GetAll(_npc.CampaignId);

        foreach (var faction in allFactions)
        {
            if (!assignedIds.Contains(faction.Id)) continue;

            int fid   = faction.Id;
            var panel = new PanelContainer();
            var row   = new HBoxContainer();

            var navBtn = new Button
            {
                Text                = faction.Name,
                Flat                = true,
                Alignment           = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            navBtn.Pressed += () => EmitSignal(SignalName.NavigateTo, "faction", fid);

            var removeBtn = new Button { Text = "×", Flat = true };
            removeBtn.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", DeleteHoverBox);
            removeBtn.MouseExited  += () => panel.RemoveThemeStyleboxOverride("panel");
            removeBtn.Pressed += () =>
            {
                _db.Npcs.RemoveFaction(_npc.Id, fid);
                _npc.FactionIds.Remove(fid);
                PopulateFactionDropdown();
                LoadFactionRows();
            };

            row.AddChild(navBtn);
            row.AddChild(removeBtn);
            panel.AddChild(row);
            _factionRowsContainer.AddChild(panel);
        }
    }

    private void OnAddFactionPressed()
    {
        int selectedId = (int)_factionSelect.GetSelectedId();
        if (selectedId == -1) return;

        _db.Npcs.AddFaction(_npc.Id, selectedId);
        _npc.FactionIds.Add(selectedId);
        PopulateFactionDropdown();
        LoadFactionRows();
    }

    private void Save()
    {
        if (_npc == null) return;

        int speciesIdx  = _speciesInput.Selected;
        _npc.Name        = string.IsNullOrEmpty(_nameInput.Text) ? "New NPC" : _nameInput.Text;
        _npc.SpeciesId   = speciesIdx == 0 ? null : _allSpecies[speciesIdx - 1].Id;
        _npc.Occupation  = _occupationInput.Text;
        _npc.Gender      = _genderInput.Text;
        _npc.StatusId           = _statusInput.Selected      == 0 ? null : _statuses[_statusInput.Selected - 1].Id;
        _npc.RelationshipTypeId = _relationshipInput.Selected == 0 ? null : _relationships[_relationshipInput.Selected - 1].Id;
        _npc.Description = _descInput.Text;
        _npc.Notes       = _notesInput.Text;
        _db.Npcs.Edit(_npc);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_npc == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            _confirmDialog.DialogText = $"Delete \"{_npc.Name}\"? This cannot be undone.";
            _confirmDialog.PopupCentered();
            AcceptEvent();
        }
    }

    private static readonly StyleBoxFlat DeleteHoverBox = MakeDeleteHoverBox();
    private static StyleBoxFlat MakeDeleteHoverBox()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0.90f, 0.55f, 0.55f) };
        box.SetCornerRadiusAll(3);
        return box;
    }

    private void RenderNotes()
    {
        if (_npc == null) return;
        _notesRenderer.Text = WikiLinkParser.Parse(_notesInput.Text, _db, _npc.CampaignId);
    }

    private void OnMetaClicked(Variant meta)
    {
        var parts = meta.AsString().Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
            EmitSignal(SignalName.NavigateTo, parts[0], id);
    }
}
