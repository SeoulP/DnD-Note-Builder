using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class NpcDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Npc                _npc;
    private List<Species>      _allSpecies = new();
    private ConfirmationDialog _confirmDialog;
    private PopupMenu          _factionsPopup;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private OptionButton  _speciesInput;
    [Export] private LineEdit      _occupationInput;
    [Export] private LineEdit      _genderInput;
    [Export] private OptionButton  _statusInput;
    [Export] private OptionButton  _relationshipInput;
    [Export] private MenuButton    _factionsDropdown;
    [Export] private Button        _deleteButton;
    [Export] private TextEdit      _descInput;
    [Export] private TextEdit      _notesInput;
    [Export] private RichTextLabel _notesRenderer;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        foreach (var s in System.Enum.GetNames(typeof(NpcStatus)))       _statusInput.AddItem(s);
        foreach (var r in System.Enum.GetNames(typeof(NpcRelationship))) _relationshipInput.AddItem(r);

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

        // Arrow icon — visual only, matches OptionButton
        var arrow = _relationshipInput.GetThemeIcon("arrow");
        if (arrow != null)
        {
            _factionsDropdown.Icon          = arrow;
            _factionsDropdown.IconAlignment = HorizontalAlignment.Right;
        }

        _factionsPopup = _factionsDropdown.GetPopup();
        _factionsPopup.HideOnCheckableItemSelection = false;
        _factionsPopup.HideOnItemSelection         = false;
        _factionsPopup.ShrinkWidth                 = false;
        _factionsPopup.AboutToPopup += () => _factionsPopup.Size = new Vector2I((int)_factionsDropdown.Size.X, 0);
        _factionsPopup.IdPressed += OnFactionToggled;
    }

    public void Load(Npc npc)
    {
        _npc = npc;

        // Populate species dropdown (campaign-specific)
        _allSpecies = _db.Species.GetAll(npc.CampaignId);
        _speciesInput.Clear();
        _speciesInput.AddItem("(unknown)");
        foreach (var s in _allSpecies) _speciesInput.AddItem(s.Name);

        _nameInput.Text       = string.IsNullOrEmpty(npc.Name) ? "New NPC" : npc.Name;
        _occupationInput.Text = npc.Occupation;
        _genderInput.Text     = npc.Gender;

        if (npc.SpeciesId.HasValue)
        {
            int idx = _allSpecies.FindIndex(s => s.Id == npc.SpeciesId.Value);
            _speciesInput.Select(idx >= 0 ? idx + 1 : 0);
        }
        else
        {
            _speciesInput.Select(0);
        }

        _statusInput.Select((int)npc.Status);
        _relationshipInput.Select((int)npc.Relationship);

        PopulateFactions();

        _descInput.Text  = npc.Description;
        _notesInput.Text = npc.Notes;
        RenderNotes();
    }

    private void PopulateFactions()
    {
        _factionsPopup.Clear();

        var allFactions = _db.Factions.GetAll(_npc.CampaignId);

        if (allFactions.Count == 0)
        {
            _factionsDropdown.Text     = "None available";
            _factionsDropdown.Disabled = true;
            return;
        }

        _factionsDropdown.Disabled = false;
        var memberSet = new HashSet<int>(_npc.FactionIds);

        foreach (var faction in allFactions)
        {
            _factionsPopup.AddCheckItem(faction.Name, faction.Id);
            int idx = _factionsPopup.GetItemIndex(faction.Id);
            _factionsPopup.SetItemChecked(idx, memberSet.Contains(faction.Id));
        }

        UpdateDropdownText();
    }

    private void OnFactionToggled(long id)
    {
        int fid        = (int)id;
        int idx        = _factionsPopup.GetItemIndex(fid);
        bool nowChecked = !_factionsPopup.IsItemChecked(idx); // invert — manage toggle manually
        _factionsPopup.SetItemChecked(idx, nowChecked);

        if (nowChecked) _db.Npcs.AddFaction(_npc.Id, fid);
        else            _db.Npcs.RemoveFaction(_npc.Id, fid);

        UpdateDropdownText();
    }

    private void UpdateDropdownText()
    {
        var selected = new List<string>();
        for (int i = 0; i < _factionsPopup.ItemCount; i++)
            if (_factionsPopup.IsItemChecked(i))
                selected.Add(_factionsPopup.GetItemText(i));

        _factionsDropdown.Text = selected.Count > 0 ? string.Join(", ", selected) : "Select factions...";
    }

    private void Save()
    {
        if (_npc == null) return;

        int speciesIdx  = _speciesInput.Selected;
        _npc.Name       = string.IsNullOrEmpty(_nameInput.Text) ? "New NPC" : _nameInput.Text;
        _npc.SpeciesId  = speciesIdx == 0 ? null : _allSpecies[speciesIdx - 1].Id;
        _npc.Occupation = _occupationInput.Text;
        _npc.Gender     = _genderInput.Text;
        _npc.Status       = (NpcStatus)_statusInput.Selected;
        _npc.Relationship = (NpcRelationship)_relationshipInput.Selected;
        _npc.Description  = _descInput.Text;
        _npc.Notes        = _notesInput.Text;
        _db.Npcs.Edit(_npc);
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
