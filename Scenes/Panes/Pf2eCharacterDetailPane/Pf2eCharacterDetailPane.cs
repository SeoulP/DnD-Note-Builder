using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Pf2eCharacterDetailPane : ScrollContainer
{
    private DatabaseService _db;
    private Pf2eCharacter   _pc;
    private bool            _loading;
    private string          _activeTab = "Stats";

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    // ── Header ────────────────────────────────────────────────────────────────
    [Export] private LineEdit      _nameInput;
    [Export] private OptionButton  _ancestryInput;
    [Export] private OptionButton  _heritageInput;
    [Export] private OptionButton  _classInput;
    [Export] private SpinBox       _levelInput;
    [Export] private SpinBox       _currentHpInput;
    [Export] private SpinBox       _maxHpInput;
    [Export] private Button        _heroPip1;
    [Export] private Button        _heroPip2;
    [Export] private Button        _heroPip3;
    [Export] private Button        _deleteButton;
    [Export] private VBoxContainer _aliasChipsRow;
    [Export] private ImageCarousel _imageCarousel;

    // ── Tab strip ────────────────────────────────────────────────────────────
    [Export] private Button  _statsTabBtn;
    [Export] private Button  _skillsTabBtn;
    [Export] private Button  _flavorTabBtn;
    [Export] private Control _statsTab;
    [Export] private Control _skillsTab;
    [Export] private Control _flavorTab;

    // ── Stats tab ─────────────────────────────────────────────────────────────
    [Export] private SpinBox _strInput, _dexInput, _conInput, _intInput, _wisInput, _chaInput;
    [Export] private Label   _strMod,  _dexMod,  _conMod,  _intMod,  _wisMod,  _chaMod;
    [Export] private SpinBox _acInput;
    [Export] private SpinBox _speedInput;
    [Export] private OptionButton _fortRank, _refRank, _willRank, _percRank;
    [Export] private Label        _fortMod,  _refMod,  _willMod,  _percMod;

    // ── Skills tab ────────────────────────────────────────────────────────────
    [Export] private VBoxContainer _skillsContainer;

    // ── Flavor tab ────────────────────────────────────────────────────────────
    [Export] private TextEdit  _descInput;
    [Export] private WikiNotes _notes;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { if (_loading || _pc == null) return; _pc.Name = name; _db.Pf2eCharacters.EditBase(_pc); EmitSignal(SignalName.NameChanged, "pf2e_pc", _pc.Id, string.IsNullOrEmpty(name) ? "New Character" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Character"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);

        _ancestryInput.ItemSelected += _ => { if (!_loading) { SavePf2e(); RefreshHeritages(false); } };
        _heritageInput.ItemSelected += _ => { if (!_loading) SavePf2e(); };
        _classInput.ItemSelected    += _ => { if (!_loading) SavePf2e(); };
        _levelInput.ValueChanged    += _ => { if (!_loading) { SavePf2e(); RefreshSaveMods(); LoadSkills(); } };
        _currentHpInput.ValueChanged += _ => { if (!_loading) SavePf2e(); };
        _maxHpInput.ValueChanged     += _ => { if (!_loading) SavePf2e(); };
        _acInput.ValueChanged        += _ => { if (!_loading) SavePf2e(); };
        _speedInput.ValueChanged     += _ => { if (!_loading) SavePf2e(); };
        _fortRank.ItemSelected += _ => { if (!_loading) { SavePf2e(); RefreshSaveMods(); } };
        _refRank.ItemSelected  += _ => { if (!_loading) { SavePf2e(); RefreshSaveMods(); } };
        _willRank.ItemSelected += _ => { if (!_loading) { SavePf2e(); RefreshSaveMods(); } };
        _percRank.ItemSelected += _ => { if (!_loading) { SavePf2e(); RefreshSaveMods(); } };
        _heroPip1.Pressed += () => { if (!_loading) SavePf2e(); };
        _heroPip2.Pressed += () => { if (!_loading) SavePf2e(); };
        _heroPip3.Pressed += () => { if (!_loading) SavePf2e(); };

        foreach (var (sb, lbl) in AbilityPairs())
        {
            var s = sb; var l = lbl;
            s.ValueChanged += v => { if (_loading) return; l.Text = ModStr((int)v); SavePf2e(); RefreshSaveMods(); LoadSkills(); };
        }

        _descInput.TextChanged += () => { if (!_loading && _pc != null) { _pc.Description = _descInput.Text; _db.Pf2eCharacters.EditBase(_pc); } };
        _notes.TextChanged     += () => { if (!_loading && _pc != null) { _pc.Notes = _notes.Text; _db.Pf2eCharacters.EditBase(_pc); } };
        _notes.NavigateTo      += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        var confirmDialog = DialogHelper.Make("Delete Character");
        AddChild(confirmDialog);
        confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_pc", _pc?.Id ?? 0);
        _deleteButton.Pressed   += () => DialogHelper.Show(confirmDialog, $"Delete \"{_pc?.Name}\"? This cannot be undone.");

        _statsTabBtn.Pressed  += () => SetActiveTab("Stats");
        _skillsTabBtn.Pressed += () => SetActiveTab("Skills");
        _flavorTabBtn.Pressed += () => SetActiveTab("Flavor");

        PopulateRankDropdowns();
        ConfigureSpinBoxes();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && IsVisibleInTree() && _pc != null && !_loading)
            LoadSkills();
    }

    private void SetActiveTab(string name)
    {
        _activeTab = name;
        _statsTab.Visible  = name == "Stats";
        _skillsTab.Visible = name == "Skills";
        _flavorTab.Visible = name == "Flavor";
        _statsTabBtn.SetPressedNoSignal(name == "Stats");
        _skillsTabBtn.SetPressedNoSignal(name == "Skills");
        _flavorTabBtn.SetPressedNoSignal(name == "Flavor");
    }

    public void Load(Pf2eCharacter pc)
    {
        _loading = true;
        _pc = pc;
        SetActiveTab("Stats");

        _nameInput.Text = pc.Name;
        LoadAliases();

        PopulateAncestry();
        SetOptionById(_ancestryInput, pc.AncestryId ?? 0);
        RefreshHeritages(true);
        SetOptionById(_heritageInput, pc.HeritageId ?? 0);
        PopulateClass();
        SetOptionById(_classInput, pc.ClassId ?? 0);

        _levelInput.Value      = pc.Level;
        _currentHpInput.Value  = pc.CurrentHp;
        _maxHpInput.Value      = pc.MaxHp;
        SetHeroPips(pc.HeroPoints);

        _strInput.Value = pc.Strength;    _strMod.Text = ModStr(pc.Strength);
        _dexInput.Value = pc.Dexterity;   _dexMod.Text = ModStr(pc.Dexterity);
        _conInput.Value = pc.Constitution; _conMod.Text = ModStr(pc.Constitution);
        _intInput.Value = pc.Intelligence; _intMod.Text = ModStr(pc.Intelligence);
        _wisInput.Value = pc.Wisdom;      _wisMod.Text = ModStr(pc.Wisdom);
        _chaInput.Value = pc.Charisma;    _chaMod.Text = ModStr(pc.Charisma);

        _acInput.Value    = pc.Ac;
        _speedInput.Value = pc.SpeedFeet;

        _fortRank.Selected = pc.FortitudeRank;
        _refRank.Selected  = pc.ReflexRank;
        _willRank.Selected = pc.WillRank;
        _percRank.Selected = pc.PerceptionRank;
        RefreshSaveMods();

        _descInput.Text = pc.Description;
        _notes.Setup(pc.CampaignId, _db);
        _notes.Text = pc.Notes;

        _imageCarousel.Setup(EntityType.Pf2eCharacter, pc.Id, _db);

        LoadSkills();

        _loading = false;
    }

    // ── Populate dropdowns ───────────────────────────────────────────────────

    private void PopulateRankDropdowns()
    {
        string[] items = { "Untrained", "Trained", "Expert", "Master", "Legendary" };
        foreach (var dd in new[] { _fortRank, _refRank, _willRank, _percRank })
        {
            dd.Clear();
            foreach (var item in items) dd.AddItem(item);
        }
    }

    private void PopulateAncestry()
    {
        _ancestryInput.Clear();
        _ancestryInput.AddItem("— Ancestry —", 0);
        foreach (var a in _db.Pf2eAncestries.GetAll(_pc.CampaignId))
            _ancestryInput.AddItem(a.Name, a.Id);
    }

    private void RefreshHeritages(bool keepSelection)
    {
        int prevId = keepSelection ? 0 : _heritageInput.GetItemId(_heritageInput.Selected);
        _heritageInput.Clear();
        _heritageInput.AddItem("— Heritage —", 0);
        int ancId = _ancestryInput.GetItemId(_ancestryInput.Selected);
        if (ancId > 0)
            foreach (var h in _db.Pf2eHeritages.GetForAncestry(ancId))
                _heritageInput.AddItem(h.Name, h.Id);
        if (!keepSelection) SetOptionById(_heritageInput, prevId);
    }

    private void PopulateClass()
    {
        _classInput.Clear();
        _classInput.AddItem("— Class —", 0);
        foreach (var c in _db.Pf2eClasses.GetAll(_pc.CampaignId))
            _classInput.AddItem(c.Name, c.Id);
    }

    private void SetOptionById(OptionButton btn, int id)
    {
        for (int i = 0; i < btn.ItemCount; i++)
            if (btn.GetItemId(i) == id) { btn.Selected = i; return; }
        btn.Selected = 0;
    }

    private void SetHeroPips(int count)
    {
        _heroPip1.SetPressedNoSignal(count >= 1);
        _heroPip2.SetPressedNoSignal(count >= 2);
        _heroPip3.SetPressedNoSignal(count >= 3);
    }

    // ── Combat derived display ────────────────────────────────────────────────

    private void RefreshSaveMods()
    {
        int lvl = (int)_levelInput.Value;
        _fortMod.Text = SignStr(Mod((int)_conInput.Value) + ProfBonus(_fortRank.Selected, lvl));
        _refMod.Text  = SignStr(Mod((int)_dexInput.Value) + ProfBonus(_refRank.Selected,  lvl));
        _willMod.Text = SignStr(Mod((int)_wisInput.Value) + ProfBonus(_willRank.Selected,  lvl));
        _percMod.Text = SignStr(Mod((int)_wisInput.Value) + ProfBonus(_percRank.Selected,  lvl));
    }

    // ── Skills tab ────────────────────────────────────────────────────────────

    private void LoadSkills()
    {
        if (_pc == null) return;
        var skillTypes = _db.Pf2eSkillTypes.GetAll(_pc.CampaignId);
        var charSkills = _db.Pf2eCharacterSkills.GetForCharacter(_pc.Id);
        var ranks      = _db.Pf2eProficiencyRanks.GetAll();
        var abilities  = _db.Pf2eAbilityScores.GetAll();

        var rankValueBySkill = new Dictionary<int, int>();
        var charSkillIdBySkill = new Dictionary<int, int>();
        foreach (var cs in charSkills)
        {
            var r = ranks.FirstOrDefault(x => x.Id == cs.ProficiencyRankId);
            if (r != null) rankValueBySkill[cs.SkillTypeId] = r.RankValue;
            charSkillIdBySkill[cs.SkillTypeId] = cs.Id;
        }

        var abbrByAbility = abilities.ToDictionary(a => a.Id, a => a.Abbreviation);

        foreach (Node child in _skillsContainer.GetChildren()) child.QueueFree();

        int lvl = (int)_levelInput.Value;
        foreach (var st in skillTypes)
        {
            int    rankValue = rankValueBySkill.TryGetValue(st.Id, out int rv) ? rv : 0;
            string abbr      = abbrByAbility.GetValueOrDefault(st.AbilityScoreId, "?");
            int    abilMod   = Mod(ScoreFor(abbr));
            int    total     = abilMod + ProfBonus(rankValue, lvl);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var nameLabel = new Label { Text = st.Name, CustomMinimumSize = new Vector2(130, 0) };
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var abbrLabel = new Label { Text = abbr, CustomMinimumSize = new Vector2(36, 0) };
            abbrLabel.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(abbrLabel);

            var rankDd = new OptionButton { CustomMinimumSize = new Vector2(105, 0) };
            foreach (var item in new[] { "Untrained", "Trained", "Expert", "Master", "Legendary" })
                rankDd.AddItem(item);
            rankDd.Selected = rankValue;
            row.AddChild(rankDd);

            var totalLabel = new Label { Text = SignStr(total), CustomMinimumSize = new Vector2(40, 0) };
            totalLabel.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(totalLabel);

            int capId      = st.Id;
            int capAbilMod = abilMod;
            rankDd.ItemSelected += newIdx =>
            {
                int newRank  = (int)newIdx;
                totalLabel.Text = SignStr(capAbilMod + ProfBonus(newRank, (int)_levelInput.Value));
                UpsertSkill(capId, newRank, charSkillIdBySkill, ranks);
            };

            _skillsContainer.AddChild(row);
        }
    }

    private void UpsertSkill(int skillTypeId, int rankValue, Dictionary<int, int> idMap, List<Pf2eProficiencyRank> ranks)
    {
        if (_pc == null) return;
        var rank = ranks.FirstOrDefault(r => r.RankValue == rankValue);
        if (rank == null) return;

        if (idMap.TryGetValue(skillTypeId, out int existId))
        {
            var cs = _db.Pf2eCharacterSkills.Get(existId);
            if (cs != null) { cs.ProficiencyRankId = rank.Id; _db.Pf2eCharacterSkills.Edit(cs); }
        }
        else
        {
            var cs    = new Pf2eCharacterSkill { CharacterId = _pc.Id, SkillTypeId = skillTypeId, ProficiencyRankId = rank.Id };
            int newId = _db.Pf2eCharacterSkills.Add(cs);
            idMap[skillTypeId] = newId;
        }
    }

    // ── Aliases ───────────────────────────────────────────────────────────────

    private void LoadAliases()
    {
        if (_pc == null || _aliasChipsRow == null) return;
        foreach (Node child in _aliasChipsRow.GetChildren()) child.QueueFree();

        var chipsRow = new HBoxContainer();
        chipsRow.AddThemeConstantOverride("separation", 4);
        _aliasChipsRow.AddChild(chipsRow);

        foreach (var alias in _db.EntityAliases.GetForEntity("pf2e_pc", _pc.Id))
        {
            int capturedId = alias.Id;
            var normal = new StyleBoxFlat { BgColor = new Color(0.18f, 0.18f, 0.18f) };
            normal.SetCornerRadiusAll(4); normal.ContentMarginLeft = 6; normal.ContentMarginRight = 4; normal.ContentMarginTop = 2; normal.ContentMarginBottom = 2;
            var hover  = new StyleBoxFlat { BgColor = new Color(0.45f, 0.10f, 0.10f) };
            hover.SetCornerRadiusAll(4); hover.ContentMarginLeft = 6; hover.ContentMarginRight = 4; hover.ContentMarginTop = 2; hover.ContentMarginBottom = 2;

            var chip    = new PanelContainer(); chip.AddThemeStyleboxOverride("panel", normal);
            var chipRow = new HBoxContainer();  chipRow.AddThemeConstantOverride("separation", 2);
            var lbl     = new Label { Text = alias.Alias }; lbl.AddThemeFontSizeOverride("font_size", 11);
            var rmBtn   = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
            rmBtn.AddThemeFontSizeOverride("font_size", 11);
            rmBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", hover);
            rmBtn.MouseExited  += () => chip.AddThemeStyleboxOverride("panel", normal);
            rmBtn.Pressed      += () => { _db.EntityAliases.Delete(capturedId); LoadAliases(); };
            chipRow.AddChild(lbl); chipRow.AddChild(rmBtn);
            chip.AddChild(chipRow); chipsRow.AddChild(chip);
        }

        var addInput = new LineEdit { PlaceholderText = "+ alias", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(80, 0) };
        addInput.TextSubmitted += text =>
        {
            string t = text.Trim();
            if (string.IsNullOrEmpty(t)) return;
            addInput.Text = "";
            _db.EntityAliases.Add(new EntityAlias { CampaignId = _pc.CampaignId, EntityType = "pf2e_pc", EntityId = _pc.Id, Alias = t });
            LoadAliases();
        };
        _aliasChipsRow.AddChild(addInput);
    }

    // ── Save helpers ──────────────────────────────────────────────────────────

    private void SavePf2e()
    {
        if (_pc == null) return;
        _pc.AncestryId     = NullIfZero(_ancestryInput.GetItemId(_ancestryInput.Selected));
        _pc.HeritageId     = NullIfZero(_heritageInput.GetItemId(_heritageInput.Selected));
        _pc.ClassId        = NullIfZero(_classInput.GetItemId(_classInput.Selected));
        _pc.Level          = (int)_levelInput.Value;
        _pc.CurrentHp      = (int)_currentHpInput.Value;
        _pc.MaxHp          = (int)_maxHpInput.Value;
        _pc.HeroPoints     = (_heroPip1.ButtonPressed ? 1 : 0) + (_heroPip2.ButtonPressed ? 1 : 0) + (_heroPip3.ButtonPressed ? 1 : 0);
        _pc.Strength       = (int)_strInput.Value;
        _pc.Dexterity      = (int)_dexInput.Value;
        _pc.Constitution   = (int)_conInput.Value;
        _pc.Intelligence   = (int)_intInput.Value;
        _pc.Wisdom         = (int)_wisInput.Value;
        _pc.Charisma       = (int)_chaInput.Value;
        _pc.Ac             = (int)_acInput.Value;
        _pc.SpeedFeet      = (int)_speedInput.Value;
        _pc.FortitudeRank  = _fortRank.Selected;
        _pc.ReflexRank     = _refRank.Selected;
        _pc.WillRank       = _willRank.Selected;
        _pc.PerceptionRank = _percRank.Selected;
        _db.Pf2eCharacters.Edit(_pc);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private void ConfigureSpinBoxes()
    {
        _levelInput.MinValue = 1;      _levelInput.MaxValue = 20;
        _currentHpInput.MinValue = 0;  _currentHpInput.MaxValue = 999;
        _maxHpInput.MinValue = 0;      _maxHpInput.MaxValue = 999;
        _acInput.MinValue = 0;         _acInput.MaxValue = 99;
        _speedInput.MinValue = 0;      _speedInput.MaxValue = 999;
        foreach (var (sb, _) in AbilityPairs()) { sb.MinValue = 1; sb.MaxValue = 30; }
    }

    private int ScoreFor(string abbr) => abbr switch
    {
        "STR" => (int)_strInput.Value,
        "DEX" => (int)_dexInput.Value,
        "CON" => (int)_conInput.Value,
        "INT" => (int)_intInput.Value,
        "WIS" => (int)_wisInput.Value,
        "CHA" => (int)_chaInput.Value,
        _     => 10
    };

    private IEnumerable<(SpinBox, Label)> AbilityPairs() => new[]
    {
        (_strInput, _strMod), (_dexInput, _dexMod), (_conInput, _conMod),
        (_intInput, _intMod), (_wisInput, _wisMod), (_chaInput, _chaMod),
    };

    private static int?    NullIfZero(int id) => id == 0 ? (int?)null : id;
    private static int     Mod(int score) => (int)Math.Floor((score - 10) / 2.0);
    private static int     ProfBonus(int rankValue, int level) => rankValue > 0 ? level + rankValue * 2 : 0;
    private static string  ModStr(int score) => SignStr(Mod(score));
    private static string  SignStr(int v) => v >= 0 ? $"+{v}" : $"{v}";
}
