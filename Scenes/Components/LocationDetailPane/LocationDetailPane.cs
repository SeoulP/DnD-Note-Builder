using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class LocationDetailPane : ScrollContainer
{
    private DatabaseService           _db;
    private Location                  _location;
    private List<LocationFactionRole> _roles = new();
    private ConfirmationDialog        _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void SubLocationAddedEventHandler(int parentLocationId, int newLocationId);

    [Export] private LineEdit      _nameInput;
    [Export] private LineEdit      _typeInput;
    [Export] private TextEdit      _descInput;
    [Export] private TextEdit      _notesInput;
    [Export] private RichTextLabel _notesRenderer;
    [Export] private Button        _deleteButton;
    [Export] private VBoxContainer _factionRowsContainer;
    [Export] private OptionButton  _factionSelect;
    [Export] private OptionButton  _roleSelect;
    [Export] private Button        _addFactionButton;
    [Export] private Button        _addSubLocationButton;
    [Export] private VBoxContainer _subLocationsContainer;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "location", _location?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Location" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Location"; };
        _typeInput.TextChanged  += _ => Save();
        _descInput.TextChanged  += () => Save();
        _notesInput.TextChanged += () => { Save(); RenderNotes(); };

        _notesRenderer.MetaClicked += OnMetaClicked;

        _factionSelect.ItemSelected += _ => _addFactionButton.Disabled = _factionSelect.GetSelectedId() == -1;

        _addFactionButton.Pressed += () =>
        {
            if (_location == null || _factionSelect.GetSelectedId() == -1) return;
            int  factionId = _factionSelect.GetSelectedId();
            int  rawRoleId = _roleSelect.GetSelectedId();
            int? roleId    = rawRoleId == -1 ? null : rawRoleId;

            _db.Locations.AddFaction(_location.Id, factionId, roleId);
            _location.Factions.Add(new LocationFaction { LocationId = _location.Id, FactionId = factionId, RoleId = roleId });

            PopulateFactionDropdowns();
            LoadFactionRows();
        };

        _confirmDialog = new ConfirmationDialog { Title = "Delete Location" };
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "location", _location?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            _confirmDialog.DialogText = $"Delete \"{_location?.Name}\"? This cannot be undone.";
            _confirmDialog.PopupCentered();
        };

        _addSubLocationButton.Pressed += () =>
        {
            if (_location == null) return;

            var popup = new PopupMenu();
            popup.MaxSize = new Vector2I(0, 120);
            AddChild(popup);
            popup.AddItem("New...", 0);

            foreach (var loc in _db.Locations.GetAll(_location.CampaignId))
            {
                if (loc.Id == _location.Id) continue;
                if (loc.ParentLocationId == _location.Id) continue;
                popup.AddItem(loc.Name, loc.Id);
            }

            popup.IdPressed += (id) =>
            {
                if (id == 0)
                {
                    var child = new Location { CampaignId = _location.CampaignId, Name = "New Location", ParentLocationId = _location.Id };
                    int newId = _db.Locations.Add(child);
                    EmitSignal(SignalName.SubLocationAdded, _location.Id, newId);
                }
                else
                {
                    var existing = _db.Locations.Get((int)id);
                    if (existing != null)
                    {
                        existing.ParentLocationId = _location.Id;
                        _db.Locations.Edit(existing);
                        EmitSignal(SignalName.SubLocationAdded, _location.Id, _location.Id);
                    }
                }
                popup.QueueFree();
            };

            var btnRect = _addSubLocationButton.GetGlobalRect();
            int x = (int)btnRect.Position.X;
            int offset = Mathf.Min((int)btnRect.Size.Y * popup.ItemCount, 120);
            int y      = Mathf.Max(0, (int)btnRect.Position.Y - offset);
            popup.Popup(new Rect2I(x, y, (int)btnRect.Size.X, 120));
        };
    }

    public void Load(Location location)
    {
        _location = location;
        _roles    = _db.LocationFactionRoles.GetAll(location.CampaignId);

        _nameInput.Text  = string.IsNullOrEmpty(location.Name) ? "New Location" : location.Name;
        _typeInput.Text  = location.Type;
        _descInput.Text  = location.Description;
        _notesInput.Text = location.Notes;
        RenderNotes();
        PopulateFactionDropdowns();
        LoadFactionRows();
        LoadSubLocations();
    }

    private void LoadSubLocations()
    {
        foreach (Node child in _subLocationsContainer.GetChildren())
            child.QueueFree();

        foreach (var sub in _location.SubLocations)
        {
            int id     = sub.Id;
            var btn    = new Button { Text = sub.Name, Flat = true, Alignment = HorizontalAlignment.Left, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btn.Pressed += () => EmitSignal(SignalName.NavigateTo, "location", id);

            var panel  = new PanelContainer();
            var delBtn = new Button { Text = "×", Flat = true };
            delBtn.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", DeleteHoverBox);
            delBtn.MouseExited  += () => panel.RemoveThemeStyleboxOverride("panel");
            delBtn.Pressed += () =>
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

            var row = new HBoxContainer();
            row.AddChild(btn);
            row.AddChild(delBtn);
            panel.AddChild(row);
            _subLocationsContainer.AddChild(panel);
        }
    }

    private void PopulateFactionDropdowns()
    {
        var assignedIds  = new HashSet<int>(_location.Factions.Select(f => f.FactionId));
        var available    = _db.Factions.GetAll(_location.CampaignId)
                             .Where(f => !assignedIds.Contains(f.Id))
                             .ToList();
        bool hasAvailable = available.Count > 0;

        _factionSelect.Clear();
        _factionSelect.AddItem("Pick a faction", -1);
        foreach (var f in available) _factionSelect.AddItem(f.Name, f.Id);
        _factionSelect.Disabled = !hasAvailable;

        _roleSelect.Clear();
        _roleSelect.AddItem("No role", -1);
        foreach (var r in _roles) _roleSelect.AddItem(r.Name, r.Id);
        _roleSelect.Disabled = !hasAvailable;

        _addFactionButton.Disabled = true; // enabled only when a real faction is selected
    }

    private void LoadFactionRows()
    {
        foreach (Node child in _factionRowsContainer.GetChildren())
            child.QueueFree();

        var allFactions  = _db.Factions.GetAll(_location.CampaignId);
        var factionNames = new Dictionary<int, string>();
        foreach (var f in allFactions) factionNames[f.Id] = f.Name;

        var roleNames = new Dictionary<int, string>();
        foreach (var r in _roles) roleNames[r.Id] = r.Name;

        foreach (var lf in _location.Factions)
        {
            int    capturedFactionId = lf.FactionId;
            string factionName       = factionNames.TryGetValue(lf.FactionId, out var fn) ? fn : "Unknown";
            string roleName          = lf.RoleId.HasValue && roleNames.TryGetValue(lf.RoleId.Value, out var rn) ? rn : "No role";

            var navBtn = new Button
            {
                Text                = $"{factionName} — {roleName}",
                Flat                = true,
                Alignment           = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            navBtn.Pressed += () => EmitSignal(SignalName.NavigateTo, "faction", capturedFactionId);

            var panel  = new PanelContainer();
            var delBtn = new Button { Text = "×", Flat = true };
            delBtn.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", DeleteHoverBox);
            delBtn.MouseExited  += () => panel.RemoveThemeStyleboxOverride("panel");
            delBtn.Pressed += () =>
            {
                _db.Locations.RemoveFaction(_location.Id, capturedFactionId);
                _location.Factions.RemoveAll(f => f.FactionId == capturedFactionId);
                PopulateFactionDropdowns();
                LoadFactionRows();
            };

            var row = new HBoxContainer();
            row.AddChild(navBtn);
            row.AddChild(delBtn);
            panel.AddChild(row);
            _factionRowsContainer.AddChild(panel);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_location == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            _confirmDialog.DialogText = $"Delete \"{_location.Name}\"? This cannot be undone.";
            _confirmDialog.PopupCentered();
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

    private static readonly StyleBoxFlat DeleteHoverBox = MakeDeleteHoverBox();
    private static StyleBoxFlat MakeDeleteHoverBox()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0.90f, 0.55f, 0.55f) };
        box.SetCornerRadiusAll(3);
        return box;
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