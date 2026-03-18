using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// A Button that, when clicked, shows a popup listing available types.
/// Each row has a hover-reveal × delete button. A "New type..." form sits at the bottom.
/// Replaces OptionButton for all seeded-type dropdowns (Species, NPC Status, etc.).
/// </summary>
public partial class TypeOptionButton : Button
{
    [Signal] public delegate void TypeSelectedEventHandler(int id);  // -1 = none

    private Func<List<(int Id, string Name)>> _getAll;
    private Action<string>                    _addItem;
    private Action<int>                       _deleteItem;

    private int?                        _selectedId;
    private List<(int Id, string Name)> _items = new();

    private ConfirmationDialog _deleteDialog;
    private int                _pendingDeleteId;
    private string             _pendingDeleteName;

    public string NoneText { get; set; } = "— None —";

    public int? SelectedId => _selectedId;

    public override void _Ready()
    {
        Alignment     = HorizontalAlignment.Left;
        IconAlignment = HorizontalAlignment.Right;
        ExpandIcon    = false;
        ClipText      = true;
        Text          = NoneText;
        Icon          = GetThemeIcon("arrow", "OptionButton");
        Pressed      += ShowPopup;

        _deleteDialog = new ConfirmationDialog { Title = "Delete Type" };
        AddChild(_deleteDialog);
        _deleteDialog.Confirmed += () =>
        {
            _deleteItem?.Invoke(_pendingDeleteId);
            if (_selectedId == _pendingDeleteId)
            {
                _selectedId = null;
                Text        = NoneText;
                EmitSignal(SignalName.TypeSelected, -1);
            }
            _items = _getAll?.Invoke() ?? _items;
        };
    }

    /// <summary>Wire the component to the relevant repository operations.</summary>
    public void Setup(
        Func<List<(int Id, string Name)>> getAll,
        Action<string>                    addItem,
        Action<int>                       deleteItem)
    {
        _getAll     = getAll;
        _addItem    = addItem;
        _deleteItem = deleteItem;
        _items      = getAll();
    }

    /// <summary>Set the selected item by DB id. Pass null for "none".</summary>
    public void SelectById(int? id)
    {
        _selectedId = id;
        if (!id.HasValue)
        {
            Text = NoneText;
            return;
        }
        var match = _items.Find(i => i.Id == id.Value);
        Text = match != default ? match.Name : NoneText;
    }

    // -------------------------------------------------------------------------

    private void ShowPopup()
    {
        if (_getAll == null) return;
        _items = _getAll();

        var popup = new PopupPanel();
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("separation", 0);

        // ── scrollable list ──────────────────────────────────────────────────
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = SizeFlags.ExpandFill;
        scroll.CustomMinimumSize   = new Vector2(0, 28);  // enough to show at least one row

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(vbox);
        outer.AddChild(scroll);

        // "— None —" row (no delete button)
        var noneBtn = MakeRowButton(NoneText);
        noneBtn.Pressed += () =>
        {
            _selectedId = null;
            Text        = NoneText;
            EmitSignal(SignalName.TypeSelected, -1);
            popup.Hide();
        };
        vbox.AddChild(noneBtn);

        // Type rows
        foreach (var (id, name) in _items)
        {
            int    cId   = id;
            string cName = name;

            var panel = new PanelContainer();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var nameBtn = MakeRowButton(cName);
            nameBtn.Pressed += () =>
            {
                _selectedId = cId;
                Text        = cName;
                EmitSignal(SignalName.TypeSelected, cId);
                popup.Hide();
            };

            var delBtn = new Button { Text = "×", Flat = true };
            delBtn.Modulate = new Color(1, 1, 1, 0);
            delBtn.Pressed += () =>
            {
                _pendingDeleteId   = cId;
                _pendingDeleteName = cName;
                popup.Hide();
                _deleteDialog.DialogText = $"Delete \"{cName}\"?\n\nThis will remove it from all records currently using this type.";
                _deleteDialog.PopupCentered();
            };

            // Reveal × and apply light hover when hovering the name button area
            nameBtn.MouseEntered += () => { delBtn.Modulate = Colors.White; panel.AddThemeStyleboxOverride("panel", RowHoverBox); };
            nameBtn.MouseExited  += () => { delBtn.Modulate = new Color(1, 1, 1, 0); panel.RemoveThemeStyleboxOverride("panel"); };

            // Red highlight only when hovering the × button itself
            delBtn.MouseEntered += () => { delBtn.Modulate = Colors.White; panel.AddThemeStyleboxOverride("panel", DeleteHoverBox); };
            delBtn.MouseExited  += () => panel.RemoveThemeStyleboxOverride("panel");

            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddThemeConstantOverride("separation", 0);
            row.AddChild(nameBtn);
            row.AddChild(delBtn);
            panel.AddChild(row);
            vbox.AddChild(panel);
        }

        // ── add-new form ─────────────────────────────────────────────────────
        outer.AddChild(new HSeparator());

        var addRow    = new HBoxContainer();
        var nameInput = new LineEdit
        {
            PlaceholderText     = "New type...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var addBtn = new Button { Text = "+ Add" };

        Action doAdd = () =>
        {
            var typeName = nameInput.Text.Trim();
            if (string.IsNullOrEmpty(typeName)) return;
            _addItem?.Invoke(typeName);
            _items = _getAll?.Invoke() ?? _items;
            var added = _items.Find(i => i.Name == typeName);
            if (added != default)
            {
                _selectedId = added.Id;
                Text        = added.Name;
                EmitSignal(SignalName.TypeSelected, added.Id);
            }
            nameInput.Text = "";
            popup.Hide();
        };
        addBtn.Pressed           += doAdd;
        nameInput.TextSubmitted  += _ => doAdd();

        addRow.AddChild(nameInput);
        addRow.AddChild(addBtn);
        outer.AddChild(addRow);

        // Fully opaque background, no extra content margins
        var panelBg = new StyleBoxFlat
        {
            BgColor             = new Color(0.14f, 0.14f, 0.14f),
            ContentMarginLeft   = 0,
            ContentMarginRight  = 0,
            ContentMarginTop    = 0,
            ContentMarginBottom = 0,
        };
        popup.AddThemeStyleboxOverride("panel", panelBg);

        popup.AddChild(outer);
        popup.PopupHide += () => popup.QueueFree();
        AddChild(popup);

        // Position below this button; height fits visible items + add row
        var rect      = GetGlobalRect();
        int popWidth  = Mathf.Max((int)rect.Size.X, 200);
        int listRows  = Math.Min(_items.Count + 1, 8);   // +1 for None; show at most 8 before scrolling
        int popHeight = listRows * 28 + 40;              // rows + separator + add row
        popup.Popup(new Rect2I((int)rect.Position.X, (int)rect.Position.Y + (int)rect.Size.Y, popWidth, popHeight));
    }

    private static readonly StyleBoxFlat RowHoverBox    = MakeRowHoverBox();
    private static readonly StyleBoxFlat DeleteHoverBox = MakeDeleteHoverBox();

    private static StyleBoxFlat MakeRowHoverBox()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0.25f, 0.25f, 0.25f) };
        box.SetCornerRadiusAll(3);
        return box;
    }

    private static StyleBoxFlat MakeDeleteHoverBox()
    {
        var box = new StyleBoxFlat { BgColor = new Color(0.90f, 0.55f, 0.55f) };
        box.SetCornerRadiusAll(3);
        return box;
    }

    private static Button MakeRowButton(string text) => new Button
    {
        Text                = text,
        Flat                = true,
        Alignment           = HorizontalAlignment.Left,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
    };
}
