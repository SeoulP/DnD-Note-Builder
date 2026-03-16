using DndBuilder.Core.Models;
using Godot;

public partial class AddFactionModal : Window
{
    private DatabaseService _databaseService;
    private int _campaignId;

    [Signal] public delegate void FactionCreatedEventHandler(int newId);
    [Signal] public delegate void FactionEditedEventHandler(int id);

    [Export] private LineEdit _nameInput;
    [Export] private LineEdit _typeInput;
    [Export] private TextEdit _descInput;
    [Export] private TextEdit _goalsInput;
    [Export] private Label _errorLabel;

    [Export] private Button _cancelButton;
    [Export] private Button _createButton;

    private enum ModalState { New, Edit }
    private ModalState _state;
    private Faction _editingFaction;

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
        _editingFaction = null;
        ResetForm();
        _createButton.Text = "Create";
        PopupCentered();
    }

    public void OpenForEdit(Faction faction)
    {
        _campaignId = faction.CampaignId;
        _state = ModalState.Edit;
        _editingFaction = faction;
        ResetForm();

        _nameInput.Text = faction.Name;
        _typeInput.Text = faction.Type;
        _descInput.Text = faction.Description;
        _goalsInput.Text = faction.Goals;

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
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("Faction name is required.");
            return;
        }

        var faction = new Faction
        {
            CampaignId  = _campaignId,
            Name        = name,
            Type        = _typeInput.Text.Trim(),
            Description = _descInput.Text.Trim(),
            Goals       = _goalsInput.Text.Trim(),
        };

        int newId = _databaseService.Factions.Add(faction);
        EmitSignal(SignalName.FactionCreated, newId);
        Hide();
        ResetForm();
    }

    private void OnSave()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("Faction name is required.");
            return;
        }

        _editingFaction.Name        = name;
        _editingFaction.Type        = _typeInput.Text.Trim();
        _editingFaction.Description = _descInput.Text.Trim();
        _editingFaction.Goals       = _goalsInput.Text.Trim();

        _databaseService.Factions.Edit(_editingFaction);
        EmitSignal(SignalName.FactionEdited, _editingFaction.Id);
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
        _nameInput.Text   = "";
        _typeInput.Text   = "";
        _descInput.Text   = "";
        _goalsInput.Text  = "";
        _errorLabel.Visible = false;
    }

    private void SetErrorMessage(string message)
    {
        _errorLabel.Text    = message;
        _errorLabel.Visible = true;
    }
}