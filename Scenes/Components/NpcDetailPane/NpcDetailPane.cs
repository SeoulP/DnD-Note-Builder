using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;


public partial class NpcDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Npc                _npc;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    [Export] private LineEdit          _nameInput;
    [Export] private TypeOptionButton  _speciesInput;
    [Export] private LineEdit          _occupationInput;
    [Export] private LineEdit          _genderInput;
    [Export] private TypeOptionButton  _statusInput;
    [Export] private TypeOptionButton  _relationshipInput;
    [Export] private TypeOptionButton  _factionSelect;
    [Export] private TypeOptionButton  _roleSelect;
    [Export] private Button            _addFactionButton;
    [Export] private VBoxContainer     _factionRowsContainer;
    [Export] private Button            _deleteButton;
    [Export] private TextEdit          _descInput;
    [Export] private WikiNotes _notes;
    [Export] private ImageCarousel     _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged          += name => { Save(); EmitSignal(SignalName.NameChanged, "npc", _npc?.Id ?? 0, string.IsNullOrEmpty(name) ? "New NPC" : name); };
        _nameInput.FocusExited          += () => { if (_nameInput.Text == "") _nameInput.Text = "New NPC"; };
        _occupationInput.TextChanged    += _ => Save();
        _genderInput.TextChanged        += _ => Save();
        _speciesInput.TypeSelected      += _ => Save();
        _statusInput.TypeSelected       += _ => Save();
        _relationshipInput.TypeSelected += _ => Save();
        _descInput.TextChanged          += () => Save();
        _notes.TextChanged += () => Save();
        _notes.NavigateTo  += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _confirmDialog = DialogHelper.Make("Delete NPC");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "npc", _npc?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_npc?.Name}\"? This cannot be undone.");
        };

        _factionSelect.TypeSelected += id => _addFactionButton.Disabled = (id == -1);
        _roleSelect.TypeSelected    += _ => { };  // no-op; role is read at add-time
        _addFactionButton.Pressed   += OnAddFactionPressed;
    }

    public void Load(Npc npc)
    {
        _npc = npc;

        _speciesInput.NoneText = "(unknown)";
        _speciesInput.Setup(
            () => _db.Species.GetAll(npc.CampaignId).ConvertAll(s => (s.Id, s.Name)),
            name => { _db.Species.Add(new Species { CampaignId = npc.CampaignId, Name = name }); },
            id   => _db.Species.Delete(id));
        _speciesInput.SelectById(npc.SpeciesId);

        _statusInput.Setup(
            () => _db.NpcStatuses.GetAll(npc.CampaignId).ConvertAll(s => (s.Id, s.Name)),
            name => { _db.NpcStatuses.Add(new NpcStatus { CampaignId = npc.CampaignId, Name = name, Description = "" }); },
            id   => _db.NpcStatuses.Delete(id));
        _statusInput.SelectById(npc.StatusId);

        _relationshipInput.Setup(
            () => _db.NpcRelationshipTypes.GetAll(npc.CampaignId).ConvertAll(r => (r.Id, r.Name)),
            name => { _db.NpcRelationshipTypes.Add(new NpcRelationshipType { CampaignId = npc.CampaignId, Name = name, Description = "" }); },
            id   => _db.NpcRelationshipTypes.Delete(id));
        _relationshipInput.SelectById(npc.RelationshipTypeId);

        _factionSelect.NoneText        = "Pick a faction";
        _factionSelect.AutoSelectOnAdd = true;
        _factionSelect.Setup(
            () => _db.Factions.GetAll(npc.CampaignId)
                      .Where(f => !_npc.Factions.Any(nf => nf.FactionId == f.Id))
                      .Select(f => (f.Id, f.Name))
                      .ToList(),
            name => _db.Factions.Add(new Faction { CampaignId = _npc.CampaignId, Name = name }),
            id   => _db.Factions.Delete(id));
        _factionSelect.TypeCreated += id => EmitSignal(SignalName.EntityCreated, "faction", id);
        _factionSelect.SelectById(null);

        _roleSelect.NoneText = "No role";
        _roleSelect.Setup(
            () => _db.NpcFactionRoles.GetAll(npc.CampaignId).ConvertAll(r => (r.Id, r.Name)),
            name => { _db.NpcFactionRoles.Add(new DndBuilder.Core.Models.NpcFactionRole { CampaignId = npc.CampaignId, Name = name, Description = "" }); },
            id   => _db.NpcFactionRoles.Delete(id));
        _roleSelect.SelectById(null);

        _imageCarousel?.Setup(EntityType.Npc, npc.Id, _db);

        _nameInput.Text       = string.IsNullOrEmpty(npc.Name) ? "New NPC" : npc.Name;
        _occupationInput.Text = npc.Occupation;
        _genderInput.Text     = npc.Gender;

        PopulateFactionDropdown();
        LoadFactionRows();

        _descInput.Text  = npc.Description;
        _notes.Setup(npc.CampaignId, _db);
        _notes.Text = npc.Notes;
    }

    private void PopulateFactionDropdown()
    {
        _factionSelect.SelectById(null);
        _roleSelect.SelectById(null);
        _addFactionButton.Disabled = true;
    }

    private void LoadFactionRows()
    {
        foreach (Node child in _factionRowsContainer.GetChildren())
            child.QueueFree();

        var allFactions  = _db.Factions.GetAll(_npc.CampaignId);
        var factionNames = new Dictionary<int, string>();
        foreach (var f in allFactions) factionNames[f.Id] = f.Name;

        var roles     = _db.NpcFactionRoles.GetAll(_npc.CampaignId);
        var roleNames = new Dictionary<int, string>();
        foreach (var r in roles) roleNames[r.Id] = r.Name;

        foreach (var nf in _npc.Factions)
        {
            int    capturedFactionId = nf.FactionId;
            string factionName       = factionNames.TryGetValue(nf.FactionId, out var fn) ? fn : "Unknown";
            string roleName          = nf.RoleId.HasValue && roleNames.TryGetValue(nf.RoleId.Value, out var rn) ? rn : "No role";

            var row = new EntityRow { Text = $"{factionName} — {roleName}" };
            row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "faction", capturedFactionId);
            row.DeletePressed   += () =>
            {
                _db.Npcs.RemoveFaction(_npc.Id, capturedFactionId);
                _npc.Factions.RemoveAll(f => f.FactionId == capturedFactionId);
                _npc.FactionIds.Remove(capturedFactionId);
                PopulateFactionDropdown();
                LoadFactionRows();
            };
            _factionRowsContainer.AddChild(row);
        }
    }

    private void OnAddFactionPressed()
    {
        if (!_factionSelect.SelectedId.HasValue) return;
        int  factionId = _factionSelect.SelectedId.Value;
        int? roleId    = _roleSelect.SelectedId;

        _db.Npcs.AddFaction(_npc.Id, factionId, roleId);
        _npc.Factions.Add(new NpcFaction { NpcId = _npc.Id, FactionId = factionId, RoleId = roleId });
        _npc.FactionIds.Add(factionId);
        PopulateFactionDropdown();
        LoadFactionRows();
    }

    private void Save()
    {
        if (_npc == null) return;
        _npc.Name              = string.IsNullOrEmpty(_nameInput.Text) ? "New NPC" : _nameInput.Text;
        _npc.SpeciesId         = _speciesInput.SelectedId;
        _npc.Occupation        = _occupationInput.Text;
        _npc.Gender            = _genderInput.Text;
        _npc.StatusId          = _statusInput.SelectedId;
        _npc.RelationshipTypeId = _relationshipInput.SelectedId;
        _npc.Description       = _descInput.Text;
        _npc.Notes             = _notes.Text;
        _db.Npcs.Edit(_npc);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_npc == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_npc.Name}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

}
