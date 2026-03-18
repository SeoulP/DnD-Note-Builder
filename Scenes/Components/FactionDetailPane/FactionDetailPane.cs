using DndBuilder.Core.Models;
using Godot;


public partial class FactionDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Faction            _faction;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private LineEdit      _typeInput;
    [Export] private TextEdit      _descInput;
    [Export] private TextEdit      _goalsInput;
    [Export] private TextEdit      _notesInput;
    [Export] private RichTextLabel _notesRenderer;
    [Export] private Button        _deleteButton;
    [Export] private ImageCarousel _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged         += name => { Save(); EmitSignal(SignalName.NameChanged, "faction", _faction?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Faction" : name); };
        _nameInput.FocusExited         += () => { if (_nameInput.Text == "") _nameInput.Text = "New Faction"; };
        _typeInput.TextChanged += _ => Save();
        _descInput.TextChanged += () => Save();
        _goalsInput.TextChanged        += () => Save();
        _notesInput.TextChanged        += () => { Save(); RenderNotes(); };

        _notesRenderer.MetaClicked += OnMetaClicked;

        _confirmDialog = DialogHelper.Make("Delete Faction");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "faction", _faction?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_faction?.Name}\"? This cannot be undone.");
        };
    }

    public void Load(Faction faction)
    {
        _faction = faction;

        _imageCarousel?.Setup(EntityType.Faction, faction.Id, _db);

        _nameInput.Text = string.IsNullOrEmpty(faction.Name) ? "New Faction" : faction.Name;
        _typeInput.Text = faction.Type;
        _descInput.Text = faction.Description;
        _goalsInput.Text        = faction.Goals;
        _notesInput.Text        = faction.Notes;
        RenderNotes();
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
        _faction.Notes        = _notesInput.Text;
        _db.Factions.Edit(_faction);
    }

    private void RenderNotes()
    {
        if (_faction == null) return;
        _notesRenderer.Text = WikiLinkParser.Parse(_notesInput.Text, _db, _faction.CampaignId);
    }

    private void OnMetaClicked(Variant meta)
    {
        var parts = meta.AsString().Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
            EmitSignal(SignalName.NavigateTo, parts[0], id);
    }
}