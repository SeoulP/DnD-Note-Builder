using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core.Models;
using Godot;

public partial class SystemSidebar : VBoxContainer
{
    private int              _campaignId;
    private Campaign         _campaign;
    private DatabaseService  _db;
    private SystemVocabulary _vocab            = SystemVocabulary.Default;
    private HashSet<int>     _collapsedClasses = new();
    private HashSet<int>     _collapsedSpecies = new();
    private bool             _classesFirstLoad = true;
    private bool             _speciesFirstLoad = true;

    [Export] private Button        _addClassesButton;
    [Export] private Button        _addSpeciesButton;
    [Export] private Button        _addAbilitiesButton;
    [Export] private Button        _addCreaturesButton;

    [Export] private VBoxContainer _classesContainer;
    [Export] private VBoxContainer _speciesContainer;
    [Export] private VBoxContainer _abilitiesContainer;
    [Export] private VBoxContainer _creaturesContainer;

    [Signal] public delegate void EntitySelectedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntitySelectedNewTabEventHandler(string entityType, int entityId);

    internal static readonly Color ClassColor    = new(0.98f, 0.55f, 0.18f);
    internal static readonly Color SpeciesColor  = new(0.20f, 0.85f, 0.75f);
    internal static readonly Color AbilityColor  = new(0.72f, 0.95f, 0.28f);
    internal static readonly Color CreatureColor = new(0.58f, 0.38f, 0.88f);

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _addClassesButton.Pressed += () =>
        {
            if (_campaign?.System == "pathfinder2e")
            {
                int keyId = _db.Pf2eAbilityScores.GetAll().FirstOrDefault()?.Id ?? 1;
                var cls   = new Pf2eClass { CampaignId = _campaignId, Name = "New Class", KeyAbilityScoreId = keyId };
                int id    = _db.Pf2eClasses.Add(cls);
                LoadClasses();
                EmitSignal(SignalName.EntitySelected, "pf2e_class", id);
            }
            else
            {
                var cls = new Class { CampaignId = _campaignId, Name = $"New {_vocab.Class}" };
                int id  = _db.Classes.Add(cls);
                LoadClasses();
                EmitSignal(SignalName.EntitySelected, "class", id);
            }
        };
        _addSpeciesButton.Pressed += () =>
        {
            if (_campaign?.System == "pathfinder2e")
            {
                int sizeId = _db.Pf2eSizes.GetAll().FirstOrDefault(s => s.Name == "Medium")?.Id ?? 1;
                var anc    = new Pf2eAncestry { CampaignId = _campaignId, Name = "New Ancestry", SizeId = sizeId };
                int id     = _db.Pf2eAncestries.Add(anc);
                LoadSpecies();
                EmitSignal(SignalName.EntitySelected, "pf2e_ancestry", id);
            }
            else
            {
                var species = new Species { CampaignId = _campaignId, Name = $"New {_vocab.Species}" };
                int id      = _db.Species.Add(species);
                LoadSpecies();
                EmitSignal(SignalName.EntitySelected, "species", id);
            }
        };
        _addAbilitiesButton.Pressed += () =>
        {
            var ability = new Ability { CampaignId = _campaignId, Name = $"New {_vocab.Ability}" };
            int id      = _db.Abilities.Add(ability);
            LoadAbilities();
            EmitSignal(SignalName.EntitySelected, "ability", id);
        };
        _addCreaturesButton.Pressed += () =>
        {
            if (_campaign?.System != "pathfinder2e") return;
            int ctid    = _db.Pf2eCreatureTypes.GetAll(_campaignId).FirstOrDefault()?.Id ?? 1;
            int szid    = _db.Pf2eSizes.GetAll().FirstOrDefault(s => s.Name == "Medium")?.Id ?? 3;
            var creature = new Pf2eCreature { CampaignId = _campaignId, Name = "New Creature", CreatureTypeId = ctid, SizeId = szid };
            int cid     = _db.Pf2eCreatures.Add(creature);
            LoadCreatures();
            EmitSignal(SignalName.EntitySelected, "pf2e_creature", cid);
        };

        NotesSidebar.StyleAddButton(_addClassesButton,   ClassColor);
        NotesSidebar.StyleAddButton(_addSpeciesButton,   SpeciesColor);
        NotesSidebar.StyleAddButton(_addAbilitiesButton, AbilityColor);
        NotesSidebar.StyleAddButton(_addCreaturesButton, CreatureColor);

