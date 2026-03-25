using DndBuilder.Core.Models;
using Godot;
using System.Collections.Generic;

public partial class PlayerCharacterDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private PlayerCharacter    _pc;
    private ConfirmationDialog _confirmDialog;
    private int                _subclassUnlockLevel  = 3;
    private bool               _loading              = false;
    private HashSet<int>       _openAbilityDropdowns = new();

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private Label         _speciesLabel;
    [Export] private Label         _classLabel;
    [Export] private OptionButton  _speciesInput;
    [Export] private OptionButton  _subspeciesInput;
    [Export] private OptionButton  _classInput;
    [Export] private OptionButton  _subclassInput;
    [Export] private SpinBox       _levelInput;
    [Export] private LineEdit      _strInput;
    [Export] private LineEdit      _dexInput;
    [Export] private LineEdit      _conInput;
    [Export] private LineEdit      _intInput;
    [Export] private LineEdit      _wisInput;
    [Export] private LineEdit      _chaInput;
    [Export] private Label         _strMod;
    [Export] private Label         _dexMod;
    [Export] private Label         _conMod;
    [Export] private Label         _intMod;
    [Export] private Label         _wisMod;
    [Export] private Label         _chaMod;
    [Export] private TextEdit      _descInput;
    [Export] private Control       _abilityChoicesSection;
    [Export] private VBoxContainer _abilityChoicesContainer;
    [Export] private Control       _resourcesSection;
    [Export] private VBoxContainer _resourcesContainer;
    [Export] private WikiNotes     _notes;
    [Export] private ImageCarousel _imageCarousel;
    [Export] private Button        _deleteButton;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "playercharacter", _pc?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Character" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Character"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _speciesInput.ItemSelected    += idx => { Save(); RefreshSubspecies(_speciesInput.GetItemId((int)idx)); LoadAbilityChoices(); LoadResources(); };
        _subspeciesInput.ItemSelected += _ => { Save(); LoadAbilityChoices(); LoadResources(); };
        _classInput.ItemSelected      += idx => { Save(); RefreshSubclass(_classInput.GetItemId((int)idx)); LoadAbilityChoices(); LoadResources(); };
        _subclassInput.ItemSelected   += _ => { Save(); LoadAbilityChoices(); LoadResources(); };
        _levelInput.ValueChanged      += _ => { Save(); RefreshSubclassVisibility(); LoadAbilityChoices(); LoadResources(); };
        _descInput.TextChanged       += () => Save();
        _notes.TextChanged           += () => Save();
        _notes.NavigateTo            += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _levelInput.MinValue = 1;
        _levelInput.MaxValue = 20;

        foreach (var (input, mod) in ScorePairs())
        {
            var capturedInput = input;
            var capturedMod   = mod;
            capturedInput.TextChanged += text =>
            {
                if (int.TryParse(text, out int v) && v >= 1 && v <= 30)
                    capturedMod.Text = ModLabel(v);
            };
            capturedInput.FocusExited += () =>
            {
                int val = ParseScore(capturedInput.Text);
                capturedInput.Text = val.ToString();
                capturedMod.Text   = ModLabel(val);
                Save();
                LoadAbilityChoices();
            };
        }

        _confirmDialog = DialogHelper.Make("Delete Character");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "playercharacter", _pc?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_pc?.Name}\"? This cannot be undone.");
    }

    public void Load(PlayerCharacter pc)
    {
        _loading = true;
        _pc = pc;
        var vocab = SystemVocabulary.For(_db.Campaigns.Get(pc.CampaignId)?.System);
        _speciesLabel.Text = vocab.Species;
        _classLabel.Text   = vocab.Class;
        _nameInput.Text   = string.IsNullOrEmpty(pc.Name) ? "New Character" : pc.Name;
        _levelInput.Value = pc.Level;
        _strInput.Text = pc.Strength.ToString();
        _dexInput.Text = pc.Dexterity.ToString();
        _conInput.Text = pc.Constitution.ToString();
        _intInput.Text = pc.Intelligence.ToString();
        _wisInput.Text = pc.Wisdom.ToString();
        _chaInput.Text = pc.Charisma.ToString();
        UpdateModLabels();
        _descInput.Text   = pc.Description;
        _notes.Setup(pc.CampaignId, _db);
        _notes.Text       = pc.Notes;

        PopulateSpecies();
        PopulateClass();
        _imageCarousel.Setup(EntityType.PlayerCharacter, pc.Id, _db);
        _loading = false;
        LoadAbilityChoices();
        LoadResources();
    }

    private void Save()
    {
        if (_pc == null || _loading) return;
        _pc.Name         = _nameInput.Text;
        _pc.SpeciesId    = GetOptionId(_speciesInput);
        _pc.SubspeciesId = GetOptionId(_subspeciesInput);
        _pc.ClassId      = GetOptionId(_classInput);
        _pc.SubclassId   = GetOptionId(_subclassInput);
        _pc.Level        = (int)_levelInput.Value;
        _pc.Strength     = ParseScore(_strInput.Text);
        _pc.Dexterity    = ParseScore(_dexInput.Text);
        _pc.Constitution = ParseScore(_conInput.Text);
        _pc.Intelligence = ParseScore(_intInput.Text);
        _pc.Wisdom       = ParseScore(_wisInput.Text);
        _pc.Charisma     = ParseScore(_chaInput.Text);
        _pc.Description  = _descInput.Text;
        _pc.Notes        = _notes.Text;
        _db.PlayerCharacters.Edit(_pc);
    }

    private void PopulateSpecies()
    {
        _speciesInput.Clear();
        _speciesInput.AddItem("(None)", -1);
        foreach (var sp in _db.Species.GetAll(_pc.CampaignId))
            _speciesInput.AddItem(sp.Name, sp.Id);
        SelectOptionById(_speciesInput, _pc.SpeciesId);
        RefreshSubspecies();
    }

    private void RefreshSubspecies(int rawSpeciesId = -2)
    {
        int? spId = rawSpeciesId == -2 ? GetOptionId(_speciesInput)
                  : rawSpeciesId == -1 ? null
                  : (int?)rawSpeciesId;
        _subspeciesInput.Clear();
        _subspeciesInput.AddItem("(None)", -1);
        if (spId.HasValue)
            foreach (var sub in _db.Subspecies.GetAllForSpecies(spId.Value))
                _subspeciesInput.AddItem(sub.Name, sub.Id);
        _subspeciesInput.Visible = _subspeciesInput.ItemCount > 1;
        SelectOptionById(_subspeciesInput, spId.HasValue ? _pc?.SubspeciesId : null);
    }

    private void PopulateClass()
    {
        _classInput.Clear();
        _classInput.AddItem("(None)", -1);
        foreach (var cls in _db.Classes.GetAll(_pc.CampaignId))
            _classInput.AddItem(cls.Name, cls.Id);
        SelectOptionById(_classInput, _pc.ClassId);
        RefreshSubclass();
    }

    private void RefreshSubclass(int rawClassId = -2)
    {
        int? clId = rawClassId == -2 ? GetOptionId(_classInput)
                  : rawClassId == -1 ? null
                  : (int?)rawClassId;
        _subclassInput.Clear();
        _subclassInput.AddItem("(None)", -1);
        if (clId.HasValue)
        {
            foreach (var sub in _db.Classes.GetSubclassesForClass(clId.Value))
                _subclassInput.AddItem(sub.Name, sub.Id);
            _subclassUnlockLevel = _db.Classes.Get(clId.Value)?.SubclassUnlockLevel ?? 3;
        }
        SelectOptionById(_subclassInput, clId.HasValue ? _pc?.SubclassId : null);
        RefreshSubclassVisibility();
    }

    private void RefreshSubclassVisibility()
    {
        _subclassInput.Visible = GetOptionId(_classInput).HasValue
                              && (int)_levelInput.Value >= _subclassUnlockLevel;
    }

    private void UpdateModLabels()
    {
        foreach (var (input, mod) in ScorePairs())
            mod.Text = ModLabel(ParseScore(input.Text));
    }

    private IEnumerable<(LineEdit input, Label mod)> ScorePairs() => new[]
    {
        (_strInput, _strMod), (_dexInput, _dexMod), (_conInput, _conMod),
        (_intInput, _intMod), (_wisInput, _wisMod), (_chaInput, _chaMod),
    };

    private static int ParseScore(string text) =>
        System.Math.Clamp(int.TryParse(text, out int v) ? v : 10, 1, 30);

    private static string ModLabel(int score)
    {
        int mod = (int)System.Math.Floor((score - 10) / 2.0);
        return mod >= 0 ? $"(+{mod})" : $"({mod})";
    }

    private void LoadAbilityChoices()
    {
        foreach (Node child in _abilityChoicesContainer.GetChildren())
            child.QueueFree();

        if (_pc == null) { _abilityChoicesSection.Visible = false; return; }

        var abilities = GetAllOwnedAbilities();

        // Current resource amounts for greying out depleted Use buttons
        var resourceAmounts = new Dictionary<int, int>();
        foreach (var res in _db.PlayerCharacters.GetResources(_pc.Id))
            resourceAmounts[res.ResourceTypeId] = res.CurrentAmount;

        // Remove abilities that are just linked options inside a fixed-choice ability —
        // they already appear inside that ability's dropdown, not as top-level rows.
        var choiceLinkedIds = new HashSet<int>();
        foreach (var a in abilities)
            if (a.ChoicePoolType == "fixed")
                foreach (var ch in _db.Abilities.GetChoicesForAbility(a.Id))
                    if (ch.LinkedAbilityId.HasValue)
                        choiceLinkedIds.Add(ch.LinkedAbilityId.Value);

        var filtered = new List<Ability>();
        foreach (var a in abilities)
            if (!choiceLinkedIds.Contains(a.Id))
                filtered.Add(a);
        abilities = filtered;

        _abilityChoicesSection.Visible = abilities.Count > 0;
        if (abilities.Count == 0) return;

        foreach (var ability in abilities)
        {
            var abilityBlock = new VBoxContainer();
            abilityBlock.AddThemeConstantOverride("separation", 2);

            if (ability.ChoicePoolType != "fixed")
            {
                // ── Regular ability: EntityRow + optional Use button ──────────
                var row = new EntityRow { ShowDelete = false, Text = ability.Name };
                int navId = ability.Id;
                row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", navId);
                row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", navId);

                if (ability.Costs.Count > 0)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", -30);
                    row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    hbox.AddChild(row);
                    var capturedCosts = ability.Costs;
                    bool depleted = IsOutOfUses(capturedCosts, resourceAmounts);
                    var useBtn = new Button { Text = "⚡", Flat = true, MouseDefaultCursorShape = depleted ? Control.CursorShape.Forbidden : Control.CursorShape.PointingHand };
                    useBtn.Disabled = depleted;
                    useBtn.Pressed += () => SpendResources(capturedCosts);
                    hbox.AddChild(useBtn);
                    abilityBlock.AddChild(hbox);
                }
                else
                {
                    abilityBlock.AddChild(row);
                }
                _abilityChoicesContainer.AddChild(abilityBlock);
                continue;
            }

            int allowed = _db.Abilities.ResolveChoiceCount(ability, _pc.Level, _pc);
            var selectedChoices = _db.Abilities.GetCharacterAbilityChoices(_pc.Id, ability.Id);
            var selectedIds = new HashSet<int>();
            foreach (var s in selectedChoices) selectedIds.Add(s.ChoiceId);
            var allChoices = _db.Abilities.GetChoicesForAbility(ability.Id);

            // ── Toggle button ─────────────────────────────────────────────────
            string countText = allowed > 0 ? $"{selectedIds.Count}/{allowed} picks"
                             : selectedIds.Count > 0 ? $"{selectedIds.Count} selected"
                             : "0 picks";
            bool isOpen = _openAbilityDropdowns.Contains(ability.Id);
            var toggleBtn = new Button
            {
                Text                = $"{(isOpen ? "▲" : "▼")}  {ability.Name}  [{countText}]",
                ToggleMode          = true,
                ButtonPressed       = isOpen,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment           = HorizontalAlignment.Left,
            };

            // ── Checkbox dropdown panel ───────────────────────────────────────
            var dropdownBg = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.06f) };
            dropdownBg.SetCornerRadiusAll(3);
            dropdownBg.ContentMarginLeft   = 8;
            dropdownBg.ContentMarginRight  = 8;
            dropdownBg.ContentMarginTop    = 6;
            dropdownBg.ContentMarginBottom = 6;

            var dropdownPanel = new PanelContainer { Visible = isOpen };
            dropdownPanel.AddThemeStyleboxOverride("panel", dropdownBg);
            var checkboxVbox = new VBoxContainer();
            checkboxVbox.AddThemeConstantOverride("separation", 4);
            dropdownPanel.AddChild(checkboxVbox);

            foreach (var choice in allChoices)
            {
                var cap         = choice;
                bool isSelected = selectedIds.Contains(cap.Id);
                bool disabled   = allowed > 0 && selectedIds.Count >= allowed && !isSelected;
                var checkbox    = new CheckBox { Text = cap.Name, ButtonPressed = isSelected, Disabled = disabled };
                checkbox.AddThemeColorOverride("font_uncheck_color",          new Color(1,    1,    1,    0.85f));
                checkbox.AddThemeColorOverride("font_uncheck_disabled_color", new Color(0.8f, 0.8f, 0.8f, 0.5f));

                if (cap.LinkedAbilityId.HasValue)
                {
                    int linkedId = cap.LinkedAbilityId.Value;
                    checkbox.MouseEntered += () =>
                        checkbox.MouseDefaultCursorShape = Input.IsKeyPressed(Key.Ctrl)
                            ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
                    checkbox.MouseExited += () =>
                        checkbox.MouseDefaultCursorShape = Control.CursorShape.Arrow;
                    checkbox.GuiInput += e =>
                    {
                        if (e is InputEventMouseMotion mm)
                        {
                            checkbox.MouseDefaultCursorShape = mm.CtrlPressed
                                ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
                        }
                        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && mb.CtrlPressed)
                        {
                            checkbox.AcceptEvent();
                            EmitSignal(SignalName.NavigateTo, "ability", linkedId);
                        }
                    };
                }

                checkbox.Toggled += toggled =>
                {
                    var current = new HashSet<int>();
                    foreach (var x in _db.Abilities.GetCharacterAbilityChoices(_pc.Id, ability.Id))
                        current.Add(x.ChoiceId);

                    if (toggled && allowed > 0 && current.Count >= allowed && !current.Contains(cap.Id))
                    {
                        checkbox.SetPressedNoSignal(false);
                        return;
                    }
                    _db.Abilities.SetCharacterAbilityChoiceSelected(_pc.Id, ability.Id, cap.Id, toggled);
                    if (toggled && allowed > 0 && _db.Abilities.GetCharacterAbilityChoices(_pc.Id, ability.Id).Count >= allowed)
                        _openAbilityDropdowns.Remove(ability.Id);
                    LoadAbilityChoices();
                };
                checkboxVbox.AddChild(checkbox);
            }

            toggleBtn.Toggled += pressed =>
            {
                if (pressed) _openAbilityDropdowns.Add(ability.Id);
                else _openAbilityDropdowns.Remove(ability.Id);
                dropdownPanel.Visible = pressed;
                toggleBtn.Text = $"{(pressed ? "▲" : "▼")}  {ability.Name}  [{countText}]";
            };

            // ── EntityRows for selected choices ───────────────────────────────
            var selectedList = new VBoxContainer();
            selectedList.AddThemeConstantOverride("separation", 1);
            var parentCosts = ability.Costs;
            foreach (var sel in selectedChoices)
            {
                var choice = allChoices.Find(c => c.Id == sel.ChoiceId);
                if (choice == null) continue;
                var row = new EntityRow { ShowDelete = false, Text = choice.Name };
                if (choice.LinkedAbilityId.HasValue)
                {
                    int linkedId = choice.LinkedAbilityId.Value;
                    row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", linkedId);
                    row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", linkedId);
                }
                var margin = new MarginContainer();
                margin.AddThemeConstantOverride("margin_left", 24);

                if (parentCosts.Count > 0)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", -30);
                    row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    hbox.AddChild(row);
                    var capturedCosts = parentCosts;
                    bool depleted = IsOutOfUses(capturedCosts, resourceAmounts);
                    var useBtn = new Button { Text = "⚡", Flat = true, MouseDefaultCursorShape = depleted ? Control.CursorShape.Forbidden : Control.CursorShape.PointingHand };
                    useBtn.Disabled = depleted;
                    useBtn.Pressed += () => SpendResources(capturedCosts);
                    hbox.AddChild(useBtn);
                    margin.AddChild(hbox);
                }
                else
                {
                    margin.AddChild(row);
                }
                selectedList.AddChild(margin);
            }

            abilityBlock.AddChild(toggleBtn);
            abilityBlock.AddChild(dropdownPanel);
            abilityBlock.AddChild(selectedList);
            _abilityChoicesContainer.AddChild(abilityBlock);
        }
    }

    private void LoadResources()
    {
        foreach (Node child in _resourcesContainer.GetChildren())
            child.QueueFree();

        if (_pc == null) { _resourcesSection.Visible = false; return; }

        _db.PlayerCharacters.SyncResources(_pc);
        var resources = _db.PlayerCharacters.GetResourcesWithNames(_pc.Id);

        _resourcesSection.Visible = resources.Count > 0;
        if (resources.Count == 0) return;

        foreach (var (res, name) in resources)
        {
            var capturedRes = res;
            int current     = res.CurrentAmount;
            int max         = res.MaximumAmount;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var nameLabel = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(nameLabel);

            var cbBox = new HBoxContainer();
            cbBox.AddThemeConstantOverride("separation", 2);

            var countLabel = new Label { Text = $"{current}/{max}" };

            var checkboxes = new List<CheckBox>();
            for (int i = 0; i < max; i++)
            {
                // First `current` boxes = available (unchecked); remainder = spent (checked)
                var cb = new CheckBox { ButtonPressed = i >= current };
                checkboxes.Add(cb);
                cbBox.AddChild(cb);
            }

            foreach (var cb in checkboxes)
            {
                cb.Toggled += _ =>
                {
                    int remaining = 0;
                    foreach (var box in checkboxes)
                        if (!box.ButtonPressed) remaining++;

                    countLabel.Text = $"{remaining}/{max}";
                    _db.PlayerCharacters.UpsertResource(new CharacterResource
                    {
                        CharacterId    = capturedRes.CharacterId,
                        ResourceTypeId = capturedRes.ResourceTypeId,
                        CurrentAmount  = remaining,
                        MaximumAmount  = max,
                        ValueText      = capturedRes.ValueText,
                        Notes          = capturedRes.Notes,
                    });
                    LoadAbilityChoices();
                };
            }

            row.AddChild(cbBox);
            row.AddChild(countLabel);
            _resourcesContainer.AddChild(row);
        }
    }

    private static bool IsOutOfUses(List<AbilityCost> costs, Dictionary<int, int> resourceAmounts)
    {
        foreach (var cost in costs)
            if (!resourceAmounts.TryGetValue(cost.ResourceTypeId, out int cur) || cur < cost.Amount)
                return true;
        return false;
    }

    private void SpendResources(List<AbilityCost> costs)
    {
        if (_pc == null) return;
        var resources = _db.PlayerCharacters.GetResources(_pc.Id);
        foreach (var cost in costs)
        {
            CharacterResource match = null;
            foreach (var r in resources)
                if (r.ResourceTypeId == cost.ResourceTypeId) { match = r; break; }
            if (match == null) continue;
            match.CurrentAmount = System.Math.Max(0, match.CurrentAmount - cost.Amount);
            _db.PlayerCharacters.UpsertResource(match);
        }
        LoadResources();
        LoadAbilityChoices();
    }

    private List<Ability> GetAllOwnedAbilities()
    {
        var ids = new HashSet<int>();

        if (_pc.ClassId.HasValue)
            foreach (var level in _db.Classes.GetLevelsForClass(_pc.ClassId.Value))
            {
                if (level.Level > _pc.Level) break;
                foreach (var abilityId in _db.Abilities.GetAbilityIdsForLevel(level.Id))
                    ids.Add(abilityId);
            }

        if (_pc.SubclassId.HasValue)
            foreach (var abilityId in _db.Abilities.GetAbilityIdsForSubclass(_pc.SubclassId.Value, _pc.Level))
                ids.Add(abilityId);

        if (_pc.SpeciesId.HasValue)
            foreach (var abilityId in _db.Abilities.GetAbilityIdsForSpecies(_pc.SpeciesId.Value))
                ids.Add(abilityId);

        if (_pc.SubspeciesId.HasValue)
            foreach (var abilityId in _db.Abilities.GetAbilityIdsForSubspecies(_pc.SubspeciesId.Value))
                ids.Add(abilityId);

        var abilities = new List<Ability>();
        foreach (var id in ids)
        {
            var ability = _db.Abilities.Get(id);
            if (ability != null) abilities.Add(ability);
        }

        abilities.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return abilities;
    }

    private static void SelectOptionById(OptionButton btn, int? id)
    {
        if (!id.HasValue) { btn.Select(0); return; }
        for (int i = 0; i < btn.ItemCount; i++)
            if (btn.GetItemId(i) == id.Value) { btn.Select(i); return; }
        btn.Select(0);
    }

    private static int? GetOptionId(OptionButton btn)
    {
        if (btn.Selected < 0) return null;
        int id = btn.GetItemId(btn.Selected);
        return id > 0 ? (int?)id : null;
    }
}
