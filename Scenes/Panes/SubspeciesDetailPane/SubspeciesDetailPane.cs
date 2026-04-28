using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class SubspeciesDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Subspecies         _subspecies;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit         _nameInput;
    [Export] private HBoxContainer    _parentRow;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes        _notes;
    [Export] private VBoxContainer    _abilitiesContainer;
    [Export] private TypesDropdown _addAbilityButton;
    [Export] private Button           _deleteButton;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "subspecies", _subspecies?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Subspecies" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Subspecies"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _descInput.TextChanged  += () => Save();
        _notes.TextChanged      += () => Save();
        _notes.NavigateTo       += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _confirmDialog = DialogHelper.Make("Delete Subspecies");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "subspecies", _subspecies?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_subspecies?.Name}\"? This cannot be undone.");
    }

    public void Load(Subspecies subspecies)
    {
        _subspecies = subspecies;
        _nameInput.Text = subspecies.Name;
        _descInput.Text = subspecies.Description;
        _notes.Text = subspecies.Notes;

        // Parent species row
        var parentSpecies = _db.Species.Get(subspecies.SpeciesId);
        if (parentSpecies != null)
        {
            int spId = parentSpecies.Id;
            var row = new EntityRow { Text = parentSpecies.Name, ShowDelete = false };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "species", spId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "species", spId);
            _parentRow.AddChild(row);
        }

        _imageCarousel.Setup(EntityType.Subspecies, subspecies.Id, _db);
        LoadAbilities();
        SetupAddAbilityButton();
    }

    private void Save()
    {
        if (_subspecies == null) return;
        _subspecies.Name        = _nameInput.Text;
        _subspecies.Description = _descInput.Text;
        _subspecies.Notes       = _notes.Text;
        _db.Subspecies.Edit(_subspecies);
    }

    private void LoadAbilities()
    {
        foreach (Node child in _abilitiesContainer.GetChildren())
            if (child != _addAbilityButton) child.QueueFree();

        var abilityIds = _db.Abilities.GetAbilityIdsForSubspecies(_subspecies.Id);
        foreach (int abilId in abilityIds)
        {
            var ability = _db.Abilities.Get(abilId);
            if (ability == null) continue;
            int capId = abilId;

            var row = new EntityRow { Text = ability.Name };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", capId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", capId);
            row.DeletePressed         += () =>
            {
                _db.Abilities.RemoveSubspeciesAbility(_subspecies.Id, capId);
                LoadAbilities();
                SetupAddAbilityButton();
            };
            _abilitiesContainer.AddChild(row);
        }
    }

    private void SetupAddAbilityButton()
    {
        if (_subspecies == null) return;
        var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForSubspecies(_subspecies.Id));
        _addAbilityButton.NoneText = "(Add ability...)";
        _addAbilityButton.Setup(
            () => _db.Abilities.GetAll(_subspecies.CampaignId)
                    .FindAll(a => !linkedIds.Contains(a.Id))
                    .ConvertAll(a => (a.Id, a.Name)),
            null, null);
        _addAbilityButton.SelectById(null);
        _addAbilityButton.TypeSelected -= OnAddAbility;
        _addAbilityButton.TypeSelected += OnAddAbility;
    }

    private void OnAddAbility(int id)
    {
        if (id < 0) return;
        _db.Abilities.AddSubspeciesAbility(_subspecies.Id, id);
        LoadAbilities();
        SetupAddAbilityButton();
    }
}
