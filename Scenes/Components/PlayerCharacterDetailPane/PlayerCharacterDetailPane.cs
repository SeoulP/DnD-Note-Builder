using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerCharacterDetailPane : ScrollContainer
{
    private DatabaseService          _db;
    private PlayerCharacter          _pc;
    private ConfirmationDialog       _confirmDialog;
    private int                      _subclassUnlockLevel   = 3;
    private bool                     _loading               = false;
    private string                   _activeTab             = "Stats";
    private HashSet<int>             _openAbilityDropdowns  = new();
    private HashSet<string>          _closedAbilitySections = new();
    private SkillExpectationService  _skillExpectations;
    private BackgroundPickerModal    _backgroundModal;
    private EffectPreviewPopup       _effectPreview;

    private static readonly string[] _abilityActionSectionOrder =
    {
        "Action",
        "Bonus Action",
        "Reaction",
        "Free",
        "No Action",
        "Passive",
        "Unspecified",
    };

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
    [Export] private Label         _backgroundLabel;
    [Export] private Button        _backgroundButton;
    [Export] private Control       _skillsSection;
    [Export] private HBoxContainer _skillsChipsContainer;
    [Export] private VBoxContainer _skillsListContainer;
    [Export] private Control       _abilityChoicesSection;
    [Export] private VBoxContainer _abilityChoicesContainer;
    [Export] private Control       _resourcesSection;
    [Export] private VBoxContainer _resourcesContainer;
    [Export] private WikiNotes     _notes;
    [Export] private ImageCarousel _imageCarousel;
    [Export] private Button        _deleteButton;
    [Export] private VBoxContainer    _aliasChipsRow;
    [Export] private Button           _addManualAbilityBtn;
    [Export] private Button        _statsTabBtn;
    [Export] private Button        _actionsTabBtn;
    [Export] private Button        _inventoryTabBtn;
    [Export] private Button        _flavorTabBtn;
    [Export] private Control       _statsTab;
    [Export] private Control       _actionsTab;
    [Export] private Control       _inventoryTab;
    [Export] private Control       _flavorTab;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");
        _skillExpectations = new SkillExpectationService(_db.Classes, _db.Abilities, _db.DnD5eBackgrounds);
        _backgroundModal   = GetNode<BackgroundPickerModal>("BackgroundPickerModal");
        _backgroundModal.Confirmed   += OnBackgroundSelected;
        _backgroundModal.NavigateTo  += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        // CanvasLayer renders above the ScrollContainer without being clipped by it.
        // EffectPreviewPopup uses MouseFilter=Ignore so it never steals scroll events.
        _effectPreview = new EffectPreviewPopup();
        var effectLayer = new CanvasLayer { Layer = 100 };
        effectLayer.AddChild(_effectPreview);
        AddChild(effectLayer);

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "playercharacter", _pc?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Character" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Character"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _backgroundButton.Pressed     += () => _backgroundModal.Open(_pc.CampaignId, _pc.BackgroundId, _pc.BackgroundAsi ?? "");
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
                {
                    capturedMod.Text = ModLabel(v);
                    Save();
                    LoadSkills();
                }
            };
            capturedInput.FocusExited += () =>
            {
                int val = ParseScore(capturedInput.Text);
                capturedInput.Text = val.ToString();
                capturedMod.Text   = ModLabel(val);
                Save();
                LoadAbilityChoices();
                LoadSkills();
            };
        }

        _confirmDialog = DialogHelper.Make("Delete Character");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "playercharacter", _pc?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_pc?.Name}\"? This cannot be undone.");

        _statsTabBtn.Pressed     += () => SetActiveTab("Stats");
        _actionsTabBtn.Pressed   += () => SetActiveTab("Actions");
        _inventoryTabBtn.Pressed += () => SetActiveTab("Inventory");
        _flavorTabBtn.Pressed    += () => SetActiveTab("Flavor");
        _addManualAbilityBtn.Pressed += ShowAbilityPicker;
    }

    private void SetActiveTab(string name)
    {
        _activeTab = name;
        _statsTab.Visible     = name == "Stats";
        _actionsTab.Visible   = name == "Actions";
        _inventoryTab.Visible = name == "Inventory";
        _flavorTab.Visible    = name == "Flavor";
        _statsTabBtn.SetPressedNoSignal(name == "Stats");
        _actionsTabBtn.SetPressedNoSignal(name == "Actions");
        _inventoryTabBtn.SetPressedNoSignal(name == "Inventory");
        _flavorTabBtn.SetPressedNoSignal(name == "Flavor");
    }

    public void Load(PlayerCharacter pc)
    {
        _loading = true;
        _pc = pc;
        _closedAbilitySections.Clear();
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
        LoadBackground();
        LoadAliases();
        LoadSkills();
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
        // BackgroundId is set directly on _pc by OnBackgroundSelected before Save() is called
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

    // ── Background ────────────────────────────────────────────────────────────

    private void OnBackgroundSelected(int? backgroundId, string asi)
    {
        if (_pc == null) return;
        SyncBackgroundAsi(_pc.BackgroundAsi, asi);
        _pc.BackgroundId  = backgroundId;
        _pc.BackgroundAsi = asi;
        // Update score inputs so Save() reads the ASI-adjusted values
        _loading = true;
        _strInput.Text = _pc.Strength.ToString();
        _dexInput.Text = _pc.Dexterity.ToString();
        _conInput.Text = _pc.Constitution.ToString();
        _intInput.Text = _pc.Intelligence.ToString();
        _wisInput.Text = _pc.Wisdom.ToString();
        _chaInput.Text = _pc.Charisma.ToString();
        _loading = false;
        UpdateModLabels();
        Save();
        LoadBackground();
        SyncBackgroundFeat(backgroundId);
        LoadSkills();
        LoadAbilityChoices();
    }

    private void SyncBackgroundAsi(string oldAsi, string newAsi)
    {
        foreach (var kv in ParseAsi(oldAsi))
            ApplyAsiDelta(kv.Key, -kv.Value);
        foreach (var kv in ParseAsi(newAsi))
            ApplyAsiDelta(kv.Key, kv.Value);
    }

    private void ApplyAsiDelta(string abbrev, int delta)
    {
        switch (abbrev.ToLowerInvariant())
        {
            case "str": _pc.Strength     = Math.Clamp(_pc.Strength     + delta, 1, 30); break;
            case "dex": _pc.Dexterity    = Math.Clamp(_pc.Dexterity    + delta, 1, 30); break;
            case "con": _pc.Constitution = Math.Clamp(_pc.Constitution + delta, 1, 30); break;
            case "int": _pc.Intelligence = Math.Clamp(_pc.Intelligence + delta, 1, 30); break;
            case "wis": _pc.Wisdom       = Math.Clamp(_pc.Wisdom       + delta, 1, 30); break;
            case "cha": _pc.Charisma     = Math.Clamp(_pc.Charisma     + delta, 1, 30); break;
        }
    }

    private static Dictionary<string, int> ParseAsi(string asi)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(asi)) return result;
        foreach (var part in asi.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && int.TryParse(kv[1], out int val))
                result[kv[0].Trim()] = val;
        }
        return result;
    }

    private void SyncBackgroundFeat(int? backgroundId)
    {
        _db.PlayerCharacters.RemoveBackgroundAbilities(_pc.Id);
        if (!backgroundId.HasValue) return;
        var bg = _db.DnD5eBackgrounds.Get(backgroundId.Value);
        if (bg?.FeatAbilityId == null) return;
        _db.PlayerCharacters.AddBackgroundAbility(_pc.Id, bg.FeatAbilityId.Value);
    }

    private void LoadAliases()
    {
        if (_pc == null || _aliasChipsRow == null) return;
        foreach (Node child in _aliasChipsRow.GetChildren())
            child.QueueFree();

        // ── chips row (above the input) ───────────────────────────────────────
        var chipsRow = new HBoxContainer();
        chipsRow.AddThemeConstantOverride("separation", 4);
        _aliasChipsRow.AddChild(chipsRow);

        var aliases = _db.EntityAliases.GetForEntity("playercharacter", _pc.Id);
        foreach (var alias in aliases)
        {
            int    capturedId = alias.Id;

            var normalStyle = new StyleBoxFlat { BgColor = new Color(0.18f, 0.18f, 0.18f) };
            normalStyle.SetCornerRadiusAll(4);
            normalStyle.ContentMarginLeft   = 6;
            normalStyle.ContentMarginRight  = 4;
            normalStyle.ContentMarginTop    = 2;
            normalStyle.ContentMarginBottom = 2;

            var hoverStyle = new StyleBoxFlat { BgColor = new Color(0.45f, 0.10f, 0.10f) };
            hoverStyle.SetCornerRadiusAll(4);
            hoverStyle.ContentMarginLeft   = 6;
            hoverStyle.ContentMarginRight  = 4;
            hoverStyle.ContentMarginTop    = 2;
            hoverStyle.ContentMarginBottom = 2;

            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", normalStyle);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);

            var label = new Label { Text = alias.Alias };
            label.AddThemeFontSizeOverride("font_size", 11);

            var removeBtn = new Button
            {
                Text                     = "×",
                Flat                     = true,
                MouseDefaultCursorShape  = CursorShape.PointingHand,
            };
            removeBtn.AddThemeFontSizeOverride("font_size", 11);
            removeBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", hoverStyle);
            removeBtn.MouseExited  += () => chip.AddThemeStyleboxOverride("panel", normalStyle);
            removeBtn.Pressed      += () => { _db.EntityAliases.Delete(capturedId); LoadAliases(); };

            row.AddChild(label);
            row.AddChild(removeBtn);
            chip.AddChild(row);
            chipsRow.AddChild(chip);
        }

        // ── add input (below the chips) ───────────────────────────────────────
        var addInput = new LineEdit
        {
            PlaceholderText     = "+ alias",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(80, 0),
        };
        addInput.TextSubmitted += text =>
        {
            string trimmed = text.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;
            addInput.Text = "";
            _db.EntityAliases.Add(new DndBuilder.Core.Models.EntityAlias
            {
                CampaignId = _pc.CampaignId,
                EntityType = "playercharacter",
                EntityId   = _pc.Id,
                Alias      = trimmed,
            });
            LoadAliases();
        };
        _aliasChipsRow.AddChild(addInput);
    }

    private void LoadBackground()
    {
        if (_pc == null) return;
        if (_pc.BackgroundId.HasValue)
        {
            var bg = _db.DnD5eBackgrounds.Get(_pc.BackgroundId.Value);
            _backgroundLabel.Text  = bg?.Name ?? "(None)";
            _backgroundButton.Text = "Change";
        }
        else
        {
            _backgroundLabel.Text  = "(None)";
            _backgroundButton.Text = "Choose";
        }
        SyncBackgroundSkills(_pc.BackgroundId);
    }

    private void SyncBackgroundSkills(int? backgroundId)
    {
        // Remove any existing background-sourced skills
        var current = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id);
        foreach (var cs in current)
            if (cs.Source == "background")
                _db.DnD5eCharacterSkills.Delete(_pc.Id, cs.SkillId);

        if (!backgroundId.HasValue) return;

        var bg = _db.DnD5eBackgrounds.Get(backgroundId.Value);
        if (bg == null || string.IsNullOrEmpty(bg.SkillNames)) return;

        var allSkills    = _db.DnD5eSkills.GetAll(_pc.CampaignId);
        var skillsByName = new Dictionary<string, DnD5eSkill>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var s in allSkills) skillsByName[s.Name] = s;

        foreach (var rawName in bg.SkillNames.Split(','))
        {
            var name = rawName.Trim();
            if (skillsByName.TryGetValue(name, out var skill))
                _db.DnD5eCharacterSkills.Upsert(new DnD5eCharacterSkill
                {
                    PlayerCharacterId = _pc.Id,
                    SkillId           = skill.Id,
                    Source            = "background",
                    IsExpertise       = false,
                });
        }
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    private void LoadSkills()
    {
        if (_pc == null) return;

        var allSkills      = _db.DnD5eSkills.GetAll(_pc.CampaignId);
        var characterSkills = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id);
        var skillMap       = new Dictionary<int, DnD5eCharacterSkill>();
        foreach (var cs in characterSkills)
            skillMap[cs.SkillId] = cs;

        var expectations   = _skillExpectations.GetExpectations(_pc);

        // Rebuild chips
        foreach (Node child in _skillsChipsContainer.GetChildren())
            child.QueueFree();
        BuildSourceChips(expectations, skillMap);

        // Rebuild skill rows
        foreach (Node child in _skillsListContainer.GetChildren())
            child.QueueFree();

        int profBonus = CalcProfBonus(_pc.Level);

        string bgName = _pc.BackgroundId.HasValue
            ? _db.DnD5eBackgrounds.Get(_pc.BackgroundId.Value)?.Name ?? "Background"
            : "Background";

        foreach (var skill in allSkills)
        {
            skillMap.TryGetValue(skill.Id, out var cs);
            bool isProficient = cs != null;
            bool isExpertise  = cs?.IsExpertise ?? false;
            int  bonus        = CalcSkillBonus(skill.Attribute, _pc, profBonus, isProficient, isExpertise);

            var row = BuildSkillRow(skill, cs, bonus, profBonus, skillMap, expectations, allSkills, bgName);
            _skillsListContainer.AddChild(row);
        }
    }

    private HBoxContainer BuildSkillRow(
        DnD5eSkill skill,
        DnD5eCharacterSkill cs,
        int bonus,
        int profBonus,
        Dictionary<int, DnD5eCharacterSkill> skillMap,
        List<SkillExpectation> expectations,
        List<DnD5eSkill> allSkills,
        string bgName = "Background")
    {
        bool isProficient = cs != null;
        bool isExpertise  = cs?.IsExpertise ?? false;
        bool isLocked     = cs?.Source == "background";

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        // Proficiency checkbox — locked for background skills
        var profCb = new CheckBox
        {
            ButtonPressed = isProficient,
            Disabled      = isLocked,
            TooltipText   = isLocked ? $"Granted by {bgName}" : "",
            MouseFilter   = Control.MouseFilterEnum.Stop,
        };
        profCb.AddThemeConstantOverride("icon_max_width", 14);

        // Expertise checkbox — disabled when not proficient or skill is locked
        var expCb = new CheckBox { ButtonPressed = isExpertise, Disabled = !isProficient || isLocked };
        expCb.AddThemeConstantOverride("icon_max_width", 14);

        // Skill name + attribute label
        var nameLabel = new Label
        {
            Text                = $"{skill.Name} ({skill.Attribute.ToUpper()})",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        // Bonus label
        var bonusLabel = new Label
        {
            Text                    = BonusText(bonus),
            CustomMinimumSize       = new Vector2(32, 0),
            HorizontalAlignment     = HorizontalAlignment.Right,
        };

        // Wire expertise → depends on proficiency
        int capturedSkillId = skill.Id;
        expCb.Toggled += toggled =>
        {
            if (_pc == null) return;
            var existing = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id).Find(x => x.SkillId == capturedSkillId);
            if (existing == null) return;
            existing.IsExpertise = toggled;
            _db.DnD5eCharacterSkills.Upsert(existing);
            LoadSkills();
        };

        profCb.Toggled += toggled =>
        {
            if (_pc == null) return;
            if (toggled)
            {
                var source = InferSource(capturedSkillId, skillMap, expectations, allSkills);
                _db.DnD5eCharacterSkills.Upsert(new DnD5eCharacterSkill
                {
                    PlayerCharacterId = _pc.Id,
                    SkillId           = capturedSkillId,
                    Source            = source,
                    IsExpertise       = false,
                });
            }
            else
            {
                _db.DnD5eCharacterSkills.Delete(_pc.Id, capturedSkillId);
            }
            LoadSkills();
        };

        row.AddChild(profCb);
        row.AddChild(expCb);
        row.AddChild(nameLabel);
        row.AddChild(bonusLabel);
        return row;
    }

    private void BuildSourceChips(List<SkillExpectation> expectations, Dictionary<int, DnD5eCharacterSkill> skillMap)
    {
        // Count actual selections per source — background is auto-applied and never shown as a chip
        var actualCounts = new Dictionary<string, int> { ["class"] = 0, ["feat"] = 0 };
        foreach (var cs in skillMap.Values)
        {
            if (actualCounts.ContainsKey(cs.Source))
                actualCounts[cs.Source]++;
        }

        // Merge expectations with same source, skipping background
        var classTotals = new Dictionary<string, int>();
        foreach (var exp in expectations)
        {
            if (exp.Source == "background") continue;
            if (!classTotals.ContainsKey(exp.Source)) classTotals[exp.Source] = 0;
            classTotals[exp.Source] += exp.ExpectedCount;
        }

        foreach (var exp in expectations)
        {
            if (exp.Source == "background") continue;
            int expected = classTotals.TryGetValue(exp.Source, out int t) ? t : exp.ExpectedCount;
            actualCounts.TryGetValue(exp.Source, out int actual);
            int diff = expected - actual;  // positive = under, 0 = met, negative = over

            string icon, tooltip;
            Color? color = null;

            if (diff > 0)
            {
                icon    = "⚠";
                tooltip = $"Select {diff} more";
            }
            else if (diff < 0)
            {
                icon    = "!";
                tooltip = $"{-diff} over limit";
                color   = new Color(0.9f, 0.25f, 0.25f);
            }
            else
            {
                icon    = "✓";
                tooltip = "";
            }

            var chip = new Label
            {
                Text        = $"{icon} {exp.SourceName} ({actual}/{expected})",
                TooltipText = tooltip,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            if (color.HasValue)
                chip.AddThemeColorOverride("font_color", color.Value);

            _skillsChipsContainer.AddChild(chip);
            classTotals.Remove(exp.Source);
        }
    }

    private string InferSource(int skillId, Dictionary<int, DnD5eCharacterSkill> skillMap,
        List<SkillExpectation> expectations, List<DnD5eSkill> allSkills)
    {
        var counts   = new Dictionary<string, int> { ["class"] = 0, ["feat"] = 0 };
        var expected = new Dictionary<string, int> { ["class"] = 0, ["feat"] = 0 };
        foreach (var cs in skillMap.Values)
            if (counts.ContainsKey(cs.Source)) counts[cs.Source]++;
        foreach (var exp in expectations)
            if (exp.Source != "background" && expected.ContainsKey(exp.Source))
                expected[exp.Source] += exp.ExpectedCount;

        // Fill any under-budget source first
        foreach (var source in new[] { "class", "feat" })
            if (expected[source] > 0 && counts[source] < expected[source]) return source;

        // All slots filled — overflow goes to the first source that has a budget
        foreach (var source in new[] { "class", "feat" })
            if (expected[source] > 0) return source;

        return "custom";
    }

    // ── Skill bonus math ──────────────────────────────────────────────────────

    private static int CalcProfBonus(int level) => 2 + (level - 1) / 4;

    private static int CalcSkillBonus(string attr, PlayerCharacter pc, int profBonus, bool isProficient, bool isExpertise)
    {
        int score = attr switch
        {
            "str" => pc.Strength,
            "dex" => pc.Dexterity,
            "con" => pc.Constitution,
            "int" => pc.Intelligence,
            "wis" => pc.Wisdom,
            "cha" => pc.Charisma,
            _     => 10,
        };
        int attrMod = (int)System.Math.Floor((score - 10) / 2.0);
        int profMod = isExpertise ? 2 * profBonus : isProficient ? profBonus : 0;
        return attrMod + profMod;
    }

    private static string BonusText(int bonus) => bonus >= 0 ? $"+{bonus}" : $"{bonus}";

    // ── Ability choices ───────────────────────────────────────────────────────

    private void LoadAbilityChoices()
    {
        _effectPreview?.Hide();

        foreach (Node child in _abilityChoicesContainer.GetChildren())
            child.QueueFree();

        if (_pc == null) { _abilityChoicesSection.Visible = false; return; }

        var abilities  = GetAllOwnedAbilities();
        var manualIds  = new HashSet<int>(_db.PlayerCharacters.GetManualAbilityIds(_pc.Id));

        // Current resource amounts for greying out depleted Use buttons, and names for tooltips
        var resourceAmounts = new Dictionary<int, int>();
        var resourceNames   = new Dictionary<int, string>();
        foreach (var (res, name) in _db.PlayerCharacters.GetResourcesWithNames(_pc.Id))
        {
            resourceAmounts[res.ResourceTypeId] = res.CurrentAmount;
            resourceNames[res.ResourceTypeId]   = name;
        }

        // Remove abilities that are choice-linked options inside a fixed-choice parent —
        // they must not appear as duplicate top-level rows.
        var choiceLinkedIds = new HashSet<int>();
        var choicesCache    = new Dictionary<int, List<AbilityChoice>>();
        foreach (var a in abilities)
        {
            if (a.ChoicePoolType != "fixed") continue;
            var choices = _db.Abilities.GetChoicesForAbility(a.Id);
            choicesCache[a.Id] = choices;
            foreach (var ch in choices)
                if (ch.LinkedAbilityId.HasValue)
                    choiceLinkedIds.Add(ch.LinkedAbilityId.Value);
        }

        var filtered = new List<Ability>();
        foreach (var a in abilities)
            if (!choiceLinkedIds.Contains(a.Id))
                filtered.Add(a);

        // Re-add only the *chosen* linked sub-abilities at the top level so they
        // appear in their own action-type section (e.g. Riposte → Bonus Action).
        var seenLinkedIds = new HashSet<int>();
        foreach (var a in abilities)
        {
            if (a.ChoicePoolType != "fixed") continue;
            if (!choicesCache.TryGetValue(a.Id, out var allChoices)) continue;
            foreach (var sel in _db.Abilities.GetCharacterAbilityChoices(_pc.Id, a.Id))
            {
                var choice = allChoices.Find(c => c.Id == sel.ChoiceId);
                if (choice?.LinkedAbilityId == null) continue;
                int linkedId = choice.LinkedAbilityId.Value;
                if (!seenLinkedIds.Add(linkedId)) continue;
                var linked = _db.Abilities.Get(linkedId);
                if (linked != null) filtered.Add(linked);
            }
        }

        abilities = filtered;

        _abilityChoicesSection.Visible = abilities.Count > 0;
        if (abilities.Count == 0) return;

        var abilitiesBySection = GroupAbilitiesByAction(abilities);
        foreach (var sectionName in GetOrderedAbilitySectionNames(abilitiesBySection.Keys))
        {
            bool isOpen = !_closedAbilitySections.Contains(sectionName);

            var sectionContainer = new VBoxContainer();
            sectionContainer.AddThemeConstantOverride("separation", 4);

            var rowStyle  = _abilityChoicesContainer.GetThemeStylebox("panel", "PanelContainer");
            var hoverBox  = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.Hover };
            hoverBox.SetCornerRadiusAll(3);

            var sectionToggle = new Button
            {
                Text                = $"{(isOpen ? "−" : "+")}  {sectionName}",
                ToggleMode          = true,
                ButtonPressed       = isOpen,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment           = HorizontalAlignment.Left,
                Flat                = false,
            };
            sectionToggle.AddThemeStyleboxOverride("normal",  rowStyle);
            sectionToggle.AddThemeStyleboxOverride("hover",   hoverBox);
            sectionToggle.AddThemeStyleboxOverride("pressed", hoverBox);
            sectionToggle.AddThemeStyleboxOverride("focus",   rowStyle);

            var sectionBody = new VBoxContainer();
            sectionBody.AddThemeConstantOverride("separation", 8);

            var bodyMargin = new MarginContainer { Visible = isOpen };
            bodyMargin.AddThemeConstantOverride("margin_left", 24);
            bodyMargin.AddChild(sectionBody);

            sectionToggle.Toggled += pressed =>
            {
                if (!pressed) _closedAbilitySections.Add(sectionName);
                else _closedAbilitySections.Remove(sectionName);
                bodyMargin.Visible = pressed;
                sectionToggle.Text = $"{(pressed ? "−" : "+")}  {sectionName}";
            };

            foreach (var ability in abilitiesBySection[sectionName])
                sectionBody.AddChild(BuildAbilityBlock(ability, resourceAmounts, resourceNames, manualIds.Contains(ability.Id)));

            sectionContainer.AddChild(sectionToggle);
            sectionContainer.AddChild(bodyMargin);
            _abilityChoicesContainer.AddChild(sectionContainer);
        }

        return;

#pragma warning disable CS0162

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

#pragma warning restore CS0162
    }

    private void ShowAbilityPicker()
    {
        if (_pc == null) return;
        var ownedIds = new HashSet<int>(GetAllOwnedAbilities().ConvertAll(a => a.Id));

        // Create a hidden TypeOptionButton child sized to match the + button so its
        // popup positions correctly via GetGlobalRect().
        var picker = new TypeOptionButton
        {
            NoneText          = "— Cancel —",
            AutoSelectOnAdd   = true,
            ShowDeleteButtons = false,
            Size              = _addManualAbilityBtn.Size,
            Position          = _addManualAbilityBtn.Position,
            Visible           = false,
        };
        _addManualAbilityBtn.AddChild(picker);
        picker.Setup(
            () => _db.Abilities.GetAll(_pc.CampaignId)
                    .FindAll(a => !ownedIds.Contains(a.Id))
                    .ConvertAll(a => (a.Id, a.Name)),
            null, null);
        picker.TypeSelected += id =>
        {
            if (id >= 0)
            {
                _db.PlayerCharacters.AddManualAbility(_pc.Id, id);
                LoadAbilityChoices();
                LoadResources();
            }
        };
        picker.PopupClosed += () => picker.QueueFree();
        picker.ShowPopup();
    }

    private static string CostTooltip(List<AbilityCost> costs, Dictionary<int, string> resourceNames)
    {
        var parts = new System.Text.StringBuilder();
        foreach (var cost in costs)
        {
            if (parts.Length > 0) parts.Append(", ");
            string name = resourceNames.TryGetValue(cost.ResourceTypeId, out var n) ? n : "resource";
            parts.Append(cost.Amount == 1 ? $"1 {name}" : $"{cost.Amount} {name}");
        }
        return parts.ToString();
    }

    private VBoxContainer BuildAbilityBlock(Ability ability, Dictionary<int, int> resourceAmounts, Dictionary<int, string> resourceNames, bool isManual = false)
    {
        var abilityBlock = new VBoxContainer();
        abilityBlock.AddThemeConstantOverride("separation", 2);

        if (ability.ChoicePoolType != "fixed")
        {
            int capturedId = ability.Id;
            var row = new EntityRow
            {
                ShowDelete      = isManual,
                Text            = ability.Name,
                ShowDescription = !string.IsNullOrWhiteSpace(ability.Trigger),
                Description     = ability.Trigger,
            };
            if (isManual)
                row.DeletePressed += () =>
                {
                    _db.PlayerCharacters.RemoveManualAbility(_pc.Id, capturedId);
                    LoadAbilityChoices();
                    LoadResources();
                };
            int navId = ability.Id;
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo, "ability", navId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "ability", navId);

            if (!string.IsNullOrWhiteSpace(ability.Effect))
            {
                string capturedEffect = ability.Effect;
                row.MouseEntered += () => _effectPreview.ShowFor(capturedEffect, row);
                row.MouseExited  += _effectPreview.Hide;
            }

            if (ability.Costs.Count > 0)
            {
                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", -30);
                row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                hbox.AddChild(row);
                var capturedCosts = ability.Costs;
                bool depleted = IsOutOfUses(capturedCosts, resourceAmounts);
                var useBtn = new Button { Text = "⚡", Flat = true, TooltipText = CostTooltip(capturedCosts, resourceNames), MouseDefaultCursorShape = depleted ? Control.CursorShape.Forbidden : Control.CursorShape.PointingHand };
                useBtn.Disabled = depleted;
                useBtn.Pressed += () => SpendResources(capturedCosts);
                hbox.AddChild(useBtn);
                abilityBlock.AddChild(hbox);
            }
            else
            {
                abilityBlock.AddChild(row);
            }

            return abilityBlock;
        }

        int allowed = _db.Abilities.ResolveChoiceCount(ability, _pc.Level, _pc);
        var selectedChoices = _db.Abilities.GetCharacterAbilityChoices(_pc.Id, ability.Id);
        var selectedIds = new HashSet<int>();
        foreach (var s in selectedChoices) selectedIds.Add(s.ChoiceId);
        var allChoices = _db.Abilities.GetChoicesForAbility(ability.Id);

        string countText = allowed > 0 ? $"{selectedIds.Count}/{allowed} picks"
                         : selectedIds.Count > 0 ? $"{selectedIds.Count} selected"
                         : "0 picks";
        bool isOpen = _openAbilityDropdowns.Contains(ability.Id);
        bool incomplete = allowed > 0 && selectedIds.Count < allowed;
        var toggleBtn = new Button
        {
            Text                = $"{(isOpen ? "▲" : "▼")}  {ability.Name}  [{countText}]",
            ToggleMode          = true,
            ButtonPressed       = isOpen,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment           = HorizontalAlignment.Left,
        };
        if (incomplete)
        {
            var redBorder = toggleBtn.GetThemeStylebox("normal") is StyleBoxFlat existing
                ? (StyleBoxFlat)existing.Duplicate()
                : new StyleBoxFlat();
            redBorder.BorderColor = new Color(0.85f, 0.15f, 0.15f);
            redBorder.SetBorderWidthAll(2);
            toggleBtn.AddThemeStyleboxOverride("normal", redBorder);
        }

        if (!string.IsNullOrWhiteSpace(ability.Effect))
        {
            string capturedEffect = ability.Effect;
            toggleBtn.MouseEntered += () => _effectPreview.ShowFor(capturedEffect, toggleBtn);
            toggleBtn.MouseExited  += _effectPreview.Hide;
        }

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
            checkbox.AddThemeColorOverride("font_uncheck_color", new Color(1, 1, 1, 0.85f));
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

        var selectedList = new VBoxContainer();
        selectedList.AddThemeConstantOverride("separation", 1);
        var parentCosts = ability.Costs;
        foreach (var sel in selectedChoices)
        {
            var choice = allChoices.Find(c => c.Id == sel.ChoiceId);
            if (choice == null) continue;
            // Linked sub-abilities appear at the top level sorted by their own action type.
            if (choice.LinkedAbilityId.HasValue) continue;
            var row = new EntityRow { ShowDelete = false, Text = choice.Name };
            if (choice.LinkedAbilityId.HasValue)
            {
                int linkedId = choice.LinkedAbilityId.Value;
                row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo, "ability", linkedId);
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
                var useBtn = new Button { Text = "⚡", Flat = true, TooltipText = CostTooltip(capturedCosts, resourceNames), MouseDefaultCursorShape = depleted ? Control.CursorShape.Forbidden : Control.CursorShape.PointingHand };
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
        return abilityBlock;
    }

    private Dictionary<string, List<Ability>> GroupAbilitiesByAction(List<Ability> abilities)
    {
        var grouped = new Dictionary<string, List<Ability>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities)
        {
            string sectionName = GetAbilityActionSectionName(ability);
            if (!grouped.TryGetValue(sectionName, out var sectionAbilities))
            {
                sectionAbilities = new List<Ability>();
                grouped[sectionName] = sectionAbilities;
            }
            sectionAbilities.Add(ability);
        }

        return grouped;
    }

    private List<string> GetOrderedAbilitySectionNames(IEnumerable<string> sectionNames)
    {
        var lookup = new HashSet<string>(sectionNames, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var actionName in _abilityActionSectionOrder)
            if (lookup.Contains(actionName))
                ordered.Add(actionName);

        var remaining = new List<string>();
        foreach (var sectionName in lookup)
            if (!ordered.Contains(sectionName))
                remaining.Add(sectionName);

        remaining.Sort(StringComparer.OrdinalIgnoreCase);
        ordered.AddRange(remaining);
        return ordered;
    }

    private static string GetAbilityActionSectionName(Ability ability)
    {
        string action = ability.Action?.Trim() ?? "";
        return string.IsNullOrEmpty(action) ? "Unspecified" : action;
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
        {
            foreach (var abilityId in _db.Abilities.GetAbilityIdsForSpecies(_pc.SpeciesId.Value))
                ids.Add(abilityId);
            foreach (var level in _db.Species.GetLevelsForSpecies(_pc.SpeciesId.Value))
            {
                if (level.Level > _pc.Level) break;
                foreach (var abilityId in _db.Abilities.GetAbilityIdsForSpeciesLevel(level.Id))
                    ids.Add(abilityId);
            }
        }

        if (_pc.SubspeciesId.HasValue)
            foreach (var abilityId in _db.Abilities.GetAbilityIdsForSubspecies(_pc.SubspeciesId.Value))
                ids.Add(abilityId);

        foreach (var abilityId in _db.PlayerCharacters.GetManualAbilityIds(_pc.Id))
            ids.Add(abilityId);

        foreach (var abilityId in _db.PlayerCharacters.GetBackgroundAbilityIds(_pc.Id))
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
