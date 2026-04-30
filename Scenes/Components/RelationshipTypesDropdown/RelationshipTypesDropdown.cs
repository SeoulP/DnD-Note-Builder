using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Two-column variant of TypesDropdown for NPC relationship types.
/// Each row shows [ Forward label ] ↔ [ Reverse label ] so the user can
/// pick both the type and its direction in one click.
/// Symmetric types (ReverseLabel == null) show a single full-width button.
/// </summary>
public partial class RelationshipTypesDropdown : Button
{
    [Signal] public delegate void TypeSelectedEventHandler(int id, bool isReversed);
    [Signal] public delegate void TypeCreatedEventHandler(int id);
    [Signal] public delegate void PopupClosedEventHandler();

    private Func<List<(int Id, string Name, string? ReverseLabel)>> _getAll;
    private Action<string, string?>                                  _addItem;
    private Action<int>                                              _deleteItem;

    private int?                                                    _selectedId;
    private bool                                                    _isReversed;
    private List<(int Id, string Name, string? ReverseLabel)>       _items = new();

    private ConfirmationDialog _deleteDialog;
    private int                _pendingDeleteId;
    private string             _pendingDeleteName;

    public string NoneText        { get; set; } = "None";
    public bool   AutoSelectOnAdd { get; set; } = false;

    public int?  SelectedId => _selectedId;
    public bool  IsReversed => _isReversed;

    public override void _Ready()
    {
        Alignment     = HorizontalAlignment.Left;
        IconAlignment = HorizontalAlignment.Right;
        ExpandIcon    = false;
        ClipText      = true;
        Text          = NoneText;
        Icon          = GetThemeIcon("arrow", "OptionButton");
        Pressed      += ShowPopup;

        _deleteDialog = DialogHelper.Make("Delete Relationship Type");
        AddChild(_deleteDialog);
        _deleteDialog.Confirmed += () =>
        {
            _deleteItem?.Invoke(_pendingDeleteId);
            if (_selectedId == _pendingDeleteId)
            {
                _selectedId = null;
                _isReversed = false;
                Text        = NoneText;
                EmitSignal(SignalName.TypeSelected, -1, false);
            }
            _items = _getAll?.Invoke() ?? _items;
        };
    }

    public void Setup(
        Func<List<(int Id, string Name, string? ReverseLabel)>> getAll,
        Action<string, string?>                                  addItem,
        Action<int>                                              deleteItem)
    {
        _getAll     = getAll;
        _addItem    = addItem;
        _deleteItem = deleteItem;
        _items      = getAll();
    }

    public void SelectById(int? id)
    {
        _selectedId = id;
        _isReversed = false;
        UpdateButtonLabel();
    }

    private void UpdateButtonLabel()
    {
        if (!_selectedId.HasValue) { Text = NoneText; return; }
        var match = _items.Find(i => i.Id == _selectedId.Value);
        if (match == default) { Text = NoneText; return; }
        Text = (_isReversed && !string.IsNullOrEmpty(match.ReverseLabel))
            ? match.ReverseLabel
            : match.Name;
    }

    // ── popup ─────────────────────────────────────────────────────────────────

