using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class Pf2eCreatureDetailPane : ScrollContainer
{
    private DatabaseService _db;
    private Pf2eCreature    _creature;
    private string          _activeTab = "Stats";

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit      _nameInput;
    [Export] private Button        _deleteButton;
    [Export] private Button        _statsTabBtn;
    [Export] private Button        _abilitiesTabBtn;
    [Export] private Button        _descriptionTabBtn;
    [Export] private VBoxContainer _statsTab;
    [Export] private VBoxContainer _abilitiesTab;
    [Export] private VBoxContainer _descriptionTab;

    private Dictionary<int, string> _creatureTypeNames = new();
    private Dictionary<int, string> _sizeNames         = new();
    private Dictionary<int, string> _abilityTypeNames  = new();
    private Dictionary<int, string> _actionCostNames   = new();
    private Dictionary<int, string> _damageTypeNames   = new();
    private Dictionary<int, string> _conditionNames    = new();
    private Dictionary<int, string> _traitNames        = new();
    private Dictionary<int, string> _traitDescriptions = new();
    private Dictionary<int, string> _skillNames        = new();
    private Dictionary<int, string> _movementNames     = new();
    private Dictionary<int, string> _senseNames        = new();
    private Dictionary<int, string> _languageNames     = new();
    private Dictionary<int, string> _traditionNames    = new();
    private Dictionary<int, string> _dieTypeNames      = new();
    private int _focusAbilityId = -1;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");


        var confirmDialog = DialogHelper.Make("Delete Creature");
        AddChild(confirmDialog);
        confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "pf2e_creature", _creature?.Id ?? 0);
        _deleteButton.Pressed   += () => DialogHelper.Show(confirmDialog, $"Delete \"{_creature?.Name}\"? This cannot be undone.");

        _nameInput.FocusExited   += () => { if (_creature == null) return; _creature.Name = _nameInput.Text; SaveCreature(); EmitSignal(SignalName.NameChanged, "pf2e_creature", _creature.Id, _creature.Name); };
        _nameInput.TextSubmitted += _ => _nameInput.ReleaseFocus();

        _statsTabBtn.Pressed       += () => SetActiveTab("Stats");
        _abilitiesTabBtn.Pressed   += () => SetActiveTab("Abilities");
        _descriptionTabBtn.Pressed += () => SetActiveTab("Description");
    }

    public void Load(Pf2eCreature creature)
    {
        _creature = creature;
        _nameInput.Text = creature.Name;
        BuildLookups(creature.CampaignId);
        LoadStats();
        LoadAbilities();
        LoadDescription();
        SetActiveTab(_activeTab);
    }

    private void SetActiveTab(string tab)
    {
        _activeTab = tab;
        _statsTab.Visible       = tab == "Stats";
        _abilitiesTab.Visible   = tab == "Abilities";
        _descriptionTab.Visible = tab == "Description";
        _statsTabBtn.SetPressedNoSignal(tab == "Stats");
        _abilitiesTabBtn.SetPressedNoSignal(tab == "Abilities");
        _descriptionTabBtn.SetPressedNoSignal(tab == "Description");
    }

    private void BuildLookups(int cid)
    {
        _creatureTypeNames = _db.Pf2eCreatureTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _sizeNames         = _db.Pf2eSizes.GetAll().ToDictionary(x => x.Id, x => x.Name);
        _abilityTypeNames  = _db.Pf2eAbilityTypes.GetAll().ToDictionary(x => x.Id, x => x.Name);
        _actionCostNames   = _db.Pf2eActionCosts.GetAll().ToDictionary(x => x.Id, x => x.Name);
        _damageTypeNames   = _db.Pf2eDamageTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _conditionNames    = _db.Pf2eConditionTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        var traitList      = _db.Pf2eTraitTypes.GetAll(cid);
        _traitNames        = traitList.ToDictionary(x => x.Id, x => x.Name);
        _traitDescriptions = traitList.ToDictionary(x => x.Id, x => x.Description);
        _skillNames        = _db.Pf2eSkillTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _movementNames     = _db.Pf2eMovementTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _senseNames        = _db.Pf2eSenseTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _languageNames     = _db.Pf2eLanguageTypes.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _traditionNames    = _db.Pf2eTraditions.GetAll(cid).ToDictionary(x => x.Id, x => x.Name);
        _dieTypeNames      = _db.Pf2eDieTypes.GetAll().ToDictionary(x => x.Id, x => x.Name);
    }

    // ── Tab: Stats ────────────────────────────────────────────────────────────

    private void LoadStats()
    {
        ClearContainer(_statsTab);
        int cid = _creature.Id;

        var identRow = new HBoxContainer();
        identRow.AddThemeConstantOverride("separation", 6);
        _statsTab.AddChild(identRow);

        identRow.AddChild(new Label { Text = "Level" });
        var lvlInput = IntInput.Make(_creature.Level, -1, 30, v => { _creature.Level = v; SaveCreature(); });
        lvlInput.CustomMinimumSize = new Vector2(60, 0);
        identRow.AddChild(lvlInput);
        identRow.AddChild(new Label { Text = "·" });
        var sizeOb = MakeTypeDropdown(_sizeNames, _creature.SizeId, id => { _creature.SizeId = id; SaveCreature(); });
        identRow.AddChild(sizeOb);

        // Trait pips with "+" to add
        _statsTab.AddChild(BuildTraitsSection(cid));

        var src = string.IsNullOrEmpty(_creature.Source) ? "" : _creature.Source;
        if (_creature.SourcePage.HasValue) src += $" p.{_creature.SourcePage.Value}";
        if (!string.IsNullOrEmpty(src)) _statsTab.AddChild(MakeDimLabel(src));

        _statsTab.AddChild(new HSeparator());

        // Ability mod grid — horizontal scroll is disabled in .tscn so ExpandFill = 1/6th each
        var grid = new GridContainer { Columns = 6, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 2);
        foreach (var abbr in new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" })
        {
            var h = new Label { Text = abbr, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            h.AddThemeFontSizeOverride("font_size", 10);
            h.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            grid.AddChild(h);
        }
        foreach (var (val, setter) in new (int, Action<int>)[]
        {
            (_creature.StrMod, v => { _creature.StrMod = v; SaveCreature(); }),
            (_creature.DexMod, v => { _creature.DexMod = v; SaveCreature(); }),
            (_creature.ConMod, v => { _creature.ConMod = v; SaveCreature(); }),
            (_creature.IntMod, v => { _creature.IntMod = v; SaveCreature(); }),
            (_creature.WisMod, v => { _creature.WisMod = v; SaveCreature(); }),
            (_creature.ChaMod, v => { _creature.ChaMod = v; SaveCreature(); }),
        })
        {
            var inp = IntInput.Make(val, -99, 99, setter);
            inp.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(inp);
        }
        _statsTab.AddChild(grid);
        _statsTab.AddChild(new HSeparator());

        var combatRow = new HBoxContainer(); combatRow.AddThemeConstantOverride("separation", 16);
        combatRow.AddChild(MakeEditableStatPair("AC", _creature.Ac,    0, 99,   v => { _creature.Ac    = v; SaveCreature(); }));
        combatRow.AddChild(MakeEditableStatPair("HP", _creature.MaxHp, 0, 9999, v => { _creature.MaxHp = v; SaveCreature(); }));
        _statsTab.AddChild(combatRow);

        var savesRow = new HBoxContainer(); savesRow.AddThemeConstantOverride("separation", 16);
        savesRow.AddChild(MakeEditableStatPair("Fort", _creature.Fortitude,  -99, 99, v => { _creature.Fortitude  = v; SaveCreature(); }));
        savesRow.AddChild(MakeEditableStatPair("Ref",  _creature.Reflex,     -99, 99, v => { _creature.Reflex     = v; SaveCreature(); }));
        savesRow.AddChild(MakeEditableStatPair("Will", _creature.Will,       -99, 99, v => { _creature.Will       = v; SaveCreature(); }));
        savesRow.AddChild(MakeEditableStatPair("Perc", _creature.Perception, -99, 99, v => { _creature.Perception = v; SaveCreature(); }));
        _statsTab.AddChild(savesRow);
        _statsTab.AddChild(new HSeparator());

        _statsTab.AddChild(BuildSensesSection(cid));
        _statsTab.AddChild(BuildLanguagesSection(cid));

        _statsTab.AddChild(BuildSkillsSection(cid));

        _statsTab.AddChild(new HSeparator());
        _statsTab.AddChild(BuildSpeedsSection(cid));
        _statsTab.AddChild(BuildImmunitiesSection(cid));
        _statsTab.AddChild(BuildResistancesSection(cid));
        _statsTab.AddChild(BuildWeaknessesSection(cid));
        _statsTab.AddChild(new HSeparator());

        _statsTab.AddChild(MakeDimLabel("Notes"));
        var notesEdit = new TextEdit
        {
            Text                = _creature.Notes,
            PlaceholderText     = "Add notes…",
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 80),
        };
        notesEdit.TextChanged += () => { _creature.Notes = notesEdit.Text; SaveCreature(); };
        _statsTab.AddChild(notesEdit);
    }

    // ── Editable sub-entity sections ──────────────────────────────────────────

    private VBoxContainer BuildTraitsSection(int creatureId)
    {
        var traits   = _db.Pf2eCreatureTraits.GetForCreature(creatureId);
        var section  = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add trait" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var hdr = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 4);
        var hdrLabel = new Label { Text = "Traits" }; hdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        hdr.AddChild(hdrLabel); hdr.AddChild(addToggle);
        section.AddChild(hdr);
        section.AddChild(chipFlow);

        foreach (var t in traits)
        {
            int rId = t.Id; int tId = t.TraitTypeId;
            chipFlow.AddChild(MakeCompactTraitChip(tId, () => _db.Pf2eCreatureTraits.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var ob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var pop = ob.GetPopup();
            var used = _db.Pf2eCreatureTraits.GetForCreature(creatureId).Select(x => x.TraitTypeId).ToHashSet();
            int i = 0;
            foreach (var kv in _traitNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
            {
                ob.AddItem(kv.Value, kv.Key);
                if (_traitDescriptions.TryGetValue(kv.Key, out var d) && !string.IsNullOrWhiteSpace(d))
                    pop.SetItemTooltip(i, d);
                i++;
            }
            if (ob.ItemCount == 0) return;
            ShowAddPopup("Add Trait", ob, () =>
            {
                if (ob.Selected < 0 || ob.ItemCount == 0) return;
                int tId = ob.GetItemId(ob.Selected);
                try
                {
                    int newId = _db.Pf2eCreatureTraits.Add(new Pf2eCreatureTrait { CreatureId = creatureId, TraitTypeId = tId });
                    int capId = tId; int capNewId = newId;
                    var chip = MakeCompactTraitChip(capId, () => _db.Pf2eCreatureTraits.Delete(capNewId));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildSensesSection(int creatureId)
    {
        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var senseRows = new VBoxContainer(); senseRows.AddThemeConstantOverride("separation", 2);
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "Add sense" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var hdr = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 4);
        var hdrLabel = new Label { Text = "Senses" }; hdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        hdr.AddChild(hdrLabel); hdr.AddChild(addToggle);
        section.AddChild(hdr);
        section.AddChild(senseRows);

        void AddSenseRow(int rowId, int senseTypeId, bool isPrecise, int? rangeFeet)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            var nameLabel = new Label { Text = L(_senseNames, senseTypeId), CustomMinimumSize = new Vector2(100, 0), VerticalAlignment = VerticalAlignment.Center };
            int capId = rowId; int capTypeId = senseTypeId; bool capPrecise = isPrecise; int? capRange = rangeFeet;

            void SaveSense() => _db.Pf2eCreatureSenses.Edit(new Pf2eCreatureSense { Id = capId, CreatureId = creatureId, SenseTypeId = capTypeId, IsPrecise = capPrecise, RangeFeet = capRange });

            var rangeSlot = new HBoxContainer(); rangeSlot.AddThemeConstantOverride("separation", 2);
            void ShowRangeInput(int initial)
            {
                foreach (Node child in rangeSlot.GetChildren()) child.QueueFree();
                var inp = IntInput.Make(initial, 0, 999, v => { capRange = v > 0 ? (int?)v : null; SaveSense(); });
                inp.CustomMinimumSize = new Vector2(60, 0);
                rangeSlot.AddChild(inp);
            }
            if (rangeFeet.HasValue && rangeFeet.Value > 0)
                ShowRangeInput(rangeFeet.Value);
            else
            {
                var dash = new Label { Text = "—", VerticalAlignment = VerticalAlignment.Center,
                    MouseDefaultCursorShape = CursorShape.PointingHand, MouseFilter = Control.MouseFilterEnum.Stop,
                    TooltipText = "Click to set range (ft)" };
                dash.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
                dash.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    { dash.AcceptEvent(); ShowRangeInput(0); }
                };
                rangeSlot.AddChild(dash);
            }

            var precChk = new CheckBox { Text = "Precise", ButtonPressed = isPrecise, FocusMode = Control.FocusModeEnum.None };
            precChk.Toggled += v => { capPrecise = v; SaveSense(); };
            var delBtn = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
            var confirmDlg = DialogHelper.Make(text: "Remove this sense? This cannot be undone.");
            confirmDlg.Confirmed += () => { _db.Pf2eCreatureSenses.Delete(capId); row.QueueFree(); };
            row.AddChild(confirmDlg);
            delBtn.Pressed += () => DialogHelper.Show(confirmDlg);
            row.AddChild(nameLabel); row.AddChild(rangeSlot); row.AddChild(precChk); row.AddChild(delBtn);
            senseRows.AddChild(row);
        }

        foreach (var s in _db.Pf2eCreatureSenses.GetForCreature(creatureId))
            AddSenseRow(s.Id, s.SenseTypeId, s.IsPrecise, s.RangeFeet);

        addToggle.Pressed += () =>
        {
            var senseOb = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureSenses.GetForCreature(creatureId).Select(s => s.SenseTypeId).ToHashSet();
            foreach (var kv in _senseNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                senseOb.AddItem(kv.Value, kv.Key);
            if (senseOb.ItemCount == 0) return;
            int rangeVal = 0;
            var rangeInp = IntInput.Make(0, 0, 999, v => rangeVal = v); rangeInp.CustomMinimumSize = new Vector2(60, 0);
            var preciseChk = new CheckBox { Text = "Precise", ButtonPressed = true };
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(senseOb); content.AddChild(rangeInp); content.AddChild(preciseChk);
            ShowAddPopup("Add Sense", content, () =>
            {
                if (senseOb.Selected < 0 || senseOb.ItemCount == 0) return;
                int tId = senseOb.GetItemId(senseOb.Selected); bool precise = preciseChk.ButtonPressed;
                int? range = rangeVal > 0 ? (int?)rangeVal : null;
                try
                {
                    int newId = _db.Pf2eCreatureSenses.Add(new Pf2eCreatureSense { CreatureId = creatureId, SenseTypeId = tId, IsPrecise = precise, RangeFeet = range });
                    AddSenseRow(newId, tId, precise, range);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildLanguagesSection(int creatureId)
    {
        var langs = _db.Pf2eCreatureLanguages.GetForCreature(creatureId);

        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow  = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add language" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var langHdr = new HBoxContainer(); langHdr.AddThemeConstantOverride("separation", 4);
        var langHdrLabel = new Label { Text = "Languages" }; langHdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        langHdr.AddChild(langHdrLabel); langHdr.AddChild(addToggle);
        section.AddChild(langHdr);
        section.AddChild(chipFlow);

        foreach (var lang in langs)
        {
            int rId = lang.Id;
            chipFlow.AddChild(MakeEntityRowChip(TitleCase(L(_languageNames, lang.LanguageTypeId)), () => _db.Pf2eCreatureLanguages.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var ob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureLanguages.GetForCreature(creatureId).Select(l => l.LanguageTypeId).ToHashSet();
            foreach (var kv in _languageNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                ob.AddItem(kv.Value, kv.Key);
            if (ob.ItemCount == 0) return;
            ShowAddPopup("Add Language", ob, () =>
            {
                if (ob.Selected < 0 || ob.ItemCount == 0) return;
                int tId = ob.GetItemId(ob.Selected); string nm = TitleCase(ob.GetItemText(ob.Selected));
                try
                {
                    int newId = _db.Pf2eCreatureLanguages.Add(new Pf2eCreatureLanguage { CreatureId = creatureId, LanguageTypeId = tId });
                    int cid = newId;
                    var chip = MakeEntityRowChip(nm, () => _db.Pf2eCreatureLanguages.Delete(cid));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildSkillsSection(int creatureId)
    {
        var skills = _db.Pf2eCreatureSkills.GetForCreature(creatureId).OrderBy(s => L(_skillNames, s.SkillTypeId)).ToList();

        var section    = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var skillRows  = new VBoxContainer(); skillRows.AddThemeConstantOverride("separation", 2);
        var addToggle  = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "Add skill" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var skillHeader = new HBoxContainer(); skillHeader.AddThemeConstantOverride("separation", 4);
        var skillHeaderLabel = new Label { Text = "Skills" };
        skillHeaderLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        skillHeader.AddChild(skillHeaderLabel);
        skillHeader.AddChild(addToggle);
        section.AddChild(skillHeader);
        section.AddChild(skillRows);

        void AddSkillRow(int rowId, int skillTypeId, int modifier, string notes)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            var nameLabel = new Label { Text = L(_skillNames, skillTypeId), CustomMinimumSize = new Vector2(100, 0), VerticalAlignment = VerticalAlignment.Center };
            int capRowId = rowId; int capTypeId = skillTypeId; string capNotes = notes ?? "";
            var modInp = IntInput.Make(modifier, -99, 99, v =>
                _db.Pf2eCreatureSkills.Edit(new Pf2eCreatureSkill { Id = capRowId, CreatureId = creatureId, SkillTypeId = capTypeId, Modifier = v, Notes = capNotes }));
            modInp.CustomMinimumSize = new Vector2(60, 0);
            var delBtn = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            var confirmDlg = DialogHelper.Make(text: "Remove this skill? This cannot be undone.");
            confirmDlg.Confirmed += () => { _db.Pf2eCreatureSkills.Delete(capRowId); row.QueueFree(); };
            row.AddChild(confirmDlg);
            delBtn.Pressed += () => DialogHelper.Show(confirmDlg);
            row.AddChild(nameLabel); row.AddChild(modInp); row.AddChild(delBtn);
            skillRows.AddChild(row);
        }

        foreach (var s in skills) AddSkillRow(s.Id, s.SkillTypeId, s.Modifier, s.Notes);

        addToggle.Pressed += () =>
        {
            var skillOb = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureSkills.GetForCreature(creatureId).Select(s => s.SkillTypeId).ToHashSet();
            foreach (var kv in _skillNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                skillOb.AddItem(kv.Value, kv.Key);
            if (skillOb.ItemCount == 0) return;
            int modVal = 0;
            var modInp = IntInput.Make(0, -99, 99, v => modVal = v); modInp.CustomMinimumSize = new Vector2(60, 0);
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(skillOb); content.AddChild(modInp);
            ShowAddPopup("Add Skill", content, () =>
            {
                if (skillOb.Selected < 0 || skillOb.ItemCount == 0) return;
                int tId = skillOb.GetItemId(skillOb.Selected);
                try
                {
                    int newId = _db.Pf2eCreatureSkills.Add(new Pf2eCreatureSkill { CreatureId = creatureId, SkillTypeId = tId, Modifier = modVal, Notes = "" });
                    AddSkillRow(newId, tId, modVal, "");
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildSpeedsSection(int creatureId)
    {
        var speeds = _db.Pf2eCreatureSpeeds.GetForCreature(creatureId);

        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow  = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add speed" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var speedHdr = new HBoxContainer(); speedHdr.AddThemeConstantOverride("separation", 4);
        var speedHdrLabel = new Label { Text = "Speed" }; speedHdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        speedHdr.AddChild(speedHdrLabel); speedHdr.AddChild(addToggle);
        section.AddChild(speedHdr);
        section.AddChild(chipFlow);

        foreach (var sp in speeds)
        {
            int rId = sp.Id;
            string chipNm = TitleCase($"{L(_movementNames, sp.MovementTypeId)} {sp.SpeedFeet}ft");
            chipFlow.AddChild(MakeEntityRowChip(chipNm, () => _db.Pf2eCreatureSpeeds.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var typeOb = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureSpeeds.GetForCreature(creatureId).Select(s => s.MovementTypeId).ToHashSet();
            foreach (var kv in _movementNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                typeOb.AddItem(kv.Value, kv.Key);
            if (typeOb.ItemCount == 0) return;
            int feetVal = 25;
            var feetInp = IntInput.Make(25, 0, 999, v => feetVal = v); feetInp.CustomMinimumSize = new Vector2(72, 0);
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(typeOb); content.AddChild(feetInp);
            ShowAddPopup("Add Speed", content, () =>
            {
                if (typeOb.Selected < 0 || typeOb.ItemCount == 0) return;
                int tId = typeOb.GetItemId(typeOb.Selected);
                string chipNm = TitleCase($"{typeOb.GetItemText(typeOb.Selected)} {feetVal}ft");
                try
                {
                    int newId = _db.Pf2eCreatureSpeeds.Add(new Pf2eCreatureSpeed { CreatureId = creatureId, MovementTypeId = tId, SpeedFeet = feetVal, Notes = "" });
                    int cid = newId; string capNm = chipNm;
                    var chip = MakeEntityRowChip(capNm, () => _db.Pf2eCreatureSpeeds.Delete(cid));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildImmunitiesSection(int creatureId)
    {
        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow  = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add immunity" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var immunHdr = new HBoxContainer(); immunHdr.AddThemeConstantOverride("separation", 4);
        var immunHdrLabel = new Label { Text = "Immunities" }; immunHdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        immunHdr.AddChild(immunHdrLabel); immunHdr.AddChild(addToggle);
        section.AddChild(immunHdr);
        section.AddChild(chipFlow);

        foreach (var im in _db.Pf2eCreatureImmunities.GetForCreature(creatureId))
        {
            int rId   = im.Id;
            string nm = TitleCase(im.DamageTypeId.HasValue    ? L(_damageTypeNames, im.DamageTypeId.Value)
                                : im.ConditionTypeId.HasValue ? L(_conditionNames, im.ConditionTypeId.Value)
                                : im.Notes ?? "");
            chipFlow.AddChild(MakeEntityRowChip(nm, () => _db.Pf2eCreatureImmunities.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var dropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var active   = _db.Pf2eCreatureImmunities.GetForCreature(creatureId);
            var usedDmg  = active.Where(x => x.DamageTypeId.HasValue).Select(x => x.DamageTypeId.Value).ToHashSet();
            var usedCond = active.Where(x => x.ConditionTypeId.HasValue).Select(x => x.ConditionTypeId.Value).ToHashSet();
            dropdown.AddSeparator("Damage");
            foreach (var kv in _damageTypeNames.Where(x => !usedDmg.Contains(x.Key)).OrderBy(x => x.Value))
                dropdown.AddItem(kv.Value, kv.Key);
            dropdown.AddSeparator("Conditions");
            foreach (var kv in _conditionNames.Where(x => !usedCond.Contains(x.Key)).OrderBy(x => x.Value))
                dropdown.AddItem(kv.Value, -kv.Key);
            for (int i = 0; i < dropdown.ItemCount; i++) { if (!dropdown.IsItemSeparator(i)) { dropdown.Selected = i; break; } }
            if (dropdown.ItemCount <= 2) return;
            ShowAddPopup("Add Immunity", dropdown, () =>
            {
                if (dropdown.Selected < 0) return;
                int idx = dropdown.Selected; if (dropdown.IsItemSeparator(idx)) return;
                int itemId = dropdown.GetItemId(idx); string nm = TitleCase(dropdown.GetItemText(idx));
                var imm = new Pf2eCreatureImmunity { CreatureId = creatureId };
                if (itemId > 0) imm.DamageTypeId    =  itemId;
                else            imm.ConditionTypeId = -itemId;
                try
                {
                    int newId = _db.Pf2eCreatureImmunities.Add(imm);
                    int cid = newId; string capNm = nm;
                    var chip = MakeEntityRowChip(capNm, () => _db.Pf2eCreatureImmunities.Delete(cid));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildResistancesSection(int creatureId)
    {
        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow  = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add resistance" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var resistHdr = new HBoxContainer(); resistHdr.AddThemeConstantOverride("separation", 4);
        var resistHdrLabel = new Label { Text = "Resistances" }; resistHdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        resistHdr.AddChild(resistHdrLabel); resistHdr.AddChild(addToggle);
        section.AddChild(resistHdr);
        section.AddChild(chipFlow);

        foreach (var r in _db.Pf2eCreatureResistances.GetForCreature(creatureId))
        {
            int rId = r.Id;
            string nm = TitleCase($"{L(_damageTypeNames, r.DamageTypeId)} {r.Value}");
            chipFlow.AddChild(MakeEntityRowChip(nm, () => _db.Pf2eCreatureResistances.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var ob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureResistances.GetForCreature(creatureId).Select(r => r.DamageTypeId).ToHashSet();
            foreach (var kv in _damageTypeNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                ob.AddItem(kv.Value, kv.Key);
            if (ob.ItemCount == 0) return;
            int resVal = 5;
            var valInp = IntInput.Make(5, 0, 999, v => resVal = v); valInp.CustomMinimumSize = new Vector2(60, 0);
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(ob); content.AddChild(valInp);
            ShowAddPopup("Add Resistance", content, () =>
            {
                if (ob.Selected < 0 || ob.ItemCount == 0) return;
                int tId = ob.GetItemId(ob.Selected);
                string nm = TitleCase($"{ob.GetItemText(ob.Selected)} {resVal}");
                try
                {
                    int newId = _db.Pf2eCreatureResistances.Add(new Pf2eCreatureResistance { CreatureId = creatureId, DamageTypeId = tId, Value = resVal });
                    int cid = newId; string capNm = nm;
                    var chip = MakeEntityRowChip(capNm, () => _db.Pf2eCreatureResistances.Delete(cid));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    private VBoxContainer BuildWeaknessesSection(int creatureId)
    {
        var section   = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var chipFlow  = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add weakness" };

        section.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var weakHdr = new HBoxContainer(); weakHdr.AddThemeConstantOverride("separation", 4);
        var weakHdrLabel = new Label { Text = "Weaknesses" }; weakHdrLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        weakHdr.AddChild(weakHdrLabel); weakHdr.AddChild(addToggle);
        section.AddChild(weakHdr);
        section.AddChild(chipFlow);

        foreach (var w in _db.Pf2eCreatureWeaknesses.GetForCreature(creatureId))
        {
            int rId = w.Id;
            string nm = TitleCase($"{L(_damageTypeNames, w.DamageTypeId)} {w.Value}");
            chipFlow.AddChild(MakeEntityRowChip(nm, () => _db.Pf2eCreatureWeaknesses.Delete(rId)));
        }

        addToggle.Pressed += () =>
        {
            var ob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var used = _db.Pf2eCreatureWeaknesses.GetForCreature(creatureId).Select(w => w.DamageTypeId).ToHashSet();
            foreach (var kv in _damageTypeNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
                ob.AddItem(kv.Value, kv.Key);
            if (ob.ItemCount == 0) return;
            int weakVal = 5;
            var valInp = IntInput.Make(5, 0, 999, v => weakVal = v); valInp.CustomMinimumSize = new Vector2(60, 0);
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(ob); content.AddChild(valInp);
            ShowAddPopup("Add Weakness", content, () =>
            {
                if (ob.Selected < 0 || ob.ItemCount == 0) return;
                int tId = ob.GetItemId(ob.Selected);
                string nm = TitleCase($"{ob.GetItemText(ob.Selected)} {weakVal}");
                try
                {
                    int newId = _db.Pf2eCreatureWeaknesses.Add(new Pf2eCreatureWeakness { CreatureId = creatureId, DamageTypeId = tId, Value = weakVal });
                    int cid = newId; string capNm = nm;
                    var chip = MakeEntityRowChip(capNm, () => _db.Pf2eCreatureWeaknesses.Delete(cid));
                    chipFlow.AddChild(chip);
                }
                catch { }
            });
        };

        return section;
    }

    // ── Tab: Abilities ────────────────────────────────────────────────────────

    private void LoadAbilities()
    {
        ClearContainer(_abilitiesTab);
        int creatureId = _creature.Id;

        // Add-new-ability button
        var addToggle = new Button { Text = "+ New Ability", FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        addToggle.Pressed += () =>
        {
            int defTypeId = _abilityTypeNames.FirstOrDefault(x => x.Value == "Passive").Key;
            int defCostId = _actionCostNames.FirstOrDefault(x => x.Value == "None").Key;
            if (defTypeId == 0) defTypeId = _abilityTypeNames.Keys.FirstOrDefault();
            if (defCostId == 0) defCostId = _actionCostNames.Keys.FirstOrDefault();
            var nameEd = new LineEdit { PlaceholderText = "Ability name", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(200, 0) };
            var typeOb = MakeTypeDropdown(_abilityTypeNames, defTypeId, _ => { });
            var costOb = MakeTypeDropdown(_actionCostNames,  defCostId, _ => { });
            var content = new HBoxContainer(); content.AddThemeConstantOverride("separation", 4);
            content.AddChild(nameEd); content.AddChild(typeOb); content.AddChild(costOb);
            ShowAddPopup("New Ability", content, () =>
            {
                string nm = string.IsNullOrWhiteSpace(nameEd.Text) ? "New Ability" : nameEd.Text;
                int tId = (int)typeOb.GetItemId(typeOb.Selected);
                int cId = (int)costOb.GetItemId(costOb.Selected);
                int sort = _db.Pf2eCreatureAbilities.GetForCreature(creatureId).Count;
                try { _focusAbilityId = _db.Pf2eCreatureAbilities.Add(new Pf2eCreatureAbility { CreatureId = creatureId, Name = nm, AbilityTypeId = tId, ActionCostId = cId, SortOrder = sort }); LoadAbilities(); }
                catch { }
            });
        };
        _abilitiesTab.AddChild(addToggle);

        var all = _db.Pf2eCreatureAbilities.GetForCreature(creatureId);
        if (all.Count == 0) return;

        // Sort priority: Strike → Passive → Active → Reactive → custom types alphabetically → Innate Spell
        static int TypeOrder(string t) => t switch
        {
            "Strike"      => 0,
            "Passive"     => 1,
            "Active"      => 2,
            "Reactive"    => 3,
            "Innate Spell"=> 10,
            _             => 5,
        };
        static string TypeHeader(string t) => t switch
        {
            "Strike"      => "Strikes",
            "Passive"     => "Passive Abilities",
            "Innate Spell"=> "Innate Spells",
            _             => $"{t} Abilities",
        };

        var groups = all
            .GroupBy(a => a.AbilityTypeId)
            .OrderBy(g => TypeOrder(L(_abilityTypeNames, g.Key)))
            .ThenBy(g => L(_abilityTypeNames, g.Key));

        bool sep = false;
        foreach (var grp in groups)
        {
            string header = TypeHeader(L(_abilityTypeNames, grp.Key));
            if (sep) _abilitiesTab.AddChild(MakeSectionSpacer());
            _abilitiesTab.AddChild(MakeSectionHeader(header));
            foreach (var a in grp.OrderBy(x => x.SortOrder))
            {
                bool isNew = a.Id == _focusAbilityId;
                var row = MakeEditableAbilityRow(a, startOpen: isNew, startEdit: isNew);
                _abilitiesTab.AddChild(row);
                if (isNew) Callable.From(() => EnsureControlVisible(row)).CallDeferred();
            }
            sep = true;
        }
        _focusAbilityId = -1;
    }

    private Control MakeEditableAbilityRow(Pf2eCreatureAbility a, bool startOpen = false, bool startEdit = false)
    {
        int abilityId = a.Id;
        bool folded   = !startOpen;
        bool editMode = startEdit;
        const int R   = 5;

        StyleBoxFlat MakeHdrBox(bool hover, bool closed) => new StyleBoxFlat
        {
            BgColor                 = hover ? ThemeManager.Instance.Current.Hover : new Color(1, 1, 1, 0.06f),
            ContentMarginLeft       = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft     = R, CornerRadiusTopRight    = R,
            CornerRadiusBottomLeft  = closed ? R : 0,
            CornerRadiusBottomRight = closed ? R : 0,
        };

        // ── Card ──────────────────────────────────────────────────────────────
        var card = new VBoxContainer(); card.SizeFlagsHorizontal = SizeFlags.ExpandFill; card.AddThemeConstantOverride("separation", 0);
        var dlg = DialogHelper.Make(text: "Delete this ability? This cannot be undone.");
        dlg.Confirmed += () => { _db.Pf2eCreatureAbilities.Delete(a.Id); card.QueueFree(); };
        card.AddChild(dlg);

        // ── Header ────────────────────────────────────────────────────────────
        var hdrPanel = new PanelContainer(); hdrPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hdrPanel.MouseDefaultCursorShape = CursorShape.PointingHand;
        hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(false, true));

        var hdrHbox = new HBoxContainer(); hdrHbox.AddThemeConstantOverride("separation", 6);
        hdrHbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        var arrow = new Label { Text = "▶", VerticalAlignment = VerticalAlignment.Center };
        arrow.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        arrow.MouseFilter = Control.MouseFilterEnum.Ignore;

        var nameLabel = new Label { Text = string.IsNullOrEmpty(a.Name) ? "New Ability" : a.Name, VerticalAlignment = VerticalAlignment.Center };
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var editBgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.14f, 0.18f),
            ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 1, ContentMarginBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        };
        float EstW(string t) => Mathf.Max(60f, t.Length * 9f + 20f);
        var nameEdit = new LineEdit { Text = a.Name, Visible = false, PlaceholderText = "Ability name", CustomMinimumSize = new Vector2(EstW(a.Name), 0) };
        nameEdit.AddThemeStyleboxOverride("normal", editBgStyle);
        nameEdit.AddThemeStyleboxOverride("focus",  editBgStyle);
        nameEdit.AddThemeStyleboxOverride("hover",  editBgStyle);
        nameEdit.TextChanged   += t => nameEdit.CustomMinimumSize = new Vector2(EstW(t), 0);
        nameEdit.FocusExited   += () => { string nm = string.IsNullOrWhiteSpace(nameEdit.Text) ? "New Ability" : nameEdit.Text; a.Name = nm; nameLabel.Text = nm; _db.Pf2eCreatureAbilities.Edit(a); };
        nameEdit.TextSubmitted += _ => nameEdit.ReleaseFocus();

        var costRow = new HBoxContainer(); costRow.AddThemeConstantOverride("separation", 2); costRow.MouseFilter = Control.MouseFilterEnum.Ignore;
        BuildActionCostDisplay(costRow, a.ActionCostId);

        // ✏/✓ pencil — lives in body right column, always at top-right corner
        var pencilBtn = new Button { Text = "✏", Flat = true, FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "Edit" };

        // × hidden until hover; header turns red when hovering ×
        var delBtn = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        delBtn.Modulate = new Color(1, 1, 1, 0);
        delBtn.Pressed += () => DialogHelper.Show(dlg);

        var hdrSpacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        hdrHbox.AddChild(arrow); hdrHbox.AddChild(nameLabel); hdrHbox.AddChild(nameEdit); hdrHbox.AddChild(hdrSpacer); hdrHbox.AddChild(costRow); hdrHbox.AddChild(delBtn);
        hdrPanel.AddChild(hdrHbox);

        // ── Body ──────────────────────────────────────────────────────────────
        var normalBodyStyle = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.NavBar, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6, CornerRadiusBottomLeft = R, CornerRadiusBottomRight = R };
        var redBodyStyle    = new StyleBoxFlat { BgColor = new Color(0.36f, 0.08f, 0.08f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6, CornerRadiusBottomLeft = R, CornerRadiusBottomRight = R };

        var bodyPanel = new PanelContainer(); bodyPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill; bodyPanel.Visible = false;
        bodyPanel.AddThemeStyleboxOverride("panel", normalBodyStyle);

        // Pencil always sits in top-right of body; body content fills the left column
        var bodyWrapper = new HBoxContainer(); bodyWrapper.AddThemeConstantOverride("separation", 0);
        var body = new VBoxContainer(); body.AddThemeConstantOverride("separation", 6); body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pencilCol = new VBoxContainer(); pencilCol.AddThemeConstantOverride("separation", 0);
        pencilCol.AddChild(pencilBtn);
        pencilCol.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        bodyWrapper.AddChild(body); bodyWrapper.AddChild(pencilCol);
        bodyPanel.AddChild(bodyWrapper);

        // ── Header / body update helpers ──────────────────────────────────────
        void UpdateHeader()
        {
            nameLabel.Visible    = !editMode;
            nameEdit.Visible     =  editMode;
            pencilBtn.Text       = editMode ? "✓" : "✏";
            pencilBtn.TooltipText = editMode ? "Done" : "Edit";
            if (editMode && nameEdit.IsInsideTree() && !nameEdit.HasFocus()) nameEdit.GrabFocus();
        }

        pencilBtn.Pressed += () => { editMode = !editMode; UpdateHeader(); RebuildBody(); };

        StyleBoxFlat MakeRedHdrBox(bool closed) => new StyleBoxFlat
        {
            BgColor                 = ThemeManager.DeleteHoverColor,
            ContentMarginLeft       = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft     = R, CornerRadiusTopRight    = R,
            CornerRadiusBottomLeft  = closed ? R : 0,
            CornerRadiusBottomRight = closed ? R : 0,
        };

        void RebuildBody()
        {
            foreach (Node child in body.GetChildren()) child.QueueFree();

            if (editMode)
            {
                var traitSec = BuildAbilityTraitsSection(abilityId);
                traitSec.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                body.AddChild(traitSec);
                BuildAbilityEditContent(body, a, abilityId);
            }
            else
            {
                var traits = _db.Pf2eAbilityTraits.GetForAbility(abilityId);
                if (traits.Count > 0)
                {
                    var traitFlow = MakeChipFlow();
                    foreach (var t in traits) traitFlow.AddChild(MakeTraitPill(t.TraitTypeId));
                    body.AddChild(traitFlow);
                }
                BuildAbilityViewContent(body, a, abilityId);
            }
        }

        void SetFolded(bool f)
        {
            folded             = f;
            bodyPanel.Visible  = !f;
            arrow.Text         = f ? "▶" : "▼";
            hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(false, f));
            if (f)
            {
                editMode = false;
                UpdateHeader();
                foreach (Node child in body.GetChildren()) child.QueueFree();
            }
            else if (body.GetChildCount() == 0)
            {
                RebuildBody();
            }
        }

        hdrPanel.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            { hdrPanel.AcceptEvent(); SetFolded(!folded); }
        };
        hdrPanel.MouseEntered += () => { delBtn.Modulate = Colors.White; hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(true,  folded)); };
        hdrPanel.MouseExited  += () => { delBtn.Modulate = new Color(1, 1, 1, 0); hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(false, folded)); bodyPanel.AddThemeStyleboxOverride("panel", normalBodyStyle); };
        // Godot fires parent MouseExited before child MouseEntered — re-show delBtn here and tint body
        delBtn.MouseEntered   += () => { delBtn.Modulate = Colors.White; hdrPanel.AddThemeStyleboxOverride("panel", MakeRedHdrBox(folded)); bodyPanel.AddThemeStyleboxOverride("panel", redBodyStyle); };
        delBtn.MouseExited    += () => { hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(true, folded)); bodyPanel.AddThemeStyleboxOverride("panel", normalBodyStyle); };

        if (startOpen)
        {
            bodyPanel.Visible = true;
            arrow.Text        = "▼";
            hdrPanel.AddThemeStyleboxOverride("panel", MakeHdrBox(false, false));
            UpdateHeader();
            RebuildBody();
            if (editMode) nameEdit.CallDeferred(Control.MethodName.GrabFocus);
        }

        card.AddChild(hdrPanel);
        card.AddChild(bodyPanel);
        return card;
    }

    private void BuildAbilityViewContent(VBoxContainer body, Pf2eCreatureAbility a, int abilityId)
    {
        string typeName = L(_abilityTypeNames, a.AbilityTypeId);
        if (typeName == "Strike")
        {
            if (a.IsMelee.HasValue || a.RangeFeet.HasValue)
            {
                string typeStr  = a.IsMelee.HasValue ? (a.IsMelee.Value == 1 ? "Melee" : "Ranged") : "";
                string rangeStr = a.RangeFeet.HasValue ? $"  Range {a.RangeFeet}ft" : "";
                body.AddChild(MakeBodyRow("Type", $"{typeStr}{rangeStr}".Trim()));
            }
            if (a.AttackBonus.HasValue)
            {
                string s = Pf2eMath.SignStr(a.AttackBonus.Value);
                if (a.AttackBonus2.HasValue) s += $" / {Pf2eMath.SignStr(a.AttackBonus2.Value)}";
                if (a.AttackBonus3.HasValue) s += $" / {Pf2eMath.SignStr(a.AttackBonus3.Value)}";
                body.AddChild(MakeBodyRow("Attack", s));
            }
            var dmgs = _db.Pf2eStrikeDamage.GetForAbility(abilityId);
            if (dmgs.Count > 0)
            {
                var parts = dmgs.Select(d =>
                {
                    string die = L(_dieTypeNames, d.DieTypeId); string tp = L(_damageTypeNames, d.DamageTypeId);
                    string bon = d.Bonus > 0 ? $"+{d.Bonus}" : d.Bonus < 0 ? d.Bonus.ToString() : "";
                    return $"{d.DiceCount}{die}{bon} {tp}";
                });
                body.AddChild(MakeBodyRow("Damage", string.Join(" + ", parts)));
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(a.Trigger)) body.AddChild(MakeBodyRow("Trigger", a.Trigger));
            if (a.TraditionId.HasValue)
            {
                string s = L(_traditionNames, a.TraditionId.Value);
                if (a.SpellDc.HasValue)     s += $"  DC {a.SpellDc}";
                if (a.SpellAttack.HasValue) s += $"  Atk {Pf2eMath.SignStr(a.SpellAttack.Value)}";
                body.AddChild(MakeBodyRow("Tradition", s));
            }
        }

        string eff = CleanText(a.EffectText);
        if (!string.IsNullOrEmpty(eff))
        {
            var lbl = new Label { Text = eff, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            body.AddChild(lbl);
        }
    }

    private void BuildAbilityEditContent(VBoxContainer body, Pf2eCreatureAbility a, int abilityId)
    {
        string typeName = L(_abilityTypeNames, a.AbilityTypeId);
        if (typeName == "Strike")
        {
            var meleeRow = new HBoxContainer(); meleeRow.AddThemeConstantOverride("separation", 8);
            var meleeLabel = new Label { Text = "Type:", VerticalAlignment = VerticalAlignment.Center }; meleeLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var meleeOb = new OptionButton();
            meleeOb.AddItem("—", 0); meleeOb.AddItem("Melee", 1); meleeOb.AddItem("Ranged", 2);
            meleeOb.Selected = !a.IsMelee.HasValue ? 0 : a.IsMelee.Value == 1 ? 1 : 2;
            meleeOb.ItemSelected += idx => { int sel = (int)idx; a.IsMelee = sel == 0 ? (int?)null : sel == 1 ? 1 : 0; _db.Pf2eCreatureAbilities.Edit(a); };
            var rangeLabel = new Label { Text = "Range:", VerticalAlignment = VerticalAlignment.Center }; rangeLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var rangeInp = IntInput.Make(a.RangeFeet ?? 0, 0, 9999, v => { a.RangeFeet = v > 0 ? (int?)v : null; _db.Pf2eCreatureAbilities.Edit(a); }); rangeInp.CustomMinimumSize = new Vector2(72, 0);
            var rangeFtLbl = new Label { Text = "ft", VerticalAlignment = VerticalAlignment.Center };
            meleeRow.AddChild(meleeLabel); meleeRow.AddChild(meleeOb); meleeRow.AddChild(rangeLabel); meleeRow.AddChild(rangeInp); meleeRow.AddChild(rangeFtLbl);
            body.AddChild(meleeRow);

            var atkRow = new HBoxContainer(); atkRow.AddThemeConstantOverride("separation", 4);
            var atkLabel = new Label { Text = "Attack:", VerticalAlignment = VerticalAlignment.Center }; atkLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var atk1 = IntInput.Make(a.AttackBonus  ?? 0, -99, 99, v => { a.AttackBonus  = v; _db.Pf2eCreatureAbilities.Edit(a); }); atk1.CustomMinimumSize = new Vector2(64, 0);
            var atk2 = IntInput.Make(a.AttackBonus2 ?? 0, -99, 99, v => { a.AttackBonus2 = v; _db.Pf2eCreatureAbilities.Edit(a); }); atk2.CustomMinimumSize = new Vector2(64, 0);
            var atk3 = IntInput.Make(a.AttackBonus3 ?? 0, -99, 99, v => { a.AttackBonus3 = v; _db.Pf2eCreatureAbilities.Edit(a); }); atk3.CustomMinimumSize = new Vector2(64, 0);
            atkRow.AddChild(atkLabel); atkRow.AddChild(atk1); atkRow.AddChild(new Label { Text = "/" }); atkRow.AddChild(atk2); atkRow.AddChild(new Label { Text = "/" }); atkRow.AddChild(atk3);
            body.AddChild(atkRow);
            body.AddChild(BuildStrikeDamageSection(abilityId));
        }

        if (typeName == "Innate Spell")
        {
            var tradRow = new HBoxContainer(); tradRow.AddThemeConstantOverride("separation", 8);
            var tradLabel = new Label { Text = "Tradition:", VerticalAlignment = VerticalAlignment.Center }; tradLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var tradOb   = MakeTypeDropdown(_traditionNames, a.TraditionId ?? 0, id => { a.TraditionId = id > 0 ? (int?)id : null; _db.Pf2eCreatureAbilities.Edit(a); });
            var dcLabel  = new Label { Text = "DC:",  VerticalAlignment = VerticalAlignment.Center }; dcLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var dcInp    = IntInput.Make(a.SpellDc     ?? 0,   0, 99, v => { a.SpellDc     = v > 0 ? (int?)v : null; _db.Pf2eCreatureAbilities.Edit(a); }); dcInp.CustomMinimumSize    = new Vector2(60, 0);
            var spAtkLbl = new Label { Text = "Atk:", VerticalAlignment = VerticalAlignment.Center }; spAtkLbl.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var spAtkInp = IntInput.Make(a.SpellAttack ?? 0, -99, 99, v => { a.SpellAttack = v;            _db.Pf2eCreatureAbilities.Edit(a); }); spAtkInp.CustomMinimumSize = new Vector2(60, 0);
            tradRow.AddChild(tradLabel); tradRow.AddChild(tradOb); tradRow.AddChild(dcLabel); tradRow.AddChild(dcInp); tradRow.AddChild(spAtkLbl); tradRow.AddChild(spAtkInp);
            body.AddChild(tradRow);
        }

        if (typeName != "Strike")
        {
            var trigRow = new HBoxContainer(); trigRow.AddThemeConstantOverride("separation", 6);
            var trigLabel = new Label { Text = "Trigger:", VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(60, 0) }; trigLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var trigEd = new LineEdit { Text = a.Trigger ?? "", PlaceholderText = "Trigger condition…", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            trigEd.FocusExited   += () => { a.Trigger = string.IsNullOrEmpty(trigEd.Text) ? null : trigEd.Text; _db.Pf2eCreatureAbilities.Edit(a); };
            trigEd.TextSubmitted += _ => trigEd.ReleaseFocus();
            trigRow.AddChild(trigLabel); trigRow.AddChild(trigEd);
            body.AddChild(trigRow);
        }

        var effEd = new TextEdit { Text = CleanText(a.EffectText), PlaceholderText = "Effect description…", WrapMode = TextEdit.LineWrappingMode.Boundary, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 60) };
        effEd.TextChanged += () => { a.EffectText = effEd.Text; _db.Pf2eCreatureAbilities.Edit(a); };
        body.AddChild(effEd);
    }

    private void BuildActionCostDisplay(HBoxContainer container, int actionCostId)
    {
        string cost = L(_actionCostNames, actionCostId);
        var (sym, count) = cost switch
        {
            "1 Action"    => ("●", 1),
            "2 Actions"   => ("●", 2),
            "3 Actions"   => ("●", 3),
            "Free Action" => ("◆", 1),
            "Reaction"    => ("↺", 1),
            _             => ("",  0),
        };
        for (int i = 0; i < count; i++)
        {
            var dot = new Label { Text = sym, VerticalAlignment = VerticalAlignment.Center };
            dot.AddThemeColorOverride("font_color", new Color(0.85f, 0.25f, 0.25f));
            dot.AddThemeFontSizeOverride("font_size", 12);
            dot.MouseFilter = Control.MouseFilterEnum.Ignore;
            container.AddChild(dot);
        }
    }

    private VBoxContainer BuildAbilityTraitsSection(int abilityId)
    {
        var section  = new VBoxContainer(); section.AddThemeConstantOverride("separation", 2);
        var chipFlow = MakeChipFlow();
        var addToggle = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = "Add trait" };

        foreach (var t in _db.Pf2eAbilityTraits.GetForAbility(abilityId))
        {
            int rId = t.Id;
            chipFlow.AddChild(MakeTraitEntityChip(t.TraitTypeId, () => _db.Pf2eAbilityTraits.Delete(rId)));
        }
        chipFlow.AddChild(addToggle);

        addToggle.Pressed += () =>
        {
            var ob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var pop = ob.GetPopup();
            var used = _db.Pf2eAbilityTraits.GetForAbility(abilityId).Select(t => t.TraitTypeId).ToHashSet();
            int i = 0;
            foreach (var kv in _traitNames.Where(x => !used.Contains(x.Key)).OrderBy(x => x.Value))
            {
                ob.AddItem(kv.Value, kv.Key);
                if (_traitDescriptions.TryGetValue(kv.Key, out var d) && !string.IsNullOrWhiteSpace(d))
                    pop.SetItemTooltip(i, d);
                i++;
            }
            if (ob.ItemCount == 0) return;
            ShowAddPopup("Add Trait", ob, () =>
            {
                if (ob.Selected < 0 || ob.ItemCount == 0) return;
                int tId = ob.GetItemId(ob.Selected);
                try
                {
                    int newId = _db.Pf2eAbilityTraits.Add(new Pf2eAbilityTrait { AbilityId = abilityId, TraitTypeId = tId });
                    int cId = newId; int capTId = tId;
                    var chip = MakeTraitEntityChip(capTId, () => _db.Pf2eAbilityTraits.Delete(cId));
                    chipFlow.AddChild(chip);
                    chipFlow.MoveChild(addToggle, chipFlow.GetChildCount() - 1);
                }
                catch { }
            });
        };

        section.AddChild(chipFlow);
        return section;
    }

    private VBoxContainer BuildStrikeDamageSection(int abilityId)
    {
        var section = new VBoxContainer(); section.AddThemeConstantOverride("separation", 4);
        var dmgRows = new VBoxContainer(); dmgRows.AddThemeConstantOverride("separation", 2);

        var hdr = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 4);
        var hdrLabel = new Label { Text = "Damage" };
        hdrLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        var addBtn = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        hdr.AddChild(hdrLabel); hdr.AddChild(addBtn);
        section.AddChild(hdr);
        section.AddChild(dmgRows);

        void AddDmgRow(Pf2eStrikeDamage d)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4);

            var diceInp = IntInput.Make(d.DiceCount, 0, 99, v => { d.DiceCount = v; _db.Pf2eStrikeDamage.Edit(d); });
            diceInp.CustomMinimumSize = new Vector2(52, 0);

            var dieOb    = MakeTypeDropdown(_dieTypeNames,    d.DieTypeId,    id => { d.DieTypeId    = id; _db.Pf2eStrikeDamage.Edit(d); });
            var dmgTypeOb = MakeTypeDropdown(_damageTypeNames, d.DamageTypeId, id => { d.DamageTypeId = id; _db.Pf2eStrikeDamage.Edit(d); });

            var bonLabel = new Label { Text = "+", VerticalAlignment = VerticalAlignment.Center };
            bonLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
            var bonInp = IntInput.Make(d.Bonus, -99, 99, v => { d.Bonus = v; _db.Pf2eStrikeDamage.Edit(d); });
            bonInp.CustomMinimumSize = new Vector2(52, 0);

            var delBtn = new Button { Text = "×", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
            var confirmDlg = DialogHelper.Make(text: "Remove this damage entry? This cannot be undone.");
            int capId = d.Id;
            confirmDlg.Confirmed += () => { _db.Pf2eStrikeDamage.Delete(capId); row.QueueFree(); };
            row.AddChild(confirmDlg);
            delBtn.Pressed += () => DialogHelper.Show(confirmDlg);

            row.AddChild(diceInp); row.AddChild(dieOb); row.AddChild(dmgTypeOb); row.AddChild(bonLabel); row.AddChild(bonInp); row.AddChild(delBtn);
            dmgRows.AddChild(row);
        }

        foreach (var d in _db.Pf2eStrikeDamage.GetForAbility(abilityId))
            AddDmgRow(d);

        addBtn.Pressed += () =>
        {
            int firstDieId = _dieTypeNames.Keys.FirstOrDefault();
            int firstDmgId = _damageTypeNames.Keys.FirstOrDefault();
            try
            {
                var newDmg = new Pf2eStrikeDamage { AbilityId = abilityId, DiceCount = 1, DieTypeId = firstDieId, DamageTypeId = firstDmgId, Bonus = 0, IsPrimary = dmgRows.GetChildCount() == 0 };
                newDmg.Id = _db.Pf2eStrikeDamage.Add(newDmg);
                AddDmgRow(newDmg);
            }
            catch { }
        };

        return section;
    }

    private void ShowAddPopup(string title, Control content, Action onConfirm)
    {
        var dlg = new ConfirmationDialog();
        dlg.Title = title;
        dlg.GetOkButton().Text = "Add";
        dlg.AddChild(content);
        AddChild(dlg);
        dlg.Confirmed      += () => { onConfirm(); dlg.QueueFree(); };
        dlg.Canceled       += () => dlg.QueueFree();
        dlg.CloseRequested += () => dlg.QueueFree();
        dlg.PopupCentered();
    }

    private string AbilityTitle(Pf2eCreatureAbility a)
    {
        string nm   = string.IsNullOrEmpty(a.Name) ? "New Ability" : a.Name;
        string type = L(_abilityTypeNames, a.AbilityTypeId);
        if (type == "Strike")
            return a.IsMelee.HasValue ? $"{nm}  [{(a.IsMelee.Value == 1 ? "Melee" : "Ranged")}]" : nm;
        string cost = L(_actionCostNames, a.ActionCostId);
        return !string.IsNullOrEmpty(cost) && cost != "None" ? $"{nm}  ({cost})" : nm;
    }

    // ── Tab: Description ──────────────────────────────────────────────────────

    private void LoadDescription()
    {
        ClearContainer(_descriptionTab);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.SizeFlagsVertical   = SizeFlags.ExpandFill;
        _descriptionTab.AddChild(hbox);

        var carousel = new ImageCarousel();
        hbox.AddChild(carousel);
        carousel.Setup(EntityType.Pf2eCreature, _creature.Id, _db);

        var descEdit = new TextEdit
        {
            Text                = CleanText(_creature.Description),
            PlaceholderText     = "Add a description…",
            WrapMode            = TextEdit.LineWrappingMode.Boundary,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 120),
        };
        descEdit.TextChanged += () => { _creature.Description = descEdit.Text; SaveCreature(); };
        hbox.AddChild(descEdit);
    }

    // ── Ability card style ────────────────────────────────────────────────────

    private static void StyleAbilityCard(FoldableContainer fc)
    {
        const int R = 5;
        var titleOpen = new StyleBoxFlat { BgColor = new Color(0.20f, 0.22f, 0.28f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft = R, CornerRadiusTopRight = R };
        var titleClosed = new StyleBoxFlat { BgColor = new Color(0.20f, 0.22f, 0.28f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft = R, CornerRadiusTopRight = R, CornerRadiusBottomLeft = R, CornerRadiusBottomRight = R };
        var hoverOpen = new StyleBoxFlat { BgColor = new Color(0.26f, 0.28f, 0.35f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft = R, CornerRadiusTopRight = R };
        var hoverClosed = new StyleBoxFlat { BgColor = new Color(0.26f, 0.28f, 0.35f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 5, ContentMarginBottom = 5,
            CornerRadiusTopLeft = R, CornerRadiusTopRight = R, CornerRadiusBottomLeft = R, CornerRadiusBottomRight = R };
        var panel = new StyleBoxFlat { BgColor = new Color(0.15f, 0.17f, 0.22f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 6, ContentMarginBottom = 6,
            CornerRadiusBottomLeft = R, CornerRadiusBottomRight = R };
        fc.AddThemeStyleboxOverride("title_panel",                 titleOpen);
        fc.AddThemeStyleboxOverride("title_hover_panel",           hoverOpen);
        fc.AddThemeStyleboxOverride("title_collapsed_panel",       titleClosed);
        fc.AddThemeStyleboxOverride("title_collapsed_hover_panel", hoverClosed);
        fc.AddThemeStyleboxOverride("panel",                       panel);
    }

    // ── Widget helpers ────────────────────────────────────────────────────────

    // HFlowContainer shared by all chip sections
    private static HFlowContainer MakeChipFlow()
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        flow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return flow;
    }

    private static OptionButton MakeTypeDropdown(Dictionary<int, string> names, int selectedId, Action<int> onChanged)
    {
        var ob = new OptionButton();
        int sel = 0, i = 0;
        foreach (var kv in names.OrderBy(x => x.Value))
        {
            ob.AddItem(kv.Value, kv.Key);
            if (kv.Key == selectedId) sel = i;
            i++;
        }
        if (names.Count == 0) ob.AddItem("—", 0);
        ob.Selected = sel;
        ob.ItemSelected += idx => onChanged((int)ob.GetItemId((int)idx));
        return ob;
    }

    private static VBoxContainer MakeEditableStatPair(string key, int value, int min, int max, Action<int> onChange)
    {
        var vbox = new VBoxContainer(); vbox.AddThemeConstantOverride("separation", 2);
        var k = new Label { Text = key, HorizontalAlignment = HorizontalAlignment.Center };
        k.AddThemeFontSizeOverride("font_size", 10);
        k.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        vbox.AddChild(k);
        vbox.AddChild(IntInput.Make(value, min, max, onChange));
        return vbox;
    }

    private static EntityRow MakeEntityRowChip(string text, Action onDelete)
    {
        var row = new EntityRow { Text = text, ShowDelete = true, ChipWidth = 140 };
        row.DeletePressed += () => { onDelete(); row.QueueFree(); };
        return row;
    }

    // Trait entity chip (like MakeEntityRowChip but with tooltip from description dict)
    private EntityRow MakeTraitEntityChip(int traitTypeId, Action onDelete)
    {
        var row = MakeEntityRowChip(TitleCase(L(_traitNames, traitTypeId)), onDelete);
        if (_traitDescriptions.TryGetValue(traitTypeId, out var desc) && !string.IsNullOrWhiteSpace(desc))
            row.TooltipText = desc;
        return row;
    }

    private Control MakeCompactTraitChip(int traitTypeId, Action onDelete) =>
        Chip.MakeCompact(
            TitleCase(L(_traitNames, traitTypeId)),
            onDelete,
            _traitDescriptions.TryGetValue(traitTypeId, out var d) && !string.IsNullOrWhiteSpace(d) ? d : null);

    // ── Trait pills (read-only, used in Abilities tab) ────────────────────────

    private Control MakeTraitPill(int traitTypeId) =>
        Chip.MakePill(
            TitleCase(L(_traitNames, traitTypeId)),
            _traitDescriptions.TryGetValue(traitTypeId, out var d) && !string.IsNullOrWhiteSpace(d) ? d : null);

    // ── Stat section label helpers ─────────────────────────────────────────────

    private static Label MakeSectionHeader(string title)
    {
        var lbl = new Label { Text = title }; lbl.AddThemeFontSizeOverride("font_size", 18); return lbl;
    }

    private static Control MakeSectionSpacer() => new Control { CustomMinimumSize = new Vector2(0, 8) };

    private static HBoxContainer MakeBodyRow(string key, string value)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var k = new Label { Text = key + ":", CustomMinimumSize = new Vector2(72, 0) };
        k.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        var v = new Label { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        row.AddChild(k); row.AddChild(v);
        return row;
    }

    private static Label MakeDimLabel(string text)
    {
        var lbl = new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        return lbl;
    }

    private static void ClearContainer(VBoxContainer c) { foreach (Node child in c.GetChildren()) child.QueueFree(); }

    private static void AddRow(VBoxContainer c, string key, string value)
    {
        var row    = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        var kLabel = new Label { Text = key + ":", CustomMinimumSize = new Vector2(90, 0) };
        kLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.62f, 0.62f));
        var vLabel = new Label { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        row.AddChild(kLabel); row.AddChild(vLabel);
        c.AddChild(row);
    }

    private void SaveCreature() => _db.Pf2eCreatures.Edit(_creature);

    // ── Text helpers ──────────────────────────────────────────────────────────

    private static string TitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace('-', ' ').Replace('_', ' ');
        return string.Join(" ", s.Split(' ').Select(w => w.Length == 0 ? "" : char.ToUpper(w[0]) + w.Substring(1)));
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = Regex.Replace(text, @"@Localize\[([^\]]+)\]", m => { var p = m.Groups[1].Value.Split('.'); return p.Length > 0 ? $"[{p[p.Length - 1]}]" : ""; });
        text = Regex.Replace(text, @"@\w+\[[^\]]*\]\{([^}]*)\}", "$1");
        text = Regex.Replace(text, @"@\w+\[[^\]]*\]", "");
        text = text.Replace("<hr />", "\n\n").Replace("<hr>", "\n\n");
        text = Regex.Replace(text, @"</p>\s*<p>", "\n\n");
        text = text.Replace("</p>", "").Replace("<p>", "");
        text = text.Replace("<br>", "\n").Replace("<br />", "\n");
        text = text.Replace("<strong>", "").Replace("</strong>", "").Replace("<em>", "").Replace("</em>", "");
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&nbsp;", " ").Replace("&mdash;", "—").Replace("&ndash;", "–");
        text = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();
        return text;
    }

    private static string L(Dictionary<int, string> dict, int id) => dict.TryGetValue(id, out var s) ? s : id.ToString();
}
