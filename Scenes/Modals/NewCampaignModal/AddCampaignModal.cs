using System;
using DndBuilder.Core.Models;
using Godot;

public partial class AddCampaignModal : Window
{
    DatabaseService _databaseService;
    [Signal] public delegate void CampaignCreatedEventHandler(int newId);
    [Signal] public delegate void CampaignEditedEventHandler(int id);

    [Export] private LineEdit _nameInput;
    [Export] private OptionButton _systemInput;
    [Export] private TextEdit _descInput;
    [Export] private Label _dateLabel;
    [Export] private Label _errorLabel;

    [Export] private Button _cancelButton;
    [Export] private Button _createButton;

    private static readonly string[] SystemIds = { "dnd5e_2024", "pathfinder2e" };

    private enum ModalState { New, Edit }
    private ModalState _state;
    private Campaign _editingCampaign;

    public override void _Ready()
    {
        _databaseService = GetNode<DatabaseService>("/root/DatabaseService");

        _systemInput.AddItem("D&D 5.5e (2024)");
        _systemInput.AddItem("Pathfinder 2e");

        CloseRequested += OnCancel;
        _cancelButton.Pressed += OnCancel;
        _createButton.Pressed += OnSubmit;

        _errorLabel.Visible = false;
    }

    public void OpenForNew()
    {
        _state = ModalState.New;
        _editingCampaign = null;
        ResetForm();
        _createButton.Text = "Create";
        PopupCentered();
    }

    public void OpenForEdit(Campaign campaign)
    {
        _state = ModalState.Edit;
        _editingCampaign = campaign;
        ResetForm();

        _nameInput.Text = campaign.Name;
        _descInput.Text = campaign.Description;
        _dateLabel.Text = campaign.DateStarted;
        _systemInput.Select(Array.IndexOf(SystemIds, campaign.System));

        _createButton.Text = "Save";
        PopupCentered();
    }

    private void OnSubmit()
    {
        if (_state == ModalState.New)
            OnCreate();
        else
            OnSave();
    }

    private void OnCreate()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("Campaign name is required.");
            return;
        }

        var campaign = new Campaign
        {
            Name = name,
            System = SystemIds[_systemInput.Selected],
            Description = _descInput.Text.Trim(),
            DateStarted = _dateLabel.Text.Trim(),
        };

        int newId = _databaseService.Campaigns.Add(campaign);
        _databaseService.Species                   .SeedDefaults(newId);
        _databaseService.Subspecies                .SeedDefaults(newId);
        _databaseService.Classes                   .SeedDefaults(newId);
        _databaseService.AbilityTypes              .SeedDefaults(newId);
        _databaseService.AbilityResourceTypes      .SeedDefaults(newId);
        _databaseService.Abilities                 .SeedDefaults(newId);
        _databaseService.LocationFactionRoles      .SeedDefaults(newId);
        _databaseService.NpcRelationshipTypes      .SeedDefaults(newId);
        _databaseService.NpcStatuses               .SeedDefaults(newId);
        _databaseService.NpcFactionRoles           .SeedDefaults(newId);
        _databaseService.FactionRelationshipTypes  .SeedDefaults(newId);
        _databaseService.CharacterRelationshipTypes.SeedDefaults(newId);
        _databaseService.ItemTypes                 .SeedDefaults(newId);
        _databaseService.QuestStatuses             .SeedDefaults(newId);
        EmitSignal(SignalName.CampaignCreated, newId);
        Hide();
        ResetForm();
    }

    private void OnSave()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetErrorMessage("Campaign name is required.");
            return;
        }

        _editingCampaign.Name = name;
        _editingCampaign.System = SystemIds[_systemInput.Selected];
        _editingCampaign.Description = _descInput.Text.Trim();
        _editingCampaign.DateStarted = _dateLabel.Text.Trim();

        _databaseService.Campaigns.Edit(_editingCampaign);
        EmitSignal(SignalName.CampaignEdited, _editingCampaign.Id);
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
        _nameInput.Text = "";
        _descInput.Text = "";
        _dateLabel.Text = DateTime.Now.ToString("yyyy-MM-dd");
        _systemInput.Select(0);
        _errorLabel.Visible = false;
    }

    private void SetErrorMessage(string message)
    {
        _errorLabel.Text    = message;
        _errorLabel.Visible = true;
    }
}