    public void ShowPopup()
    {
        if (_getAll == null) return;
        _items = _getAll();

        var popup = new PopupPanel();
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("separation", 0);

        // ── search ────────────────────────────────────────────────────────────
        var searchInput = new LineEdit
        {
            PlaceholderText    = "Search...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClearButtonEnabled  = true,
            CaretBlink          = true,
        };
        outer.AddChild(searchInput);
        outer.AddChild(new HSeparator());

        // ── list ──────────────────────────────────────────────────────────────
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = SizeFlags.ExpandFill;
        scroll.CustomMinimumSize   = new Vector2(0, 28);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(vbox);
        outer.AddChild(scroll);

        BuildList(vbox, popup, _items);

        searchInput.TextChanged += text =>
        {
            var filtered = string.IsNullOrEmpty(text.Trim())
                ? _items
                : _items.Where(i => FuzzyMatch(text.Trim(), i.Name)
                               || (!string.IsNullOrEmpty(i.ReverseLabel) && FuzzyMatch(text.Trim(), i.ReverseLabel)))
                        .ToList();
            BuildList(vbox, popup, filtered);
        };

        // ── add form ──────────────────────────────────────────────────────────
        outer.AddChild(new HSeparator());

        var addRow     = new HBoxContainer();
        addRow.AddThemeConstantOverride("separation", 4);
        var fwdInput   = new LineEdit { PlaceholderText = "Forward label...", SizeFlagsHorizontal = SizeFlags.ExpandFill, CaretBlink = true };
        var arrowLabel = new Label   { Text = "↔" };
        var revInput   = new LineEdit { PlaceholderText = "Reverse (optional)...", SizeFlagsHorizontal = SizeFlags.ExpandFill, CaretBlink = true };
        var addBtn     = new Button  { Text = "+ Add" };

        Action doAdd = () =>
        {
            string fwd = fwdInput.Text.Trim();
            if (string.IsNullOrEmpty(fwd)) return;
            string? rev = string.IsNullOrEmpty(revInput.Text.Trim()) ? null : revInput.Text.Trim();
            _addItem?.Invoke(fwd, rev);
            _items         = _getAll?.Invoke() ?? _items;
            fwdInput.Text  = "";
            revInput.Text  = "";
            searchInput.Text = "";
            var created = _items.Find(i => i.Name == fwd);
            if (AutoSelectOnAdd && created != default)
            {
                _selectedId = created.Id;
                _isReversed = false;
                UpdateButtonLabel();
                EmitSignal(SignalName.TypeSelected, created.Id, false);
                popup.Hide();
                EmitSignal(SignalName.TypeCreated, created.Id);
                return;
            }
            if (created != default)
                EmitSignal(SignalName.TypeCreated, created.Id);
            BuildList(vbox, popup, _items);
            fwdInput.GrabFocus();
        };
        addBtn.Pressed          += doAdd;
        fwdInput.TextSubmitted  += _ => doAdd();
        revInput.TextSubmitted  += _ => doAdd();

        addRow.AddChild(fwdInput);
        addRow.AddChild(arrowLabel);
        addRow.AddChild(revInput);
        addRow.AddChild(addBtn);
        outer.AddChild(addRow);

        popup.AddChild(outer);
        popup.PopupHide += () => { popup.QueueFree(); EmitSignal(SignalName.PopupClosed); };
        AddChild(popup);

        var rect      = GetGlobalRect();
        int popWidth  = Mathf.Max((int)rect.Size.X, 380);
        int listRows  = Math.Min(_items.Count + 1, 8);
        int popHeight = listRows * 28 + 40 + 40; // search + add row
        popup.Popup(new Rect2I((int)rect.Position.X, (int)rect.Position.Y + (int)rect.Size.Y, popWidth, popHeight));

        searchInput.CallDeferred(LineEdit.MethodName.GrabFocus);
    }

