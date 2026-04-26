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
    [Export] private IntInput      _levelInput;
    [Export] private IntInput      _currentHpInput;
    [Export] private IntInput      _maxHpInput;
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
    [Export] private LineEdit _strInput, _dexInput, _conInput, _intInput, _wisInput, _chaInput;
    [Export] private Label    _strMod,  _dexMod,  _conMod,  _intMod,  _wisMod,  _chaMod;
    [Export] private IntInput _acInput;
    [Export] private IntInput _speedInput;
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

        foreach (var (inp, lbl) in AbilityPairs())
        {
            var input = inp; var l = lbl;
            input.TextChanged += text => { if (_loading) return; if (int.TryParse(text, out int v) && v >= 1 && v <= 30) { l.Text = ModStr(v); SavePf2e(); RefreshSaveMods(); LoadSkills(); } };
            input.FocusExited += () => { int val = ParseScore(input.Text); input.Text = val.ToString(); l.Text = ModStr(val); SavePf2e(); RefreshSaveMods(); LoadSkills(); };
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

        _strInput.Text = pc.Strength.ToString();     _strMod.Text = ModStr(pc.Strength);
        _dexInput.Text = pc.Dexterity.ToString();    _dexMod.Text = ModStr(pc.Dexterity);
        _conInput.Text = pc.Constitution.ToString();  _conMod.Text = ModStr(pc.Constitution);
        _intInput.Text = pc.Intelligence.ToString();  _intMod.Text = ModStr(pc.Intelligence);
        _wisInput.Text = pc.Wisdom.ToString();        _wisMod.Text = ModStr(pc.Wisdom);
        _chaInput.Text = pc.Charisma.ToString();      _chaMod.Text = ModStr(pc.Charisma);

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
        int lvl = _levelInput.Value;
        _fortMod.Text = SignStr(Mod(ParseScore(_conInput.Text)) + ProfBonus(_fortRank.Selected, lvl));
        _refMod.Text  = SignStr(Mod(ParseScore(_dexInput.Text)) + ProfBonus(_refRank.Selected,  lvl));
        _willMod.Text = SignStr(Mod(ParseScore(_wisInput.Text)) + ProfBonus(_willRank.Selected,  lvl));
        _percMod.Text = SignStr(Mod(ParseScore(_wisInput.Text)) + ProfBonus(_percRank.Selected,  lvl));
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

        int lvl = _levelInput.Value;
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
                totalLabel.Text = SignStr(capAbilMod + ProfBonus(newRank, _levelInput.Value));
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

    private void LoadAliases() =>
        AliasChipsHelper.Reload(_aliasChipsRow, _db, "pf2e_pc", _pc?.Id ?? 0, _pc?.CampaignId ?? 0, LoadAliases);

    // ── Save helpers ──────────────────────────────────────────────────────────

    private void SavePf2e()
    {
        if (_pc == null) return;
        _pc.AncestryId     = NullIfZero(_ancestryInput.GetItemId(_ancestryInput.Selected));
        _pc.HeritageId     = NullIfZero(_heritageInput.GetItemId(_heritageInput.Selected));
        _pc.ClassId        = NullIfZero(_classInput.GetItemId(_classInput.Selected));
        _pc.Level          = _levelInput.Value;
        _pc.CurrentHp      = _currentHpInput.Value;
        _pc.MaxHp          = _maxHpInput.Value;
        _pc.HeroPoints     = (_heroPip1.ButtonPressed ? 1 : 0) + (_heroPip2.ButtonPressed ? 1 : 0) + (_heroPip3.ButtonPressed ? 1 : 0);
        _pc.Strength       = ParseScore(_strInput.Text);
        _pc.Dexterity      = ParseScore(_dexInput.Text);
        _pc.Constitution   = ParseScore(_conInput.Text);
        _pc.Intelligence   = ParseScore(_intInput.Text);
        _pc.Wisdom         = ParseScore(_wisInput.Text);
        _pc.Charisma       = ParseScore(_chaInput.Text);
        _pc.Ac             = _acInput.Value;
        _pc.SpeedFeet      = _speedInput.Value;
        _pc.FortitudeRank  = _fortRank.Selected;
        _pc.ReflexRank     = _refRank.Selected;
        _pc.WillRank       = _willRank.Selected;
        _pc.PerceptionRank = _percRank.Selected;
        _db.Pf2eCharacters.Edit(_pc);
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private int ScoreFor(string abbr) => abbr switch
    {
        "STR" => ParseScore(_strInput.Text),
        "DEX" => ParseScore(_dexInput.Text),
        "CON" => ParseScore(_conInput.Text),
        "INT" => ParseScore(_intInput.Text),
        "WIS" => ParseScore(_wisInput.Text),
        "CHA" => ParseScore(_chaInput.Text),
        _     => 10
    };

    private IEnumerable<(LineEdit, Label)> AbilityPairs() => new[]
    {
        (_strInput, _strMod), (_dexInput, _dexMod), (_conInput, _conMod),
        (_intInput, _intMod), (_wisInput, _wisMod), (_chaInput, _chaMod),
    };

    private static int     ParseScore(string text) => Math.Clamp(int.TryParse(text, out int v) ? v : 10, 1, 30);
    private static int?    NullIfZero(int id) => id == 0 ? (int?)null : id;
    private static int     Mod(int score) => (int)Math.Floor((score - 10) / 2.0);
    private static int     ProfBonus(int rankValue, int level) => rankValue > 0 ? level + rankValue * 2 : 0;
    private static string  ModStr(int score) => SignStr(Mod(score));
    private static string  SignStr(int v) => v >= 0 ? $"+{v}" : $"{v}";
}
