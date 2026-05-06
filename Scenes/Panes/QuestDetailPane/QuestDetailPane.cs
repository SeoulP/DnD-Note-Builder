using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class QuestDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Quest              _quest;
    private ConfirmationDialog _confirmDialog;
    private Button             _npcNavBtn;
    private Button             _locNavBtn;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    [Export] private LineEdit         _nameInput;
    [Export] private TypesDropdown _statusInput;
    [Export] private TypesDropdown _questGiverInput;
    [Export] private TypesDropdown _locationInput;
    [Export] private VBoxContainer    _rewardsContainer;
    [Export] private Button           _addRewardButton;
    [Export] private TextEdit         _descInput;
    [Export] private MarkdownNotes        _notes;
    [Export] private VBoxContainer    _historyContainer;
    [Export] private Button           _addHistoryButton;
    [Export] private Button           _deleteButton;
    [Export] private VBoxContainer    _aliasChipsRow;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "quest", _quest?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Quest" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Quest"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _statusInput.TypeSelected     += _ => Save();
        _questGiverInput.TypeSelected += _ => Save();
        _locationInput.TypeSelected   += _ => Save();

        _npcNavBtn = new Button { Text = "→", Flat = true, TooltipText = "Open NPC", Disabled = true, MouseDefaultCursorShape = CursorShape.PointingHand };
        _questGiverInput.TypeSelected += id => _npcNavBtn.Disabled = id <= 0;
        _npcNavBtn.Pressed += () => { if (_questGiverInput.SelectedId.HasValue) EmitSignal(SignalName.NavigateTo, "npc", _questGiverInput.SelectedId.Value); };
        _questGiverInput.GetParent().AddChild(_npcNavBtn);

        _locNavBtn = new Button { Text = "→", Flat = true, TooltipText = "Open Location", Disabled = true, MouseDefaultCursorShape = CursorShape.PointingHand };
        _locationInput.TypeSelected += id => _locNavBtn.Disabled = id <= 0;
        _locNavBtn.Pressed += () => { if (_locationInput.SelectedId.HasValue) EmitSignal(SignalName.NavigateTo, "location", _locationInput.SelectedId.Value); };
        _locationInput.GetParent().AddChild(_locNavBtn);
        _addRewardButton.Pressed += () =>
        {
            if (_quest == null) return;
            _db.QuestRewards.Add(new QuestReward { QuestId = _quest.Id, SortOrder = _db.QuestRewards.GetAll(_quest.Id).Count });
            LoadRewardRows();
        };
        _descInput.TextChanged       += () => Save();
        _notes.TextChanged   += () => Save();
        _notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);

        _addHistoryButton.Pressed += () =>
        {
            if (_quest == null) return;
            _db.QuestHistory.Add(new QuestHistory { QuestId = _quest.Id });
            LoadHistoryRows();
        };

        _confirmDialog = DialogHelper.Make("Delete Quest");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "quest", _quest?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_quest?.Name}\"? This cannot be undone.");
    }

    public void Load(Quest quest)
    {
        _quest = quest;

        _statusInput.NoneText        = "(none)";
        _statusInput.AutoSelectOnAdd = true;
        _statusInput.Setup(
            () => _db.QuestStatuses.GetAll(quest.CampaignId).ConvertAll(s => (s.Id, s.Name)),
            name => _db.QuestStatuses.Add(new QuestStatus { CampaignId = quest.CampaignId, Name = name, Description = "" }),
            id   => _db.QuestStatuses.Delete(id));
        _statusInput.SelectById(quest.StatusId);

        _questGiverInput.NoneText = "(none)";
        _questGiverInput.Setup(
            () => _db.Npcs.GetAll(quest.CampaignId).ConvertAll(n => (n.Id, n.Name)),
            null, null);
        _questGiverInput.SelectById(quest.QuestGiverId);
        if (_npcNavBtn != null) _npcNavBtn.Disabled = !(quest.QuestGiverId > 0);

        _locationInput.NoneText = "(none)";
        _locationInput.Setup(
            () => _db.Locations.GetAll(quest.CampaignId).ConvertAll(l => (l.Id, l.Name)),
            null, null);
        _locationInput.SelectById(quest.LocationId);
        if (_locNavBtn != null) _locNavBtn.Disabled = !(quest.LocationId > 0);

        _imageCarousel?.Setup(EntityType.Quest, quest.Id, _db, quest.CampaignId);

        _nameInput.Text = string.IsNullOrEmpty(quest.Name) ? "New Quest" : quest.Name;
        _descInput.Text = quest.Description;
        _notes.Setup(quest.CampaignId, _db);
        _notes.Text = quest.Notes;

        LoadRewardRows();
        LoadHistoryRows();
        LoadAliases();
    }

    private void LoadAliases() =>
        AliasChipsHelper.Reload(_aliasChipsRow, _db, "quest", _quest?.Id ?? 0, _quest?.CampaignId ?? 0, LoadAliases);

    private void LoadRewardRows()
    {
        foreach (Node child in _rewardsContainer.GetChildren())
            child.QueueFree();

        foreach (var reward in _db.QuestRewards.GetAll(_quest.Id))
            _rewardsContainer.AddChild(BuildRewardRow(reward));
    }

    private Control BuildRewardRow(QuestReward reward)
    {
        int rewardId = reward.Id;

        var hbox = new HBoxContainer();

        var claimed = new CheckBox { ButtonPressed = reward.IsClaimed };
        claimed.Toggled += pressed =>
        {
            var r = _db.QuestRewards.GetAll(_quest.Id).Find(x => x.Id == rewardId);
            if (r == null) return;
            r.IsClaimed = pressed;
            _db.QuestRewards.Edit(r);
        };

        var desc = new LineEdit
        {
            Text                = reward.Description,
            PlaceholderText     = "Reward description...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        desc.TextChanged += _ =>
        {
            var r = _db.QuestRewards.GetAll(_quest.Id).Find(x => x.Id == rewardId);
            if (r == null) return;
            r.Description = desc.Text;
            _db.QuestRewards.Edit(r);
        };

        var deleteBtn  = new Button { Text = "×", Flat = true };
        var delConfirm = DialogHelper.Make("Delete Reward");
        AddChild(delConfirm);
        delConfirm.Confirmed += () => { _db.QuestRewards.Delete(rewardId); LoadRewardRows(); };
        deleteBtn.Pressed    += () => DialogHelper.Show(delConfirm, "Delete this reward?");

        hbox.AddChild(claimed);
        hbox.AddChild(desc);
        hbox.AddChild(deleteBtn);
        return hbox;
    }

    private void LoadHistoryRows()
    {
        foreach (Node child in _historyContainer.GetChildren())
            child.QueueFree();

        var sessions = _db.Sessions.GetAll(_quest.CampaignId);
        var entries  = _db.QuestHistory.GetAll(_quest.Id);

        foreach (var entry in entries)
        {
            var (row, notes) = BuildHistoryRow(entry, sessions);
            _historyContainer.AddChild(row); // _Ready() fires on notes here
            notes.Setup(_quest.CampaignId, _db);
            notes.Text = entry.Note;
        }
    }

    private (Control row, MarkdownNotes notes) BuildHistoryRow(QuestHistory entry, List<Session> sessions)
    {
        int entryId = entry.Id;

        var panel = new PanelContainer();
        var vbox  = new VBoxContainer();
        panel.AddChild(vbox);

        // Session selector row
        var hbox      = new HBoxContainer();
        var sesLabel  = new Label { Text = "Session:" };
        sesLabel.AddThemeFontSizeOverride("font_size", 12);
        var sesOption = new OptionButton();
        sesOption.AddThemeFontSizeOverride("font_size", 12);
        sesOption.AddItem("(none)", -1);
        int selectedIdx = 0;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            sesOption.AddItem(string.IsNullOrEmpty(s.Title) ? $"Session {s.Number}" : s.Title, s.Id);
            if (entry.SessionId == s.Id) selectedIdx = i + 1;
        }
        sesOption.Selected = selectedIdx;
        sesOption.ItemSelected += idx =>
        {
            var e = _db.QuestHistory.GetAll(_quest.Id).Find(h => h.Id == entryId);
            if (e == null) return;
            e.SessionId = sesOption.GetItemId((int)idx) == -1 ? null : sesOption.GetItemId((int)idx);
            _db.QuestHistory.Edit(e);
        };

        var deleteBtn = new Button { Text = "×" };
        deleteBtn.AddThemeFontSizeOverride("font_size", 12);
        var delConfirm = DialogHelper.Make("Delete Entry");
        AddChild(delConfirm);
        delConfirm.Confirmed += () => { _db.QuestHistory.Delete(entryId); LoadHistoryRows(); };
        deleteBtn.Pressed    += () => DialogHelper.Show(delConfirm, "Delete this history entry?");

        hbox.AddChild(sesLabel);
        hbox.AddChild(sesOption);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hbox.AddChild(spacer);
        hbox.AddChild(deleteBtn);
        vbox.AddChild(hbox);

        var notes = GD.Load<PackedScene>("res://Scenes/Components/MarkdownNotes/markdown_notes.tscn")
                      .Instantiate<MarkdownNotes>();
        notes.PlaceholderText = "What happened...";
        notes.TextChanged += () =>
        {
            var e = _db.QuestHistory.GetAll(_quest.Id).Find(h => h.Id == entryId);
            if (e == null) return;
            e.Note = notes.Text;
            _db.QuestHistory.Edit(e);
        };
        notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);
        vbox.AddChild(notes);

        return (panel, notes);
    }

    private void Save()
    {
        if (_quest == null) return;
        _quest.Name         = string.IsNullOrEmpty(_nameInput.Text) ? "New Quest" : _nameInput.Text;
        _quest.StatusId     = _statusInput.SelectedId;
        _quest.QuestGiverId = _questGiverInput.SelectedId;
        _quest.LocationId   = _locationInput.SelectedId;
        _quest.Description  = _descInput.Text;
        _quest.Notes        = _notes.Text;
        _db.Quests.Edit(_quest);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_quest == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_quest.Name}\"? This cannot be undone.");
            AcceptEvent();
        }
    }
}