    private void BuildList(VBoxContainer vbox, PopupPanel popup, List<(int Id, string Name, string? ReverseLabel)> items)
    {
        foreach (Node child in vbox.GetChildren())
            child.QueueFree();

        var rowHoverBox    = GetThemeStylebox("row_hover",    "DndBuilder") as StyleBoxFlat ?? MakeHoverBox(ThemeManager.Instance.Current.Hover);
        var deleteHoverBox = GetThemeStylebox("delete_hover", "DndBuilder") as StyleBoxFlat ?? MakeHoverBox(ThemeManager.DeleteHoverColor);

        // — None —
        var noneBtn = MakeHalfButton(NoneText);
        noneBtn.Pressed += () =>
        {
            _selectedId = null;
            _isReversed = false;
            Text        = NoneText;
            EmitSignal(SignalName.TypeSelected, -1, false);
            popup.Hide();
        };
        vbox.AddChild(noneBtn);

        foreach (var (id, name, reverseLabel) in items)
        {
            int     cId    = id;
            string  cName  = name;
            string? cRev   = reverseLabel;
            bool    hasRev = !string.IsNullOrEmpty(reverseLabel);

            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var row   = new HBoxContainer  { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 8);

            if (hasRev)
            {
                // Two-column: [ Forward ] ↔ [ Reverse ]
                var fwdBtn = MakeHalfButton(cName);
                var sep = new Label
                {
                    Text                = "↔",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    Modulate            = new Color(0.5f, 0.5f, 0.5f),
                };
                sep.AddThemeFontSizeOverride("font_size", 11);
                var revBtn = MakeHalfButton(cRev!);

                fwdBtn.Pressed += () => { Select(cId, false, cName, popup); };
                revBtn.Pressed += () => { Select(cId, true,  cRev!, popup); };

                fwdBtn.MouseEntered += () => { fwdBtn.Modulate = Colors.White; panel.AddThemeStyleboxOverride("panel", rowHoverBox); };
                fwdBtn.MouseExited  += () => { fwdBtn.Modulate = Colors.White; panel.RemoveThemeStyleboxOverride("panel"); };
                revBtn.MouseEntered += () => { revBtn.Modulate = Colors.White; panel.AddThemeStyleboxOverride("panel", rowHoverBox); };
                revBtn.MouseExited  += () => { revBtn.Modulate = Colors.White; panel.RemoveThemeStyleboxOverride("panel"); };

                row.AddChild(fwdBtn);
                row.AddChild(sep);
                row.AddChild(revBtn);
            }
            else
            {
                // Single column: symmetric type
                var nameBtn = MakeHalfButton(cName);
                nameBtn.Pressed      += () => { Select(cId, false, cName, popup); };
                nameBtn.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", rowHoverBox);
                nameBtn.MouseExited  += () => panel.RemoveThemeStyleboxOverride("panel");
                row.AddChild(nameBtn);
            }

            // Delete ×
            var delBtn = new Button { Text = "×", Flat = true };
            delBtn.Modulate = new Color(1, 1, 1, 0);
            delBtn.Pressed += () =>
            {
                _pendingDeleteId   = cId;
                _pendingDeleteName = cName;
                popup.Hide();
                DialogHelper.Show(_deleteDialog, $"Delete \"{cName}\"?\n\nThis will remove it from all records currently using this type.");
            };
            delBtn.MouseEntered += () => { delBtn.Modulate = Colors.White; panel.AddThemeStyleboxOverride("panel", deleteHoverBox); };
            delBtn.MouseExited  += () => { delBtn.Modulate = new Color(1, 1, 1, 0); panel.RemoveThemeStyleboxOverride("panel"); };

            row.AddChild(delBtn);
            panel.AddChild(row);
            vbox.AddChild(panel);
        }
    }

    private void Select(int id, bool isReversed, string label, PopupPanel popup)
    {
        _selectedId = id;
        _isReversed = isReversed;
        Text        = label;
        EmitSignal(SignalName.TypeSelected, id, isReversed);
        popup.Hide();
    }

    // ── fuzzy match (same as TypesDropdown) ────────────────────────────────

    private const float FuzzyThreshold = 0.7f;

    private static bool FuzzyMatch(string query, string target)
    {
        query  = query.ToLowerInvariant();
        target = target.ToLowerInvariant();
        if (target.Contains(query)) return true;
        int qLen = query.Length, tLen = target.Length;
        if (qLen <= tLen)
        {
            for (int s = 0; s <= tLen - qLen; s++)
                if (Similarity(query, target.Substring(s, qLen)) >= FuzzyThreshold) return true;
        }
        else if (Similarity(query, target) >= FuzzyThreshold) return true;
        return false;
    }

    private static float Similarity(string a, string b)
    {
        int dist = LevenshteinDistance(a, b);
        return 1f - (float)dist / Math.Max(a.Length, b.Length);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int aLen = a.Length, bLen = b.Length;
        var dp = new int[aLen + 1, bLen + 1];
        for (int i = 0; i <= aLen; i++) dp[i, 0] = i;
        for (int j = 0; j <= bLen; j++) dp[0, j] = j;
        for (int i = 1; i <= aLen; i++)
            for (int j = 1; j <= bLen; j++)
                dp[i, j] = a[i - 1] == b[j - 1]
                    ? dp[i - 1, j - 1]
                    : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
        return dp[aLen, bLen];
    }

    // ── styles ────────────────────────────────────────────────────────────────

    private static StyleBoxFlat MakeHoverBox(Color color)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(3);
        return box;
    }

    private static Button MakeHalfButton(string text) => new Button
    {
        Text                = text,
        Flat                = true,
        Alignment           = HorizontalAlignment.Left,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
    };
}
