using System;
using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eEncounterDetailPane : VBoxContainer
{
    [Signal] public delegate void EncounterUpdatedEventHandler();
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);

    [Export] private Button        _deleteButton;
    [Export] private LineEdit      _nameInput;
    [Export] private OptionButton  _sessionOption;
    [Export] private Button        _resolveButton;
    [Export] private Button        _addCombatantButton;
    [Export] private Button        _showDefeatedButton;
    [Export] private VBoxContainer _activeCombatantsVBox;
    [Export] private Label         _defeatedDivider;
    [Export] private VBoxContainer _defeatedCombatantsVBox;
    [Export] private Button        _nextTurnButton;
    [Export] private VBoxContainer _statBlockVBox;
    [Export] private PackedScene   _combatantRowScene;

    private DatabaseService             _db;
    private Encounter                   _encounter;
    private int                         _activeTurnCombatantId  = -1;
    private int                         _selectedCombatantId    = -1;
    private int                         _liveDragLastTargetId   = -1;
    private bool                        _showDefeated           = false;
    private Dictionary<int, string>     _conditionNames         = new();
    private Dictionary<int, Pf2eCombatantRow> _rowById          = new();

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        var confirmDialog = DialogHelper.Make("Delete Encounter");
        AddChild(confirmDialog);
        confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "encounter", _encounter?.Id ?? 0);
        _deleteButton.Pressed   += () => DialogHelper.Show(confirmDialog, $"Delete \"{_encounter?.Name}\"? This cannot be undone.");

        _nameInput.FocusExited   += SaveName;
        _nameInput.TextSubmitted += _ => _nameInput.ReleaseFocus();

        _sessionOption.ItemSelected += idx =>
        {
            if (_encounter == null) return;
            _encounter.SessionId = idx == 0 ? null : (int?)(int)_sessionOption.GetItemId((int)idx);
            _db.Encounters.Edit(_encounter);
            EmitSignal(SignalName.EncounterUpdated);
        };

        _resolveButton.Pressed      += ToggleResolved;
        _addCombatantButton.Pressed += ShowAddCombatantModal;

        _showDefeatedButton.Pressed += () =>
        {
            _showDefeated = !_showDefeated;
            _defeatedCombatantsVBox.Visible = _showDefeated;
            _defeatedDivider.Visible        = _showDefeated;
            UpdateShowDefeatedButton();
        };

        _nextTurnButton.Pressed += AdvanceTurn;
    }

    public void Load(Encounter enc)
    {
        _encounter             = enc;
        _activeTurnCombatantId = -1;
        _selectedCombatantId   = -1;
        _nameInput.Text        = enc.Name;

        LoadSessionOptions();
        UpdateResolveButton();
        BuildConditionNameCache();
        LoadCombatants();
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    private void SaveName()
    {
        if (_encounter == null) return;
        _encounter.Name = _nameInput.Text;
        _db.Encounters.Edit(_encounter);
        EmitSignal(SignalName.EncounterUpdated);
    }

    private void LoadSessionOptions()
    {
        _sessionOption.Clear();
        _sessionOption.AddItem("No Session", 0);
        var sessions = _db.Sessions.GetAll(_encounter.CampaignId);
        foreach (var s in sessions)
            _sessionOption.AddItem($"Session {s.Number}: {s.Title}", s.Id);

        int selected = 0;
        if (_encounter.SessionId.HasValue)
        {
            for (int i = 0; i < _sessionOption.ItemCount; i++)
            {
                if (_sessionOption.GetItemId(i) == _encounter.SessionId.Value) { selected = i; break; }
            }
        }
        _sessionOption.Selected = selected;
    }

    private void UpdateResolveButton()
    {
        if (_encounter == null) return;
        _resolveButton.Text = _encounter.IsResolved ? "☑ Resolved" : "☐ Resolve";
    }

    private void ToggleResolved()
    {
        if (_encounter == null) return;
        _encounter.IsResolved = !_encounter.IsResolved;
        _db.Encounters.SetResolved(_encounter.Id, _encounter.IsResolved);
        UpdateResolveButton();
        EmitSignal(SignalName.EncounterUpdated);
    }

    private void BuildConditionNameCache()
    {
        _conditionNames = _db.Pf2eConditionTypes.GetAll(_encounter.CampaignId)
                            .ToDictionary(c => c.Id, c => c.Name);
    }

    // ── Initiative List ──────────────────────────────────────────────────────

    private void LiveInsertRow(int draggedId, int targetId)
    {
        if (targetId == _liveDragLastTargetId) return;
        _liveDragLastTargetId = targetId;

        if (!_rowById.TryGetValue(draggedId, out var draggedRow) ||
            !_rowById.TryGetValue(targetId,  out var targetRow)) return;

        var wDragged = draggedRow.GetParent() as MarginContainer;
        var wTarget  = targetRow.GetParent()  as MarginContainer;
        if (wDragged == null || wTarget == null || wDragged.GetParent() != wTarget.GetParent()) return;

        ((VBoxContainer)wDragged.GetParent()).MoveChild(wDragged, wTarget.GetIndex());
    }

    private void PersistReorder()
    {
        _liveDragLastTargetId = -1;
        int order = 0;
        foreach (var child in _activeCombatantsVBox.GetChildren())
        {
            var row = (child as MarginContainer)?.GetChildOrNull<Pf2eCombatantRow>(0);
            if (row == null) continue;
            int id = row.CombatantId;
            if (id < 0) continue;
            var c = _db.Pf2eEncounterCombatants.Get(id);
            if (c == null) continue;
            c.SortOrder = order++;
            _db.Pf2eEncounterCombatants.Edit(c);
        }
        LoadCombatants();
    }

    private void LoadCombatants()
    {
        _rowById.Clear();
        foreach (Node c in _activeCombatantsVBox.GetChildren())   c.QueueFree();
        foreach (Node c in _defeatedCombatantsVBox.GetChildren()) c.QueueFree();

        var all      = _db.Pf2eEncounterCombatants.GetAll(_encounter.Id);
        var active   = all.Where(c => c.IsActive).ToList();
        var defeated = all.Where(c => !c.IsActive).ToList();

        if (_activeTurnCombatantId == -1 && active.Count > 0)
            _activeTurnCombatantId = active[0].Id;

        foreach (var c in active)   AddCombatantRow(c, _activeCombatantsVBox);
        foreach (var c in defeated) AddCombatantRow(c, _defeatedCombatantsVBox);

        int defeatedCount = defeated.Count;
        _defeatedDivider.Text           = $"── Defeated ({defeatedCount}) ──";
        _defeatedDivider.Visible        = defeatedCount > 0 && _showDefeated;
        _defeatedCombatantsVBox.Visible = defeatedCount > 0 && _showDefeated;
        _showDefeatedButton.Visible     = defeatedCount > 0;
        UpdateShowDefeatedButton();

        if (_selectedCombatantId != -1)
            RefreshStatBlock(_selectedCombatantId);
        else if (active.Count > 0)
            RefreshStatBlock(_activeTurnCombatantId);
        else
        {
            foreach (Node c in _statBlockVBox.GetChildren()) c.QueueFree();
            var hint = new Label
            {
                Text = "No combatants yet.\nClick + Combatant to add.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode        = TextServer.AutowrapMode.Off,
            };
            hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.35f));
            _activeCombatantsVBox.AddChild(hint);
        }
    }

    private void AddCombatantRow(Pf2eEncounterCombatant c, VBoxContainer parent)
    {
        if (_combatantRowScene == null) return;
        var row     = _combatantRowScene.Instantiate<Pf2eCombatantRow>();
        var wrapper = new MarginContainer();
        wrapper.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(wrapper);
        wrapper.AddChild(row);
        row.Setup(_db, c, c.Id == _activeTurnCombatantId);
        row.OnLiveReorder = (dId, tId) => LiveInsertRow(dId, tId);
        row.OnDrop        = PersistReorder;
        _rowById[c.Id] = row;

        row.Selected          += cid => { _selectedCombatantId = cid; RefreshStatBlock(cid); };
        row.HpChanged         += (cid, hp, reason) => { RefreshRowData(cid); if (_selectedCombatantId == cid) RefreshStatBlock(cid); };
        row.InitiativeChanged += (cid, init) =>
        {
            var data = _db.Pf2eEncounterCombatants.Get(cid);
            if (data == null) return;
            data.Initiative = init;
            data.SortOrder  = -init;
            _db.Pf2eEncounterCombatants.Edit(data);
            LoadCombatants();
        };
        row.Defeated          += cid => PromptMarkDefeated(cid);
        row.ActionsChanged    += (cid, count) => { };
        row.Removed              += cid =>
        {
            _db.Pf2eEncounterCombatants.Delete(cid);
            if (_selectedCombatantId   == cid) _selectedCombatantId   = -1;
            if (_activeTurnCombatantId == cid) _activeTurnCombatantId = -1;
            LoadCombatants();
        };
        row.EntityOpenRequested  += (et, eid) => EmitSignal(SignalName.NavigateTo, et, eid);
    }

    private void RefreshRowData(int combatantId)
    {
        var updated = _db.Pf2eEncounterCombatants.Get(combatantId);
        if (updated == null) return;
        foreach (var container in new[] { _activeCombatantsVBox, _defeatedCombatantsVBox })
        {
            foreach (var child in container.GetChildren())
            {
                var rowNode = child is MarginContainer mc
                    ? mc.GetChildOrNull<Pf2eCombatantRow>(0)
                    : child as Pf2eCombatantRow;
                rowNode?.Refresh(updated.Id == _activeTurnCombatantId);
            }
        }
    }

    private void PromptMarkDefeated(int combatantId)
    {
        var dlg = DialogHelper.Make("Mark Defeated");
        AddChild(dlg);
        var c = _db.Pf2eEncounterCombatants.Get(combatantId);
        DialogHelper.Show(dlg, $"Mark \"{c?.DisplayName}\" as defeated?");
        dlg.Confirmed += () =>
        {
            _db.Pf2eEncounterCombatants.SetDefeated(combatantId);
            if (_activeTurnCombatantId == combatantId)
                AdvanceTurn();
            else
                LoadCombatants();
        };
    }

    private void AdvanceTurn()
    {
        if (_encounter == null) return;
        var active = _db.Pf2eEncounterCombatants.GetAll(_encounter.Id)
                        .Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToList();
        if (active.Count == 0) { LoadCombatants(); return; }

        int curIdx  = active.FindIndex(c => c.Id == _activeTurnCombatantId);
        int nextIdx = (curIdx + 1) % active.Count;
        var next    = active[nextIdx];

        _db.Pf2eEncounterCombatants.SetActionsRemaining(next.Id, 3);
        _activeTurnCombatantId = next.Id;
        LoadCombatants();
    }

    private void UpdateShowDefeatedButton()
    {
        if (_encounter == null) return;
        int defeatedCount = _db.Pf2eEncounterCombatants.GetAll(_encounter.Id).Count(c => !c.IsActive);
        _showDefeatedButton.Text = _showDefeated
            ? "Hide Defeated"
            : $"Show Defeated ({defeatedCount})";
    }

    // ── Stat Block ───────────────────────────────────────────────────────────

    private void RefreshStatBlock(int combatantId)
    {
        foreach (Node child in _statBlockVBox.GetChildren()) child.QueueFree();
        var c = _db.Pf2eEncounterCombatants.Get(combatantId);
        if (c == null) return;

        _selectedCombatantId = combatantId;

        var nameRow = new HBoxContainer();
        var nameLabel = new Label { Text = c.DisplayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        nameLabel.AddThemeFontSizeOverride("font_size", 18);
        string typeTag = c.CharacterId.HasValue ? "PC"
                       : c.CreatureId.HasValue  ? "Creature"
                       : "Custom";
        var typeLabel = new Label { Text = typeTag };
        nameRow.AddChild(nameLabel);
        nameRow.AddChild(typeLabel);
        _statBlockVBox.AddChild(nameRow);

        _statBlockVBox.AddChild(new HSeparator());

        // Core stats row
        var statsRow = new HBoxContainer { Name = "StatsRow" };
        statsRow.AddThemeConstantOverride("separation", 12);

        statsRow.AddChild(MakeStatChip("HP", $"{c.CurrentHp}/{c.MaxHp}"));
        statsRow.AddChild(MakeStatChip("AC", c.Ac.ToString()));

        if (c.CharacterId.HasValue)
            statsRow.AddChild(MakeStatChip("Hero ✦", c.HeroPoints.ToString()));
        statsRow.AddChild(MakeStatChip("Actions", c.ActionsRemaining.ToString()));

        _statBlockVBox.AddChild(statsRow);

        // Extra stats from source entity
        if (c.CreatureId.HasValue)
        {
            var creature = _db.Pf2eCreatures.Get(c.CreatureId.Value);
            if (creature != null)
            {
                var savesRow = new HBoxContainer();
                savesRow.AddThemeConstantOverride("separation", 12);
                savesRow.AddChild(MakeStatChip("Fort",  $"+{creature.Fortitude}"));
                savesRow.AddChild(MakeStatChip("Ref",   $"+{creature.Reflex}"));
                savesRow.AddChild(MakeStatChip("Will",  $"+{creature.Will}"));
                savesRow.AddChild(MakeStatChip("Perc",  $"+{creature.Perception}"));
                _statBlockVBox.AddChild(savesRow);

                _statBlockVBox.AddChild(new Label { Text = $"Lv {creature.Level}" });
            }
        }
        else if (c.CharacterId.HasValue)
        {
            var pc = _db.Pf2eCharacters.Get(c.CharacterId.Value);
            if (pc != null)
                _statBlockVBox.AddChild(new Label { Text = $"Level {pc.Level}" });
        }

        _statBlockVBox.AddChild(new HSeparator());

        // Conditions
        BuildConditionsSection(c);
    }

    private void BuildConditionsSection(Pf2eEncounterCombatant combatant)
    {
        var header = new HBoxContainer();
        var condLabel = new Label { Text = "Conditions" };
        condLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var addCondBtn = new Button { Text = "+ Add" };
        header.AddChild(condLabel);
        header.AddChild(addCondBtn);
        _statBlockVBox.AddChild(header);

        var chipsBox = new HFlowContainer { Name = "ConditionChips" };
        _statBlockVBox.AddChild(chipsBox);

        RefreshConditionChips(combatant, chipsBox);
        addCondBtn.Pressed += () => ShowAddConditionWindow(combatant, chipsBox);
    }

    private void RefreshConditionChips(Pf2eEncounterCombatant combatant, HFlowContainer container)
    {
        foreach (Node c in container.GetChildren()) c.QueueFree();
        var conditions = _db.Pf2eEncounterCombatantConditions.GetForCombatant(combatant.Id);
        foreach (var cond in conditions)
        {
            string condName = _conditionNames.TryGetValue(cond.ConditionTypeId, out string n) ? n : $"#{cond.ConditionTypeId}";
            string label    = cond.ConditionValue > 0 ? $"{condName} {cond.ConditionValue}" : condName;
            var chip = new Button { Text = $"{label} ×" };
            int tid = cond.ConditionTypeId;
            chip.Pressed += () =>
            {
                _db.Pf2eEncounterCombatantConditions.Delete(combatant.Id, tid);
                RefreshConditionChips(combatant, container);
            };
            container.AddChild(chip);
        }
    }

    private void ShowAddConditionWindow(Pf2eEncounterCombatant combatant, HFlowContainer chipsBox)
    {
        var win = new Window { Title = "Add Condition", Size = new Vector2I(300, 240), Exclusive = true, Unresizable = true };
        win.CloseRequested += win.QueueFree;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        win.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        var search = new LineEdit { PlaceholderText = "Search..." };
        vbox.AddChild(search);

        var list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vbox.AddChild(list);

        var allCondTypes = _db.Pf2eConditionTypes.GetAll(_encounter.CampaignId);
        var filtered     = allCondTypes.ToList();

        void Repopulate(string txt = "")
        {
            filtered = string.IsNullOrEmpty(txt)
                ? allCondTypes.ToList()
                : allCondTypes.Where(c => c.Name.Contains(txt, StringComparison.OrdinalIgnoreCase)).ToList();
            list.Clear();
            foreach (var ct in filtered) list.AddItem(ct.Name);
        }
        Repopulate();
        search.TextChanged += Repopulate;

        var valueRow = new HBoxContainer();
        valueRow.AddChild(new Label { Text = "Value:" });
        var valueSpin = new SpinBox { MinValue = 0, MaxValue = 20 };
        WireScroll(valueSpin);
        valueRow.AddChild(valueSpin);
        vbox.AddChild(valueRow);

        var addBtn = new Button { Text = "Add" };
        vbox.AddChild(addBtn);

        AddChild(win);
        win.PopupCentered();

        addBtn.Pressed += () =>
        {
            int[] sel = list.GetSelectedItems();
            if (sel.Length == 0 || sel[0] >= filtered.Count) { win.QueueFree(); return; }
            var ct = filtered[sel[0]];
            _db.Pf2eEncounterCombatantConditions.Upsert(combatant.Id, ct.Id, (int)valueSpin.Value);
            _conditionNames.TryAdd(ct.Id, ct.Name);
            RefreshConditionChips(combatant, chipsBox);
            win.QueueFree();
        };
    }

    private static Control MakeStatChip(string label, string value)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        var lbl = new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center };
        var val = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Center };
        val.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(lbl);
        vbox.AddChild(val);
        return vbox;
    }

    // ── Add Combatant Modal ──────────────────────────────────────────────────

    private void ShowAddCombatantModal()
    {
        var win = new Window { Title = "Add Combatant", Size = new Vector2I(560, 480), Exclusive = true, Unresizable = true };
        win.CloseRequested += win.QueueFree;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        win.AddChild(margin);

        var outerVBox = new VBoxContainer();
        outerVBox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(outerVBox);

        var tabs = new TabContainer();
        tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outerVBox.AddChild(tabs);

        var getPartySelections    = BuildPartyTab(tabs);
        var getCreatureSelections = BuildCreatureTab(tabs);
        BuildCustomTab(tabs, win);

        var addBtn = new Button { Text = "Add Selected" };
        outerVBox.AddChild(addBtn);

        AddChild(win);
        win.PopupCentered();

        addBtn.Pressed += () =>
        {
            var pcIds              = getPartySelections();
            var creatureSelections = getCreatureSelections();
            if (pcIds.Count == 0 && creatureSelections.Count == 0) return;

            var existing = _db.Pf2eEncounterCombatants.GetAll(_encounter.Id);
            var pending  = new List<(string DisplayName, System.Action<int> AddWithInit)>();

            foreach (int pcId in pcIds)
            {
                var pc = _db.Pf2eCharacters.Get(pcId);
                if (pc == null) continue;
                var pcCapture = pc;
                pending.Add((pc.Name, init => _db.Pf2eEncounterCombatants.Add(new Pf2eEncounterCombatant
                {
                    EncounterId      = _encounter.Id,
                    CharacterId      = pcCapture.Id,
                    DisplayName      = pcCapture.Name,
                    CurrentHp        = pcCapture.CurrentHp,
                    MaxHp            = pcCapture.MaxHp,
                    Ac               = pcCapture.Ac,
                    Initiative       = init,
                    SortOrder        = -init,
                    HeroPoints       = pcCapture.HeroPoints,
                    ActionsRemaining = 3
                })));
            }

            foreach (var (cr, qty) in creatureSelections)
            {
                int startNum = existing.Count(c => c.CreatureId == cr.Id) + 1;
                for (int i = 0; i < qty; i++)
                {
                    bool   needsNumber  = qty > 1 || startNum > 1;
                    string displayName  = needsNumber ? $"{cr.Name} {startNum + i}" : cr.Name;
                    var    crCapture    = cr;
                    string dnCapture    = displayName;
                    pending.Add((displayName, init => _db.Pf2eEncounterCombatants.Add(new Pf2eEncounterCombatant
                    {
                        EncounterId      = _encounter.Id,
                        CreatureId       = crCapture.Id,
                        DisplayName      = dnCapture,
                        CurrentHp        = crCapture.MaxHp,
                        MaxHp            = crCapture.MaxHp,
                        Ac               = crCapture.Ac,
                        Initiative       = init,
                        SortOrder        = -init,
                        ActionsRemaining = 3
                    })));
                }
            }

            win.QueueFree();
            ShowInitModal(pending);
        };
    }

    private static Button MakeCheckButton(bool disabled = false)
    {
        var offSb = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.15f) };
        offSb.SetBorderWidthAll(2);
        offSb.BorderColor = new Color(0.60f, 0.60f, 0.60f);
        offSb.SetCornerRadiusAll(3);
        offSb.SetContentMarginAll(2);

        var onSb = new StyleBoxFlat { BgColor = new Color(0.18f, 0.62f, 0.24f) };
        onSb.SetBorderWidthAll(2);
        onSb.BorderColor = new Color(0.30f, 0.85f, 0.36f);
        onSb.SetCornerRadiusAll(3);
        onSb.SetContentMarginAll(2);

        var dimSb = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.15f, 0.4f) };
        dimSb.SetBorderWidthAll(2);
        dimSb.BorderColor = new Color(0.40f, 0.40f, 0.40f, 0.4f);
        dimSb.SetCornerRadiusAll(3);
        dimSb.SetContentMarginAll(2);

        var btn = new Button
        {
            ToggleMode        = true,
            Disabled          = disabled,
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        btn.AddThemeStyleboxOverride("normal",        disabled ? dimSb : offSb);
        btn.AddThemeStyleboxOverride("hover",         offSb);
        btn.AddThemeStyleboxOverride("pressed",        onSb);
        btn.AddThemeStyleboxOverride("hover_pressed",  onSb);
        btn.AddThemeStyleboxOverride("disabled",      dimSb);
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.Toggled += on => btn.Text = on ? "✓" : "";
        return btn;
    }

    private static void WireScroll(SpinBox spin)
    {
        spin.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true } btn) return;
            if (btn.ButtonIndex == MouseButton.WheelUp)
                { spin.Value += spin.Step; spin.AcceptEvent(); }
            else if (btn.ButtonIndex == MouseButton.WheelDown)
                { spin.Value -= spin.Step; spin.AcceptEvent(); }
        };
    }

    private Func<List<int>> BuildPartyTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Party" };
        tabs.AddChild(vbox);

        var pcs      = _db.Pf2eCharacters.GetAll(_encounter.CampaignId);
        var existing = _db.Pf2eEncounterCombatants.GetAll(_encounter.Id);
        var entries  = new Dictionary<int, Button>();

        var selectedSb = new StyleBoxFlat { BgColor = new Color(0.15f, 0.55f, 0.20f, 0.30f) };
        selectedSb.SetBorderWidthAll(1);
        selectedSb.BorderColor = new Color(0.30f, 0.80f, 0.35f, 0.50f);
        selectedSb.SetCornerRadiusAll(3);

        var scroll    = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var checkVBox = new VBoxContainer();
        checkVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(checkVBox);
        vbox.AddChild(scroll);

        foreach (var pc in pcs)
        {
            bool alreadyIn = existing.Any(c => c.CharacterId == pc.Id);

            var panel = new PanelContainer();
            var row   = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            panel.AddChild(row);

            var chk       = MakeCheckButton(alreadyIn);
            var nameLabel = new Label
            {
                Text                = alreadyIn ? $"{pc.Name} (Lv{pc.Level}) — already in" : $"{pc.Name} (Lv{pc.Level}  HP {pc.CurrentHp}/{pc.MaxHp})",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            if (alreadyIn) nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.45f));

            chk.Toggled += on =>
            {
                if (on) panel.AddThemeStyleboxOverride("panel", selectedSb);
                else    panel.RemoveThemeStyleboxOverride("panel");
            };
            panel.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && !alreadyIn)
                {
                    chk.ButtonPressed = !chk.ButtonPressed;
                    panel.AcceptEvent();
                }
            };

            row.AddChild(chk);
            row.AddChild(nameLabel);
            checkVBox.AddChild(panel);
            entries[pc.Id] = chk;
        }

        return () => entries
            .Where(kvp => kvp.Value.ButtonPressed && !kvp.Value.Disabled)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private Func<List<(Pf2eCreature cr, int qty)>> BuildCreatureTab(TabContainer tabs)
    {
        var vbox = new VBoxContainer { Name = "Creature" };
        tabs.AddChild(vbox);

        // ── Search ────────────────────────────────────────────────────────────
        var search = new LineEdit { PlaceholderText = "Search creatures..." };
        vbox.AddChild(search);

        // ── Filter row 1: Level + Type ────────────────────────────────────────
        var filterRow1 = new HBoxContainer();
        filterRow1.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(filterRow1);

        filterRow1.AddChild(new Label { Text = "Lv:" });
        var minLvSpin = new SpinBox { MinValue = -1, MaxValue = 25, Value = -1, CustomMinimumSize = new Vector2(56, 0) };
        WireScroll(minLvSpin);
        filterRow1.AddChild(minLvSpin);
        filterRow1.AddChild(new Label { Text = "–" });
        var maxLvSpin = new SpinBox { MinValue = -1, MaxValue = 25, Value = 25, CustomMinimumSize = new Vector2(56, 0) };
        WireScroll(maxLvSpin);
        filterRow1.AddChild(maxLvSpin);
        filterRow1.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        filterRow1.AddChild(new Label { Text = "Type:" });
        var creatureTypes = _db.Pf2eCreatureTypes.GetAll(_encounter.CampaignId);
        var optType = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0) };
        optType.AddItem("Any type", 0);
        foreach (var ct in creatureTypes) optType.AddItem(ct.Name, ct.Id);
        filterRow1.AddChild(optType);

        // ── Filter row 2: Trait + Source ──────────────────────────────────────
        var filterRow2 = new HBoxContainer();
        filterRow2.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(filterRow2);

        filterRow2.AddChild(new Label { Text = "Trait:" });
        var traitTypes = _db.Pf2eTraitTypes.GetAll(_encounter.CampaignId);
        var optTrait = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0) };
        optTrait.AddItem("Any trait", 0);
        foreach (var tt in traitTypes) optTrait.AddItem(tt.Name, tt.Id);
        filterRow2.AddChild(optTrait);

        var allCreatures = _db.Pf2eCreatures.GetAll(_encounter.CampaignId);
        var sources = allCreatures.Select(c => c.Source).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        filterRow2.AddChild(new Label { Text = "Src:" });
        var optSrc = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 0) };
        optSrc.AddItem("Any source", 0);
        for (int i = 0; i < sources.Count; i++) optSrc.AddItem(sources[i], i + 1);
        filterRow2.AddChild(optSrc);

        // ── Creature rows ─────────────────────────────────────────────────────
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var rowVBox = new VBoxContainer();
        rowVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rowVBox.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(rowVBox);
        vbox.AddChild(scroll);

        var traitMap = _db.Pf2eCreatureTraits.GetTraitTypeIdsByCreature(_encounter.CampaignId);
        var allRows  = new List<(Pf2eCreature cr, Button chk, SpinBox qtySpin, PanelContainer panel)>();

        var selectedSb = new StyleBoxFlat { BgColor = new Color(0.15f, 0.55f, 0.20f, 0.30f) };
        selectedSb.SetBorderWidthAll(1);
        selectedSb.BorderColor = new Color(0.30f, 0.80f, 0.35f, 0.50f);
        selectedSb.SetCornerRadiusAll(3);

        foreach (var cr in allCreatures)
        {
            var panel = new PanelContainer();
            var row   = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            panel.AddChild(row);

            var chk = MakeCheckButton();

            var nameScroll = new ScrollContainer
            {
                SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
                VerticalScrollMode   = ScrollContainer.ScrollMode.Disabled,
                MouseFilter          = Control.MouseFilterEnum.Ignore,
            };
            var nameLabel = new Label
            {
                Text         = cr.Name,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter  = Control.MouseFilterEnum.Ignore,
            };
            nameScroll.AddChild(nameLabel);

            var statsLabel = new Label { Text = $"Lv {cr.Level}, HP {cr.MaxHp}, AC {cr.Ac}" };
            statsLabel.CustomMinimumSize = new Vector2(140, 0);
            statsLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.6f));
            var qtyLabel = new Label { Text = "×" };
            var qtySpin  = new SpinBox { MinValue = 1, MaxValue = 20, Value = 1, CustomMinimumSize = new Vector2(60, 0) };
            WireScroll(qtySpin);

            chk.Toggled += on =>
            {
                if (on) panel.AddThemeStyleboxOverride("panel", selectedSb);
                else    panel.RemoveThemeStyleboxOverride("panel");
            };
            panel.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    chk.ButtonPressed = !chk.ButtonPressed;
                    panel.AcceptEvent();
                }
            };

            Tween nameTween = null;
            panel.MouseEntered += () =>
            {
                nameTween?.Kill();
                var hbar = nameScroll.GetHScrollBar();
                int maxH = (int)(hbar.MaxValue - hbar.Page);
                if (maxH <= 0) return;
                nameTween = nameScroll.CreateTween();
                nameTween.TweenProperty(nameScroll, "scroll_horizontal", maxH, 1.5f);
            };
            panel.MouseExited += () =>
            {
                nameTween?.Kill();
                nameTween = nameScroll.CreateTween();
                nameTween.TweenProperty(nameScroll, "scroll_horizontal", 0, 0.3f);
            };

            row.AddChild(chk);
            row.AddChild(nameScroll);
            row.AddChild(statsLabel);
            row.AddChild(qtyLabel);
            row.AddChild(qtySpin);
            rowVBox.AddChild(panel);
            allRows.Add((cr, chk, qtySpin, panel));
        }

        // ── Filter logic ──────────────────────────────────────────────────────
        void ApplyFilters()
        {
            string txt     = search.Text;
            int    minLv   = (int)minLvSpin.Value;
            int    maxLv   = (int)maxLvSpin.Value;
            int    typeId  = (int)optType.GetItemId(optType.Selected);
            int    traitId = (int)optTrait.GetItemId(optTrait.Selected);
            string src     = optSrc.Selected == 0 ? "" : sources[optSrc.Selected - 1];

            foreach (var (cr, _, _, panel) in allRows)
            {
                bool visible = FuzzyMatch(cr.Name, txt)
                            && cr.Level >= minLv
                            && cr.Level <= maxLv
                            && (typeId  == 0 || cr.CreatureTypeId == typeId)
                            && (traitId == 0 || (traitMap.TryGetValue(cr.Id, out var traits) && traits.Contains(traitId)))
                            && (src    == "" || cr.Source == src);
                panel.Visible = visible;
            }
        }

        search.TextChanged     += _ => ApplyFilters();
        minLvSpin.ValueChanged += _ => ApplyFilters();
        maxLvSpin.ValueChanged += _ => ApplyFilters();
        optType.ItemSelected   += _ => ApplyFilters();
        optTrait.ItemSelected  += _ => ApplyFilters();
        optSrc.ItemSelected    += _ => ApplyFilters();

        return () => allRows
            .Where(r => r.chk.ButtonPressed)
            .Select(r => (r.cr, (int)r.qtySpin.Value))
            .ToList();
    }

    private static bool FuzzyMatch(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return true;
        int hi = 0;
        foreach (char c in needle.ToLowerInvariant())
        {
            while (hi < haystack.Length && char.ToLowerInvariant(haystack[hi]) != c) hi++;
            if (hi >= haystack.Length) return false;
            hi++;
        }
        return true;
    }

    private void ShowInitModal(List<(string DisplayName, System.Action<int> AddWithInit)> pending)
    {
        if (pending.Count == 0) return;

        int height = Math.Min(pending.Count * 38 + 80, 380);
        var win = new Window { Title = "Set Initiative", Size = new Vector2I(380, height), Unresizable = true };
        win.CloseRequested += win.QueueFree;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        win.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var rowBox = new VBoxContainer();
        rowBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rowBox.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(rowBox);
        vbox.AddChild(scroll);

        var spinBoxes = new List<(System.Action<int> AddWithInit, SpinBox initSpin)>();

        foreach (var (displayName, addWithInit) in pending)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var nameLabel = new Label { Text = displayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var initLabel = new Label { Text = "Init" };
            var initSpin  = new SpinBox { MinValue = -99, MaxValue = 99, CustomMinimumSize = new Vector2(80, 0) };
            WireScroll(initSpin);

            row.AddChild(nameLabel);
            row.AddChild(initLabel);
            row.AddChild(initSpin);
            rowBox.AddChild(row);
            spinBoxes.Add((addWithInit, initSpin));
        }

        var addBtn = new Button { Text = "Add All" };
        vbox.AddChild(addBtn);

        AddChild(win);
        win.PopupCentered();

        addBtn.Pressed += () =>
        {
            foreach (var (addWithInit, initSpin) in spinBoxes)
                addWithInit((int)initSpin.Value);
            LoadCombatants();
            win.QueueFree();
        };
    }

    private void BuildCustomTab(TabContainer tabs, Window win)
    {
        var vbox = new VBoxContainer { Name = "Custom" };
        tabs.AddChild(vbox);

        (string label, string placeholder)[] fields =
        {
            ("Name",       "Goblin Guard"),
            ("Max HP",     "12"),
            ("AC",         "15"),
            ("Initiative", "10")
        };
        var inputs = new Dictionary<string, LineEdit>();
        foreach (var (label, placeholder) in fields)
        {
            var row = new HBoxContainer();
            var lbl = new Label { Text = label };
            lbl.CustomMinimumSize = new Vector2(80, 0);
            var input = new LineEdit { PlaceholderText = placeholder };
            input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            row.AddChild(input);
            vbox.AddChild(row);
            inputs[label] = input;
        }

        var addBtn = new Button { Text = "Add" };
        vbox.AddChild(addBtn);

        addBtn.Pressed += () =>
        {
            string name = inputs["Name"].Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "Custom";
            int.TryParse(inputs["Max HP"].Text,     out int maxHp);
            int.TryParse(inputs["AC"].Text,          out int ac);
            int.TryParse(inputs["Initiative"].Text,  out int init);

            _db.Pf2eEncounterCombatants.Add(new Pf2eEncounterCombatant
            {
                EncounterId      = _encounter.Id,
                DisplayName      = name,
                CurrentHp        = maxHp,
                MaxHp            = maxHp,
                Ac               = ac,
                Initiative       = init,
                SortOrder        = -init,
                ActionsRemaining = 3
            });
            LoadCombatants();
            win.QueueFree();
        };
    }
}