        NotesSidebar.StyleAccordion(GetNode<Control>("ClassesPanel"),   ClassColor);
        NotesSidebar.StyleAccordion(GetNode<Control>("SpeciesPanel"),   SpeciesColor);
        NotesSidebar.StyleAccordion(GetNode<Control>("AbilitiesPanel"), AbilityColor);
        NotesSidebar.StyleAccordion(GetNode<Control>("CreaturesPanel"), CreatureColor);

        GetNode<LineEdit>("SystemSearchInput").TextChanged += FilterSystemSidebar;
    }

    public void SetCampaign(int campaignId, Campaign campaign, SystemVocabulary vocab)
    {
        _campaignId       = campaignId;
        _campaign         = campaign;
        _vocab            = vocab;
        _collapsedClasses.Clear();
        _collapsedSpecies.Clear();
        _classesFirstLoad = true;
        _speciesFirstLoad = true;
        ApplyVocabulary();
        ReloadAll();
    }

    public void ReloadAll()
    {
        LoadClasses();
        LoadSpecies();
        LoadAbilities();
        LoadCreatures();
    }

    public void Reload(string entityType)
    {
        switch (entityType)
        {
            case "class": case "subclass": case "pf2e_class":        LoadClasses();   break;
            case "species": case "subspecies": case "pf2e_ancestry":  LoadSpecies();  break;
            case "ability":                                            LoadAbilities(); break;
            case "pf2e_creature":                                      LoadCreatures(); break;
        }
    }

    public void UpdateButtonName(string entityType, int entityId, string name)
    {
        var container = entityType switch
        {
            "class" or "subclass" or "pf2e_class"        => _classesContainer,
            "species" or "subspecies" or "pf2e_ancestry" => _speciesContainer,
            "ability"                                     => _abilitiesContainer,
            "pf2e_creature"                               => _creaturesContainer,
            _                                             => null,
        };
        FindAndRenameButton(container, entityId, name);
    }

    private void ApplyVocabulary()
    {
        GetNode<Control>("ClassesPanel")  .Set("title", _vocab.ClassesPlural);
        GetNode<Control>("SpeciesPanel")  .Set("title", _vocab.SpeciesPlural);
        GetNode<Control>("AbilitiesPanel").Set("title", _vocab.AbilitiesPlural);
    }

    // ── load methods ──────────────────────────────────────────────────────────

    private void LoadClasses()
    {
        ClearItems(_classesContainer, _addClassesButton);
        if (_campaign?.System == "pathfinder2e")
        {
            foreach (var cls in _db.Pf2eClasses.GetAll(_campaignId))
            {
                int id  = cls.Id;
                var btn = MakeSidebarButton(cls.Name, ClassColor);
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "pf2e_class", id);
                WireCtrlClick(btn, "pf2e_class", id);
                _classesContainer.AddChild(btn);
            }
            _classesFirstLoad = false;
            return;
        }
        foreach (var cls in _db.Classes.GetAll(_campaignId))
        {
            int clsId      = cls.Id;
            var subclasses = _db.Classes.GetSubclassesForClass(cls.Id);
            if (_classesFirstLoad && subclasses.Count > 0) _collapsedClasses.Add(clsId);
            bool isCollapsed = _collapsedClasses.Contains(cls.Id);

            if (subclasses.Count > 0)
            {
                var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                hbox.AddThemeConstantOverride("separation", 0);
                var toggleBtn = new Button { Text = isCollapsed ? "▶" : "▼", Flat = false, CustomMinimumSize = new Vector2(24, 0) };
                NotesSidebar.ApplyButtonStyle(toggleBtn, ClassColor, roundLeft: true, roundRight: false);
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedClasses.Contains(clsId)) _collapsedClasses.Remove(clsId);
                    else _collapsedClasses.Add(clsId);
                    LoadClasses();
                };
                var btn = new Button { Text = cls.Name, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                NotesSidebar.ApplyButtonStyle(btn, ClassColor, roundLeft: false, roundRight: true);
                btn.SetMeta("id", clsId);
                btn.Pressed += () => { EmitSignal(SignalName.EntitySelected, "class", clsId); if (_collapsedClasses.Remove(clsId)) LoadClasses(); };
                WireCtrlClick(btn, "class", clsId);
                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _classesContainer.AddChild(hbox);

                if (!isCollapsed)
                {
                    foreach (var sub in subclasses)
                    {
                        int subId   = sub.Id;
                        var subHbox = new HBoxContainer();
                        subHbox.AddThemeConstantOverride("separation", 0);
                        subHbox.AddChild(new Control { CustomMinimumSize = new Vector2(14, 0) });
                        var subBtn = MakeSidebarButton(sub.Name, ClassColor, extraLeft: 24);
                        subBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                        subBtn.SetMeta("id", subId);
                        subBtn.Pressed += () => EmitSignal(SignalName.EntitySelected, "subclass", subId);
                        WireCtrlClick(subBtn, "subclass", subId);
                        subHbox.AddChild(subBtn);
                        _classesContainer.AddChild(subHbox);
                    }
                }
            }
            else
            {
                var btn = MakeSidebarButton(cls.Name, ClassColor);
                btn.SetMeta("id", clsId);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "class", clsId);
                WireCtrlClick(btn, "class", clsId);
                _classesContainer.AddChild(btn);
            }
        }
        _classesFirstLoad = false;
    }

    private void LoadSpecies()
    {
        ClearItems(_speciesContainer, _addSpeciesButton);
        if (_campaign?.System == "pathfinder2e")
        {
            foreach (var anc in _db.Pf2eAncestries.GetAll(_campaignId))
            {
                int id  = anc.Id;
                var btn = MakeSidebarButton(anc.Name, SpeciesColor);
                btn.SetMeta("id", id);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "pf2e_ancestry", id);
                WireCtrlClick(btn, "pf2e_ancestry", id);
                _speciesContainer.AddChild(btn);
            }
            _speciesFirstLoad = false;
            return;
        }
        foreach (var sp in _db.Species.GetAll(_campaignId))
        {
            int spId       = sp.Id;
            var subspecies = _db.Subspecies.GetAllForSpecies(sp.Id);
            if (_speciesFirstLoad && subspecies.Count > 0) _collapsedSpecies.Add(spId);
            bool isCollapsed = _collapsedSpecies.Contains(sp.Id);

            if (subspecies.Count > 0)
            {
                var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                hbox.AddThemeConstantOverride("separation", 0);
                var toggleBtn = new Button { Text = isCollapsed ? "▶" : "▼", Flat = false, CustomMinimumSize = new Vector2(24, 0) };
                NotesSidebar.ApplyButtonStyle(toggleBtn, SpeciesColor, roundLeft: true, roundRight: false);
                toggleBtn.Pressed += () =>
                {
                    if (_collapsedSpecies.Contains(spId)) _collapsedSpecies.Remove(spId);
                    else _collapsedSpecies.Add(spId);
                    LoadSpecies();
                };
                var btn = new Button { Text = sp.Name, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                NotesSidebar.ApplyButtonStyle(btn, SpeciesColor, roundLeft: false, roundRight: true);
                btn.SetMeta("id", spId);
                btn.Pressed += () => { EmitSignal(SignalName.EntitySelected, "species", spId); if (_collapsedSpecies.Remove(spId)) LoadSpecies(); };
                WireCtrlClick(btn, "species", spId);
                hbox.AddChild(toggleBtn);
                hbox.AddChild(btn);
                _speciesContainer.AddChild(hbox);

                if (!isCollapsed)
                {
                    foreach (var sub in subspecies)
                    {
                        int subId   = sub.Id;
                        var subHbox = new HBoxContainer();
                        subHbox.AddThemeConstantOverride("separation", 0);
                        subHbox.AddChild(new Control { CustomMinimumSize = new Vector2(14, 0) });
                        var subBtn = MakeSidebarButton(sub.Name, SpeciesColor, extraLeft: 24);
                        subBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                        subBtn.SetMeta("id", subId);
                        subBtn.Pressed += () => EmitSignal(SignalName.EntitySelected, "subspecies", subId);
                        WireCtrlClick(subBtn, "subspecies", subId);
                        subHbox.AddChild(subBtn);
                        _speciesContainer.AddChild(subHbox);
                    }
                }
            }
            else
            {
                var btn = MakeSidebarButton(sp.Name, SpeciesColor);
                btn.SetMeta("id", spId);
                btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "species", spId);
                WireCtrlClick(btn, "species", spId);
                _speciesContainer.AddChild(btn);
            }
        }
        _speciesFirstLoad = false;
    }

    private void LoadAbilities()
    {
        ClearItems(_abilitiesContainer, _addAbilitiesButton);
        foreach (var ability in _db.Abilities.GetAll(_campaignId))
        {
            int id  = ability.Id;
            var btn = MakeSidebarButton(ability.Name, AbilityColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "ability", id);
            WireCtrlClick(btn, "ability", id);
            _abilitiesContainer.AddChild(btn);
        }
    }

    private void LoadCreatures()
    {
        var creaturesPanel = GetNode<Control>("CreaturesPanel");
        if (_campaign?.System != "pathfinder2e") { creaturesPanel.Visible = false; return; }
        creaturesPanel.Visible = true;
        ClearItems(_creaturesContainer, _addCreaturesButton);
        foreach (var c in _db.Pf2eCreatures.GetAll(_campaignId))
        {
            int id  = c.Id;
            var btn = MakeSidebarButton(string.IsNullOrEmpty(c.Name) ? "New Creature" : c.Name, CreatureColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "pf2e_creature", id);
            WireCtrlClick(btn, "pf2e_creature", id);
            _creaturesContainer.AddChild(btn);
        }
    }

    // ── search ────────────────────────────────────────────────────────────────

    private void FilterSystemSidebar(string query)
    {
        bool searching = !string.IsNullOrEmpty(query);
        var creatPnl = GetNode<Control>("CreaturesPanel");
        bool isPf2e  = _campaign?.System == "pathfinder2e";
        var sections = new (Control Panel, VBoxContainer Items)[]
        {
            (GetNode<Control>("ClassesPanel"),   _classesContainer),
            (GetNode<Control>("SpeciesPanel"),   _speciesContainer),
            (GetNode<Control>("AbilitiesPanel"), _abilitiesContainer),
            (creatPnl,                           _creaturesContainer),
        };

        foreach (var (panel, items) in sections)
        {
            if (panel == creatPnl && !isPf2e) continue;
            if (!searching)
            {
                panel.Visible = true; panel.Set("folded", true); items.Visible = false;
                foreach (Node child in items.GetChildren()) if (child is Control c) c.Visible = true;
                continue;
            }
            bool hasMatch = false;
            foreach (Node child in items.GetChildren())
            {
                if (child is Button btn && btn.HasMeta("id"))
                    { bool m = SidebarSearch.FuzzyMatch(query, btn.Text); btn.Visible = m; if (m) hasMatch = true; }
                else if (child is HBoxContainer hbox)
                {
                    var mainBtn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
                    if (mainBtn != null) { bool m = SidebarSearch.FuzzyMatch(query, mainBtn.Text); hbox.Visible = m; if (m) hasMatch = true; }
                }
            }
            panel.Visible = hasMatch;
            if (hasMatch) { panel.Set("folded", false); items.Visible = true; }
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void WireCtrlClick(Button btn, string entityType, int entityId)
    {
        btn.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && mb.CtrlPressed)
            { btn.AcceptEvent(); EmitSignal(SignalName.EntitySelectedNewTab, entityType, entityId); }
        };
    }

    private static void FindAndRenameButton(VBoxContainer container, int entityId, string name)
    {
        if (container == null) return;
        foreach (Node child in container.GetChildren())
        {
            Button btn = null;
            if (child is Button b && b.HasMeta("id")) btn = b;
            else if (child is HBoxContainer hbox) btn = hbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.HasMeta("id"));
            if (btn != null && btn.GetMeta("id").AsInt32() == entityId) { btn.Text = name; break; }
        }
    }

    private static void ClearItems(VBoxContainer container, Button keepButton)
    {
        foreach (Node child in container.GetChildren())
            if (child != keepButton) child.QueueFree();
    }

    private static Button MakeSidebarButton(string text, Color color, int extraLeft = 0)
    {
        var btn = new Button { Text = text, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        NotesSidebar.ApplyButtonStyle(btn, color, extraLeft);
        return btn;
    }
}
