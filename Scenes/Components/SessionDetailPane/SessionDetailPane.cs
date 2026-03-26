using System.Collections.Generic;
using System.Text.RegularExpressions;
using DndBuilder.Core.Models;
using Godot;


public partial class SessionDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Session            _session;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntityCreatedEventHandler(string entityType, int entityId);

    [Export] private Label         _numberLabel;
    [Export] private LineEdit      _titleInput;
    [Export] private LineEdit      _playedOnInput;
    [Export] private WikiNotes     _notes;
    [Export] private Button        _deleteButton;
    [Export] private VBoxContainer _aliasChipsRow;
    [Export] private ImageCarousel _imageCarousel;
    [Export] private VBoxContainer _relatedLinksContainer;

    private static readonly Regex _wikiLinkRx = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);
    private HashSet<string>       _lastLinkNames = new(System.StringComparer.OrdinalIgnoreCase);

    private static readonly (string type, string label)[] TypeOrder =
    {
        ("npc",             "NPCs"),
        ("faction",         "Factions"),
        ("location",        "Locations"),
        ("session",         "Sessions"),
        ("item",            "Items"),
        ("quest",           "Quests"),
        ("playercharacter", "Characters"),
    };

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _titleInput.TextChanged    += title => { Save(); EmitSignal(SignalName.NameChanged, "session", _session?.Id ?? 0, string.IsNullOrEmpty(title) ? "Untitled Session" : title); };
        _titleInput.FocusExited    += () => { if (_titleInput.Text == "") _titleInput.Text = "New Session"; };
        _titleInput.FocusEntered   += () => _titleInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _playedOnInput.TextChanged += _ => Save();
        _notes.TextChanged   += () => { Save(); RefreshRelatedLinks(); };
        _notes.NavigateTo    += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);
        _notes.EntityCreated += (type, id) => EmitSignal(SignalName.EntityCreated, type, id);

        _confirmDialog = DialogHelper.Make("Delete Session");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "session", _session?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_session?.Title}\"? This cannot be undone.");
        };
    }

    public void Load(Session session)
    {
        _session = session;

        _imageCarousel?.Setup(EntityType.Session, session.Id, _db, session.CampaignId);

        _numberLabel.Text   = $"Session #{session.Number:D3}";
        _titleInput.Text    = string.IsNullOrEmpty(session.Title) ? "New Session" : session.Title;
        _playedOnInput.Text = session.PlayedOn;
        _notes.Setup(session.CampaignId, _db);
        _notes.Text = session.Notes;

        _lastLinkNames.Clear();
        RefreshRelatedLinks();
        LoadAliases();
    }

    private void LoadAliases()
    {
        if (_session == null || _aliasChipsRow == null) return;
        foreach (Node child in _aliasChipsRow.GetChildren()) child.QueueFree();
        var chipsRow = new HBoxContainer();
        chipsRow.AddThemeConstantOverride("separation", 4);
        _aliasChipsRow.AddChild(chipsRow);
        foreach (var alias in _db.EntityAliases.GetForEntity("session", _session.Id))
        {
            int capturedId  = alias.Id;
            var normalStyle = new StyleBoxFlat { BgColor = new Color(0.18f, 0.18f, 0.18f) };
            normalStyle.SetCornerRadiusAll(4);
            normalStyle.ContentMarginLeft = 6; normalStyle.ContentMarginRight = 4;
            normalStyle.ContentMarginTop  = 2; normalStyle.ContentMarginBottom = 2;
            var hoverStyle  = new StyleBoxFlat { BgColor = new Color(0.45f, 0.10f, 0.10f) };
            hoverStyle.SetCornerRadiusAll(4);
            hoverStyle.ContentMarginLeft = 6; hoverStyle.ContentMarginRight = 4;
            hoverStyle.ContentMarginTop  = 2; hoverStyle.ContentMarginBottom = 2;
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", normalStyle);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 2);
            var label = new Label { Text = alias.Alias }; label.AddThemeFontSizeOverride("font_size", 11);
            var removeBtn = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
            removeBtn.AddThemeFontSizeOverride("font_size", 11);
            removeBtn.MouseEntered += () => chip.AddThemeStyleboxOverride("panel", hoverStyle);
            removeBtn.MouseExited  += () => chip.AddThemeStyleboxOverride("panel", normalStyle);
            removeBtn.Pressed      += () => { _db.EntityAliases.Delete(capturedId); LoadAliases(); };
            row.AddChild(label); row.AddChild(removeBtn); chip.AddChild(row); chipsRow.AddChild(chip);
        }
        var addInput = new LineEdit { PlaceholderText = "+ alias", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(80, 0) };
        addInput.TextSubmitted += text =>
        {
            string t = text.Trim(); if (string.IsNullOrEmpty(t)) return; addInput.Text = "";
            _db.EntityAliases.Add(new DndBuilder.Core.Models.EntityAlias { CampaignId = _session.CampaignId, EntityType = "session", EntityId = _session.Id, Alias = t });
            LoadAliases();
        };
        _aliasChipsRow.AddChild(addInput);
    }

    private void RefreshRelatedLinks()
    {
        if (_relatedLinksContainer == null || _session == null) return;

        // Extract ordered, deduplicated link names
        var seen         = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var orderedNames = new List<string>();
        foreach (Match m in _wikiLinkRx.Matches(_notes.Text ?? ""))
        {
            var name = m.Groups[1].Value;
            if (seen.Add(name)) orderedNames.Add(name);
        }

        if (seen.SetEquals(_lastLinkNames)) return;
        _lastLinkNames = seen;

        // Build entity lookup: name → list of (entityType, id)
        // Aliases may map to multiple entities (same alias on different entities is allowed).
        int cid    = _session.CampaignId;
        var lookup      = new Dictionary<string, List<(string type, int id)>>(System.StringComparer.OrdinalIgnoreCase);
        var entityNames = new Dictionary<(string type, int id), string>();

        void AddEntry(string key, string type, int id)
        {
            if (!lookup.TryGetValue(key, out var list)) { list = new List<(string, int)>(); lookup[key] = list; }
            if (!list.Contains((type, id))) list.Add((type, id));
        }

        foreach (var x in _db.Npcs.GetAll(cid))      { entityNames[("npc",             x.Id)] = x.Name;  AddEntry(x.Name,  "npc",             x.Id); }
        foreach (var x in _db.Factions.GetAll(cid))  { entityNames[("faction",         x.Id)] = x.Name;  AddEntry(x.Name,  "faction",         x.Id); }
        foreach (var x in _db.Locations.GetAll(cid)) { entityNames[("location",        x.Id)] = x.Name;  AddEntry(x.Name,  "location",        x.Id); }
        foreach (var x in _db.Sessions.GetAll(cid))  { entityNames[("session",         x.Id)] = x.Title; AddEntry(x.Title, "session",         x.Id); }
        foreach (var x in _db.Items.GetAll(cid))     { entityNames[("item",            x.Id)] = x.Name;  AddEntry(x.Name,  "item",            x.Id); }
        foreach (var x in _db.Quests.GetAll(cid))    { entityNames[("quest",           x.Id)] = x.Name;  AddEntry(x.Name,  "quest",           x.Id); }
        foreach (var a in _db.EntityAliases.GetAll(cid)) AddEntry(a.Alias, a.EntityType, a.EntityId);

        // Group links by entity type — one row per resolved (name, entityId) pair
        var groups     = new Dictionary<string, List<(string name, int id)>>();
        foreach (var (type, _) in TypeOrder) groups[type] = new List<(string, int)>();
        var unresolved = new List<string>();

        foreach (var name in orderedNames)
        {
            if (lookup.TryGetValue(name, out var entries))
            {
                foreach (var (type, id) in entries)
                {
                    if (groups.TryGetValue(type, out var bucket))
                        bucket.Add((name, id));
                }
            }
            else
            {
                unresolved.Add(name);
            }
        }

        // Rebuild UI
        foreach (Node child in _relatedLinksContainer.GetChildren())
            child.QueueFree();

        bool any = false;
        foreach (var (type, label) in TypeOrder)
        {
            var items = groups[type];
            if (items.Count == 0) continue;
            any = true;
            _relatedLinksContainer.AddChild(BuildLinkSection(label, items, type, entityNames));
        }
        if (unresolved.Count > 0)
        {
            any = true;
            _relatedLinksContainer.AddChild(BuildLinkSection("Not Found",
                unresolved.ConvertAll(n => (n, 0)), null, entityNames));
        }

        _relatedLinksContainer.Visible = any;
    }

    private VBoxContainer BuildLinkSection(string label, List<(string name, int id)> items, string entityType, Dictionary<(string type, int id), string> entityNames = null)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 0);

        var itemBox = new VBoxContainer();
        itemBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        itemBox.AddThemeConstantOverride("separation", 0);

        var indent = new MarginContainer();
        indent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        indent.AddThemeConstantOverride("margin_left", 24);
        indent.AddChild(itemBox);

        var header = new Button
        {
            Text                    = $"▼ {label} ({items.Count})",
            Flat                    = true,
            Alignment               = HorizontalAlignment.Left,
            SizeFlagsHorizontal     = SizeFlags.ExpandFill,
            TextOverrunBehavior     = TextServer.OverrunBehavior.TrimEllipsis,
            ClipText                = true,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        StripButtonPadding(header);
        bool collapsed = false;
        header.Pressed += () =>
        {
            collapsed      = !collapsed;
            indent.Visible = !collapsed;
            header.Text    = $"{(collapsed ? "▶" : "▼")} {label} ({items.Count})";
        };
        section.AddChild(header);
        section.AddChild(indent);

        bool resolved = entityType != null;
        foreach (var (name, id) in items)
        {
            var btn = new Button
            {
                Text                    = name,
                Flat                    = true,
                Alignment               = HorizontalAlignment.Left,
                SizeFlagsHorizontal     = SizeFlags.ExpandFill,
                TextOverrunBehavior     = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText                = true,
                Disabled                = !resolved,
                FocusMode               = FocusModeEnum.None,
                MouseDefaultCursorShape = resolved ? CursorShape.PointingHand : CursorShape.Arrow,
            };

            StripButtonPadding(btn);
            if (resolved)
            {
                btn.AddThemeColorOverride("font_color",         new Color(0.83f, 0.67f, 0.44f));
                btn.AddThemeColorOverride("font_hover_color",   new Color(0.91f, 0.78f, 0.49f));
                btn.AddThemeColorOverride("font_pressed_color", new Color(0.83f, 0.67f, 0.44f));
                if (entityNames != null && entityNames.TryGetValue((entityType, id), out var actualName)
                    && !string.Equals(actualName, name, System.StringComparison.OrdinalIgnoreCase))
                    btn.TooltipText = actualName;
                string capturedType = entityType;
                int    capturedId   = id;
                btn.Pressed += () => EmitSignal(SignalName.NavigateTo, capturedType, capturedId);
            }
            else
            {
                btn.AddThemeColorOverride("font_disabled_color", new Color(0.53f, 0.53f, 0.53f));
            }

            itemBox.AddChild(btn);
        }

        return section;
    }

    private static void StripButtonPadding(Button btn)
    {
        var empty = new StyleBoxEmpty();
        btn.AddThemeStyleboxOverride("normal",   empty);
        btn.AddThemeStyleboxOverride("hover",    empty);
        btn.AddThemeStyleboxOverride("pressed",  empty);
        btn.AddThemeStyleboxOverride("disabled", empty);
        btn.AddThemeStyleboxOverride("focus",    empty);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_session == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_session.Title}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

    private void Save()
    {
        if (_session == null) return;
        _session.Title    = string.IsNullOrEmpty(_titleInput.Text) ? "New Session" : _titleInput.Text;
        _session.PlayedOn = _playedOnInput.Text;
        _session.Notes    = _notes.Text;
        _db.Sessions.Edit(_session);
    }

}