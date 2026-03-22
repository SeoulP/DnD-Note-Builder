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
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

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

    private bool           _stubCreated;

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

        // Grow to fit content instead of showing a scrollbar — height is managed via
        // CustomMinimumSize so the control never needs to scroll internally.
        _input.ScrollFitContentHeight = true;

        _input.TextChanged += () => EmitSignal(SignalName.TextChanged);
        _input.TextChanged += UpdateInputHeight;
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

        // Match renderer appearance to the TextEdit so view/edit look identical.
        // Use type-specific lookup so we get the font the TextEdit actually renders with.

        var font        = _input.GetThemeFont("font", "TextEdit") ?? ThemeDB.Singleton.FallbackFont;
        int fontSize    = _input.GetThemeFontSize("font_size", "TextEdit");
        int lineSpacing = _input.GetThemeConstant("line_spacing", "TextEdit");
        foreach (var name in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            _renderer.AddThemeFontOverride(name, font);
        foreach (var name in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
            _renderer.AddThemeFontSizeOverride(name, fontSize);
        _renderer.AddThemeConstantOverride("line_separation", lineSpacing);

        // Font color
        var fontColor = _input.GetThemeColor("font_color", "TextEdit");
        _renderer.AddThemeColorOverride("default_color", fontColor);

        // Renderer stylebox: apply the same stylebox as TextEdit "normal" (defined in theme.tres).
        // Both TextEdit states share identical geometry in the theme (2px border, 4px margins),
        // so the renderer wraps at exactly the same width with no layout shift on focus.
        var normalSb = _input.GetThemeStylebox("normal", "TextEdit") as StyleBoxFlat;
        if (normalSb != null)
            _renderer.AddThemeStyleboxOverride("normal", normalSb);
        _renderer.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                var scroll      = FindParentScrollContainer();
                int savedScroll = scroll?.ScrollVertical ?? 0;
                var clickPos    = mb.Position;

                _input.Visible    = true;
                _renderer.Visible = false;
                UpdateInputHeight();

                // Disable FollowFocus so the ScrollContainer doesn't auto-scroll
                // when GrabFocus fires — we restore the position manually below.
                if (scroll != null) scroll.FollowFocus = false;
                _input.GrabFocus();
                if (scroll != null) scroll.FollowFocus = true;

                CallDeferred(nameof(PlaceCaretAt), clickPos);
                if (scroll != null)
                    CallDeferred(nameof(RestoreScroll), scroll, savedScroll);
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

    private void UpdateInputHeight()
    {
        float lh = _input.GetLineHeight();
        if (lh <= 0) return;
        int visualLines = 0;
        for (int i = 0; i < _input.GetLineCount(); i++)
            visualLines += 1 + _input.GetLineWrapCount(i);
        _input.CustomMinimumSize = new Vector2(0, Mathf.Max(120f, visualLines * lh + 16f));
        CallDeferred(nameof(ScrollToCaretInParent));
    }

    private void ScrollToCaretInParent()
    {
        if (_input == null || !_input.HasFocus()) return;
        var scroll = FindParentScrollContainer();
        if (scroll == null) return;

        int caretLine = _input.GetCaretLine();
        var caretRect = _input.GetRectAtLineColumn(caretLine, _input.GetCaretColumn());
        float caretBottom = _input.GlobalPosition.Y + caretRect.End.Y;

        float visibleBottom = scroll.GlobalPosition.Y + scroll.Size.Y;
        if (caretBottom > visibleBottom - 8f)
            scroll.ScrollVertical += (int)(caretBottom - visibleBottom + 40f);
    }

    private ScrollContainer FindParentScrollContainer()
    {
        Node node = GetParent();
        while (node != null)
        {
            if (node is ScrollContainer sc) return sc;
            node = node.GetParent();
        }
        return null;
    }

    private void PlaceCaretAt(Vector2 clickPos)
    {
        if (_input == null || !_input.Visible) return;
        var lineCol = _input.GetLineColumnAtPos(new Vector2I((int)clickPos.X, (int)clickPos.Y), true);
        _input.SetCaretLine(lineCol.Y);
        _input.SetCaretColumn(lineCol.X);
    }

    private void RestoreScroll(ScrollContainer scroll, int savedScroll)
    {
        if (IsInstanceValid(scroll))
            scroll.ScrollVertical = savedScroll;
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

        // Stub trigger fires when the user closes the bracket: [[+NPC]]
        if (query.EndsWith("]]"))
        {
            string inner    = query[..^2];
            string stubType = DetectStubTrigger(inner);
            if (stubType != null)
            {
                HideAutocomplete();
                OpenStubModal(stubType, openIdx, col);
                return;
            }
        }

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
        if (e is not InputEventKey { Pressed: true } key) return;

        bool acVisible = _acPanel != null && IsInstanceValid(_acPanel) && _acPanel.Visible;
        int  count     = _acList?.GetChildCount() ?? 0;

        switch (key.Keycode)
        {
            case Key.Escape:
                if (!acVisible) return;
                HideAutocomplete();
                _input.AcceptEvent();
                break;

            case Key.Down:
                if (!acVisible) return;
                _acSelectedIndex = (_acSelectedIndex + 1) % count;
                UpdateRowHighlight();
                _input.AcceptEvent();
                break;

            case Key.Up:
                if (!acVisible) return;
                _acSelectedIndex = (_acSelectedIndex - 1 + count) % count;
                UpdateRowHighlight();
                _input.AcceptEvent();
                break;

            case Key.Tab:
                if (!acVisible) return;
                ConfirmSelection();
                _input.AcceptEvent();
                break;

            case Key.Enter:
            case Key.KpEnter:
                if (acVisible)
                {
                    ConfirmSelection();
                    _input.AcceptEvent();
                    break;
                }
                if (HandleBulletContinuation())
                    _input.AcceptEvent();
                break;
        }
    }

    private bool HandleBulletContinuation()
    {
        int    line     = _input.GetCaretLine();
        string lineText = _input.GetLine(line);

        if (!lineText.StartsWith("- ")) return false;

        string body = lineText[2..];

        if (string.IsNullOrEmpty(body))
        {
            // Empty bullet — remove the prefix and return to normal text
            _input.SetLine(line, "");
            _input.SetCaretColumn(0);
        }
        else
        {
            // Continue the list on a new line
            int col = _input.GetCaretColumn();
            _input.SetLine(line, lineText[..col]);
            _input.InsertLineAt(line + 1, "- ");
            _input.SetCaretLine(line + 1);
            _input.SetCaretColumn(2);
        }
        return true;
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
        foreach (var x in _db.Items.GetAll(_campaignId))     Add(x.Name,  "Item");
        foreach (var x in _db.Quests.GetAll(_campaignId))    Add(x.Name,  "Quest");

        return results;
    }

    // ── stub creation ─────────────────────────────────────────────────────────

    private static readonly System.Collections.Generic.Dictionary<string, string> StubTriggers =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "+NPC",      "npc"      },
            { "+Location", "location" },
            { "+Item",     "item"     },
            { "+Faction",  "faction"  },
            { "+Quest",    "quest"    },
        };

    private string DetectStubTrigger(string query)
        => StubTriggers.TryGetValue(query, out string entityType) ? entityType : null;

    private void OpenStubModal(string entityType, int openIdx, int caretCol)
    {
        int    line      = _input.GetCaretLine();
        var    caretRect = _input.GetRectAtLineColumn(line, _input.GetCaretColumn());
        var    screenPos = _input.GlobalPosition + caretRect.Position + new Vector2(0, caretRect.Size.Y);

        _stubCreated = false;

        var popup     = new PopupPanel();
        var vbox      = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var nameInput = new LineEdit
        {
            PlaceholderText     = "Name...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CaretBlink          = true,
        };
        var addBtn = new Button { Text = "+ Create" };

        System.Action doCreate = () =>
        {
            string name = nameInput.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            int newId = CreateStub(entityType, name);
            _stubCreated = true;

            string currentLine = _input.GetLine(line);
            int closeIdx  = currentLine.IndexOf("]]", openIdx, System.StringComparison.Ordinal);
            int replaceEnd = closeIdx >= 0 ? closeIdx + 2 : caretCol;
            _input.SetLine(line, currentLine[..openIdx] + $"[[{name}]]" + currentLine[replaceEnd..]);
            _input.SetCaretColumn(openIdx + 2 + name.Length + 2);

            popup.Hide();
            _input.GrabFocus();

            if (newId > 0)
                EmitSignal(SignalName.EntityCreated, entityType, newId);
        };

        addBtn.Pressed          += doCreate;
        nameInput.TextSubmitted += _ => doCreate();

        vbox.AddChild(nameInput);
        vbox.AddChild(addBtn);
        popup.AddChild(vbox);

        popup.PopupHide += () =>
        {
            popup.QueueFree();
            if (!_stubCreated)
            {
                string currentLine = _input.GetLine(line);
                int closeIdx  = currentLine.IndexOf("]]", openIdx, System.StringComparison.Ordinal);
                int replaceEnd = closeIdx >= 0 ? closeIdx + 2 : caretCol;
                _input.SetLine(line, currentLine[..openIdx] + currentLine[replaceEnd..]);
                _input.SetCaretColumn(openIdx);
            }
            _input.GrabFocus();
        };

        AddChild(popup);
        popup.Popup(new Rect2I((int)screenPos.X, (int)screenPos.Y, 220, 70));
        nameInput.CallDeferred(LineEdit.MethodName.GrabFocus);
    }

    private int CreateStub(string entityType, string name)
    {
        if (_db == null) return 0;
        return entityType switch
        {
            "npc"      => _db.Npcs.Add(new DndBuilder.Core.Models.Npc      { CampaignId = _campaignId, Name = name }),
            "location" => _db.Locations.Add(new DndBuilder.Core.Models.Location { CampaignId = _campaignId, Name = name }),
            "item"     => _db.Items.Add(new DndBuilder.Core.Models.Item    { CampaignId = _campaignId, Name = name }),
            "faction"  => _db.Factions.Add(new DndBuilder.Core.Models.Faction  { CampaignId = _campaignId, Name = name }),
            "quest"    => _db.Quests.Add(new DndBuilder.Core.Models.Quest  { CampaignId = _campaignId, Name = name }),
            _          => 0,
        };
    }

    // ── render ────────────────────────────────────────────────────────────────

    private void Render()
    {
        if (_db == null || _input == null) return;
        _renderer.Text = WikiLinkParser.Parse(_input.Text, _db, _campaignId);
    }
}
