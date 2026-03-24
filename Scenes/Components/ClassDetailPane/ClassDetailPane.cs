using System;
using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class ClassDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Class              _class;
    private ConfirmationDialog _confirmDialog;
    private bool               _loading;

    private static readonly int[]    _hitDieValues          = { 6, 8, 10, 12 };
    private static readonly string[] _spellcastingAbilities = { "", "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    private static readonly string[] _savingThrowOptions =
    {
        "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma",
    };

    private static readonly string[] _skillOptions =
    {
        "Acrobatics",     "Animal Handling", "Arcana",      "Athletics",
        "Deception",      "History",         "Insight",     "Intimidation",
        "Investigation",  "Medicine",        "Nature",      "Perception",
        "Performance",    "Persuasion",      "Religion",    "Sleight of Hand",
        "Stealth",        "Survival",
    };

    private readonly List<CheckBox> _primaryAbilityChecks = new();
    private readonly List<CheckBox> _savingThrowChecks    = new();
    private readonly List<CheckBox> _skillChecks          = new();

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void SubclassAddedEventHandler(int classId, int subclassId);

    // ── Fields ────────────────────────────────────────────────────────────────
    [Export] private LineEdit         _nameInput;
    [Export] private Button           _deleteButton;
    [Export] private Button           _descToggle;
    [Export] private Control          _descInset;
    [Export] private TextEdit         _descInput;
    [Export] private Button           _coreStatsToggle;
    [Export] private Control          _coreStatsInset;
    [Export] private OptionButton     _hitDieInput;
    [Export] private VBoxContainer    _primaryAbilityContainer;
    [Export] private OptionButton     _spellcastingAbilityInput;
    [Export] private CheckBox         _isRitualCasterInput;
    [Export] private CheckBox         _isPreparedCasterInput;
    [Export] private Button           _proficienciesToggle;
    [Export] private Control          _proficienciesInset;
    [Export] private VBoxContainer    _savingThrowsContainer;
    [Export] private LineEdit         _armorProfsInput;
    [Export] private LineEdit         _weaponProfsInput;
    [Export] private LineEdit         _toolProfsInput;
    [Export] private SpinBox          _skillChoicesCountInput;
    [Export] private VBoxContainer    _skillOptionsContainer;
    [Export] private Button           _startingEquipToggle;
    [Export] private Control          _startingEquipInset;
    [Export] private TextEdit         _startingEquipAInput;
    [Export] private LineEdit         _startingEquipBInput;
    [Export] private Button           _levelsToggle;
    [Export] private Control          _levelsInset;
    [Export] private Button           _initLevelsButton;
    [Export] private VBoxContainer    _levelsContainer;
    [Export] private Button           _subclassesToggle;
    [Export] private Control          _subclassesInset;
    [Export] private SpinBox          _unlockLevelInput;
    [Export] private VBoxContainer    _subclassesContainer;
    [Export] private Button           _addSubclassButton;
    [Export] private Button           _abilitiesToggle;
    [Export] private Control          _abilitiesInset;
    [Export] private VBoxContainer    _abilitiesContainer;
    [Export] private TypeOptionButton _addAbilityButton;
    [Export] private Button           _notesToggle;
    [Export] private Control          _notesInset;
    [Export] private WikiNotes        _notes;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        // Section toggles
        WireSectionToggle(_descToggle,          _descInset);
        WireSectionToggle(_coreStatsToggle,     _coreStatsInset);
        WireSectionToggle(_proficienciesToggle, _proficienciesInset);
        WireSectionToggle(_startingEquipToggle, _startingEquipInset);
        WireSectionToggle(_levelsToggle,        _levelsInset);
        WireSectionToggle(_subclassesToggle,    _subclassesInset);
        WireSectionToggle(_abilitiesToggle,     _abilitiesInset);
        WireSectionToggle(_notesToggle,         _notesInset);

        // Name
        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "class", _class?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Class" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Class"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);

        // Description
        _descInput.TextChanged += () => Save();

        // Core stats — hit die
        foreach (int d in _hitDieValues)
            _hitDieInput.AddItem($"d{d}");
        _hitDieInput.ItemSelected += _ => Save();

        // Core stats — primary ability checkboxes
        BuildCheckboxRows(_primaryAbilityContainer, _savingThrowOptions, _primaryAbilityChecks, Save, 3);

        // Core stats — spellcasting
        _spellcastingAbilityInput.AddItem("None");
        foreach (var ab in _spellcastingAbilities)
            if (!string.IsNullOrEmpty(ab)) _spellcastingAbilityInput.AddItem(ab);
        _spellcastingAbilityInput.ItemSelected += _ => { UpdateSpellcastingOptions(); Save(); };
        _isRitualCasterInput.Toggled           += _ => Save();
        _isPreparedCasterInput.Toggled         += _ => Save();

        // Proficiencies — saving throw checkboxes (3 per row, evenly spaced)
        BuildCheckboxRows(_savingThrowsContainer, _savingThrowOptions, _savingThrowChecks, Save, 3);

        _armorProfsInput.TextChanged          += _ => Save();
        _weaponProfsInput.TextChanged         += _ => Save();
        _toolProfsInput.TextChanged           += _ => Save();
        _skillChoicesCountInput.MinValue       = 1;
        _skillChoicesCountInput.MaxValue       = 6;
        _skillChoicesCountInput.ValueChanged  += _ => Save();

        // Skills checkboxes (3 per row, evenly spaced)
        BuildCheckboxRows(_skillOptionsContainer, _skillOptions, _skillChecks, Save, 3);

        // Starting equipment
        _startingEquipAInput.TextChanged += () => Save();
        _startingEquipBInput.TextChanged += _ => Save();

        // Level progression
        _initLevelsButton.Pressed += () =>
        {
            if (_class == null) return;
            _db.Classes.InitializeLevels(_class.Id);
            LoadLevelProgression();
        };

        // Subclasses
        _unlockLevelInput.MinValue     = 1;
        _unlockLevelInput.MaxValue     = 20;
        _unlockLevelInput.ValueChanged += _ => Save();
        _addSubclassButton.Pressed     += AddSubclass;

        // Notes
        _notes.TextChanged += () => Save();
        _notes.NavigateTo  += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        // Delete
        _confirmDialog = DialogHelper.Make("Delete Class");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "class", _class?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_class?.Name}\"? All subclasses will also be deleted. This cannot be undone.");
    }

    public void Load(Class cls)
    {
        _loading = true;
        _class   = cls;

        _nameInput.Text = cls.Name;
        _descInput.Text = cls.Description;
        _notes.Text     = cls.Notes;
        _imageCarousel.Setup(EntityType.Class, cls.Id, _db);

        // Core stats
        int hitDieIdx = Array.IndexOf(_hitDieValues, cls.HitDie);
        _hitDieInput.Selected = hitDieIdx >= 0 ? hitDieIdx : 1;

        var selectedPrimary = new HashSet<string>(
            cls.PrimaryAbility.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var cb in _primaryAbilityChecks)
            cb.ButtonPressed = selectedPrimary.Contains(cb.Text);

        int spellIdx = Array.IndexOf(_spellcastingAbilities, cls.SpellcastingAbility);
        _spellcastingAbilityInput.Selected   = spellIdx >= 0 ? spellIdx : 0;
        _isRitualCasterInput.ButtonPressed   = cls.IsRitualCaster;
        _isPreparedCasterInput.ButtonPressed = cls.IsPreparedCaster;
        UpdateSpellcastingOptions();

        // Proficiencies
        var selectedThrows = new HashSet<string>(
            cls.SavingThrowProfs.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var cb in _savingThrowChecks)
            cb.ButtonPressed = selectedThrows.Contains(cb.Text);

        _armorProfsInput.Text         = cls.ArmorProfs;
        _weaponProfsInput.Text        = cls.WeaponProfs;
        _toolProfsInput.Text          = cls.ToolProfs;
        _skillChoicesCountInput.Value = cls.SkillChoicesCount;

        var selectedSkills = new HashSet<string>(
            cls.SkillChoicesOptions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var cb in _skillChecks)
            cb.ButtonPressed = selectedSkills.Contains(cb.Text);

        // Starting equipment
        _startingEquipAInput.Text = cls.StartingEquipA;
        _startingEquipBInput.Text = cls.StartingEquipB;

        // Subclass unlock level
        _unlockLevelInput.Value = cls.SubclassUnlockLevel;

        _loading = false;

        LoadLevelProgression();
        LoadSubclasses();
        LoadAbilities();
        SetupAddAbilityButton();
    }

    private void Save()
    {
        if (_class == null || _loading) return;
        _class.Name               = _nameInput.Text;
        _class.Description        = _descInput.Text;
        _class.Notes              = _notes.Text;
        _class.SubclassUnlockLevel = (int)_unlockLevelInput.Value;

        _class.HitDie              = _hitDieValues[_hitDieInput.Selected];
        _class.PrimaryAbility      = string.Join(", ", _primaryAbilityChecks.Where(cb => cb.ButtonPressed).Select(cb => cb.Text));
        _class.SpellcastingAbility = _spellcastingAbilities[_spellcastingAbilityInput.Selected];
        _class.IsRitualCaster      = _isRitualCasterInput.ButtonPressed;
        _class.IsPreparedCaster    = _isPreparedCasterInput.ButtonPressed;

        _class.SavingThrowProfs    = string.Join(", ", _savingThrowChecks.Where(cb => cb.ButtonPressed).Select(cb => cb.Text));
        _class.ArmorProfs          = _armorProfsInput.Text;
        _class.WeaponProfs         = _weaponProfsInput.Text;
        _class.ToolProfs           = _toolProfsInput.Text;
        _class.SkillChoicesCount   = (int)_skillChoicesCountInput.Value;
        _class.SkillChoicesOptions = string.Join(", ", _skillChecks.Where(cb => cb.ButtonPressed).Select(cb => cb.Text));

        _class.StartingEquipA = _startingEquipAInput.Text;
        _class.StartingEquipB = _startingEquipBInput.Text;

        _db.Classes.Edit(_class);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateSpellcastingOptions()
    {
        bool hasSpell = _spellcastingAbilityInput.Selected != 0;
        _isRitualCasterInput.Visible  = hasSpell;
        _isPreparedCasterInput.Visible = hasSpell;
    }

    private static void WireSectionToggle(Button toggle, Control content, bool startCollapsed = false)
    {
        content.Visible = !startCollapsed;
        string label    = toggle.Text;
        toggle.Text     = (startCollapsed ? "▶  " : "▼  ") + label;
        toggle.Pressed += () =>
        {
            content.Visible = !content.Visible;
            toggle.Text     = (content.Visible ? "▼  " : "▶  ") + label;
        };
    }

    private static void BuildCheckboxRows(VBoxContainer container, string[] options, List<CheckBox> checks, Action onChange, int columns)
    {
        HBoxContainer row = null;
        for (int i = 0; i < options.Length; i++)
        {
            if (i % columns == 0)
            {
                row = new HBoxContainer();
                container.AddChild(row);
            }
            var cb = new CheckBox
            {
                Text                = options[i],
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            cb.Toggled += _ => onChange();
            checks.Add(cb);
            row!.AddChild(cb);
        }
        // Pad the last row so columns stay even-width
        if (row != null)
        {
            int rem = options.Length % columns;
            if (rem > 0)
            {
                for (int i = rem; i < columns; i++)
                {
                    var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    row.AddChild(spacer);
                }
            }
        }
    }

    // ── Level Progression ─────────────────────────────────────────────────────

    private void LoadLevelProgression()
    {
        foreach (Node child in _levelsContainer.GetChildren())
            child.QueueFree();

        var levels = _db.Classes.GetLevelsForClass(_class.Id);
        _initLevelsButton.Visible = levels.Count == 0;

        foreach (var lvl in levels)
            _levelsContainer.AddChild(BuildLevelRow(lvl));
    }

    private Control BuildLevelRow(ClassLevel lvl)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var content = new VBoxContainer { Visible = false };
        content.AddThemeConstantOverride("separation", 4);

        var headerRow = new HBoxContainer();
        var header = new Button
        {
            Text                    = "▶  " + FormatLevelHeader(lvl),
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
        };

        // Ability rows container (above the add button)
        var abilityRows = new VBoxContainer();
        abilityRows.AddThemeConstantOverride("separation", 2);

        var addAbilityBtn = new TypeOptionButton();

        // uses map: abilityId → uses string ("--" or a number). Stored in class_data as "id:uses,..."
        var usesMap = new Dictionary<int, string>();
        foreach (var seg in lvl.ClassData.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = seg.Split(':', 2);
            if (p.Length == 2 && int.TryParse(p[0].Trim(), out int uid) && uid > 0)
                usesMap[uid] = p[1].Trim();
        }

        void SaveUsesMap()
        {
            lvl.ClassData = usesMap.Count > 0
                ? string.Join(",", usesMap.Select(kv => $"{kv.Key}:{kv.Value}"))
                : "";
            _db.Classes.SaveLevel(lvl);
        }

        void Refresh()
        {
            foreach (Node child in abilityRows.GetChildren()) child.QueueFree();

            var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForLevel(lvl.Id));

            // Remove stale uses entries for unlinked abilities
            foreach (var key in usesMap.Keys.Except(linkedIds).ToList()) usesMap.Remove(key);

            foreach (int abilId in linkedIds)
            {
                var ability = _db.Abilities.Get(abilId);
                if (ability == null) continue;
                int capId = abilId;

                if (!usesMap.ContainsKey(capId)) usesMap[capId] = "--";

                var row      = new HBoxContainer();
                var nameBtn  = new Button
                {
                    Text                    = ability.Name,
                    Flat                    = true,
                    Alignment               = HorizontalAlignment.Left,
                    SizeFlagsHorizontal     = SizeFlags.ExpandFill,
                    MouseDefaultCursorShape = CursorShape.PointingHand,
                };
                nameBtn.Pressed += () => EmitSignal(SignalName.NavigateTo, "ability", capId);

                int usesInt = usesMap[capId] == "--"
                    ? -1
                    : (int.TryParse(usesMap[capId], out int uv) ? uv : -1);
                var usesBox = new SpinBox { MinValue = -1, MaxValue = 999, Step = 1, Value = usesInt };
                usesBox.CustomMinimumSize = new Vector2(80, 0);
                if (usesInt < 0) Callable.From(() => usesBox.GetLineEdit().Text = "--").CallDeferred();
                usesBox.ValueChanged += val =>
                {
                    usesMap[capId] = val < 0 ? "--" : ((int)val).ToString();
                    SaveUsesMap();
                    if (val < 0) Callable.From(() => usesBox.GetLineEdit().Text = "--").CallDeferred();
                };
                usesBox.GetLineEdit().FocusExited += () =>
                {
                    if (usesBox.Value < 0)
                        Callable.From(() => usesBox.GetLineEdit().Text = "--").CallDeferred();
                };

                var delBtn = new Button { Text = "×", Flat = true };
                delBtn.Pressed += () =>
                {
                    _db.Abilities.RemoveLevelAbility(lvl.Id, capId);
                    usesMap.Remove(capId);
                    SaveUsesMap();
                    Refresh();
                };

                row.AddChild(nameBtn);
                row.AddChild(usesBox);
                row.AddChild(delBtn);
                abilityRows.AddChild(row);
            }

            addAbilityBtn.NoneText = "(Add ability...)";
            addAbilityBtn.Setup(
                () => _db.Abilities.GetAll(_class.CampaignId)
                        .FindAll(a => !linkedIds.Contains(a.Id))
                        .ConvertAll(a => (a.Id, a.Name)),
                null, null);
            addAbilityBtn.SelectById(null);

            header.Text = (content.Visible ? "▼  " : "▶  ") + FormatLevelHeader(lvl);
        }

        addAbilityBtn.TypeSelected += id =>
        {
            if (id < 0) return;
            _db.Abilities.AddLevelAbility(lvl.Id, id);
            usesMap[id] = "--";
            SaveUsesMap();
            Refresh();
        };

        var profLabel = new Label { Text = "Prof" };
        var profBox   = new SpinBox { MinValue = 1, MaxValue = 10, Step = 1, Value = lvl.ProfBonus };
        profBox.ValueChanged += val => { lvl.ProfBonus = (int)val; _db.Classes.SaveLevel(lvl); };
        headerRow.AddChild(header);
        headerRow.AddChild(profLabel);
        headerRow.AddChild(profBox);

        Refresh();

        content.AddChild(abilityRows);
        content.AddChild(addAbilityBtn);

        header.Pressed += () =>
        {
            content.Visible = !content.Visible;
            header.Text     = (content.Visible ? "▼  " : "▶  ") + FormatLevelHeader(lvl);
        };

        box.AddChild(headerRow);
        box.AddChild(content);
        return box;
    }

    private static string FormatLevelHeader(ClassLevel lvl) => $"Level {lvl.Level,2}";

    // ── Subclasses ────────────────────────────────────────────────────────────

    private void LoadSubclasses()
    {
        foreach (Node child in _subclassesContainer.GetChildren())
            if (child != _addSubclassButton) child.QueueFree();

        foreach (var sub in _db.Classes.GetSubclassesForClass(_class.Id))
        {
            int subId = sub.Id;
            var row = new EntityRow
            {
                Text            = sub.Name,
                Description     = sub.Description,
                ShowDescription = true,
            };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "subclass", subId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "subclass", subId);
            row.DeletePressed         += () =>
            {
                _db.Classes.DeleteSubclass(subId);
                LoadSubclasses();
            };
            _subclassesContainer.AddChild(row);
        }
    }

    private void AddSubclass()
    {
        if (_class == null) return;
        var sub = new Subclass { CampaignId = _class.CampaignId, ClassId = _class.Id, Name = "New Subclass" };
        int newId = _db.Classes.AddSubclass(sub);
        LoadSubclasses();
        EmitSignal(SignalName.SubclassAdded, _class.Id, newId);
        EmitSignal(SignalName.NavigateTo, "subclass", newId);
    }

    // ── Abilities ─────────────────────────────────────────────────────────────

    private void LoadAbilities()
    {
        foreach (Node child in _abilitiesContainer.GetChildren())
            if (child != _addAbilityButton) child.QueueFree();

        foreach (int abilId in _db.Abilities.GetAbilityIdsForClass(_class.Id))
        {
            var ability = _db.Abilities.Get(abilId);
            if (ability == null) continue;
            int capId = abilId;

            var row = new EntityRow { Text = ability.Name };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "ability", capId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", capId);
            row.DeletePressed         += () =>
            {
                _db.Abilities.RemoveClassAbility(_class.Id, capId);
                LoadAbilities();
                SetupAddAbilityButton();
            };
            _abilitiesContainer.AddChild(row);
        }
    }

    private void SetupAddAbilityButton()
    {
        if (_class == null) return;
        var linkedIds = new HashSet<int>(_db.Abilities.GetAbilityIdsForClass(_class.Id));
        _addAbilityButton.NoneText = "(Add ability...)";
        _addAbilityButton.Setup(
            () => _db.Abilities.GetAll(_class.CampaignId)
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
        _db.Abilities.AddClassAbility(_class.Id, id);
        LoadAbilities();
        SetupAddAbilityButton();
    }
}
