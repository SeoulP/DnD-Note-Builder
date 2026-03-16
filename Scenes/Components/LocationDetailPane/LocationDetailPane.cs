using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class LocationDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Location           _location;
    private ConfirmationDialog _confirmDialog;
    private PopupMenu          _factionsPopup;

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
    [Export] private MenuButton    _factionsDropdown;
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

        _factionsPopup = _factionsDropdown.GetPopup();
        _factionsPopup.HideOnCheckableItemSelection = false;
        _factionsPopup.HideOnItemSelection         = false;
        _factionsPopup.ShrinkWidth                 = false;
        _factionsPopup.AboutToPopup += () => _factionsPopup.Size = new Vector2I((int)_factionsDropdown.Size.X, 0);
        _factionsPopup.IdPressed    += OnFactionToggled;

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
            AddChild(popup);
            popup.AddItem("New...", 0);

            foreach (var loc in _db.Locations.GetAll(_location.CampaignId))
            {
                if (loc.Id == _location.Id) continue;
                if (loc.ParentLocationId == _location.Id) continue; // already a child
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
            int btnRight  = (int)(btnRect.Position.X + btnRect.Size.X);
            int btnBottom = (int)(btnRect.Position.Y + btnRect.Size.Y);
            popup.Popup(new Rect2I(btnRight, btnBottom, 0, 0));
            popup.Position = new Vector2I(btnRight - popup.Size.X, btnBottom);
        };
    }

    public void Load(Location location)
    {
        _location = location;
        _nameInput.Text  = string.IsNullOrEmpty(location.Name) ? "New Location" : location.Name;
        _typeInput.Text  = location.Type;
        _descInput.Text  = location.Description;
        _notesInput.Text = location.Notes;
        RenderNotes();
        PopulateFactions();
        LoadSubLocations();
    }

    private void LoadSubLocations()
    {
        foreach (Node child in _subLocationsContainer.GetChildren())
            child.QueueFree();

        foreach (var sub in _location.SubLocations)
        {
            int id = sub.Id;
            var btn = new Button { Text = sub.Name, Flat = true, Alignment = HorizontalAlignment.Left, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btn.Pressed += () => EmitSignal(SignalName.NavigateTo, "location", id);
            var panel = new PanelContainer();
            panel.AddChild(btn);
            _subLocationsContainer.AddChild(panel);
        }
    }

    private void PopulateFactions()
    {
        _factionsPopup.Clear();
        var allFactions = _db.Factions.GetAll(_location.CampaignId);

        if (allFactions.Count == 0)
        {
            _factionsDropdown.Text     = "None available";
            _factionsDropdown.Disabled = true;
            return;
        }

        _factionsDropdown.Disabled = false;
        var memberSet = new HashSet<int>(_location.FactionIds);

        foreach (var faction in allFactions)
        {
            _factionsPopup.AddCheckItem(faction.Name, faction.Id);
            int idx = _factionsPopup.GetItemIndex(faction.Id);
            _factionsPopup.SetItemChecked(idx, memberSet.Contains(faction.Id));
        }

        UpdateFactionDropdownText();
    }

    private void OnFactionToggled(long id)
    {
        int  fid        = (int)id;
        int  idx        = _factionsPopup.GetItemIndex(fid);
        bool nowChecked = !_factionsPopup.IsItemChecked(idx);
        _factionsPopup.SetItemChecked(idx, nowChecked);

        if (nowChecked) _db.Locations.AddFaction(_location.Id, fid);
        else            _db.Locations.RemoveFaction(_location.Id, fid);

        UpdateFactionDropdownText();
    }

    private void UpdateFactionDropdownText()
    {
        var selected = new List<string>();
        for (int i = 0; i < _factionsPopup.ItemCount; i++)
            if (_factionsPopup.IsItemChecked(i))
                selected.Add(_factionsPopup.GetItemText(i));

        _factionsDropdown.Text = selected.Count > 0 ? string.Join(", ", selected) : "Select factions...";
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