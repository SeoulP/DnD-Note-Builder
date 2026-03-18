using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;


public partial class LocationDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Location           _location;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void SubLocationAddedEventHandler(int parentLocationId, int newLocationId);
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    [Export] private LineEdit         _nameInput;
    [Export] private LineEdit         _typeInput;
    [Export] private TextEdit         _descInput;
    [Export] private TextEdit         _notesInput;
    [Export] private RichTextLabel    _notesRenderer;
    [Export] private Button           _deleteButton;
    [Export] private VBoxContainer    _factionRowsContainer;
    [Export] private TypeOptionButton _factionSelect;
    [Export] private TypeOptionButton _roleSelect;
    [Export] private Button           _addFactionButton;
    [Export] private TypeOptionButton _subLocationSelect;
    [Export] private Button           _addSubLocationButton;
    [Export] private VBoxContainer    _subLocationsContainer;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "location", _location?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Location" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Location"; };
        _typeInput.TextChanged  += _ => Save();
        _descInput.TextChanged  += () => Save();
        _notesInput.TextChanged += () => { Save(); RenderNotes(); };

        _notesRenderer.MetaClicked += OnMetaClicked;

        _factionSelect.TypeSelected += id => _addFactionButton.Disabled = (id == -1);
        _factionSelect.TypeCreated  += id => EmitSignal(SignalName.EntityCreated, "faction", id);

        _addFactionButton.Pressed += () =>
        {
            if (_location == null || !_factionSelect.SelectedId.HasValue) return;
            int  factionId = _factionSelect.SelectedId.Value;
            int? roleId    = _roleSelect.SelectedId;

            _db.Locations.AddFaction(_location.Id, factionId, roleId);
            _location.Factions.Add(new LocationFaction { LocationId = _location.Id, FactionId = factionId, RoleId = roleId });

            _factionSelect.SelectById(null);
            LoadFactionRows();
        };

        _confirmDialog = DialogHelper.Make("Delete Location");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "location", _location?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_location?.Name}\"? This cannot be undone.");
        };

        _subLocationSelect.TypeSelected += id => _addSubLocationButton.Disabled = (id == -1);
        _subLocationSelect.TypeCreated  += id =>
        {
            EmitSignal(SignalName.EntityCreated, "location", id);
            if (_location == null) return;
            var loc = _db.Locations.Get(id);
            if (loc == null) return;
            loc.ParentLocationId = _location.Id;
            _db.Locations.Edit(loc);
            _location.SubLocations.Add(loc);
            _subLocationSelect.SelectById(null);
            LoadSubLocations();
            EmitSignal(SignalName.SubLocationAdded, _location.Id, id);
        };

        _addSubLocationButton.Pressed += () =>
        {
            if (_location == null || !_subLocationSelect.SelectedId.HasValue) return;
            int subId    = _subLocationSelect.SelectedId.Value;
            var existing = _db.Locations.Get(subId);
            if (existing == null) return;
            existing.ParentLocationId = _location.Id;
            _db.Locations.Edit(existing);
            _location.SubLocations.Add(existing);
            _subLocationSelect.SelectById(null);
            LoadSubLocations();
            EmitSignal(SignalName.SubLocationAdded, _location.Id, subId);
        };
    }

    public void Load(Location location)
    {
        _location = location;

        _roleSelect.NoneText = "No role";
        _roleSelect.Setup(
            () => _db.LocationFactionRoles.GetAll(location.CampaignId).ConvertAll(r => (r.Id, r.Name)),
            name => { _db.LocationFactionRoles.Add(new LocationFactionRole { CampaignId = location.CampaignId, Name = name, Description = "" }); },
            id   => _db.LocationFactionRoles.Delete(id));
        _roleSelect.SelectById(null);

        _imageCarousel?.Setup(EntityType.Location, location.Id, _db);

        _nameInput.Text  = string.IsNullOrEmpty(location.Name) ? "New Location" : location.Name;
        _typeInput.Text  = location.Type;
        _descInput.Text  = location.Description;
        _notesInput.Text = location.Notes;
        RenderNotes();
        _factionSelect.NoneText       = "Pick a faction";
        _factionSelect.AutoSelectOnAdd = true;
        _factionSelect.Setup(
            () => _db.Factions.GetAll(_location.CampaignId)
                      .Where(f => !_location.Factions.Any(lf => lf.FactionId == f.Id))
                      .Select(f => (f.Id, f.Name))
                      .ToList(),
            name => _db.Factions.Add(new Faction { CampaignId = _location.CampaignId, Name = name }),
            id   => _db.Factions.Delete(id));
        _factionSelect.SelectById(null);
        _subLocationSelect.NoneText       = "Pick a sub-location";
        _subLocationSelect.AutoSelectOnAdd = true;
        _subLocationSelect.Setup(
            () => _db.Locations.GetAll(_location.CampaignId)
                      .Where(l => l.Id != _location.Id && l.ParentLocationId != _location.Id)
                      .Select(l => (l.Id, l.Name))
                      .ToList(),
            name => _db.Locations.Add(new Location { CampaignId = _location.CampaignId, Name = name }),
            id =>
            {
                var loc = _db.Locations.Get(id);
                if (loc != null) { loc.ParentLocationId = null; _db.Locations.Edit(loc); }
            });
        _subLocationSelect.SelectById(null);
        LoadFactionRows();
        LoadSubLocations();
    }

    private void LoadSubLocations()
    {
        foreach (Node child in _subLocationsContainer.GetChildren())
            child.QueueFree();

        foreach (var sub in _location.SubLocations)
        {
            int id  = sub.Id;
            var row = new EntityRow { Text = sub.Name };
            row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "location", id);
            row.DeletePressed   += () =>
            {
                var subloc = _db.Locations.Get(id);
                if (subloc != null)
                {
                    subloc.ParentLocationId = null;
                    _db.Locations.Edit(subloc);
                }
                _location.SubLocations.RemoveAll(s => s.Id == id);
                LoadSubLocations();
            };
            _subLocationsContainer.AddChild(row);
        }
    }


    private void LoadFactionRows()
    {
        foreach (Node child in _factionRowsContainer.GetChildren())
            child.QueueFree();

        var allFactions  = _db.Factions.GetAll(_location.CampaignId);
        var factionNames = new Dictionary<int, string>();
        foreach (var f in allFactions) factionNames[f.Id] = f.Name;

        var roles     = _db.LocationFactionRoles.GetAll(_location.CampaignId);
        var roleNames = new Dictionary<int, string>();
        foreach (var r in roles) roleNames[r.Id] = r.Name;

        foreach (var lf in _location.Factions)
        {
            int    capturedFactionId = lf.FactionId;
            string factionName       = factionNames.TryGetValue(lf.FactionId, out var fn) ? fn : "Unknown";
            string roleName          = lf.RoleId.HasValue && roleNames.TryGetValue(lf.RoleId.Value, out var rn) ? rn : "No role";

            var row = new EntityRow { Text = $"{factionName} — {roleName}" };
            row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "faction", capturedFactionId);
            row.DeletePressed   += () =>
            {
                _db.Locations.RemoveFaction(_location.Id, capturedFactionId);
                _location.Factions.RemoveAll(f => f.FactionId == capturedFactionId);
                _factionSelect.SelectById(null);
                LoadFactionRows();
            };
            _factionRowsContainer.AddChild(row);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_location == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_location.Name}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

    private void Save()
    {
        if (_location == null) return;
        _location.Name        = string.IsNullOrEmpty(_nameInput.Text) ? "New Location" : _nameInput.Text;
        _location.Type        = _typeInput.Text;
        _location.Description = _descInput.Text;
        _location.Notes       = _notesInput.Text;
        _db.Locations.Edit(_location);
    }

    private void RenderNotes()
    {
        if (_location == null) return;
        _notesRenderer.Text = WikiLinkParser.Parse(_notesInput.Text, _db, _location.CampaignId);
    }

    private void OnMetaClicked(Variant meta)
    {
        var parts = meta.AsString().Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
            EmitSignal(SignalName.NavigateTo, parts[0], id);
    }
}
