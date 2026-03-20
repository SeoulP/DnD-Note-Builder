using DndBuilder.Core.Models;
using Godot;


public partial class SessionDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Session            _session;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    [Export] private Label         _numberLabel;
    [Export] private LineEdit      _titleInput;
    [Export] private LineEdit      _playedOnInput;
    [Export] private WikiNotes _notes;
    [Export] private Button        _deleteButton;
    [Export] private ImageCarousel _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _titleInput.TextChanged    += title => { Save(); EmitSignal(SignalName.NameChanged, "session", _session?.Id ?? 0, string.IsNullOrEmpty(title) ? "Untitled Session" : title); };
        _titleInput.FocusExited    += () => { if (_titleInput.Text == "") _titleInput.Text = "New Session"; };
        _titleInput.FocusEntered   += () => _titleInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _playedOnInput.TextChanged += _ => Save();
        _notes.TextChanged   += () => Save();
        _notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);

        _confirmDialog = DialogHelper.Make("Delete Session");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "session", _session?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_session?.Title}\"? This cannot be undone.");
        };
    }

    public void Load(Session session)
    {
        _session = session;

        _imageCarousel?.Setup(EntityType.Session, session.Id, _db);

        _numberLabel.Text   = $"Session #{session.Number:D3}";
        _titleInput.Text    = string.IsNullOrEmpty(session.Title) ? "New Session" : session.Title;
        _playedOnInput.Text = session.PlayedOn;
        _notes.Setup(session.CampaignId, _db);
        _notes.Text = session.Notes;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_session == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_session.Title}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

    private void Save()
    {
        if (_session == null) return;
        _session.Title    = string.IsNullOrEmpty(_titleInput.Text) ? "New Session" : _titleInput.Text;
        _session.PlayedOn = _playedOnInput.Text;
        _session.Notes    = _notes.Text;
        _db.Sessions.Edit(_session);
    }

}