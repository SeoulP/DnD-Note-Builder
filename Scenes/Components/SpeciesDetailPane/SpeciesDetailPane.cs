using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class SpeciesDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Species            _species;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void SubspeciesAddedEventHandler(int speciesId, int subspeciesId);

    [Export] private LineEdit         _nameInput;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes        _notes;
    [Export] private VBoxContainer    _subspeciesContainer;
    [Export] private Button           _addSubspeciesButton;
    [Export] private VBoxContainer    _abilitiesContainer;
    [Export] private TypeOptionButton _addAbilityButton;
    [Export] private Button           _deleteButton;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "species", _species?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Species" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Species"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _descInput.TextChanged  += () => Save();
        _notes.TextChanged      += () => Save();
        _notes.NavigateTo       += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _addSubspeciesButton.Pressed += AddSubspecies;

        _confirmDialog = DialogHelper.Make("Delete Species");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "species", _species?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_species?.Name}\"? All subspecies will also be deleted. This cannot be undone.");
    }

    public void Load(Species species)
    {
        _species = species;
        _nameInput.Text = species.Name;
        _descInput.Text = species.Description;
        _notes.Text = species.Notes;
        _imageCarousel.Setup(EntityType.Species, species.Id, _db);
        LoadSubspecies();
        LoadAbilities();
        SetupAddAbilityButton();
    }

    private void Save()
    {
        if (_species == null) return;
        _species.Name        = _nameInput.Text;
        _species.Description = _descInput.Text;
        _species.Notes       = _notes.Text;
        _db.Species.Edit(_species);
    }

    private void LoadSubspecies()
    {
        foreach (Node child in _subspeciesContainer.GetChildren())
            if (child != _addSubspeciesButton) child.QueueFree();

        foreach (var sub in _db.Subspecies.GetAllForSpecies(_species.Id))
        {
            int subId = sub.Id;
            var row = new EntityRow
            {
                Text            = sub.Name,
                Description     = sub.Description,
                ShowDescription = true,
            };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "subspecies", subId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "subspecies", subId);
            row.DeletePressed   += () =>
            {
                _db.Subspecies.Delete(subId);
                LoadSubspecies();
            };
            _subspeciesContainer.AddChild(row);
        }
    }

    private void AddSubspecies()
    {
        if (_species == null) return;
        var sub = new Subspecies { CampaignId = _species.CampaignId, SpeciesId = _species.Id, Name = "New Subspecies" };
        int newId = _db.Subspecies.Add(sub);
        LoadSubspecies();
        EmitSignal(SignalName.SubspeciesAdded, _species.Id, newId);
        EmitSignal(SignalName.NavigateTo, "subspecies", newId);
    }

    private void LoadAbilities()
    {
        foreach (Node child in _abilitiesContainer.GetChildren())
            if (child != _addAbilityButton) child.QueueFree();

        var abilityIds = _db.Abilities.GetAbilityIdsForSpecies(_species.Id);
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
                _db.Abilities.RemoveSpeciesAbility(_species.Id, capId);
                LoadAbilities();
                SetupAddAbilityButton();
            };
            _abilitiesContainer.AddChild(row);
        }
    }

    private void SetupAddAbilityButton()
    {
        if (_species == null) return;
        var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForSpecies(_species.Id));
        _addAbilityButton.NoneText = "(Add ability...)";
        _addAbilityButton.Setup(
            () => _db.Abilities.GetAll(_species.CampaignId)
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
        _db.Abilities.AddSpeciesAbility(_species.Id, id);
        LoadAbilities();
        SetupAddAbilityButton();
    }
}
