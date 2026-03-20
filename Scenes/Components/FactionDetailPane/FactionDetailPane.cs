using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;


public partial class FactionDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Faction            _faction;
    private ConfirmationDialog _confirmDialog;
    private bool               _loaded;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private LineEdit      _typeInput;
    [Export] private TextEdit      _descInput;
    [Export] private TextEdit      _goalsInput;
    [Export] private WikiNotes     _notes;
    [Export] private Button        _deleteButton;
    [Export] private ImageCarousel _imageCarousel;
    [Export] private TypeOptionButton _npcSelect;
    [Export] private TypeOptionButton _roleSelect;
    [Export] private Button           _addNpcButton;
    [Export] private VBoxContainer    _npcRowsContainer;
    [Export] private Button           _relFactionSelfLabel;
    [Export] private TypeOptionButton _relFactionTypeSelect;
    [Export] private TypeOptionButton _relFactionSelect;
    [Export] private Button           _addRelFactionButton;
    [Export] private VBoxContainer    _relFactionRowsContainer;

    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged         += name =>
        {
            Save();
            EmitSignal(SignalName.NameChanged, "faction", _faction?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Faction" : name);
            if (!_loaded) return;
            _relFactionSelfLabel.Text = string.IsNullOrEmpty(name) ? "New Faction" : name;
            LoadRelFactionRows();
        };
        _nameInput.FocusExited         += () => { if (_nameInput.Text == "") _nameInput.Text = "New Faction"; };
        _nameInput.FocusEntered        += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _typeInput.TextChanged += _ => Save();
        _descInput.TextChanged += () => Save();
        _goalsInput.TextChanged        += () => Save();
        _notes.TextChanged   += () => Save();
        _notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);

        _confirmDialog = DialogHelper.Make("Delete Faction");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "faction", _faction?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_faction?.Name}\"? This cannot be undone.");
        };

        _npcSelect.TypeSelected += id => _addNpcButton.Disabled = (id == -1);
        _roleSelect.TypeSelected += _ => { };
        _addNpcButton.Pressed   += OnAddNpcPressed;

        _relFactionSelect.TypeSelected   += id => _addRelFactionButton.Disabled = (id == -1);
        _relFactionTypeSelect.TypeSelected += _ => { };
        _addRelFactionButton.Pressed     += OnAddRelFactionPressed;
    }

    public void Load(Faction faction)
    {
        _faction = faction;

        _imageCarousel?.Setup(EntityType.Faction, faction.Id, _db);

        _nameInput.Text = string.IsNullOrEmpty(faction.Name) ? "New Faction" : faction.Name;
        _typeInput.Text = faction.Type;
        _descInput.Text = faction.Description;
        _goalsInput.Text = faction.Goals;
        _notes.Setup(faction.CampaignId, _db);
        _notes.Text = faction.Notes;

        _npcSelect.NoneText        = "Pick an NPC";
        _npcSelect.AutoSelectOnAdd = true;
        _npcSelect.Setup(
            () => _db.Npcs.GetAll(faction.CampaignId)
                      .Where(n => !_db.Npcs.GetByFaction(_faction.Id).Any(fn => fn.Id == n.Id))
                      .Select(n => (n.Id, n.Name))
                      .ToList(),
            name =>
            {
                var newNpc = new Npc { CampaignId = _faction.CampaignId, Name = name };
                _db.Npcs.Add(newNpc);
            },
            id => _db.Npcs.Delete(id));
        _npcSelect.TypeCreated += id => EmitSignal(SignalName.EntityCreated, "npc", id);
        _npcSelect.SelectById(null);

        _roleSelect.NoneText        = "No role";
        _roleSelect.AutoSelectOnAdd = true;
        _roleSelect.Setup(
            () => _db.NpcFactionRoles.GetAll(faction.CampaignId).ConvertAll(r => (r.Id, r.Name)),
            name => { _db.NpcFactionRoles.Add(new DndBuilder.Core.Models.NpcFactionRole { CampaignId = _faction.CampaignId, Name = name, Description = "" }); },
            id   => _db.NpcFactionRoles.Delete(id));
        _roleSelect.SelectById(null);

        LoadNpcRows();

        _faction.RelatedFactions = _db.Factions.GetRelationships(faction.Id);

        _relFactionSelfLabel.Text = string.IsNullOrEmpty(faction.Name) ? "New Faction" : faction.Name;

        _relFactionTypeSelect.NoneText        = "Relationship";
        _relFactionTypeSelect.AutoSelectOnAdd = true;
        _relFactionTypeSelect.Setup(
            () => _db.FactionRelationshipTypes.GetAll(faction.CampaignId).ConvertAll(t => (t.Id, t.Name)),
            name => { _db.FactionRelationshipTypes.Add(new DndBuilder.Core.Models.FactionRelationshipType { CampaignId = _faction.CampaignId, Name = name, Description = "" }); },
            id   => _db.FactionRelationshipTypes.Delete(id));
        _relFactionTypeSelect.SelectById(null);

        _relFactionSelect.NoneText        = "Pick a faction";
        _relFactionSelect.AutoSelectOnAdd = true;
        _relFactionSelect.Setup(
            () => _db.Factions.GetAll(faction.CampaignId)
                      .Where(f => f.Id != _faction.Id && !_faction.RelatedFactions.Any(rf => rf.FactionId == f.Id || rf.RelatedFactionId == f.Id))
                      .Select(f => (f.Id, f.Name))
                      .ToList(),
            name =>
            {
                var newFaction = new Faction { CampaignId = _faction.CampaignId, Name = name };
                _db.Factions.Add(newFaction);
            },
            id => _db.Factions.Delete(id));
        _relFactionSelect.TypeCreated += id => EmitSignal(SignalName.EntityCreated, "faction", id);
        _relFactionSelect.SelectById(null);

        LoadRelFactionRows();
        _loaded = true;
    }

    private void PopulateNpcDropdown()
    {
        _npcSelect.SelectById(null);
        _roleSelect.SelectById(null);
        _addNpcButton.Disabled = true;
    }

    private void LoadNpcRows()
    {
        foreach (Node child in _npcRowsContainer.GetChildren())
            child.QueueFree();

        var npcs = _db.Npcs.GetByFaction(_faction.Id);

        var roles     = _db.NpcFactionRoles.GetAll(_faction.CampaignId);
        var roleNames = new Dictionary<int, string>();
        foreach (var r in roles) roleNames[r.Id] = r.Name;

        foreach (var npc in npcs)
        {
            int    capturedNpcId = npc.Id;
            var    link          = npc.Factions.Find(f => f.FactionId == _faction.Id);
            string roleName      = link?.RoleId.HasValue == true && roleNames.TryGetValue(link.RoleId.Value, out var rn) ? rn : "No role";

            var row = new EntityRow { Text = $"{npc.Name}, {roleName}" };
            row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "npc", capturedNpcId);
            row.DeletePressed   += () =>
            {
                _db.Npcs.RemoveFaction(capturedNpcId, _faction.Id);
                LoadNpcRows();
            };
            _npcRowsContainer.AddChild(row);
        }

        PopulateNpcDropdown();
    }

    private void OnAddNpcPressed()
    {
        if (!_npcSelect.SelectedId.HasValue) return;
        int  npcId = _npcSelect.SelectedId.Value;
        int? roleId = _roleSelect.SelectedId;

        _db.Npcs.AddFaction(npcId, _faction.Id, roleId);
        LoadNpcRows();
    }

    private void PopulateRelFactionDropdown()
    {
        _relFactionSelect.SelectById(null);
        _relFactionTypeSelect.SelectById(null);
        _addRelFactionButton.Disabled = true;
    }

    private void LoadRelFactionRows()
    {
        foreach (Node child in _relFactionRowsContainer.GetChildren())
            child.QueueFree();

        var allFactions  = _db.Factions.GetAll(_faction.CampaignId);
        var factionNames = new Dictionary<int, string>();
        foreach (var f in allFactions) factionNames[f.Id] = f.Name;

        var types     = _db.FactionRelationshipTypes.GetAll(_faction.CampaignId);
        var typeNames = new Dictionary<int, string>();
        foreach (var t in types) typeNames[t.Id] = t.Name;

        foreach (var rel in _faction.RelatedFactions)
        {
            int    capturedA    = rel.FactionId;
            int    capturedB    = rel.RelatedFactionId;
            int    capturedOther = rel.FactionId == _faction.Id ? rel.RelatedFactionId : rel.FactionId;
            string nameA    = factionNames.TryGetValue(rel.FactionId,        out var fa) ? fa : "Unknown";
            string nameB    = factionNames.TryGetValue(rel.RelatedFactionId, out var fb) ? fb : "Unknown";
            string typeName = rel.RelationshipTypeId.HasValue && typeNames.TryGetValue(rel.RelationshipTypeId.Value, out var tn) ? tn : "";

            var row = new EntityRow { Text = string.IsNullOrEmpty(typeName) ? $"{nameA}, {nameB}" : $"{nameA}, {typeName} {nameB}" };
            row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "faction", capturedOther);
            row.DeletePressed   += () =>
            {
                _db.Factions.RemoveRelationship(capturedA, capturedB);
                _faction.RelatedFactions = _db.Factions.GetRelationships(_faction.Id);
                LoadRelFactionRows();
            };
            _relFactionRowsContainer.AddChild(row);
        }

        PopulateRelFactionDropdown();
    }

    private void OnAddRelFactionPressed()
    {
        if (!_relFactionSelect.SelectedId.HasValue) return;
        int  relFactionId = _relFactionSelect.SelectedId.Value;
        int? typeId       = _relFactionTypeSelect.SelectedId;
        _db.Factions.AddRelationship(_faction.Id, relFactionId, typeId);
        _faction.RelatedFactions = _db.Factions.GetRelationships(_faction.Id);
        LoadRelFactionRows();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_faction == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_faction.Name}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

    private void Save()
    {
        if (_faction == null) return;
        _faction.Name        = string.IsNullOrEmpty(_nameInput.Text) ? "New Faction" : _nameInput.Text;
        _faction.Type        = _typeInput.Text;
        _faction.Description = _descInput.Text;
        _faction.Goals        = _goalsInput.Text;
        _faction.Notes        = _notes.Text;
        _db.Factions.Edit(_faction);
    }

}