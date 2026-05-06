using System.Collections.Generic;
using System.Text.RegularExpressions;
using DndBuilder.Core;
using Godot;

public partial class MarkdownNotes : VBoxContainer
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

    private bool   _stubCreated;
    private string _hoveredMeta = null;

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
            bool hasText      = !string.IsNullOrEmpty(value);
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
        _input.ScrollFitContentHeight = true;

        _input.TextChanged += () => EmitSignal(SignalName.TextChanged);
        _input.TextChanged += UpdateInputHeight;
        _input.TextChanged += CheckAutocomplete;
        _input.FocusExited += OnInputFocusExited;
        _input.GuiInput    += OnInputKey;

        _renderer.MetaClicked += (Variant meta) =>
        {
            if (!Input.IsKeyPressed(Key.Ctrl)) return;
            string s = meta.AsString();
            if (s.StartsWith("http://") || s.StartsWith("https://"))
            {
                OS.ShellOpen(s);
                return;
            }
            var parts = s.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                EmitSignal(SignalName.NavigateTo, parts[0], id);
        };
        _renderer.MetaHoverStarted += (Variant meta) => _hoveredMeta = meta.AsString();
        _renderer.MetaHoverEnded   += (Variant meta) => _hoveredMeta = null;
        _renderer.MouseDefaultCursorShape = CursorShape.Ibeam;

        var font        = _input.GetThemeFont("font", "TextEdit") ?? ThemeDB.Singleton.FallbackFont;
        int fontSize    = _input.GetThemeFontSize("font_size", "TextEdit");
        int lineSpacing = _input.GetThemeConstant("line_spacing", "TextEdit");
        _renderer.AddThemeFontOverride("normal_font", font);
        foreach (var name in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
            _renderer.AddThemeFontSizeOverride(name, fontSize);
        _renderer.AddThemeConstantOverride("line_separation", lineSpacing);

        var fontColor = _input.GetThemeColor("font_color", "TextEdit");
        _renderer.AddThemeColorOverride("default_color", fontColor);

        var normalSb = _input.GetThemeStylebox("normal", "TextEdit") as StyleBoxFlat;
        if (normalSb != null)
            _renderer.AddThemeStyleboxOverride("normal", normalSb);

        _renderer.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            {
                if (mb.CtrlPressed && _hoveredMeta != null) return; // Ctrl+link: MetaClicked handles it

                var scroll      = FindParentScrollContainer();
                int savedScroll = scroll?.ScrollVertical ?? 0;
                var clickPos    = mb.Position;

                _input.Visible    = true;
                _renderer.Visible = false;
                UpdateInputHeight();

                if (scroll != null) scroll.FollowFocus = false;
                _input.GrabFocus();
                if (scroll != null) scroll.FollowFocus = true;

                CallDeferred(nameof(PlaceCaretAt), clickPos);
                if (scroll != null)
                    CallDeferred(nameof(RestoreScroll), scroll, savedScroll);
            }
        };

        // Help button — optionally present in the scene
        var helpBtn = GetNodeOrNull<Button>("BottomBar/HelpButton");
        if (helpBtn != null)
        {
            helpBtn.Pressed += OpenHelpModal;
            if (helpBtn.HasThemeIcon("Help", "EditorIcons"))
            {
                helpBtn.Icon = helpBtn.GetThemeIcon("Help", "EditorIcons");
                helpBtn.Text = "";
            }
            var empty = new StyleBoxEmpty();
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
                helpBtn.AddThemeStyleboxOverride(state, empty);
        }
    }

    public override void _Process(double delta)
    {
        if (_renderer.Visible && _hoveredMeta != null && !Input.IsKeyPressed(Key.Ctrl))
            DisplayServer.CursorSetShape(DisplayServer.CursorShape.Ibeam);
    }

    public override void _ExitTree()
    {
        if (_acPanel != null && IsInstanceValid(_acPanel))
            _acPanel.QueueFree();
    }

    public override void _Input(InputEvent e)
    {
        // Click-outside-to-blur
        if (_input != null && _input.Visible && e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
            if (!_input.GetGlobalRect().HasPoint(mb.GlobalPosition))
                _input.ReleaseFocus();

        // Intercept autocomplete navigation BEFORE TextEdit consumes Up/Down
        if (_acPanel == null || !IsInstanceValid(_acPanel) || !_acPanel.Visible) return;
        if (_input == null || !_input.HasFocus()) return;
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;

        int count = _acList?.GetChildCount() ?? 0;
        switch (key.Keycode)
        {
            case Key.Down:
                _acSelectedIndex = (_acSelectedIndex + 1) % count;
                UpdateRowHighlight();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Up:
                _acSelectedIndex = (_acSelectedIndex - 1 + count) % count;
                UpdateRowHighlight();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Tab:
            case Key.Enter:
            case Key.KpEnter:
                ConfirmSelection();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Escape:
                HideAutocomplete();
                GetViewport().SetInputAsHandled();
                break;
        }
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

    // ── key handling (GuiInput — in-document edits and shortcuts only) ────────

    private void OnInputKey(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } key) return;

        // Autocomplete navigation is handled in _Input before TextEdit sees it.
        // Here we only handle Enter when autocomplete is NOT visible.
        bool acVisible = _acPanel != null && IsInstanceValid(_acPanel) && _acPanel.Visible;

        if (key.Keycode == Key.Escape && acVisible)
        {
            HideAutocomplete();
            _input.AcceptEvent();
            return;
        }

        if ((key.Keycode == Key.Enter || key.Keycode == Key.KpEnter) && !acVisible)
        {
            if (HandleListContinuation())
                _input.AcceptEvent();
            return;
        }

        // Formatting shortcuts
        bool ctrl  = key.CtrlPressed;
        bool shift = key.ShiftPressed;

        if (ctrl && !shift)
        {
            switch (key.Keycode)
            {
                case Key.B: WrapSelection("**", "**"); _input.AcceptEvent(); return;
                case Key.I: WrapSelection("*",  "*");  _input.AcceptEvent(); return;
                case Key.U: WrapSelection("<u>", "</u>"); _input.AcceptEvent(); return;
                case Key.K: InsertLink(); _input.AcceptEvent(); return;
                case Key.Key1: ToggleLinePrefix("# ");   _input.AcceptEvent(); return;
                case Key.Key2: ToggleLinePrefix("## ");  _input.AcceptEvent(); return;
                case Key.Key3: ToggleLinePrefix("### "); _input.AcceptEvent(); return;
                case Key.Key4: ToggleLinePrefix("#### ");    _input.AcceptEvent(); return;
                case Key.Key5: ToggleLinePrefix("##### ");   _input.AcceptEvent(); return;
                case Key.Key6: ToggleLinePrefix("###### ");  _input.AcceptEvent(); return;
            }
        }
        if (ctrl && shift)
        {
            switch (key.Keycode)
            {
                case Key.S: WrapSelection("~~", "~~"); _input.AcceptEvent(); return;
                case Key.Key8: ToggleLinePrefix("- ");  _input.AcceptEvent(); return;
                case Key.Key7: ToggleNumberedList();    _input.AcceptEvent(); return;
                case Key.Greater: ToggleLinePrefix("> "); _input.AcceptEvent(); return;
            }
        }
    }

    // ── list continuation ─────────────────────────────────────────────────────

    // Called on Enter. Returns true (and consumes) if a list continuation was handled.
    private bool HandleListContinuation()
    {
        int    line     = _input.GetCaretLine();
        string lineText = _input.GetLine(line);

        bool   isBullet     = lineText.StartsWith("- ");
        var    numberMatch  = Regex.Match(lineText, @"^(\d+)\. ");
        if (!isBullet && !numberMatch.Success) return false;

        _input.AcceptEvent();
        int    prefixLen = isBullet ? 2 : numberMatch.Length;
        string body      = lineText[prefixLen..];

        if (string.IsNullOrEmpty(body))
        {
            // Empty list item → remove prefix, leave blank line
            _input.SetLine(line, "");
            _input.SetCaretLine(line);
            _input.SetCaretColumn(0);
            _input.InsertTextAtCaret("\n");
        }
        else
        {
            string nextPrefix = isBullet
                ? "- "
                : (int.Parse(numberMatch.Groups[1].Value) + 1) + ". ";
            _input.InsertTextAtCaret("\n" + nextPrefix);
        }
        return true;
    }

    // ── formatting helpers ────────────────────────────────────────────────────

    private void WrapSelection(string prefix, string suffix)
    {
        if (!_input.HasSelection())
        {
            _input.InsertTextAtCaret(prefix + suffix);
            // Back up cursor to between the delimiters
            int line = _input.GetCaretLine();
            int col  = _input.GetCaretColumn() - suffix.Length;
            _input.SetCaretColumn(Mathf.Max(0, col));
            return;
        }

        string selected = _input.GetSelectedText();

        // Toggle: if already wrapped, unwrap instead
        if (selected.StartsWith(prefix) && selected.EndsWith(suffix) && selected.Length > prefix.Length + suffix.Length)
        {
            _input.InsertTextAtCaret(selected[prefix.Length..^suffix.Length]);
            return;
        }

        // Check if selection is inside surrounding delimiters (cursor at exact boundaries)
        int fromLine = _input.GetSelectionFromLine();
        int fromCol  = _input.GetSelectionFromColumn();
        int toLine   = _input.GetSelectionToLine();
        int toCol    = _input.GetSelectionToColumn();

        if (fromLine == toLine)
        {
            string fullLine = _input.GetLine(fromLine);
            int ps = prefix.Length;
            int ss = suffix.Length;
            if (fromCol >= ps && toCol + ss <= fullLine.Length)
            {
                string before = fullLine.Substring(fromCol - ps, ps);
                string after  = fullLine.Substring(toCol, ss);
                if (before == prefix && after == suffix)
                {
                    // Unwrap
                    _input.SetLine(fromLine, fullLine[..(fromCol - ps)] + selected + fullLine[(toCol + ss)..]);
                    _input.Select(fromLine, fromCol - ps, fromLine, fromCol - ps + selected.Length);
                    return;
                }
            }
        }

        // Handle multi-paragraph selections: wrap each paragraph independently
        var paragraphs = selected.Split("\n\n");
        if (paragraphs.Length > 1)
        {
            var wrapped = new string[paragraphs.Length];
            for (int i = 0; i < paragraphs.Length; i++)
                wrapped[i] = string.IsNullOrWhiteSpace(paragraphs[i])
                    ? paragraphs[i]
                    : prefix + paragraphs[i] + suffix;
            _input.InsertTextAtCaret(string.Join("\n\n", wrapped));
            return;
        }

        _input.InsertTextAtCaret(prefix + selected + suffix);
    }

    private void ToggleLinePrefix(string prefix)
    {
        int fromLine = _input.HasSelection() ? _input.GetSelectionFromLine() : _input.GetCaretLine();
        int toLine   = _input.HasSelection() ? _input.GetSelectionToLine()   : _input.GetCaretLine();

        bool allHave = true;
        for (int li = fromLine; li <= toLine; li++)
            if (!_input.GetLine(li).StartsWith(prefix)) { allHave = false; break; }

        for (int li = fromLine; li <= toLine; li++)
        {
            string line = _input.GetLine(li);
            _input.SetLine(li, allHave ? line[prefix.Length..] : prefix + line);
        }
    }

    private void ToggleNumberedList()
    {
        int fromLine = _input.HasSelection() ? _input.GetSelectionFromLine() : _input.GetCaretLine();
        int toLine   = _input.HasSelection() ? _input.GetSelectionToLine()   : _input.GetCaretLine();

        var numPat = new Regex(@"^\d+\. ");
        bool allHave = true;
        for (int li = fromLine; li <= toLine; li++)
            if (!numPat.IsMatch(_input.GetLine(li))) { allHave = false; break; }

        if (allHave)
        {
            for (int li = fromLine; li <= toLine; li++)
            {
                string line = _input.GetLine(li);
                _input.SetLine(li, numPat.Replace(line, "", 1));
            }
        }
        else
        {
            int num = 1;
            for (int li = fromLine; li <= toLine; li++)
            {
                string line = _input.GetLine(li);
                string stripped = numPat.Replace(line, "");
                _input.SetLine(li, $"{num++}. {stripped}");
            }
        }
    }

    private void InsertLink()
    {
        string sel = _input.HasSelection() ? _input.GetSelectedText() : "";
        string clipboard = DisplayServer.ClipboardGet().Trim();
        bool clipIsUrl = clipboard.StartsWith("http://") || clipboard.StartsWith("https://");
        string url = clipIsUrl ? clipboard : "https://";
        _input.InsertTextAtCaret($"[{sel}]({url})");
        if (!clipIsUrl && !string.IsNullOrEmpty(sel))
        {
            // Position cursor inside the URL parens so user can type the URL
            int line = _input.GetCaretLine();
            int col  = _input.GetCaretColumn() - 1; // before the closing )
            _input.SetCaretColumn(col);
        }
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
            _acPanel.Position          = screenPos;
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

        _acSelectedIndex = 0;

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
                FocusMode           = FocusModeEnum.None,
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

    private void ConfirmSelection()
    {
        if (_acList == null || _acSelectedIndex < 0 || _acSelectedIndex >= _acList.GetChildCount()) return;
        var row  = _acList.GetChild(_acSelectedIndex) as PanelContainer;
        var hbox = row?.GetChild(0) as HBoxContainer;
        var btn  = hbox?.GetChild(0) as Button;
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

        foreach (var x in _db.Npcs.GetAll(_campaignId))             Add(x.Name,  "NPC");
        foreach (var x in _db.Factions.GetAll(_campaignId))         Add(x.Name,  "Faction");
        foreach (var x in _db.Locations.GetAll(_campaignId))        Add(x.Name,  "Location");
        foreach (var x in _db.Sessions.GetAll(_campaignId))         Add(x.Title, "Session");
        foreach (var x in _db.Items.GetAll(_campaignId))            Add(x.Name,  "Item");
        foreach (var x in _db.Quests.GetAll(_campaignId))           Add(x.Name,  "Quest");
        foreach (var x in _db.Pf2eCreatures.GetAll(_campaignId))    Add(x.Name,  "Creature");
        foreach (var x in _db.PlayerCharacters.GetAll(_campaignId)) Add(x.Name,  "Character");
        foreach (var x in _db.Pf2eCharacters.GetAll(_campaignId))   Add(x.Name,  "P2e PC");
        foreach (var x in _db.Pf2eClasses.GetAll(_campaignId))      Add(x.Name,  "Class");
        foreach (var x in _db.Pf2eAncestries.GetAll(_campaignId))   Add(x.Name,  "Ancestry");
        foreach (var x in _db.Pf2eHeritages.GetAll(_campaignId))    Add(x.Name,  "Heritage");

        var entityNames = new Dictionary<string, string>();
        foreach (var x in _db.Npcs.GetAll(_campaignId))             entityNames[$"npc:{x.Id}"]             = x.Name;
        foreach (var x in _db.Factions.GetAll(_campaignId))         entityNames[$"faction:{x.Id}"]         = x.Name;
        foreach (var x in _db.Locations.GetAll(_campaignId))        entityNames[$"location:{x.Id}"]        = x.Name;
        foreach (var x in _db.Sessions.GetAll(_campaignId))         entityNames[$"session:{x.Id}"]         = x.Title;
        foreach (var x in _db.Items.GetAll(_campaignId))            entityNames[$"item:{x.Id}"]            = x.Name;
        foreach (var x in _db.Quests.GetAll(_campaignId))           entityNames[$"quest:{x.Id}"]           = x.Name;
        foreach (var x in _db.Pf2eCreatures.GetAll(_campaignId))    entityNames[$"pf2e_creature:{x.Id}"]   = x.Name;
        foreach (var x in _db.PlayerCharacters.GetAll(_campaignId)) entityNames[$"playercharacter:{x.Id}"] = x.Name;
        foreach (var x in _db.Pf2eCharacters.GetAll(_campaignId))   entityNames[$"pf2e_pc:{x.Id}"]         = x.Name;
        foreach (var x in _db.Pf2eClasses.GetAll(_campaignId))      entityNames[$"pf2e_class:{x.Id}"]      = x.Name;
        foreach (var x in _db.Pf2eAncestries.GetAll(_campaignId))   entityNames[$"pf2e_ancestry:{x.Id}"]   = x.Name;
        foreach (var x in _db.Pf2eHeritages.GetAll(_campaignId))    entityNames[$"pf2e_heritage:{x.Id}"]   = x.Name;
        foreach (var a in _db.EntityAliases.GetAll(_campaignId))
        {
            if (q.Length > 0 && !a.Alias.ToLowerInvariant().Contains(q)) continue;
            string entityName = entityNames.TryGetValue($"{a.EntityType}:{a.EntityId}", out var n) ? n : a.EntityType;
            results.Add((a.Alias, $"→ {entityName}"));
        }

        return results;
    }

    // Wikilink lookup for rendering: lowercased name → "type:id"
    private Dictionary<string, string> BuildLookup()
    {
        var d = new Dictionary<string, string>();
        if (_db == null) return d;

        foreach (var x in _db.Npcs.GetAll(_campaignId))             d[x.Name.ToLowerInvariant()]  = $"npc:{x.Id}";
        foreach (var x in _db.Factions.GetAll(_campaignId))         d[x.Name.ToLowerInvariant()]  = $"faction:{x.Id}";
        foreach (var x in _db.Locations.GetAll(_campaignId))        d[x.Name.ToLowerInvariant()]  = $"location:{x.Id}";
        foreach (var x in _db.Sessions.GetAll(_campaignId))         d[x.Title.ToLowerInvariant()] = $"session:{x.Id}";
        foreach (var x in _db.Items.GetAll(_campaignId))            d[x.Name.ToLowerInvariant()]  = $"item:{x.Id}";
        foreach (var x in _db.Quests.GetAll(_campaignId))           d[x.Name.ToLowerInvariant()]  = $"quest:{x.Id}";
        foreach (var x in _db.Pf2eCreatures.GetAll(_campaignId))    d[x.Name.ToLowerInvariant()]  = $"pf2e_creature:{x.Id}";
        foreach (var x in _db.PlayerCharacters.GetAll(_campaignId)) d[x.Name.ToLowerInvariant()]  = $"playercharacter:{x.Id}";
        foreach (var x in _db.Pf2eCharacters.GetAll(_campaignId))   d[x.Name.ToLowerInvariant()]  = $"pf2e_pc:{x.Id}";
        foreach (var x in _db.Pf2eClasses.GetAll(_campaignId))      d[x.Name.ToLowerInvariant()]  = $"pf2e_class:{x.Id}";
        foreach (var x in _db.Pf2eAncestries.GetAll(_campaignId))   d[x.Name.ToLowerInvariant()]  = $"pf2e_ancestry:{x.Id}";
        foreach (var x in _db.Pf2eHeritages.GetAll(_campaignId))    d[x.Name.ToLowerInvariant()]  = $"pf2e_heritage:{x.Id}";
        foreach (var a in _db.EntityAliases.GetAll(_campaignId))
        {
            string key = a.Alias.ToLowerInvariant();
            if (!d.ContainsKey(key))
                d[key] = $"{a.EntityType}:{a.EntityId}";
        }
        return d;
    }

    // ── stub creation ─────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> StubTriggers =
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
            "item"     => _db.Items.Add(new DndBuilder.Core.Models.DnD5eItem    { CampaignId = _campaignId, Name = name }),
            "faction"  => _db.Factions.Add(new DndBuilder.Core.Models.Faction  { CampaignId = _campaignId, Name = name }),
            "quest"    => _db.Quests.Add(new DndBuilder.Core.Models.Quest  { CampaignId = _campaignId, Name = name }),
            _          => 0,
        };
    }

    // ── help modal ────────────────────────────────────────────────────────────

    private void OpenHelpModal()
    {
        var modal = GD.Load<PackedScene>("res://Scenes/Modals/MarkdownHelpModal/markdown_help_modal.tscn").Instantiate<MarkdownHelpModal>();
        GetTree().Root.AddChild(modal);
        modal.PopupCentered();
    }

    // ── render ────────────────────────────────────────────────────────────────

    private void Render()
    {
        if (_db == null || _input == null) return;
        _renderer.Text = MarkdownService.Render(_input.Text, BuildLookup());
    }
}
