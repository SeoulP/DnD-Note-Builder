using System.Collections.Generic;
using Godot;

/// <summary>
/// Click-to-edit notes field with live wiki-link rendering and [[autocomplete]].
/// Typing [[ opens a non-stealing autocomplete panel.
/// Arrow keys navigate, Tab/Enter confirm, Escape closes.
/// </summary>
public partial class WikiNotes : VBoxContainer
{
    [Signal] public delegate void TextChangedEventHandler();
    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);

    [Export] public string PlaceholderText
    {
        get => _input?.PlaceholderText ?? _pendingPlaceholder;
        set { _pendingPlaceholder = value; if (_input != null) _input.PlaceholderText = value; }
    }

    private string          _pendingPlaceholder = "DM notes... use [[Name]] to link";
    private TextEdit        _input;
    private RichTextLabel   _renderer;
    private DatabaseService _db;
    private int             _campaignId;

    // Autocomplete — plain PanelContainer so it NEVER steals focus
    private PanelContainer _acPanel;
    private VBoxContainer  _acList;
    private int            _acSelectedIndex = -1;

    private static readonly StyleBoxFlat AcSelectedBox = MakeAcSelectedBox();
    private static StyleBoxFlat MakeAcSelectedBox()
    {
        var b = new StyleBoxFlat { BgColor = new Color(0.25f, 0.35f, 0.55f) };
        return b;
    }

    // ── public API ────────────────────────────────────────────────────────────

    public string Text
    {
        get => _input?.Text ?? "";
        set
        {
            if (_input == null) return;
            _input.Text = value;
            bool hasText   = !string.IsNullOrEmpty(value);
            _input.Visible    = !hasText;
            _renderer.Visible = hasText;
            if (hasText && _db != null) Render();
        }
    }

    public void Setup(int campaignId, DatabaseService db)
    {
        _campaignId = campaignId;
        _db         = db;
        if (!string.IsNullOrEmpty(_input?.Text))
            Render();
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _input    = GetNode<TextEdit>("NotesInput");
        _renderer = GetNode<RichTextLabel>("NotesRenderer");

        _input.PlaceholderText      = _pendingPlaceholder;
        _renderer.CustomMinimumSize = new Vector2(0, _input.CustomMinimumSize.Y);

        _input.TextChanged += () => EmitSignal(SignalName.TextChanged);
        _input.TextChanged += CheckAutocomplete;
        _input.FocusExited += OnInputFocusExited;
        _input.GuiInput    += OnInputKey;

        _renderer.MetaClicked += (Variant meta) =>
        {
            var parts = meta.AsString().Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                EmitSignal(SignalName.NavigateTo, parts[0], id);
        };
        _renderer.MouseDefaultCursorShape = CursorShape.Ibeam;
        var inputStyle = _input.GetThemeStylebox("normal").Duplicate() as StyleBox;
        if (inputStyle != null)
            _renderer.AddThemeStyleboxOverride("normal", inputStyle);
        _renderer.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _input.Visible    = true;
                _renderer.Visible = false;
                _input.GrabFocus();
            }
        };
    }

    public override void _ExitTree()
    {
        if (_acPanel != null && IsInstanceValid(_acPanel))
            _acPanel.QueueFree();
    }

    public override void _Input(InputEvent e)
    {
        if (_input == null || !_input.Visible) return;
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            if (!_input.GetGlobalRect().HasPoint(mb.GlobalPosition))
                _input.ReleaseFocus();
    }

    private void OnInputFocusExited()
    {
        HideAutocomplete();
        Render();
        bool hasText      = !string.IsNullOrEmpty(_input.Text);
        _input.Visible    = !hasText;
        _renderer.Visible = hasText;
    }

    // ── autocomplete ──────────────────────────────────────────────────────────

    private void CheckAutocomplete()
    {
        if (_db == null) return;

        int    line     = _input.GetCaretLine();
        int    col      = _input.GetCaretColumn();
        string lineText = _input.GetLine(line);
        if (col > lineText.Length) col = lineText.Length;

        string before  = lineText[..col];
        int    openIdx = before.LastIndexOf("[[");
        if (openIdx < 0) { HideAutocomplete(); return; }

        string query = before[(openIdx + 2)..];
        if (query.Contains("]]") || query.Contains("[[")) { HideAutocomplete(); return; }

        var matches = GetEntityMatches(query);
        if (matches.Count == 0) { HideAutocomplete(); return; }

        var caretRect = _input.GetRectAtLineColumn(line, col);
        var screenPos = _input.GlobalPosition + caretRect.Position + new Vector2(0, caretRect.Size.Y);

        EnsurePanel(screenPos, matches.Count);
        BuildList(matches);
    }

    private void EnsurePanel(Vector2 screenPos, int rowCount)
    {
        int h = Mathf.Min(rowCount, 7) * 28 + 4;

        if (_acPanel != null && IsInstanceValid(_acPanel))
        {
            _acPanel.Position         = screenPos;
            _acPanel.CustomMinimumSize = new Vector2(280, h);
            _acPanel.Size              = new Vector2(280, h);
            return;
        }

        _acPanel = new PanelContainer();
        _acPanel.ZIndex            = 100;
        _acPanel.CustomMinimumSize = new Vector2(280, h);
        _acPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor             = new Color(0.14f, 0.14f, 0.14f),
            BorderColor         = new Color(0.40f, 0.40f, 0.40f),
            BorderWidthLeft     = 1, BorderWidthRight  = 1,
            BorderWidthTop      = 1, BorderWidthBottom = 1,
        });

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = SizeFlags.ExpandFill;

        _acList = new VBoxContainer();
        _acList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _acList.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_acList);
        _acPanel.AddChild(scroll);

        GetTree().Root.AddChild(_acPanel);
        _acPanel.Position = screenPos;
    }

    private void BuildList(List<(string Name, string TypeLabel)> matches)
    {
        if (_acList == null) return;
        foreach (Node child in _acList.GetChildren())
            child.QueueFree();

        _acSelectedIndex = 0; // auto-select first

        foreach (var (name, typeLabel) in matches)
        {
            string cName  = name;
            string cLabel = typeLabel;

            var row = new PanelContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            var nameBtn = new Button
            {
                Text                = cName,
                Flat                = true,
                Alignment           = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                FocusMode           = FocusModeEnum.None, // never steals keyboard focus
            };
            var typeTag = new Label { Text = cLabel, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            typeTag.Modulate = new Color(0.55f, 0.55f, 0.55f);
            typeTag.AddThemeFontSizeOverride("font_size", 10);

            nameBtn.Pressed += () =>
            {
                InsertCompletion(cName);
                HideAutocomplete();
                _input.GrabFocus();
            };

            hbox.AddChild(nameBtn);
            hbox.AddChild(typeTag);
            row.AddChild(hbox);
            _acList.AddChild(row);
        }

        UpdateRowHighlight();
    }

    private void UpdateRowHighlight()
    {
        if (_acList == null) return;
        for (int i = 0; i < _acList.GetChildCount(); i++)
        {
            var row = _acList.GetChild(i) as PanelContainer;
            if (row == null) continue;
            if (i == _acSelectedIndex)
                row.AddThemeStyleboxOverride("panel", AcSelectedBox);
            else
                row.RemoveThemeStyleboxOverride("panel");
        }
    }

    private void HideAutocomplete()
    {
        if (_acPanel != null && IsInstanceValid(_acPanel))
        {
            _acPanel.QueueFree();
            _acPanel = null;
            _acList  = null;
        }
        _acSelectedIndex = -1;
    }

    private void OnInputKey(InputEvent e)
    {
        if (_acPanel == null || !IsInstanceValid(_acPanel)) return;
        if (e is not InputEventKey { Pressed: true } key) return;

        int count = _acList?.GetChildCount() ?? 0;

        switch (key.Keycode)
        {
            case Key.Escape:
                HideAutocomplete();
                _input.AcceptEvent();
                break;

            case Key.Down:
                _acSelectedIndex = (_acSelectedIndex + 1) % count;
                UpdateRowHighlight();
                _input.AcceptEvent();
                break;

            case Key.Up:
                _acSelectedIndex = (_acSelectedIndex - 1 + count) % count;
                UpdateRowHighlight();
                _input.AcceptEvent();
                break;

            case Key.Tab:
            case Key.Enter:
            case Key.KpEnter:
                ConfirmSelection();
                _input.AcceptEvent();
                break;
        }
    }

    private void ConfirmSelection()
    {
        if (_acList == null || _acSelectedIndex < 0 || _acSelectedIndex >= _acList.GetChildCount()) return;
        var row    = _acList.GetChild(_acSelectedIndex) as PanelContainer;
        var hbox   = row?.GetChild(0) as HBoxContainer;
        var btn    = hbox?.GetChild(0) as Button;
        btn?.EmitSignal(Button.SignalName.Pressed);
    }

    private void InsertCompletion(string name)
    {
        int    line     = _input.GetCaretLine();
        int    col      = _input.GetCaretColumn();
        string lineText = _input.GetLine(line);
        if (col > lineText.Length) col = lineText.Length;

        string before  = lineText[..col];
        int    openIdx = before.LastIndexOf("[[");
        if (openIdx < 0) return;

        _input.SetLine(line, lineText[..openIdx] + $"[[{name}]]" + lineText[col..]);
        _input.SetCaretColumn(openIdx + 2 + name.Length + 2);
    }

    // ── entity lookup ─────────────────────────────────────────────────────────

    private List<(string Name, string TypeLabel)> GetEntityMatches(string query)
    {
        var results = new List<(string, string)>();
        if (_db == null) return results;

        string q = query.ToLowerInvariant();
        void Add(string name, string label)
        {
            if (q.Length == 0 || name.ToLowerInvariant().Contains(q))
                results.Add((name, label));
        }

        foreach (var x in _db.Npcs.GetAll(_campaignId))      Add(x.Name,  "NPC");
        foreach (var x in _db.Factions.GetAll(_campaignId))  Add(x.Name,  "Faction");
        foreach (var x in _db.Locations.GetAll(_campaignId)) Add(x.Name,  "Location");
        foreach (var x in _db.Sessions.GetAll(_campaignId))  Add(x.Title, "Session");

        return results;
    }

    // ── render ────────────────────────────────────────────────────────────────

    private void Render()
    {
        if (_db == null || _input == null) return;
        _renderer.Text = WikiLinkParser.Parse(_input.Text, _db, _campaignId);
    }
}
