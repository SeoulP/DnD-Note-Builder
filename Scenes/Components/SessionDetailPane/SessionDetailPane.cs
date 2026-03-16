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

    [Export] private Label         _numberLabel;
    [Export] private LineEdit      _titleInput;
    [Export] private LineEdit      _playedOnInput;
    [Export] private TextEdit      _notesInput;
    [Export] private RichTextLabel _notesRenderer;
    [Export] private Button        _deleteButton;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _titleInput.TextChanged    += title => { Save(); EmitSignal(SignalName.NameChanged, "session", _session?.Id ?? 0, $"#{_session?.Number ?? 0:D3} \u2013 {(string.IsNullOrEmpty(title) ? "New Session" : title)}"); };
        _titleInput.FocusExited    += () => { if (_titleInput.Text == "") _titleInput.Text = "New Session"; };
        _playedOnInput.TextChanged += _ => Save();
        _notesInput.TextChanged    += () => { Save(); RenderNotes(); };

        _notesRenderer.MetaClicked += OnMetaClicked;

        _confirmDialog = new ConfirmationDialog { Title = "Delete Session" };
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "session", _session?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            _confirmDialog.DialogText = $"Delete \"{_session?.Title}\"? This cannot be undone.";
            _confirmDialog.PopupCentered();
        };
    }

    public void Load(Session session)
    {
        _session = session;
        _numberLabel.Text   = $"Session #{session.Number:D3}";
        _titleInput.Text    = string.IsNullOrEmpty(session.Title) ? "New Session" : session.Title;
        _playedOnInput.Text = session.PlayedOn;
        _notesInput.Text    = session.Notes;
        RenderNotes();
    }

    private void Save()
    {
        if (_session == null) return;
        _session.Title    = string.IsNullOrEmpty(_titleInput.Text) ? "New Session" : _titleInput.Text;
        _session.PlayedOn = _playedOnInput.Text;
        _session.Notes    = _notesInput.Text;
        _db.Sessions.Edit(_session);
    }

    private void RenderNotes()
    {
        if (_session == null) return;
        _notesRenderer.Text = WikiLinkParser.Parse(_notesInput.Text, _db, _session.CampaignId);
    }

    private void OnMetaClicked(Variant meta)
    {
        var parts = meta.AsString().Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
            EmitSignal(SignalName.NavigateTo, parts[0], id);
    }
}