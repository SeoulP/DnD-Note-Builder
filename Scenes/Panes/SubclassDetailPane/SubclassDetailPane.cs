using System.Collections.Generic;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public partial class SubclassDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Subclass           _subclass;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NavigateToNewTabEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit         _nameInput;
    [Export] private VBoxContainer    _parentClassContainer;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes        _notes;
    [Export] private Button           _levelsToggle;
    [Export] private Control          _levelsInset;
    [Export] private VBoxContainer    _levelsContainer;
    [Export] private Button           _deleteButton;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged  += name => { Save(); EmitSignal(SignalName.NameChanged, "subclass", _subclass?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Subclass" : name); };
        _nameInput.FocusExited  += () => { if (_nameInput.Text == "") _nameInput.Text = "New Subclass"; };
        _nameInput.FocusEntered += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _descInput.TextChanged  += () => Save();
        _notes.TextChanged      += () => Save();
        _notes.NavigateTo       += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        WireSectionToggle(_levelsToggle, _levelsInset);

        _confirmDialog = DialogHelper.Make("Delete Subclass");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "subclass", _subclass?.Id ?? 0);
        _deleteButton.Pressed    += () => DialogHelper.Show(_confirmDialog, $"Delete \"{_subclass?.Name}\"? This cannot be undone.");
    }

    public void Load(Subclass subclass)
    {
        _subclass = subclass;
        _nameInput.Text = subclass.Name;
        _descInput.Text = subclass.Description;
        _notes.Text = subclass.Notes;

        // Parent class row
        foreach (Node child in _parentClassContainer.GetChildren()) child.QueueFree();
        var parentClass = _db.Classes.Get(subclass.ClassId);
        if (parentClass != null)
        {
            int clsId = parentClass.Id;
            var row = new EntityRow { Text = parentClass.Name, ShowDelete = false };
            row.NavigatePressed       += () => EmitSignal(SignalName.NavigateTo,       "class", clsId);
            row.NavigatePressedNewTab += () => EmitSignal(SignalName.NavigateToNewTab, "class", clsId);
            _parentClassContainer.AddChild(row);
        }

        _imageCarousel.Setup(EntityType.Subclass, subclass.Id, _db);
        LoadLevelProgression();
    }

    private void Save()
    {
        if (_subclass == null) return;
        _subclass.Name        = _nameInput.Text;
        _subclass.Description = _descInput.Text;
        _subclass.Notes       = _notes.Text;
        _db.Classes.EditSubclass(_subclass);
    }

    private void LoadLevelProgression()
    {
        foreach (Node child in _levelsContainer.GetChildren())
            child.QueueFree();

        if (_subclass == null) return;

        var parentClass = _db.Classes.Get(_subclass.ClassId);
        if (parentClass == null) return;

        var levels = _db.Classes.GetLevelsForClass(parentClass.Id);
        foreach (var lvl in levels)
            _levelsContainer.AddChild(BuildSubclassLevelRow(lvl));
    }

    private Control BuildSubclassLevelRow(ClassLevel lvl)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var content = new VBoxContainer { Visible = false };
        content.AddThemeConstantOverride("separation", 4);

        var header = new Button
        {
            Text                    = "▶  Level " + lvl.Level,
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
        };

        var abilityRows   = new VBoxContainer();
        abilityRows.AddThemeConstantOverride("separation", 2);
        var addAbilityBtn = new TypesDropdown();

        void Refresh()
        {
            foreach (Node child in abilityRows.GetChildren()) child.QueueFree();

            var links     = _db.Abilities.GetAbilityIdsForSubclassLevel(_subclass.Id, lvl.Level);
            var linkedIds = new HashSet<int>();
            foreach (var (id, _) in links) linkedIds.Add(id);

            foreach (var (abilId, usesStr) in links)
            {
                var ability = _db.Abilities.Get(abilId);
                if (ability == null) continue;
                int capId = abilId;

                var row     = new HBoxContainer();
                var nameBtn = new Button
                {
                    Text                    = ability.Name,
                    Flat                    = true,
                    Alignment               = HorizontalAlignment.Left,
                    SizeFlagsHorizontal     = SizeFlags.ExpandFill,
                    MouseDefaultCursorShape = CursorShape.PointingHand,
                };
                nameBtn.Pressed += () => EmitSignal(SignalName.NavigateTo, "ability", capId);

                var usesBtn = new Button
                {
                    Text              = UsesFormula.FormatForDisplay(usesStr),
                    CustomMinimumSize = new Vector2(80, 0),
                    TooltipText       = "Click to edit usage scaling",
                };
                string currentFormula = usesStr;
                usesBtn.Pressed += () =>
                {
                    var popup = new UsageProgressionPopup();
                    AddChild(popup);
                    popup.Setup(ability.Name, currentFormula);
                    popup.Saved += formula =>
                    {
                        currentFormula = formula;
                        _db.Abilities.UpdateSubclassAbilityUses(_subclass.Id, capId, formula);
                        usesBtn.Text = UsesFormula.FormatForDisplay(formula);
                    };
                    popup.PopupCentered();
                };

                var delBtn = new Button { Text = "×", Flat = true };
                delBtn.Pressed += () =>
                {
                    _db.Abilities.RemoveSubclassAbility(_subclass.Id, capId);
                    Refresh();
                };

                row.AddChild(nameBtn);
                row.AddChild(usesBtn);
                row.AddChild(delBtn);
                abilityRows.AddChild(row);
            }

            addAbilityBtn.NoneText = "(Add ability...)";
            addAbilityBtn.Setup(
                () => _db.Abilities.GetAll(_subclass.CampaignId)
                        .FindAll(a => !linkedIds.Contains(a.Id))
                        .ConvertAll(a => (a.Id, a.Name)),
                null, null);
            addAbilityBtn.SelectById(null);

            header.Text = (content.Visible ? "▼  " : "▶  ") + "Level " + lvl.Level;
        }

        addAbilityBtn.TypeSelected += id =>
        {
            if (id < 0) return;
            _db.Abilities.AddSubclassAbilityAtLevel(_subclass.Id, id, lvl.Level);
            Refresh();
        };

        Refresh();

        content.AddChild(abilityRows);
        content.AddChild(addAbilityBtn);

        header.Pressed += () =>
        {
            content.Visible = !content.Visible;
            header.Text     = (content.Visible ? "▼  " : "▶  ") + "Level " + lvl.Level;
        };

        box.AddChild(header);
        box.AddChild(content);
        return box;
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
}
