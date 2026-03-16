using System;
using DndBuilder.Core.Models;
using Godot;

public partial class AddSessionModal : Window
{
    private DatabaseService _databaseService;
    private int _campaignId;

    [Signal] public delegate void SessionCreatedEventHandler(int newId);
    [Signal] public delegate void SessionEditedEventHandler(int id);

    [Export] private LineEdit _titleInput;
    [Export] private Label _dateLabel;
    [Export] private Label _errorLabel;

    [Export] private Button _cancelButton;
    [Export] private Button _createButton;

    private enum ModalState { New, Edit }
    private ModalState _state;
    private Session _editingSession;

    public override void _Ready()
    {
        _databaseService = GetNode<DatabaseService>("/root/DatabaseService");

        CloseRequested += OnCancel;
        _cancelButton.Pressed += OnCancel;
        _createButton.Pressed += OnSubmit;

        _errorLabel.Visible = false;
    }

    public void OpenForNew(int campaignId)
    {
        _campaignId = campaignId;
        _state = ModalState.New;
        _editingSession = null;
        ResetForm();
        _createButton.Text = "Create";
        PopupCentered();
    }

    public void OpenForEdit(Session session)
    {
        _campaignId = session.CampaignId;
        _state = ModalState.Edit;
        _editingSession = session;
        ResetForm();

        _titleInput.Text = session.Title;
        _dateLabel.Text  = session.PlayedOn;

        _createButton.Text = "Save";
        PopupCentered();
    }

    private void OnSubmit()
    {
        if (_state == ModalState.New) OnCreate();
        else OnSave();
    }

    private void OnCreate()
    {
        var title = _titleInput.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            SetErrorMessage("Session title is required.");
            return;
        }

        var existing = _databaseService.Sessions.GetAll(_campaignId);
        var session = new Session
        {
            CampaignId = _campaignId,
            Number     = existing.Count + 1,
            Title      = title,
            PlayedOn   = _dateLabel.Text.Trim(),
        };

        int newId = _databaseService.Sessions.Add(session);
        EmitSignal(SignalName.SessionCreated, newId);
        Hide();
        ResetForm();
    }

    private void OnSave()
    {
        var title = _titleInput.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            SetErrorMessage("Session title is required.");
            return;
        }

        _editingSession.Title    = title;
        _editingSession.PlayedOn = _dateLabel.Text.Trim();

        _databaseService.Sessions.Edit(_editingSession);
        EmitSignal(SignalName.SessionEdited, _editingSession.Id);
        Hide();
        ResetForm();
    }

    private void OnCancel()
    {
        Hide();
        ResetForm();
    }

    private void ResetForm()
    {
        _titleInput.Text    = "";
        _dateLabel.Text     = DateTime.Now.ToString("yyyy-MM-dd");
        _errorLabel.Visible = false;
    }

    private void SetErrorMessage(string message)
    {
        _errorLabel.Text    = message;
        _errorLabel.Visible = true;
    }
}