using DndBuilder.Core.Models;
using Godot;

public partial class AddNpcModal : Window
{
    private DatabaseService _databaseService;
    private int _campaignId;

    [Signal] public delegate void NpcCreatedEventHandler(int newId);
    [Signal] public delegate void NpcEditedEventHandler(int id);

    [Export] private LineEdit _nameInput;
    [Export] private LineEdit _speciesInput;
    [Export] private LineEdit _occupationInput;
    [Export] private TextEdit _descInput;
    [Export] private Label _errorLabel;

    [Export] private Button _cancelButton;
    [Export] private Button _createButton;

    private enum ModalState { New, Edit }
    private ModalState _state;
    private Npc _editingNpc;

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
        _editingNpc = null;
        ResetForm();
        _createButton.Text = "Create";
        PopupCentered();
    }

    public void OpenForEdit(Npc npc)
    {
        _campaignId = npc.CampaignId;
        _state = ModalState.Edit;
        _editingNpc = npc;
        ResetForm();

        _nameInput.Text = npc.Name;
        _speciesInput.Text = npc.Species;
        _occupationInput.Text = npc.Occupation;
        _descInput.Text = npc.Description;

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
            SetErrorMessage("NPC name is required.");
            return;
        }

        var npc = new Npc
        {
            CampaignId  = _campaignId,
            Name        = name,
            Species     = _speciesInput.Text.Trim(),
            Occupation  = _occupationInput.Text.Trim(),
            Description = _descInput.Text.Trim(),
        };

        int newId = _databaseService.Npcs.Add(npc);
        EmitSignal(SignalName.NpcCreated, newId);
        Hide();
        ResetForm();
    }

    private void OnSave()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("NPC name is required.");
            return;
        }

        _editingNpc.Name        = name;
        _editingNpc.Species     = _speciesInput.Text.Trim();
        _editingNpc.Occupation  = _occupationInput.Text.Trim();
        _editingNpc.Description = _descInput.Text.Trim();

        _databaseService.Npcs.Edit(_editingNpc);
        EmitSignal(SignalName.NpcEdited, _editingNpc.Id);
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
        _nameInput.Text       = "";
        _speciesInput.Text    = "";
        _occupationInput.Text = "";
        _descInput.Text       = "";
        _errorLabel.Visible   = false;
    }

    private void SetErrorMessage(string message)
    {
        _errorLabel.Text    = message;
        _errorLabel.Visible = true;
    }
}