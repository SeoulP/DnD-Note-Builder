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
    [Signal] public delegate void ParentLocationChangedEventHandler(int locationId);

    [Export] private LineEdit         _nameInput;
    [Export] private LineEdit         _typeInput;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes _notes;
    [Export] private Button           _deleteButton;
    [Export] private VBoxContainer    _factionRowsContainer;
    [Export] private TypeOptionButton _factionSelect;
    [Export] private TypeOptionButton _roleSelect;
    [Export] private Button           _addFactionButton;
    [Export] private Button           _setParentButton;
    [Export] private TypeOptionButton _parentLocationSelect;
    [Export] private VBoxContainer    _parentLocationContainer;
    [Export] private TypeOptionButton _subLocationSelect;
    [Export] private Button           _addSubLocationButton;
    [Export] private VBoxContainer    _subLocationsContainer;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "location", _location?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Location" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Location"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _typeInput.TextChanged  += _ => Save();
        _descInput.TextChanged  += () => Save();
        _notes.TextChanged   += () => Save();
        _notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);

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

        _setParentButton.Pressed += EnterParentEditMode;

        _parentLocationSelect.TypeSelected += id =>
        {
            if (_location == null) return;
            _location.ParentLocationId = id == -1 ? null : (int?)id;
            _db.Locations.Edit(_location);
            ExitParentEditMode();
            EmitSignal(SignalName.ParentLocationChanged, _location.Id);
        };
        _parentLocationSelect.TypeCreated  += _ => EmitSignal(SignalName.ParentLocationChanged, _location?.Id ?? 0);
        _parentLocationSelect.PopupClosed  += ExitParentEditMode;

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

        _roleSelect.NoneText        = "No role";
        _roleSelect.AutoSelectOnAdd = true;
        _roleSelect.Setup(
            () => _db.LocationFactionRoles.GetAll(location.CampaignId).ConvertAll(r => (r.Id, r.Name)),
            name => { _db.LocationFactionRoles.Add(new LocationFactionRole { CampaignId = location.CampaignId, Name = name, Description = "" }); },
            id   => _db.LocationFactionRoles.Delete(id));
        _roleSelect.SelectById(null);

        _imageCarousel?.Setup(EntityType.Location, location.Id, _db, location.CampaignId);

        _parentLocationSelect.NoneText          = "No parent (top-level)";
        _parentLocationSelect.AutoSelectOnAdd   = true;
        _parentLocationSelect.ShowDeleteButtons = false;
        _parentLocationSelect.Setup(
            () =>
            {
                var excluded = GetDescendantIds(_location.Id);
                excluded.Add(_location.Id);
                return _db.Locations.GetAll(_location.CampaignId)
                          .Where(l => !excluded.Contains(l.Id))
                          .Select(l => (l.Id, l.Name))
                          .ToList();
            },
            name => _db.Locations.Add(new Location { CampaignId = _location.CampaignId, Name = name }),
            _ => { });
        _parentLocationSelect.SelectById(location.ParentLocationId);
        ExitParentEditMode();

        _nameInput.Text  = string.IsNullOrEmpty(location.Name) ? "New Location" : location.Name;
        _typeInput.Text  = location.Type;
        _descInput.Text  = location.Description;
        _notes.Setup(location.CampaignId, _db);
        _notes.Text = location.Notes;
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
            () =>
            {
                var excluded = GetAncestorIds(_location.Id);
                excluded.Add(_location.Id);
                if (_location.ParentLocationId.HasValue)
                    foreach (var sibling in _db.Locations.GetChildren(_location.ParentLocationId.Value))
                        excluded.Add(sibling.Id);
                return _db.Locations.GetAll(_location.CampaignId)
                          .Where(l => !excluded.Contains(l.Id) && l.ParentLocationId != _location.Id)
                          .Select(l => (l.Id, l.Name))
                          .ToList();
            },
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

    private void EnterParentEditMode()
    {
        _parentLocationContainer.Visible  = false;
        _setParentButton.Visible          = false;
        _parentLocationSelect.Visible     = true;
        Callable.From(_parentLocationSelect.ShowPopup).CallDeferred();
    }

    private void ExitParentEditMode()
    {
        _parentLocationSelect.Visible     = false;
        _setParentButton.Visible          = true;
        _parentLocationContainer.Visible  = true;
        LoadParentRow();
    }

    private void LoadParentRow()
    {
        foreach (Node child in _parentLocationContainer.GetChildren())
            child.QueueFree();

        if (!_location.ParentLocationId.HasValue)
        {
            var none = new Label { Text = "No Parent" };
            none.AddThemeColorOverride("font_color", ThemeManager.Instance.Current.FontPlaceholder);
            none.AddThemeConstantOverride("margin_left", 6);
            _parentLocationContainer.AddChild(none);
            return;
        }

        var parent = _db.Locations.Get(_location.ParentLocationId.Value);
        if (parent == null) return;

        int parentId = parent.Id;
        var row = new EntityRow { Text = parent.Name };
        row.NavigatePressed += () => EmitSignal(SignalName.NavigateTo, "location", parentId);
        row.DeletePressed   += () =>
        {
            _location.ParentLocationId = null;
            _db.Locations.Edit(_location);
            _parentLocationSelect.SelectById(null);
            ExitParentEditMode();
            EmitSignal(SignalName.ParentLocationChanged, _location.Id);
        };
        _parentLocationContainer.AddChild(row);
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

            var row = new EntityRow { Text = $"{factionName}, {roleName}" };
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

    private HashSet<int> GetAncestorIds(int locationId)
    {
        var ids = new HashSet<int>();
        var current = _db.Locations.Get(locationId);
        while (current?.ParentLocationId.HasValue == true)
        {
            int parentId = current.ParentLocationId.Value;
            if (!ids.Add(parentId)) break; // cycle guard
            current = _db.Locations.Get(parentId);
        }
        return ids;
    }

    private HashSet<int> GetDescendantIds(int locationId)
    {
        var ids   = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(locationId);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (var child in _db.Locations.GetChildren(cur))
                if (ids.Add(child.Id)) queue.Enqueue(child.Id);
        }
        return ids;
    }

    private void Save()
    {
        if (_location == null) return;
        _location.Name        = string.IsNullOrEmpty(_nameInput.Text) ? "New Location" : _nameInput.Text;
        _location.Type        = _typeInput.Text;
        _location.Description = _descInput.Text;
        _location.Notes       = _notes.Text;
        _db.Locations.Edit(_location);
    }

}
