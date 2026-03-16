using DndBuilder.Core.Models;
using Godot;

public partial class AddLocationModal : Window
{
    private DatabaseService _databaseService;
    private int _campaignId;

    [Signal] public delegate void LocationCreatedEventHandler(int newId);
    [Signal] public delegate void LocationEditedEventHandler(int id);

    [Export] private LineEdit _nameInput;
    [Export] private LineEdit _typeInput;
    [Export] private LineEdit _mapRefInput;
    [Export] private TextEdit _descInput;
    [Export] private Label _errorLabel;

    [Export] private Button _cancelButton;
    [Export] private Button _createButton;

    private enum ModalState { New, Edit }
    private ModalState _state;
    private Location _editingLocation;

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
        _editingLocation = null;
        ResetForm();
        _createButton.Text = "Create";
        PopupCentered();
    }

    public void OpenForEdit(Location location)
    {
        _campaignId = location.CampaignId;
        _state = ModalState.Edit;
        _editingLocation = location;
        ResetForm();

        _nameInput.Text   = location.Name;
        _typeInput.Text   = location.Type;
        _mapRefInput.Text = location.MapRef;
        _descInput.Text   = location.Description;

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
            SetErrorMessage("Location name is required.");
            return;
        }

        var location = new Location
        {
            CampaignId  = _campaignId,
            Name        = name,
            Type        = _typeInput.Text.Trim(),
            MapRef      = _mapRefInput.Text.Trim(),
            Description = _descInput.Text.Trim(),
        };

        int newId = _databaseService.Locations.Add(location);
        EmitSignal(SignalName.LocationCreated, newId);
        Hide();
        ResetForm();
    }

    private void OnSave()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("Location name is required.");
            return;
        }

        _editingLocation.Name        = name;
        _editingLocation.Type        = _typeInput.Text.Trim();
        _editingLocation.MapRef      = _mapRefInput.Text.Trim();
        _editingLocation.Description = _descInput.Text.Trim();

        _databaseService.Locations.Edit(_editingLocation);
        EmitSignal(SignalName.LocationEdited, _editingLocation.Id);
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
        _mapRefInput.Text = "";
        _descInput.Text   = "";
        _errorLabel.Visible = false;
    }

    private void SetErrorMessage(string message)
    {
        _errorLabel.Text    = message;
        _errorLabel.Visible = true;
    }
}