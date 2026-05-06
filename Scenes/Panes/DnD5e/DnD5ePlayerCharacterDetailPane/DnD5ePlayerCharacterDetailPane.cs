using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DnD5ePlayerCharacterDetailPane : ScrollContainer
{
    private DatabaseService          _db;
    private DnD5ePlayerCharacter          _pc;
    private ConfirmationDialog       _confirmDialog;
    private int                      _subclassUnlockLevel   = 3;
    private bool                     _loading               = false;
    private string                   _activeTab             = "Stats";
    private HashSet<int>             _openAbilityDropdowns  = new();
    private HashSet<string>          _closedAbilitySections = new();
    private DnD5eSkillExpectationService  _skillExpectations;
    private DnD5eBackgroundPickerModal    _backgroundModal;
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

    private static readonly (string Name, string Abbrev)[] _dmgTypes =
    {
        ("bludgeoning", "B"), ("piercing", "P"),  ("slashing", "S"),
        ("acid", "A"),        ("cold", "C"),       ("fire", "F"),
        ("force", "Fo"),      ("lightning", "L"),  ("necrotic", "N"),
        ("poison", "Po"),     ("psychic", "Ps"),   ("radiant", "R"),
        ("thunder", "T"),
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
    [Export] private IntInput      _levelInput;
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
    [Export] private MarkdownNotes     _notes;
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

    // HP / Initiative
    [Export] private Button        _hpButton;
    [Export] private Button        _initButton;

    // Saving throws
    [Export] private CheckBox      _saveProfStrCb;
    [Export] private CheckBox      _saveProfDexCb;
    [Export] private CheckBox      _saveProfConCb;
    [Export] private CheckBox      _saveProfIntCb;
    [Export] private CheckBox      _saveProfWisCb;
    [Export] private CheckBox      _saveProfChaCb;
    [Export] private Label         _saveStrTotal;
    [Export] private Label         _saveDexTotal;
    [Export] private Label         _saveConTotal;
    [Export] private Label         _saveIntTotal;
    [Export] private Label         _saveWisTotal;
    [Export] private Label         _saveChaTotal;

    // Weapon attacks
    [Export] private VBoxContainer _weaponsContainer;
    [Export] private Button        _addWeaponButton;

    // Combat banner
    [Export] private Button        _acButton;
    [Export] private IntInput      _speedInput;
    [Export] private Button        _ppButton;
    [Export] private CheckBox      _inspirationCb;

    // Death saves
    [Export] private CheckBox      _deathSucc1;
    [Export] private CheckBox      _deathSucc2;
    [Export] private CheckBox      _deathSucc3;
    [Export] private CheckBox      _deathFail1;
    [Export] private CheckBox      _deathFail2;
    [Export] private CheckBox      _deathFail3;

    // Inventory
    [Export] private MarkdownNotes _inventoryNotes;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");
        _skillExpectations = new DnD5eSkillExpectationService(_db.Classes, _db.Abilities, _db.DnD5eBackgrounds);
        _backgroundModal   = GetNode<DnD5eBackgroundPickerModal>("BackgroundPickerModal");
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
        _levelInput.ValueChanged      += _ => { Save(); RefreshSubclassVisibility(); RefreshSaves(); LoadWeapons(); LoadAbilityChoices(); LoadResources(); };
        _descInput.TextChanged       += () => Save();
        _notes.TextChanged           += () => Save();
        _notes.NavigateTo            += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _inventoryNotes.TextChanged  += () => Save();
        _inventoryNotes.NavigateTo   += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        foreach (var (input, mod) in ScorePairs())
        {
            var capturedInput = input;
            var capturedMod   = mod;
            capturedInput.TextChanged += text =>
            {
                if (int.TryParse(text, out int v) && v >= 1 && v <= 30)
                {
                    capturedMod.Text = DnD5eMath.ModLabel(v);
                    Save();
                    LoadSkills();
                }
            };
            capturedInput.FocusExited += () =>
            {
                int val = DnD5eMath.ParseScore(capturedInput.Text);
                capturedInput.Text = val.ToString();
                capturedMod.Text   = DnD5eMath.ModLabel(val);
                Save();
                RefreshInitButton();
                RefreshAcButton();
                RefreshSaves();
                LoadWeapons();
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

        WireHpInitModals();
        WireAcModal();
        WirePpPopup();

        _speedInput.ValueChanged   += v => { if (_pc == null || _loading) return; _pc.Speed = v; _db.PlayerCharacters.Edit(_pc); };
        _inspirationCb.Toggled     += _ => { if (_pc == null || _loading) return; _pc.Inspiration = _inspirationCb.ButtonPressed; _db.PlayerCharacters.Edit(_pc); };

        foreach (var cb in DeathSaveCheckboxes())
        {
            var captured = cb;
            captured.Toggled += _ => SaveDeathSaves();
        }

        foreach (var cb in SaveCheckboxes())
        {
            var captured = cb;
            captured.Toggled += _ => { Save(); RefreshSaves(); };
        }

        _addWeaponButton.Pressed += () =>
        {
            if (_pc == null) return;
            _db.PlayerCharacters.AddWeapon(new DnD5ePlayerCharacterWeapon
            {
                PlayerCharacterId = _pc.Id,
                SortOrder         = _db.PlayerCharacters.GetWeapons(_pc.Id).Count,
            });
            LoadWeapons();
        };
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

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && IsVisibleInTree() && _pc != null && !_loading)
        {
            LoadSkills();
            LoadAbilityChoices();
            LoadResources();
        }
    }

    public void Load(DnD5ePlayerCharacter pc)
    {
        _loading = true;
        _pc = pc;
        _closedAbilitySections.Clear();
        SetActiveTab("Stats");
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

        _saveProfStrCb.SetPressedNoSignal(pc.SaveProfStr);
        _saveProfDexCb.SetPressedNoSignal(pc.SaveProfDex);
        _saveProfConCb.SetPressedNoSignal(pc.SaveProfCon);
        _saveProfIntCb.SetPressedNoSignal(pc.SaveProfInt);
        _saveProfWisCb.SetPressedNoSignal(pc.SaveProfWis);
        _saveProfChaCb.SetPressedNoSignal(pc.SaveProfCha);

        PopulateSpecies();
        PopulateClass();
        _speedInput.Value = pc.Speed;
        _inspirationCb.SetPressedNoSignal(pc.Inspiration);
        SetDeathSaveCheckboxes(pc.DeathSaveSuccesses, _deathSucc1, _deathSucc2, _deathSucc3);
        SetDeathSaveCheckboxes(pc.DeathSaveFailures,  _deathFail1, _deathFail2, _deathFail3);

        _inventoryNotes.Setup(pc.CampaignId, _db);
        _inventoryNotes.Text = pc.InventoryNotes;

        _imageCarousel.Setup(EntityType.PlayerCharacter, pc.Id, _db);
        _loading = false;
        LoadBackground();
        LoadAliases();
        RefreshHpButton();
        RefreshInitButton();
        RefreshAcButton();
        RefreshSaves();
        LoadSkills();
        LoadAbilityChoices();
        LoadResources();
        LoadWeapons();
    }

    private void Save()
    {
        if (_pc == null || _loading) return;
        _pc.Name         = _nameInput.Text;
        _pc.SpeciesId    = GetOptionId(_speciesInput);
        _pc.SubspeciesId = GetOptionId(_subspeciesInput);
        _pc.ClassId      = GetOptionId(_classInput);
        _pc.SubclassId   = GetOptionId(_subclassInput);
        _pc.Level        = _levelInput.Value;
        _pc.Strength     = DnD5eMath.ParseScore(_strInput.Text);
        _pc.Dexterity    = DnD5eMath.ParseScore(_dexInput.Text);
        _pc.Constitution = DnD5eMath.ParseScore(_conInput.Text);
        _pc.Intelligence = DnD5eMath.ParseScore(_intInput.Text);
        _pc.Wisdom       = DnD5eMath.ParseScore(_wisInput.Text);
        _pc.Charisma     = DnD5eMath.ParseScore(_chaInput.Text);
        _pc.Description   = _descInput.Text;
        _pc.Notes         = _notes.Text;
        _pc.InventoryNotes = _inventoryNotes.Text;
        _pc.Speed         = _speedInput.Value;
        _pc.Inspiration   = _inspirationCb.ButtonPressed;
        _pc.SaveProfStr   = _saveProfStrCb.ButtonPressed;
        _pc.SaveProfDex   = _saveProfDexCb.ButtonPressed;
        _pc.SaveProfCon   = _saveProfConCb.ButtonPressed;
        _pc.SaveProfInt   = _saveProfIntCb.ButtonPressed;
        _pc.SaveProfWis   = _saveProfWisCb.ButtonPressed;
        _pc.SaveProfCha   = _saveProfChaCb.ButtonPressed;
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
                              && _levelInput.Value >= _subclassUnlockLevel;
    }

    private void UpdateModLabels()
    {
        foreach (var (input, mod) in ScorePairs())
            mod.Text = DnD5eMath.ModLabel(DnD5eMath.ParseScore(input.Text));
    }

    private IEnumerable<(LineEdit input, Label mod)> ScorePairs() => new[]
    {
        (_strInput, _strMod), (_dexInput, _dexMod), (_conInput, _conMod),
        (_intInput, _intMod), (_wisInput, _wisMod), (_chaInput, _chaMod),
    };

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

    private void LoadAliases() =>
        AliasChipsHelper.Reload(_aliasChipsRow, _db, "playercharacter", _pc?.Id ?? 0, _pc?.CampaignId ?? 0, LoadAliases);

    // ── HP / Initiative ───────────────────────────────────────────────────────

    private void RefreshHpButton()
    {
        if (_pc == null || _hpButton == null) return;
        _hpButton.Text = _pc.TempHp > 0
            ? $"HP {_pc.CurrentHp}+{_pc.TempHp}/{_pc.MaxHp}"
            : $"HP {_pc.CurrentHp}/{_pc.MaxHp}";
    }

    private void RefreshInitButton()
    {
        if (_pc == null || _initButton == null) return;
        _initButton.Text = $"Init {DnD5eMath.SignStr(DnD5eMath.InitiativeBonus(_pc))}";
    }

    private void WireHpInitModals()
    {
        // HP popup
        var hpPopup  = new PopupPanel();
        var hpMargin = new MarginContainer();
        hpMargin.AddThemeConstantOverride("margin_left",   12);
        hpMargin.AddThemeConstantOverride("margin_right",  12);
        hpMargin.AddThemeConstantOverride("margin_top",     8);
        hpMargin.AddThemeConstantOverride("margin_bottom",  8);
        var hpVBox = new VBoxContainer();
        hpVBox.AddThemeConstantOverride("separation", 6);
        hpMargin.AddChild(hpVBox);
        hpPopup.AddChild(hpMargin);
        AddChild(hpPopup);

        var curInp = MakeIntInputRow(hpVBox, "Current HP", 0, 999);
        var maxInp = MakeIntInputRow(hpVBox, "Max HP",     0, 999);
        var tmpInp = MakeIntInputRow(hpVBox, "Temp HP",    0, 999);

        _hpButton.Pressed += () =>
        {
            if (_pc == null) return;
            curInp.Value = _pc.CurrentHp;
            maxInp.Value = _pc.MaxHp;
            tmpInp.Value = _pc.TempHp;
            var rect = _hpButton.GetGlobalRect();
            hpPopup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };
        curInp.ValueChanged += v => { if (_pc == null) return; _pc.CurrentHp = v; _db.PlayerCharacters.Edit(_pc); RefreshHpButton(); };
        maxInp.ValueChanged += v => { if (_pc == null) return; _pc.MaxHp     = v; _db.PlayerCharacters.Edit(_pc); RefreshHpButton(); };
        tmpInp.ValueChanged += v => { if (_pc == null) return; _pc.TempHp    = v; _db.PlayerCharacters.Edit(_pc); RefreshHpButton(); };

        // Initiative popup
        var initPopup  = new PopupPanel();
        var initMargin = new MarginContainer();
        initMargin.AddThemeConstantOverride("margin_left",   12);
        initMargin.AddThemeConstantOverride("margin_right",  12);
        initMargin.AddThemeConstantOverride("margin_top",     8);
        initMargin.AddThemeConstantOverride("margin_bottom",  8);
        var initVBox = new VBoxContainer();
        initVBox.AddThemeConstantOverride("separation", 6);
        initMargin.AddChild(initVBox);
        initPopup.AddChild(initMargin);
        AddChild(initPopup);

        var dexReadout = new Label();
        initVBox.AddChild(dexReadout);
        var miscInp = MakeIntInputRow(initVBox, "Misc", -20, 20);

        _initButton.Pressed += () =>
        {
            if (_pc == null) return;
            dexReadout.Text = $"DEX mod: {DnD5eMath.SignStr(DnD5eMath.AbilityMod(_pc.Dexterity))}";
            miscInp.Value = _pc.InitiativeMisc;
            var rect = _initButton.GetGlobalRect();
            initPopup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };
        miscInp.ValueChanged += v =>
        {
            if (_pc == null) return;
            _pc.InitiativeMisc = v;
            _db.PlayerCharacters.Edit(_pc);
            RefreshInitButton();
        };
    }

    // ── AC / Speed / PP ───────────────────────────────────────────────────────

    private void RefreshAcButton()
    {
        if (_pc == null || _acButton == null) return;
        _acButton.Text = $"AC {DnD5eMath.ArmorClass(_pc)}";
    }

    private void RefreshPpButton()
    {
        if (_pc == null || _ppButton == null) return;
        _ppButton.Text = $"PP {ComputePassivePerception()}";
    }

    private int ComputePassivePerception()
    {
        if (_pc == null) return 10;
        var allSkills = _db.DnD5eSkills.GetAll(_pc.CampaignId);
        var perception = allSkills.Find(s => s.Name.Equals("Perception", StringComparison.OrdinalIgnoreCase));
        if (perception == null) return 10 + DnD5eMath.AbilityMod(_pc.Wisdom);
        var charSkills = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id);
        var cs = charSkills.Find(c => c.SkillId == perception.Id);
        int profBonus = DnD5eMath.ProfBonus(_pc.Level);
        return 10 + DnD5eMath.SkillBonus("wis", _pc, profBonus, cs != null, cs?.IsExpertise ?? false);
    }

    private void WireAcModal()
    {
        var popup  = new PopupPanel();
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_top",     8);
        margin.AddThemeConstantOverride("margin_bottom",  8);
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);
        popup.AddChild(margin);
        AddChild(popup);

        var baseInp = MakeIntInputRow(vbox, "Base AC",   0, 30);
        var dexCb   = new CheckBox { Text = "Add DEX mod" };
        dexCb.AddThemeConstantOverride("icon_max_width", 14);
        vbox.AddChild(dexCb);
        var miscInp  = MakeIntInputRow(vbox, "Misc", -20, 20);
        var totalLbl = new Label();
        vbox.AddChild(totalLbl);

        void RefreshTotal()
        {
            if (_pc == null) return;
            totalLbl.Text = $"Total: {DnD5eMath.ArmorClass(_pc)}";
        }

        bool loading = false;
        _acButton.Pressed += () =>
        {
            if (_pc == null) return;
            loading = true;
            baseInp.Value = _pc.ArmorClassBase;
            dexCb.SetPressedNoSignal(_pc.AcUseDexMod);
            miscInp.Value = _pc.AcMisc;
            loading = false;
            RefreshTotal();
            var rect = _acButton.GetGlobalRect();
            popup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };
        baseInp.ValueChanged += v =>
        {
            if (loading || _pc == null) return;
            _pc.ArmorClassBase = v;
            _db.PlayerCharacters.Edit(_pc);
            RefreshTotal();
            RefreshAcButton();
        };
        dexCb.Toggled += _ =>
        {
            if (loading || _pc == null) return;
            _pc.AcUseDexMod = dexCb.ButtonPressed;
            _db.PlayerCharacters.Edit(_pc);
            RefreshTotal();
            RefreshAcButton();
        };
        miscInp.ValueChanged += v =>
        {
            if (loading || _pc == null) return;
            _pc.AcMisc = v;
            _db.PlayerCharacters.Edit(_pc);
            RefreshTotal();
            RefreshAcButton();
        };
    }

    private void WirePpPopup()
    {
        var popup  = new PopupPanel();
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_top",     8);
        margin.AddThemeConstantOverride("margin_bottom",  8);
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);
        popup.AddChild(margin);
        AddChild(popup);

        var breakdown = new Label();
        vbox.AddChild(breakdown);

        _ppButton.Pressed += () =>
        {
            if (_pc == null) return;
            int wisMod   = DnD5eMath.AbilityMod(_pc.Wisdom);
            int profBonus = DnD5eMath.ProfBonus(_pc.Level);
            var allSkills = _db.DnD5eSkills.GetAll(_pc.CampaignId);
            var perception = allSkills.Find(s => s.Name.Equals("Perception", StringComparison.OrdinalIgnoreCase));
            string profPart = "";
            if (perception != null)
            {
                var cs = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id).Find(c => c.SkillId == perception.Id);
                if (cs != null)
                    profPart = cs.IsExpertise ? $" + {2 * profBonus} (expertise)" : $" + {profBonus} (prof)";
            }
            breakdown.Text = $"10 + {DnD5eMath.SignStr(wisMod)} WIS{profPart} = {ComputePassivePerception()}";
            var rect = _ppButton.GetGlobalRect();
            popup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };
    }

    // ── Death Saves ───────────────────────────────────────────────────────────

    private void SaveDeathSaves()
    {
        if (_pc == null || _loading) return;
        _pc.DeathSaveSuccesses = CountChecked(_deathSucc1, _deathSucc2, _deathSucc3);
        _pc.DeathSaveFailures  = CountChecked(_deathFail1, _deathFail2, _deathFail3);
        _db.PlayerCharacters.Edit(_pc);
    }

    private static int CountChecked(params CheckBox[] boxes)
    {
        int count = 0;
        foreach (var cb in boxes) if (cb.ButtonPressed) count++;
        return count;
    }

    private static void SetDeathSaveCheckboxes(int count, CheckBox cb1, CheckBox cb2, CheckBox cb3)
    {
        cb1.SetPressedNoSignal(count >= 1);
        cb2.SetPressedNoSignal(count >= 2);
        cb3.SetPressedNoSignal(count >= 3);
    }

    private IEnumerable<CheckBox> DeathSaveCheckboxes() => new CheckBox[]
    {
        _deathSucc1, _deathSucc2, _deathSucc3,
        _deathFail1, _deathFail2, _deathFail3,
    };

    private static IntInput MakeIntInputRow(VBoxContainer parent, string label, int min, int max)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(90, 0), VerticalAlignment = VerticalAlignment.Center };
        var inp = new IntInput { MinValue = min, MaxValue = max };
        row.AddChild(lbl);
        row.AddChild(inp);
        parent.AddChild(row);
        return inp;
    }

    // ── Saving Throws ─────────────────────────────────────────────────────────

    private void RefreshSaves()
    {
        if (_pc == null) return;
        int profBonus = DnD5eMath.ProfBonus(_pc.Level);
        foreach (var (attr, total) in SaveLabels())
            total.Text = DnD5eMath.SignStr(DnD5eMath.SaveBonus(_pc, attr, profBonus));
    }

    private IEnumerable<(string attr, Label total)> SaveLabels() => new[]
    {
        ("str", _saveStrTotal), ("dex", _saveDexTotal), ("con", _saveConTotal),
        ("int", _saveIntTotal), ("wis", _saveWisTotal), ("cha", _saveChaTotal),
    };

    private IEnumerable<CheckBox> SaveCheckboxes() => new CheckBox[]
    {
        _saveProfStrCb, _saveProfDexCb, _saveProfConCb,
        _saveProfIntCb, _saveProfWisCb, _saveProfChaCb,
    };

    // ── Weapon Attacks ────────────────────────────────────────────────────────

    private void LoadWeapons()
    {
        foreach (Node child in _weaponsContainer.GetChildren())
            child.QueueFree();
        if (_pc == null) return;
        foreach (var weapon in _db.PlayerCharacters.GetWeapons(_pc.Id))
            _weaponsContainer.AddChild(BuildWeaponRow(weapon));
    }

    private Control BuildWeaponRow(DnD5ePlayerCharacterWeapon weapon)
    {
        var capturedWeapon = weapon;

        var panel = new PanelContainer();
        var vbox  = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);

        var attackBtn = new Button { CustomMinimumSize = new Vector2(52, 0), TooltipText = "Edit attack" };
        var damageBtn = new Button { CustomMinimumSize = new Vector2(76, 0), TooltipText = "Edit damage" };

        void RefreshButtons()
        {
            int atk = DnD5eMath.WeaponAttackBonus(_pc, capturedWeapon, DnD5eMath.ProfBonus(_pc.Level));
            int dm  = DnD5eMath.WeaponDamageMod(_pc, capturedWeapon);
            attackBtn.Text = DnD5eMath.SignStr(atk);
            damageBtn.Text = string.IsNullOrEmpty(capturedWeapon.DamageDice)
                ? DnD5eMath.SignStr(dm)
                : $"{capturedWeapon.DamageDice}{(dm != 0 ? DnD5eMath.SignStr(dm) : "")}";
        }
        RefreshButtons();

        // ── Row: name, attack button, damage button, delete ──────────────
        var nameInput = new LineEdit
        {
            Text                = weapon.Name,
            PlaceholderText     = "Weapon name...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        nameInput.TextChanged += _ =>
        {
            capturedWeapon.Name = nameInput.Text;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
        };

        var deleteBtn = new Button { Text = "×", Flat = true };
        deleteBtn.Pressed += () => { _db.PlayerCharacters.DeleteWeapon(capturedWeapon.Id); LoadWeapons(); };

        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 6);
        row1.AddChild(nameInput);
        row1.AddChild(attackBtn);
        row1.AddChild(damageBtn);
        row1.AddChild(deleteBtn);
        vbox.AddChild(row1);

        // ── Attack popup ──────────────────────────────────────────────────
        var atkPopup  = new PopupPanel();
        var atkMargin = new MarginContainer();
        atkMargin.AddThemeConstantOverride("margin_left",   10);
        atkMargin.AddThemeConstantOverride("margin_right",  10);
        atkMargin.AddThemeConstantOverride("margin_top",     6);
        atkMargin.AddThemeConstantOverride("margin_bottom",  6);
        var atkVBox = new VBoxContainer();
        atkVBox.AddThemeConstantOverride("separation", 4);
        atkMargin.AddChild(atkVBox);
        atkPopup.AddChild(atkMargin);
        panel.AddChild(atkPopup);

        var abilityRow      = new HBoxContainer();
        abilityRow.AddThemeConstantOverride("separation", 6);
        var deriveAbilityCb = new CheckBox { Text = "Ability mod", ButtonPressed = capturedWeapon.DeriveFromAbility };
        deriveAbilityCb.AddThemeConstantOverride("icon_max_width", 14);
        var abilityOpt = new OptionButton { CustomMinimumSize = new Vector2(60, 0), Visible = capturedWeapon.DeriveFromAbility };
        abilityOpt.AddItem("STR", 0);
        abilityOpt.AddItem("DEX", 1);
        abilityOpt.AddItem("FIN", 2);
        abilityOpt.Select(capturedWeapon.DeriveAbility switch { "dex" => 1, "fin" => 2, _ => 0 });
        abilityRow.AddChild(deriveAbilityCb);
        abilityRow.AddChild(abilityOpt);
        atkVBox.AddChild(abilityRow);

        var profCb = new CheckBox { Text = "Proficiency", ButtonPressed = capturedWeapon.IsProficient };
        profCb.AddThemeConstantOverride("icon_max_width", 14);
        atkVBox.AddChild(profCb);

        var atkMiscInput = MakeIntInputRow(atkVBox, "Misc bonus", -99, 99);

        var atkTotalLbl = new Label();
        atkVBox.AddChild(atkTotalLbl);

        void RefreshAtkTotal()
        {
            int atk = DnD5eMath.WeaponAttackBonus(_pc, capturedWeapon, DnD5eMath.ProfBonus(_pc.Level));
            atkTotalLbl.Text = $"Total: {DnD5eMath.SignStr(atk)}";
        }

        bool atkLoading = false;
        attackBtn.Pressed += () =>
        {
            if (_pc == null) return;
            atkLoading = true;
            deriveAbilityCb.SetPressedNoSignal(capturedWeapon.DeriveFromAbility);
            abilityOpt.Visible  = capturedWeapon.DeriveFromAbility;
            abilityOpt.Select(capturedWeapon.DeriveAbility switch { "dex" => 1, "fin" => 2, _ => 0 });
            profCb.SetPressedNoSignal(capturedWeapon.IsProficient);
            atkMiscInput.Value = capturedWeapon.AttackBonus;
            atkLoading = false;
            RefreshAtkTotal();
            var rect = attackBtn.GetGlobalRect();
            atkPopup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };
        deriveAbilityCb.Toggled += toggled =>
        {
            abilityOpt.Visible = toggled;
            if (atkLoading) return;
            capturedWeapon.DeriveFromAbility = toggled;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshAtkTotal();
            RefreshButtons();
        };
        abilityOpt.ItemSelected += idx =>
        {
            if (atkLoading) return;
            capturedWeapon.DeriveAbility = idx == 1 ? "dex" : idx == 2 ? "fin" : "str";
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshAtkTotal();
            RefreshButtons();
        };
        profCb.Toggled += _ =>
        {
            if (atkLoading) return;
            capturedWeapon.IsProficient = profCb.ButtonPressed;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshAtkTotal();
            RefreshButtons();
        };
        atkMiscInput.ValueChanged += v =>
        {
            if (atkLoading) return;
            capturedWeapon.AttackBonus = v;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshAtkTotal();
            RefreshButtons();
        };

        // ── Damage popup ──────────────────────────────────────────────────
        var dmgPopup  = new PopupPanel();
        var dmgMargin = new MarginContainer();
        dmgMargin.AddThemeConstantOverride("margin_left",   10);
        dmgMargin.AddThemeConstantOverride("margin_right",  10);
        dmgMargin.AddThemeConstantOverride("margin_top",     6);
        dmgMargin.AddThemeConstantOverride("margin_bottom",  6);
        var dmgVBox = new VBoxContainer();
        dmgVBox.AddThemeConstantOverride("separation", 4);
        dmgMargin.AddChild(dmgVBox);
        dmgPopup.AddChild(dmgMargin);
        panel.AddChild(dmgPopup);

        var diceTypes   = new[] { "—", "d4", "d6", "d8", "d10", "d12" };
        var diceRow     = new HBoxContainer();
        diceRow.AddThemeConstantOverride("separation", 6);
        var diceLbl     = new Label { Text = "Dice", CustomMinimumSize = new Vector2(60, 0), VerticalAlignment = VerticalAlignment.Center };
        var diceQtyInp  = new IntInput { MinValue = 1, MaxValue = 20 };
        var diceTypeOpt = new OptionButton { CustomMinimumSize = new Vector2(64, 0) };
        foreach (var dt in diceTypes) diceTypeOpt.AddItem(dt);

        var (initQty, initDType) = ParseDice(capturedWeapon.DamageDice);
        diceQtyInp.Value = initQty;
        int initDTypeIdx = Array.IndexOf(diceTypes, initDType);
        diceTypeOpt.Select(initDTypeIdx >= 0 ? initDTypeIdx : 0);

        diceRow.AddChild(diceLbl);
        diceRow.AddChild(diceQtyInp);
        diceRow.AddChild(diceTypeOpt);
        dmgVBox.AddChild(diceRow);

        var deriveDmgCb = new CheckBox { Text = "Add ability mod", ButtonPressed = capturedWeapon.DeriveDamageMod };
        deriveDmgCb.AddThemeConstantOverride("icon_max_width", 14);
        dmgVBox.AddChild(deriveDmgCb);

        // Damage type sub-popup added to panel to avoid popup nesting issues
        var dmgTypePopup = new PopupPanel();
        var typeMargin   = new MarginContainer();
        typeMargin.AddThemeConstantOverride("margin_left",   8);
        typeMargin.AddThemeConstantOverride("margin_right",  8);
        typeMargin.AddThemeConstantOverride("margin_top",    4);
        typeMargin.AddThemeConstantOverride("margin_bottom", 4);
        var typeVBox = new VBoxContainer();
        typeVBox.AddThemeConstantOverride("separation", 2);
        typeMargin.AddChild(typeVBox);
        dmgTypePopup.AddChild(typeMargin);
        panel.AddChild(dmgTypePopup);

        var currentTypes   = new HashSet<string>(
            capturedWeapon.DamageType.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
        var typeCheckboxes = new Dictionary<string, CheckBox>();
        foreach (var (typeName, _) in _dmgTypes)
        {
            var tcb = new CheckBox
            {
                Text          = char.ToUpper(typeName[0]) + typeName.Substring(1),
                ButtonPressed = currentTypes.Contains(typeName),
            };
            tcb.AddThemeConstantOverride("icon_max_width", 14);
            tcb.Toggled += _ =>
            {
                capturedWeapon.DamageType = string.Join(",", _dmgTypes
                    .Where(t => typeCheckboxes[t.Name].ButtonPressed)
                    .Select(t => t.Name));
                _db.PlayerCharacters.EditWeapon(capturedWeapon);
                RefreshButtons();
            };
            typeCheckboxes[typeName] = tcb;
            typeVBox.AddChild(tcb);
        }

        var dmgTypeRow = new HBoxContainer();
        dmgTypeRow.AddThemeConstantOverride("separation", 6);
        var dmgTypeLbl = new Label { Text = "Type", CustomMinimumSize = new Vector2(60, 0), VerticalAlignment = VerticalAlignment.Center };
        var dmgTypeBtn = new Button
        {
            Text              = DmgTypeLabel(capturedWeapon.DamageType),
            TooltipText       = DmgTypeTooltip(capturedWeapon.DamageType),
            CustomMinimumSize = new Vector2(80, 0),
        };
        dmgTypeBtn.Pressed += () =>
        {
            var storedTypes = new HashSet<string>(
                capturedWeapon.DamageType.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
            foreach (var (tName, _) in _dmgTypes)
                typeCheckboxes[tName].SetPressedNoSignal(storedTypes.Contains(tName));
            dmgTypeBtn.Text        = DmgTypeLabel(capturedWeapon.DamageType);
            dmgTypeBtn.TooltipText = DmgTypeTooltip(capturedWeapon.DamageType);
            // dmgTypeBtn is inside dmgPopup (a Window) — GetGlobalRect() is in the
            // popup's local viewport space, so add the window's screen position.
            var btnRect   = dmgTypeBtn.GetGlobalRect();
            var screenPos = dmgPopup.Position + (Vector2I)btnRect.Position;
            dmgTypePopup.PopupOnParent(new Rect2I(
                screenPos.X, screenPos.Y,
                (int)btnRect.Size.X, (int)btnRect.Size.Y));
        };
        dmgTypeRow.AddChild(dmgTypeLbl);
        dmgTypeRow.AddChild(dmgTypeBtn);
        dmgVBox.AddChild(dmgTypeRow);

        var dmgMiscInput = MakeIntInputRow(dmgVBox, "Misc bonus", -99, 99);

        var dmgTotalLbl = new Label();
        dmgVBox.AddChild(dmgTotalLbl);

        void RefreshDmgTotal()
        {
            int dm = DnD5eMath.WeaponDamageMod(_pc, capturedWeapon);
            dmgTotalLbl.Text = string.IsNullOrEmpty(capturedWeapon.DamageDice)
                ? $"Total: {DnD5eMath.SignStr(dm)}"
                : $"Total: {capturedWeapon.DamageDice}{(dm != 0 ? DnD5eMath.SignStr(dm) : "")}";
        }

        bool dmgLoading = false;
        damageBtn.Pressed += () =>
        {
            if (_pc == null) return;
            dmgLoading = true;
            var (qty, dType) = ParseDice(capturedWeapon.DamageDice);
            diceQtyInp.Value = qty;
            int dTypeIdx = Array.IndexOf(diceTypes, dType);
            diceTypeOpt.Select(dTypeIdx >= 0 ? dTypeIdx : 0);
            deriveDmgCb.SetPressedNoSignal(capturedWeapon.DeriveDamageMod);
            dmgMiscInput.Value = capturedWeapon.DamageBonus;
            dmgLoading = false;
            RefreshDmgTotal();
            var rect = damageBtn.GetGlobalRect();
            dmgPopup.PopupOnParent(new Rect2I(
                (int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y),
                (int)rect.Size.X, 0));
        };

        void SaveDiceField()
        {
            string dType = diceTypes[diceTypeOpt.Selected];
            capturedWeapon.DamageDice = dType == "—" ? "" : $"{diceQtyInp.Value}{dType}";
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshDmgTotal();
            RefreshButtons();
        }

        diceQtyInp.ValueChanged  += _ => { if (dmgLoading) return; SaveDiceField(); };
        diceTypeOpt.ItemSelected += _ => { if (dmgLoading) return; SaveDiceField(); };
        deriveDmgCb.Toggled += _ =>
        {
            if (dmgLoading) return;
            capturedWeapon.DeriveDamageMod = deriveDmgCb.ButtonPressed;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshDmgTotal();
            RefreshButtons();
        };
        dmgMiscInput.ValueChanged += v =>
        {
            if (dmgLoading) return;
            capturedWeapon.DamageBonus = v;
            _db.PlayerCharacters.EditWeapon(capturedWeapon);
            RefreshDmgTotal();
            RefreshButtons();
        };

        return panel;
    }

    private static (int Qty, string DType) ParseDice(string dice)
    {
        if (string.IsNullOrEmpty(dice)) return (1, "—");
        int idx = dice.IndexOf('d');
        if (idx > 0 && int.TryParse(dice.Substring(0, idx), out int q) && q >= 1)
            return (q, dice.Substring(idx));
        return (1, "—");
    }

    private static string DmgTypeLabel(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return "—";
        return string.Join(", ", stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => DmgTypeAbbrev(p.Trim())));
    }

    private static string DmgTypeTooltip(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return "";
        return string.Join(", ", stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => { var t = p.Trim(); return t.Length > 0 ? char.ToUpper(t[0]) + t.Substring(1) : t; }));
    }

    private static string DmgTypeAbbrev(string name)
    {
        foreach (var (n, a) in _dmgTypes)
            if (n == name) return a;
        return name;
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

        var allSkills       = _db.DnD5eSkills.GetAll(_pc.CampaignId);
        var characterSkills = _db.DnD5eCharacterSkills.GetForCharacter(_pc.Id);
        var skillMap        = new Dictionary<int, DnD5eCharacterSkill>();
        foreach (var cs in characterSkills)
            skillMap[cs.SkillId] = cs;

        var expectations = _skillExpectations.GetExpectations(_pc);

        // Precompute source states once — used by both chips and skill row icons
        var actualCounts   = new Dictionary<string, int>();
        var expectedCounts = new Dictionary<string, int>();
        var sourceNames    = new Dictionary<string, string>();
        foreach (var cs in skillMap.Values)
        {
            if (!actualCounts.ContainsKey(cs.Source)) actualCounts[cs.Source] = 0;
            actualCounts[cs.Source]++;
        }
        foreach (var exp in expectations)
        {
            if (!expectedCounts.ContainsKey(exp.Source)) expectedCounts[exp.Source] = 0;
            expectedCounts[exp.Source] += exp.ExpectedCount;
            sourceNames[exp.Source] = exp.SourceName;
        }

        string bgName = _pc.BackgroundId.HasValue
            ? _db.DnD5eBackgrounds.Get(_pc.BackgroundId.Value)?.Name ?? "Background"
            : "Background";
        sourceNames["background"] = bgName;

        // Rebuild chips
        foreach (Node child in _skillsChipsContainer.GetChildren())
            child.QueueFree();
        BuildSourceChips(expectations, actualCounts, expectedCounts);

        // Rebuild skill rows
        foreach (Node child in _skillsListContainer.GetChildren())
            child.QueueFree();

        int profBonus = DnD5eMath.ProfBonus(_pc.Level);

        foreach (var skill in allSkills)
        {
            skillMap.TryGetValue(skill.Id, out var cs);
            bool isProficient = cs != null;
            bool isExpertise  = cs?.IsExpertise ?? false;
            int  bonus        = DnD5eMath.SkillBonus(skill.Attribute, _pc, profBonus, isProficient, isExpertise);

            var row = BuildSkillRow(skill, cs, bonus, profBonus, skillMap, expectations, allSkills, bgName, actualCounts, expectedCounts, sourceNames);
            _skillsListContainer.AddChild(row);
        }

        RefreshPpButton();
    }

    private HBoxContainer BuildSkillRow(
        DnD5eSkill skill,
        DnD5eCharacterSkill cs,
        int bonus,
        int profBonus,
        Dictionary<int, DnD5eCharacterSkill> skillMap,
        List<DnD5eSkillExpectation> expectations,
        List<DnD5eSkill> allSkills,
        string bgName,
        Dictionary<string, int> actualCounts,
        Dictionary<string, int> expectedCounts,
        Dictionary<string, string> sourceNames)
    {
        bool isProficient = cs != null;
        bool isExpertise  = cs?.IsExpertise ?? false;
        bool isLocked     = cs?.Source == "background";

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        // State icon — shows source validity, only visible when proficient
        string stateIcon    = "";
        string stateTooltip = "";
        Color? stateColor   = null;
        if (cs != null)
        {
            string src     = cs.Source;
            string srcName = sourceNames.TryGetValue(src, out string sn) ? sn : src;
            actualCounts.TryGetValue(src, out int actual);
            expectedCounts.TryGetValue(src, out int expected);
            bool isOverBudget = expected > 0 && src != "background" && actual > expected;
            stateIcon    = isOverBudget ? "!" : "✓";
            stateTooltip = isOverBudget
                ? $"Over {srcName} limit — review your selections"
                : $"Source: {srcName}";
            if (isOverBudget) stateColor = new Color(0.9f, 0.25f, 0.25f);
        }
        var stateLabel = new Label
        {
            Text                = stateIcon,
            CustomMinimumSize   = new Vector2(14, 0),
            TooltipText         = stateTooltip,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter         = Control.MouseFilterEnum.Stop,
        };
        if (stateColor.HasValue) stateLabel.AddThemeColorOverride("font_color", stateColor.Value);

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
            Text                    = DnD5eMath.SignStr(bonus),
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

        row.AddChild(stateLabel);
        row.AddChild(profCb);
        row.AddChild(expCb);
        row.AddChild(nameLabel);
        row.AddChild(bonusLabel);
        return row;
    }

    private void BuildSourceChips(List<DnD5eSkillExpectation> expectations, Dictionary<string, int> actualCounts, Dictionary<string, int> expectedCounts)
    {
        int totalActual   = 0;
        int totalExpected = 0;
        var tooltipLines  = new System.Text.StringBuilder();
        var seenSources   = new HashSet<string>();

        foreach (var exp in expectations)
        {
            if (!seenSources.Add(exp.Source)) continue;

            actualCounts.TryGetValue(exp.Source, out int actual);
            expectedCounts.TryGetValue(exp.Source, out int expected);

            // Background is auto-granted — include in tooltip but not in the selectable totals
            if (exp.Source != "background")
            {
                totalActual   += actual;
                totalExpected += expected;
            }

            int diff = expected - actual;
            string status = diff > 0 ? $"(need {diff} more)"
                          : diff < 0 ? $"({-diff} over limit)"
                          : "(✓)";
            if (tooltipLines.Length > 0) tooltipLines.Append('\n');
            tooltipLines.Append($"{exp.SourceName}: {actual}/{expected} {status}");
        }

        if (totalExpected == 0 && tooltipLines.Length == 0) return;

        int totalDiff = totalExpected - totalActual;
        string icon   = totalDiff > 0 ? "⚠" : totalDiff < 0 ? "!" : "✓";
        Color? color  = totalDiff < 0 ? new Color(0.9f, 0.25f, 0.25f) : null;

        var chip = new Label
        {
            Text        = $"{icon} {totalActual}/{totalExpected}",
            TooltipText = tooltipLines.ToString(),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        if (color.HasValue) chip.AddThemeColorOverride("font_color", color.Value);
        _skillsChipsContainer.AddChild(chip);
    }

    private string InferSource(int skillId, Dictionary<int, DnD5eCharacterSkill> skillMap,
        List<DnD5eSkillExpectation> expectations, List<DnD5eSkill> allSkills)
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

    // ── DnD5eAbility choices ───────────────────────────────────────────────────────

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
        var choicesCache    = new Dictionary<int, List<DnD5eAbilityChoice>>();
        foreach (var a in abilities)
        {
            if (a.ChoicePoolType != "fixed") continue;
            var choices = _db.Abilities.GetChoicesForAbility(a.Id);
            choicesCache[a.Id] = choices;
            foreach (var ch in choices)
                if (ch.LinkedAbilityId.HasValue)
                    choiceLinkedIds.Add(ch.LinkedAbilityId.Value);
        }

        var filtered = new List<DnD5eAbility>();
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

        _abilityChoicesSection.Visible = true;
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

    }

    private void ShowAbilityPicker()
    {
        if (_pc == null) return;
        var ownedIds = new HashSet<int>(GetAllOwnedAbilities().ConvertAll(a => a.Id));

        // Create a hidden TypesDropdown child sized to match the + button so its
        // popup positions correctly via GetGlobalRect().
        var picker = new TypesDropdown
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

    private static string CostTooltip(List<DnD5eAbilityCost> costs, Dictionary<int, string> resourceNames)
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

    private VBoxContainer BuildAbilityBlock(DnD5eAbility ability, Dictionary<int, int> resourceAmounts, Dictionary<int, string> resourceNames, bool isManual = false)
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
            // Linked choices appear at the top level via the filtered list; skip them here.
            if (choice.LinkedAbilityId.HasValue) continue;
            var row = new EntityRow { ShowDelete = false, Text = choice.Name };
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

    private Dictionary<string, List<DnD5eAbility>> GroupAbilitiesByAction(List<DnD5eAbility> abilities)
    {
        var grouped = new Dictionary<string, List<DnD5eAbility>>(StringComparer.OrdinalIgnoreCase);
        foreach (var ability in abilities)
        {
            string sectionName = GetAbilityActionSectionName(ability);
            if (!grouped.TryGetValue(sectionName, out var sectionAbilities))
            {
                sectionAbilities = new List<DnD5eAbility>();
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

    private static string GetAbilityActionSectionName(DnD5eAbility ability)
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
                    _db.PlayerCharacters.UpsertResource(new DnD5eCharacterResource
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

    private static bool IsOutOfUses(List<DnD5eAbilityCost> costs, Dictionary<int, int> resourceAmounts)
    {
        foreach (var cost in costs)
            if (!resourceAmounts.TryGetValue(cost.ResourceTypeId, out int cur) || cur < cost.Amount)
                return true;
        return false;
    }

    private void SpendResources(List<DnD5eAbilityCost> costs)
    {
        if (_pc == null) return;
        var resources = _db.PlayerCharacters.GetResources(_pc.Id);
        foreach (var cost in costs)
        {
            DnD5eCharacterResource match = null;
            foreach (var r in resources)
                if (r.ResourceTypeId == cost.ResourceTypeId) { match = r; break; }
            if (match == null) continue;
            match.CurrentAmount = System.Math.Max(0, match.CurrentAmount - cost.Amount);
            _db.PlayerCharacters.UpsertResource(match);
        }
        LoadResources();
        LoadAbilityChoices();
    }

    private List<DnD5eAbility> GetAllOwnedAbilities()
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

        var abilities = new List<DnD5eAbility>();
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
