using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class AbilityDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Ability            _ability;
    private ConfirmationDialog _confirmDialog;

    private static readonly string[] _actionTypes    = { "—", "Action", "Bonus Action", "Reaction", "No Action", "Passive", "Free" };
    private static readonly string[] _poolTypes      = { "None", "Fixed List", "Weapon", "Skill", "Custom" };
    private static readonly string[] _recoveryIntervals = { "—", "Short Rest", "Long Rest" };

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit          _nameInput;
    [Export] private TypesDropdown  _typeInput;
    [Export] private OptionButton  _actionInput;
    [Export] private LineEdit      _triggerInput;
    [Export] private OptionButton  _recoveryIntervalInput;
    [Export] private SpinBox       _recoveryAmountInput;
    [Export] private VBoxContainer _costsContainer;
    [Export] private Button        _addCostButton;
    [Export] private TextEdit      _effectInput;
    [Export] private WikiNotes     _notes;
    [Export] private Button        _deleteButton;
    [Export] private ImageCarousel _imageCarousel;

    [Export] private OptionButton  _poolTypeInput;
    [Export] private Control       _pickCountModeRow;
    [Export] private OptionButton  _pickCountModeInput;
    [Export] private Control       _formulaRow;
    [Export] private IntInput      _choiceCountBaseInput;
    [Export] private OptionButton  _choiceCountAttrInput;
    [Export] private CheckBox      _choiceCountProfInput;
    [Export] private OptionButton  _choiceCountLevelInput;

    private static readonly string[] _attrOptions  = { "(none)", "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    private static readonly string[] _attrValues   = { "",       "str", "dex", "con", "int", "wis", "cha" };
    private static readonly string[] _levelOptions = { "(none)", "+ Level", "+ ½ Level ↓", "+ ½ Level ↑" };
    private static readonly string[] _levelValues  = { "",       "full",   "half_down",    "half_up"     };
    [Export] private Control       _fixedOptionsSection;
    [Export] private VBoxContainer _optionsContainer;
    [Export] private Control       _choiceProgressionSection;
    [Export] private VBoxContainer _choiceProgressionContainer;
    [Export] private Button        _addChoiceProgressionButton;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "ability", _ability?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Ability" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Ability"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _typeInput.TypeSelected   += id => { if (_ability != null) { _ability.TypeId = id < 0 ? null : (int?)id; _db.Abilities.Edit(_ability); } };
        _triggerInput.TextChanged += _ => Save();
        foreach (var ri in _recoveryIntervals) _recoveryIntervalInput.AddItem(ri);
        _recoveryIntervalInput.ItemSelected += _ => { RefreshRecoveryAmountVisibility(); Save(); };
        _recoveryAmountInput.MinValue = 0;
        _recoveryAmountInput.MaxValue = 99;
        _recoveryAmountInput.Step     = 1;
        _recoveryAmountInput.ValueChanged += _ => { Save(); ShowMaxIfZero(); };
        _recoveryAmountInput.GetLineEdit().FocusExited += () =>
        {
            if (_recoveryAmountInput.GetLineEdit().Text.Trim().ToLower() == "max")
                _recoveryAmountInput.Value = 0;
        };
        _addCostButton.Pressed += AddCost;
        _effectInput.TextChanged += () => Save();
        _notes.TextChanged += () => Save();
        _notes.NavigateTo  += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        foreach (var at in _actionTypes) _actionInput.AddItem(at);
        _actionInput.ItemSelected      += _ => Save();

        foreach (var pt in _poolTypes) _poolTypeInput.AddItem(pt);
        _poolTypeInput.ItemSelected      += _ => { UpdateChoiceVisibility(); Save(); };
        _pickCountModeInput.AddItem("Formula");
        _pickCountModeInput.AddItem("Level Progression");
        _pickCountModeInput.ItemSelected += _ => { UpdateChoiceVisibility(); Save(); };
        _choiceCountBaseInput.ValueChanged += _ => Save();
        _choiceCountProfInput.Toggled      += _ => Save();
        foreach (var opt in _attrOptions)  _choiceCountAttrInput.AddItem(opt);
        foreach (var opt in _levelOptions) _choiceCountLevelInput.AddItem(opt);
        _choiceCountAttrInput.ItemSelected  += _ => Save();
        _choiceCountLevelInput.ItemSelected += _ => Save();
        _addChoiceProgressionButton.Pressed += AddChoiceProgression;

        _confirmDialog = DialogHelper.Make("Delete Ability");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "ability", _ability?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_ability?.Name}\"? This cannot be undone.");
    }

    public void Load(Ability ability)
    {
        _ability = ability;

        _nameInput.Text    = ability.Name;
        _typeInput.Setup(
            () => _db.AbilityTypes.GetAll(ability.CampaignId).ConvertAll(t => (t.Id, t.Name)),
            name => _db.AbilityTypes.Add(new AbilityType { CampaignId = ability.CampaignId, Name = name }),
            id   => _db.AbilityTypes.Delete(id));
        _typeInput.SelectById(ability.TypeId);
        _triggerInput.Text = ability.Trigger;
        _effectInput.Text  = ability.Effect;

        int recoveryIdx = Array.IndexOf(_recoveryIntervals, RecoveryIntervalToLabel(ability.RecoveryInterval));
        _recoveryIntervalInput.Selected = recoveryIdx >= 0 ? recoveryIdx : 0;
        _recoveryAmountInput.Value = ability.RecoveryAmount;
        RefreshRecoveryAmountVisibility();
        ShowMaxIfZero();
        _notes.Text         = ability.Notes;
        _imageCarousel.Setup(EntityType.Ability, ability.Id, _db);

        LoadCosts();

        // Action dropdown
        string actionLabel = string.IsNullOrEmpty(ability.Action) ? "—" : ability.Action;
        int actionIdx = Array.IndexOf(_actionTypes, actionLabel);
        _actionInput.Selected = actionIdx >= 0 ? actionIdx : 0;

        int poolIdx = Array.IndexOf(_poolTypes, PoolTypeToLabel(ability.ChoicePoolType));
        _poolTypeInput.Selected      = poolIdx >= 0 ? poolIdx : 0;
        _pickCountModeInput.Selected = ability.PickCountMode == "progression" ? 1 : 0;
        // Migrate from legacy MaxChoices if ChoiceCountBase has never been set
        _choiceCountBaseInput.Value = ability.ChoiceCountBase > 0 ? ability.ChoiceCountBase : ability.MaxChoices;
        _choiceCountAttrInput.Selected  = Array.IndexOf(_attrValues,  ability.ChoiceCountAttribute)  is int ai  && ai  >= 0 ? ai  : 0;
        _choiceCountLevelInput.Selected = Array.IndexOf(_levelValues, ability.ChoiceCountAddLevel)   is int li  && li  >= 0 ? li  : 0;
        _choiceCountProfInput.ButtonPressed = ability.ChoiceCountAddProf;
        UpdateChoiceVisibility();
        LoadOptions();
        LoadChoiceProgression();
    }

    private void Save()
    {
        if (_ability == null) return;
        _ability.Name             = _nameInput.Text;
        _ability.Action           = _actionTypes[_actionInput.Selected] == "—" ? "" : _actionTypes[_actionInput.Selected];
        _ability.Trigger          = _triggerInput.Text;
        _ability.RecoveryInterval = LabelToRecoveryInterval(_recoveryIntervals[_recoveryIntervalInput.Selected]);
        _ability.RecoveryAmount   = (int)_recoveryAmountInput.Value;
        _ability.Effect           = _effectInput.Text;
        _ability.Notes          = _notes.Text;
        _ability.ChoicePoolType        = LabelToPoolType(_poolTypes[_poolTypeInput.Selected]);
        _ability.PickCountMode         = _pickCountModeInput.Selected == 1 ? "progression" : "formula";
        _ability.ChoiceCountBase       = _choiceCountBaseInput.Value;
        _ability.ChoiceCountAttribute  = _attrValues[_choiceCountAttrInput.Selected];
        _ability.ChoiceCountAddProf    = _choiceCountProfInput.ButtonPressed;
        _ability.ChoiceCountAddLevel   = _levelValues[_choiceCountLevelInput.Selected];
        _db.Abilities.Edit(_ability);
    }

    private void UpdateChoiceVisibility()
    {
        bool hasChoices    = _poolTypeInput.Selected != 0;
        bool isFixed       = _poolTypes[_poolTypeInput.Selected] == "Fixed List";
        bool isProgression = _pickCountModeInput.Selected == 1;
        _pickCountModeRow.Visible         = hasChoices;
        _formulaRow.Visible               = hasChoices && !isProgression;
        _fixedOptionsSection.Visible      = isFixed;
        _choiceProgressionSection.Visible = hasChoices && isProgression;
    }

    private void LoadOptions()
    {
        foreach (Node child in _optionsContainer.GetChildren()) child.QueueFree();
        if (_ability == null) return;

        var choices   = _db.Abilities.GetChoicesForAbility(_ability.Id);
        var linkedIds = new HashSet<int>();
        foreach (var c in choices)
            if (c.LinkedAbilityId.HasValue) linkedIds.Add(c.LinkedAbilityId.Value);

        foreach (var choice in choices)
        {
            var cap          = choice;
            var linkedAbility = cap.LinkedAbilityId.HasValue ? _db.Abilities.Get(cap.LinkedAbilityId.Value) : null;
            string desc      = linkedAbility?.Effect ?? cap.Description;

            var row = new EntityRow
            {
                Text            = cap.Name,
                Description     = desc,
                ShowDescription = !string.IsNullOrEmpty(desc),
                ShowDelete      = true,
            };

            if (cap.LinkedAbilityId.HasValue)
            {
                int linkedId = cap.LinkedAbilityId.Value;
                row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", linkedId);
                row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", linkedId);
            }
            row.DeletePressed += () => { _db.Abilities.DeleteChoice(cap.Id); LoadOptions(); };

            _optionsContainer.AddChild(row);
        }

        var addBtn = new TypesDropdown { NoneText = "(Add option...)" };
        _optionsContainer.AddChild(addBtn);
        addBtn.Setup(
            () => _db.Abilities.GetAll(_ability.CampaignId)
                    .FindAll(a => a.Id != _ability.Id && !linkedIds.Contains(a.Id))
                    .ConvertAll(a => (a.Id, a.Name)),
            null, null);
        addBtn.SelectById(null);

        addBtn.TypeSelected += id =>
        {
            if (id < 0) return;
            var selected = _db.Abilities.Get(id);
            if (selected == null) return;
            _db.Abilities.AddChoice(new AbilityChoice
            {
                AbilityId       = _ability.Id,
                Name            = selected.Name,
                Description     = selected.Effect,
                LinkedAbilityId = selected.Id,
                SortOrder       = choices.Count,
            });
            LoadOptions();
        };
    }

    private void LoadCosts()
    {
        foreach (Node child in _costsContainer.GetChildren()) child.QueueFree();
        if (_ability == null) return;

        foreach (var cost in _db.Abilities.GetCosts(_ability.Id))
            AddCostRow(cost.ResourceTypeId, cost.Amount, pending: false);
    }

    private void AddCost()
    {
        if (_ability == null) return;
        AddCostRow(savedResourceTypeId: -1, savedAmount: 1, pending: true);
    }

    private void AddCostRow(int savedResourceTypeId, int savedAmount, bool pending)
    {
        var resourceTypeId = savedResourceTypeId;
        var amount         = savedAmount;

        var row        = new HBoxContainer();
        var typePicker = new TypesDropdown { SizeFlagsHorizontal = SizeFlags.ExpandFill, NoneText = "(pick resource)", AutoSelectOnAdd = true };
        var amountInput = IntInput.Make(amount, 1, 99, val => {
            amount = val;
            if (!pending && resourceTypeId != -1)
                _db.Abilities.UpdateCostAmount(_ability.Id, resourceTypeId, amount);
        });
        amountInput.CustomMinimumSize = new Vector2(72, 0);
        var delBtn     = new Button { Text = "×", Flat = true };

        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(typePicker);
        row.AddChild(amountInput);
        row.AddChild(delBtn);
        _costsContainer.AddChild(row);

        // Setup AFTER node is in the scene tree so _Ready() has run
        typePicker.Setup(
            () => _db.AbilityResourceTypes.GetAll(_ability.CampaignId).ConvertAll(t => (t.Id, t.Name)),
            name => _db.AbilityResourceTypes.Add(new AbilityResourceType { CampaignId = _ability.CampaignId, Name = name }),
            id   => _db.AbilityResourceTypes.Delete(id));

        if (!pending && resourceTypeId != -1)
            typePicker.SelectById(resourceTypeId);

        typePicker.TypeSelected += newId =>
        {
            if (!pending && resourceTypeId != -1)
                _db.Abilities.RemoveCost(_ability.Id, resourceTypeId);

            if (newId != -1)
            {
                resourceTypeId = newId;
                _db.Abilities.AddCost(new AbilityCost { AbilityId = _ability.Id, ResourceTypeId = resourceTypeId, Amount = amount });
                pending = false;
            }
            else
            {
                resourceTypeId = -1;
                pending = true;
            }
        };

        delBtn.Pressed += () =>
        {
            if (!pending && resourceTypeId != -1)
                _db.Abilities.RemoveCost(_ability.Id, resourceTypeId);
            row.QueueFree();
        };
    }

    private void LoadChoiceProgression()
    {
        foreach (Node child in _choiceProgressionContainer.GetChildren()) child.QueueFree();
        if (_ability == null) return;

        var progression = _db.Abilities.GetChoiceProgressionForAbility(_ability.Id);
        foreach (var step in progression)
        {
            var cap = step;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var levelLabel = new Label { Text = "Level", CustomMinimumSize = new Vector2(42, 0) };
            var levelInput = IntInput.Make(cap.RequiredLevel, 1, 20, val => { cap.RequiredLevel = val; _db.Abilities.EditChoiceProgression(cap); });
            levelInput.CustomMinimumSize = new Vector2(80, 0);
            var countLabel = new Label { Text = "Picks", CustomMinimumSize = new Vector2(42, 0) };
            var countInput = IntInput.Make(cap.ChoiceCount, 0, 20, val => { cap.ChoiceCount = val; _db.Abilities.EditChoiceProgression(cap); });
            countInput.CustomMinimumSize = new Vector2(80, 0);
            var delBtn = new Button { Text = "×", Flat = true };
            delBtn.Pressed += () =>
            {
                _db.Abilities.DeleteChoiceProgression(cap.Id);
                LoadChoiceProgression();
            };

            row.AddChild(levelLabel);
            row.AddChild(levelInput);
            row.AddChild(countLabel);
            row.AddChild(countInput);
            row.AddChild(delBtn);
            _choiceProgressionContainer.AddChild(row);
        }
    }

    private void AddChoiceProgression()
    {
        if (_ability == null) return;
        _db.Abilities.AddChoiceProgression(new AbilityChoiceProgression
        {
            AbilityId = _ability.Id,
            RequiredLevel = 1,
            ChoiceCount = _ability.MaxChoices,
        });
        LoadChoiceProgression();
    }

    private void RefreshRecoveryAmountVisibility()
    {
        _recoveryAmountInput.Visible = _recoveryIntervalInput.Selected > 0;
    }

    private void ShowMaxIfZero()
    {
        if ((int)_recoveryAmountInput.Value == 0)
            _recoveryAmountInput.GetLineEdit().CallDeferred(LineEdit.MethodName.SetText, "Max");
    }

    private static string RecoveryIntervalToLabel(string raw) => raw switch
    {
        "ShortRest" => "Short Rest",
        "LongRest"  => "Long Rest",
        _           => "—",
    };

    private static string LabelToRecoveryInterval(string label) => label switch
    {
        "Short Rest" => "ShortRest",
        "Long Rest"  => "LongRest",
        _            => "",
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string PoolTypeToLabel(string raw) => raw switch
    {
        "fixed"  => "Fixed List",
        "weapon" => "Weapon",
        "skill"  => "Skill",
        "custom" => "Custom",
        _        => "None",
    };

    private static string LabelToPoolType(string label) => label switch
    {
        "Fixed List" => "fixed",
        "Weapon"     => "weapon",
        "Skill"      => "skill",
        "Custom"     => "custom",
        _            => "",
    };
}
