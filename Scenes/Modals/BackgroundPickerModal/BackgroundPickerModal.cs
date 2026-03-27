using System;
using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class BackgroundPickerModal : Window
{
    private static readonly string[] AttrAbbrevs   = { "str", "dex", "con", "int", "wis", "cha" };
    private static readonly string[] AttrFullNames  = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    private static readonly string[] StandardTools =
    {
        "Alchemist's Supplies", "Artisan's Tools (choice)", "Brewer's Supplies",
        "Calligrapher's Supplies", "Carpenter's Tools", "Cartographer's Tools",
        "Cobbler's Tools", "Cook's Utensils", "Disguise Kit",
        "Forgery Kit", "Gaming Set (Dice)", "Gaming Set (Playing Cards)",
        "Glassblower's Tools", "Herbalism Kit", "Jeweler's Tools",
        "Leatherworker's Tools", "Mason's Tools",
        "Musical Instrument (Bagpipes)", "Musical Instrument (Drum)",
        "Musical Instrument (Flute)", "Musical Instrument (Lute)",
        "Musical Instrument (Lyre)", "Musical Instrument (Viol)",
        "Navigator's Tools", "Painter's Supplies", "Poisoner's Kit",
        "Potter's Tools", "Smith's Tools", "Thieves' Tools",
        "Tinker's Tools", "Weaver's Tools", "Woodcarver's Tools",
    };

    private DatabaseService       _db;
    private int                   _campaignId;
    private int?                  _selectedId;
    private string                _currentAsi  = "";
    private string[]              _standardAttrList = Array.Empty<string>();
    private List<DnD5eBackground> _backgrounds = new();
    private TypeOptionButton      _featPicker;
    private TypeOptionButton      _toolsPicker;
    private Action                _featButtonHandler;
    private LineEdit.TextChangedEventHandler      _nameChangedHandler;
    private Action                                _descEditChangedHandler;
    private ConfirmationDialog                    _deleteDialog;

    public event Action<int?, string> Confirmed;   // (backgroundId, asiString)
    public event Action<string, int>  NavigateTo;

    [Export] private VBoxContainer _listContainer;
    [Export] private Button        _confirmButton;
    [Export] private Button        _cancelButton;
    [Export] private Button        _addCustomButton;

    [Export] private Label _emptyHint;

    // Standard (read-only)
    [Export] private VBoxContainer _standardDetail;
    [Export] private Label         _standardName;
    [Export] private Label         _standardAttrsLabel;
    [Export] private Label         _standardSkillsLabel;
    [Export] private Button        _standardFeatButton;
    [Export] private Label         _standardToolsLabel;
    [Export] private Label         _standardDesc;
    // Standard ASI — 3 attr-picker dropdowns limited to the background's fixed attrs
    [Export] private OptionButton  _standardAsiBon1;
    [Export] private OptionButton  _standardAsiBon2;
    [Export] private OptionButton  _standardAsiBon3;

    // Custom (editable)
    [Export] private VBoxContainer _customDetail;
    [Export] private LineEdit      _customNameInput;
    [Export] private FlowContainer _customSkillCheckboxes;
    [Export] private HBoxContainer _customFeatRow;
    [Export] private HBoxContainer _customToolsRow;
    [Export] private TextEdit      _customDescEdit;
    [Export] private Button        _deleteCustomButton;
    // Custom ASI — 3 attr-picker dropdowns from all 6 attrs
    [Export] private OptionButton  _customAsiBon1;
    [Export] private OptionButton  _customAsiBon2;
    [Export] private OptionButton  _customAsiBon3;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        CloseRequested           += OnCancel;
        _cancelButton.Pressed    += OnCancel;
        _confirmButton.Pressed   += OnConfirm;
        _addCustomButton.Pressed += OnAddCustom;

        // Link-style appearance for feat button
        _standardFeatButton.AddThemeColorOverride("font_color",         new Color(0.45f, 0.75f, 1.0f));
        _standardFeatButton.AddThemeColorOverride("font_hover_color",   new Color(0.65f, 0.88f, 1.0f));
        _standardFeatButton.AddThemeColorOverride("font_pressed_color", new Color(0.35f, 0.60f, 0.90f));

        // Wire validation once — same dropdowns used every time
        _standardAsiBon1.ItemSelected += _ => ValidateStandardAsi();
        _standardAsiBon2.ItemSelected += _ => ValidateStandardAsi();
        _standardAsiBon3.ItemSelected += _ => ValidateStandardAsi();

        _customAsiBon1.ItemSelected += _ => ValidateCustomAsi();
        _customAsiBon2.ItemSelected += _ => ValidateCustomAsi();
        _customAsiBon3.ItemSelected += _ => ValidateCustomAsi();

        _deleteDialog = DialogHelper.Make("Delete Background");
        AddChild(_deleteDialog);
        _deleteDialog.Confirmed += () =>
        {
            if (!_selectedId.HasValue) return;
            var bg = _backgrounds.Find(b => b.Id == _selectedId.Value);
            if (bg == null) return;
            _db.DnD5eBackgrounds.Delete(bg.Id);
            _backgrounds.Remove(bg);
            _selectedId = null;
            BuildList();
            ShowEmpty();
        };
        _deleteCustomButton.Pressed += () =>
        {
            if (!_selectedId.HasValue) return;
            var bg = _backgrounds.Find(b => b.Id == _selectedId.Value);
            if (bg == null || !bg.IsCustom) return;
            DialogHelper.Show(_deleteDialog, $"Delete \"{bg.Name}\"? This cannot be undone.");
        };
    }

    public void Open(int campaignId, int? currentBackgroundId, string currentAsi)
    {
        _campaignId = campaignId;
        _selectedId = currentBackgroundId;
        _currentAsi = currentAsi ?? "";
        _backgrounds = _db.DnD5eBackgrounds.GetAll(campaignId);

        BuildList();

        var selected = _selectedId.HasValue ? _backgrounds.Find(b => b.Id == _selectedId.Value) : null;
        if (selected == null)        ShowEmpty();
        else if (selected.IsCustom)  ShowCustom(selected);
        else                         ShowStandard(selected);

        PopupCentered();
    }

    private void BuildList()
    {
        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree();

        foreach (var bg in _backgrounds)
        {
            var captured = bg;
            var btn = new Button
            {
                Text                = bg.Name,
                ToggleMode          = true,
                ButtonPressed       = _selectedId == bg.Id,
                Alignment           = HorizontalAlignment.Left,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Flat                = true,
            };
            btn.Pressed += () =>
            {
                _selectedId = captured.Id;
                _currentAsi = "";   // reset ASI when switching backgrounds
                RefreshListSelection();
                if (captured.IsCustom) ShowCustom(captured);
                else ShowStandard(captured);
            };
            _listContainer.AddChild(btn);
        }
    }

    private void RefreshListSelection()
    {
        var children = _listContainer.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is Button btn)
                btn.ButtonPressed = _backgrounds.Count > i && _backgrounds[i].Id == _selectedId;
        }
    }

    // ── Display modes ─────────────────────────────────────────────────────────

    private void ShowEmpty()
    {
        _emptyHint.Visible      = true;
        _standardDetail.Visible = false;
        _customDetail.Visible   = false;
        FreePickers();
    }

    private void ShowStandard(DnD5eBackground bg)
    {
        _emptyHint.Visible      = false;
        _customDetail.Visible   = false;
        _standardDetail.Visible = true;
        FreePickers();

        _standardName.Text = bg.Name;

        // Ability score options label
        _standardAttrsLabel.Text = FormatAttrOptions(bg.AbilityScoreOptions);

        // ASI — 3 pickers limited to this background's fixed attrs
        _standardAttrList = bg.AbilityScoreOptions
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim()).ToArray();
        var expanded = ExpandAsi(_currentAsi);
        SetupStandardAsiDropdown(_standardAsiBon1, _standardAttrList, expanded.Count > 0 ? expanded[0] : "");
        SetupStandardAsiDropdown(_standardAsiBon2, _standardAttrList, expanded.Count > 1 ? expanded[1] : "");
        SetupStandardAsiDropdown(_standardAsiBon3, _standardAttrList, expanded.Count > 2 ? expanded[2] : "");
        ValidateStandardAsi();

        // Skills
        _standardSkillsLabel.Text = !string.IsNullOrEmpty(bg.SkillNames)
            ? bg.SkillNames.Replace(",", ", ")
            : $"{bg.SkillCount} of your choice";

        // Feat — navigable link
        if (_featButtonHandler != null) _standardFeatButton.Pressed -= _featButtonHandler;
        _featButtonHandler = null;
        if (bg.FeatAbilityId.HasValue)
        {
            var feat = _db.Abilities.Get(bg.FeatAbilityId.Value);
            _standardFeatButton.Text     = feat?.Name ?? "(unknown)";
            _standardFeatButton.Disabled = false;
            int capturedId = bg.FeatAbilityId.Value;
            _featButtonHandler = () => NavigateTo?.Invoke("ability", capturedId);
            _standardFeatButton.Pressed += _featButtonHandler;
        }
        else
        {
            _standardFeatButton.Text     = "(none)";
            _standardFeatButton.Disabled = true;
        }

        // Tools
        _standardToolsLabel.Text = !string.IsNullOrEmpty(bg.ToolOptions)
            ? bg.ToolOptions
            : "(none)";

        _standardDesc.Text = bg.Description;
    }

    private void ShowCustom(DnD5eBackground bg)
    {
        _emptyHint.Visible      = false;
        _standardDetail.Visible = false;
        _customDetail.Visible   = true;
        FreePickers();

        // Name
        if (_nameChangedHandler != null) _customNameInput.TextChanged -= _nameChangedHandler;
        _customNameInput.Text = bg.Name;
        _nameChangedHandler = text =>
        {
            var b = _backgrounds.Find(x => x.Id == _selectedId);
            if (b == null) return;
            b.Name = text;
            _db.DnD5eBackgrounds.Edit(b);
            int idx = _backgrounds.IndexOf(b);
            if (idx >= 0 && idx < _listContainer.GetChildCount())
                if (_listContainer.GetChild(idx) is Button btn) btn.Text = text;
        };
        _customNameInput.TextChanged += _nameChangedHandler;

        // Custom ASI — 3 pickers from all 6 attrs
        var expanded = ExpandAsi(_currentAsi);
        SetupCustomAsiDropdown(_customAsiBon1, expanded.Count > 0 ? expanded[0] : "");
        SetupCustomAsiDropdown(_customAsiBon2, expanded.Count > 1 ? expanded[1] : "");
        SetupCustomAsiDropdown(_customAsiBon3, expanded.Count > 2 ? expanded[2] : "");
        ValidateCustomAsi();

        // Skill checkboxes (max 2)
        BuildSkillCheckboxes(bg);

        // Feat picker
        _featPicker = new TypeOptionButton
        {
            NoneText            = "(none)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _customFeatRow.AddChild(_featPicker);
        _featPicker.Setup(
            () => _db.Abilities.GetAll(_campaignId)
                      .FindAll(a => a.Type.IndexOf("Feat", StringComparison.OrdinalIgnoreCase) >= 0)
                      .ConvertAll(a => (a.Id, a.Name)),
            null, null);
        _featPicker.SelectById(bg.FeatAbilityId);
        _featPicker.TypeSelected += id =>
        {
            var b = _backgrounds.Find(x => x.Id == _selectedId);
            if (b == null) return;
            b.FeatAbilityId = id > 0 ? (int?)id : null;
            _db.DnD5eBackgrounds.Edit(b);
        };

        // Tools dropdown
        _toolsPicker = new TypeOptionButton
        {
            NoneText            = "(none)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _customToolsRow.AddChild(_toolsPicker);
        _toolsPicker.Setup(
            () => StandardTools.Select((t, i) => (i + 1, t)).ToList(),
            null, null);
        int toolIdx = Array.IndexOf(StandardTools, bg.ToolOptions);
        _toolsPicker.SelectById(toolIdx >= 0 ? (int?)(toolIdx + 1) : null);
        _toolsPicker.TypeSelected += id =>
        {
            var b = _backgrounds.Find(x => x.Id == _selectedId);
            if (b == null) return;
            b.ToolOptions = id > 0 && id <= StandardTools.Length ? StandardTools[id - 1] : "";
            _db.DnD5eBackgrounds.Edit(b);
        };

        // Description
        if (_descEditChangedHandler != null) _customDescEdit.TextChanged -= _descEditChangedHandler;
        _customDescEdit.Text = bg.Description;
        _descEditChangedHandler = () =>
        {
            var b = _backgrounds.Find(x => x.Id == _selectedId);
            if (b == null) return;
            b.Description = _customDescEdit.Text;
            _db.DnD5eBackgrounds.Edit(b);
        };
        _customDescEdit.TextChanged += _descEditChangedHandler;
    }

    // ── ASI dropdown setup ─────────────────────────────────────────────────────

    // Options limited to the background's 3 fixed attrs
    private static void SetupStandardAsiDropdown(OptionButton ob, string[] attrList, string selectedAbbrev)
    {
        ob.Clear();
        for (int i = 0; i < attrList.Length; i++)
        {
            int nameIdx = Array.IndexOf(AttrAbbrevs, attrList[i]);
            ob.AddItem(nameIdx >= 0 ? AttrFullNames[nameIdx] : attrList[i], i);
        }
        int selIdx = Array.IndexOf(attrList, selectedAbbrev);
        ob.Select(selIdx >= 0 ? selIdx : 0);
    }

    // Options from all 6 attrs (with "—" as unset)
    private static void SetupCustomAsiDropdown(OptionButton ob, string selectedAbbrev)
    {
        ob.Clear();
        ob.AddItem("—", 0);
        for (int i = 0; i < AttrAbbrevs.Length; i++)
            ob.AddItem(AttrFullNames[i], i + 1);
        int selIdx = Array.IndexOf(AttrAbbrevs, selectedAbbrev);
        ob.Select(selIdx >= 0 ? selIdx + 1 : 0);
    }

    // ── ASI validation ─────────────────────────────────────────────────────────

    // Red border only when all 3 pick the same attr (would grant +3 to one attr)
    private void ValidateStandardAsi()
    {
        string p1 = GetStandardPick(_standardAsiBon1);
        string p2 = GetStandardPick(_standardAsiBon2);
        string p3 = GetStandardPick(_standardAsiBon3);
        bool allSame = !string.IsNullOrEmpty(p1)
            && string.Equals(p1, p2, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p2, p3, StringComparison.OrdinalIgnoreCase);
        ApplyAsiBonStyle(_standardAsiBon1, !allSame);
        ApplyAsiBonStyle(_standardAsiBon2, !allSame);
        ApplyAsiBonStyle(_standardAsiBon3, !allSame);
    }

    // Same rule for custom: red when all 3 pick the same attr
    private void ValidateCustomAsi()
    {
        string p1 = GetCustomPick(_customAsiBon1);
        string p2 = GetCustomPick(_customAsiBon2);
        string p3 = GetCustomPick(_customAsiBon3);
        bool allSame = !string.IsNullOrEmpty(p1)
            && string.Equals(p1, p2, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p2, p3, StringComparison.OrdinalIgnoreCase);
        ApplyAsiBonStyle(_customAsiBon1, !allSame);
        ApplyAsiBonStyle(_customAsiBon2, !allSame);
        ApplyAsiBonStyle(_customAsiBon3, !allSame);
    }

    private static void ApplyAsiBonStyle(OptionButton ob, bool valid)
    {
        if (!valid)
        {
            var style = ob.GetThemeStylebox("normal") is StyleBoxFlat existing
                ? (StyleBoxFlat)existing.Duplicate()
                : new StyleBoxFlat();
            style.BorderColor = new Color(0.85f, 0.15f, 0.15f);
            style.SetBorderWidthAll(2);
            ob.AddThemeStyleboxOverride("normal", style);
        }
        else
        {
            ob.RemoveThemeStyleboxOverride("normal");
        }
    }

    // ── ASI pick readers ──────────────────────────────────────────────────────

    private string GetStandardPick(OptionButton ob)
    {
        int idx = ob.Selected;
        return idx >= 0 && idx < _standardAttrList.Length ? _standardAttrList[idx] : "";
    }

    private static string GetCustomPick(OptionButton ob)
    {
        int idx = ob.Selected - 1;   // -1 for "—" at index 0
        return idx >= 0 && idx < AttrAbbrevs.Length ? AttrAbbrevs[idx] : "";
    }

    // ── ASI string helpers ────────────────────────────────────────────────────

    // Build "str:2,dex:1" by counting how many times each attr is picked
    private string BuildCurrentAsi()
    {
        List<string> picks;
        if (_standardDetail.Visible)
        {
            picks = new List<string>
            {
                GetStandardPick(_standardAsiBon1),
                GetStandardPick(_standardAsiBon2),
                GetStandardPick(_standardAsiBon3),
            };
        }
        else if (_customDetail.Visible)
        {
            picks = new List<string>
            {
                GetCustomPick(_customAsiBon1),
                GetCustomPick(_customAsiBon2),
                GetCustomPick(_customAsiBon3),
            };
        }
        else return "";

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pick in picks.Where(p => !string.IsNullOrEmpty(p)))
            counts[pick] = counts.GetValueOrDefault(pick) + 1;

        return string.Join(",", counts.Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    // Expand "wis:2,int:1" → ["wis", "wis", "int"] for pre-populating slots
    private static List<string> ExpandAsi(string asi)
    {
        var result = new List<string>();
        foreach (var kv in ParseAsi(asi))
            for (int i = 0; i < kv.Value; i++)
                result.Add(kv.Key);
        return result;
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

    private void BuildSkillCheckboxes(DnD5eBackground bg)
    {
        foreach (Node child in _customSkillCheckboxes.GetChildren())
            child.QueueFree();

        var skills   = _db.DnD5eSkills.GetAll(_campaignId);
        var selected = new HashSet<string>(
            bg.SkillNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var skill in skills)
        {
            string capturedName = skill.Name;
            var cb = new CheckBox { Text = skill.Name, ButtonPressed = selected.Contains(skill.Name) };
            cb.Toggled += pressed =>
            {
                var b = _backgrounds.Find(x => x.Id == _selectedId);
                if (b == null) return;
                var names = new HashSet<string>(
                    b.SkillNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                if (pressed)
                {
                    if (names.Count >= 2) { cb.SetPressedNoSignal(false); return; }
                    names.Add(capturedName);
                }
                else
                {
                    names.Remove(capturedName);
                }
                b.SkillNames = string.Join(",", names);
                _db.DnD5eBackgrounds.Edit(b);
                RefreshSkillCheckboxStates(b.SkillNames);
            };
            _customSkillCheckboxes.AddChild(cb);
        }
        RefreshSkillCheckboxStates(bg.SkillNames);
    }

    private void RefreshSkillCheckboxStates(string skillNames)
    {
        var selected = new HashSet<string>(
            skillNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
        bool atMax = selected.Count >= 2;
        foreach (Node child in _customSkillCheckboxes.GetChildren())
        {
            if (child is CheckBox cb)
                cb.Disabled = atMax && !selected.Contains(cb.Text);
        }
    }

    private void OnAddCustom()
    {
        var bg = new DnD5eBackground
        {
            CampaignId           = _campaignId,
            Name                 = "Custom Background",
            SkillCount           = 2,
            SkillNames           = "",
            Description          = "",
            ToolOptions          = "",
            AbilityScoreOptions  = "",
            IsCustom             = true,
        };
        bg.Id = _db.DnD5eBackgrounds.Add(bg);
        _backgrounds.Add(bg);
        _selectedId = bg.Id;
        _currentAsi = "";
        BuildList();
        ShowCustom(bg);
    }

    private void FreePickers()
    {
        if (_featPicker  != null) { _featPicker.QueueFree();  _featPicker  = null; }
        if (_toolsPicker != null) { _toolsPicker.QueueFree(); _toolsPicker = null; }
    }

    private static string FormatAttrOptions(string options)
    {
        if (string.IsNullOrEmpty(options)) return "(none)";
        var parts = options.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = parts.Select(p =>
        {
            int idx = Array.IndexOf(AttrAbbrevs, p.Trim());
            return idx >= 0 ? AttrFullNames[idx] : p;
        });
        return string.Join(", ", names);
    }

    private void OnConfirm()
    {
        Confirmed?.Invoke(_selectedId, BuildCurrentAsi());
        Hide();
    }

    private void OnCancel() => Hide();
}
