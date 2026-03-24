using System;
using System.Collections.Generic;
using System.Linq;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public partial class ImportExportModal : Window
{
    public enum Mode { Export, Import }

    // Called with the built selection when the user confirms.
    public event Action<ExportSelection> Confirmed;

    [Export] private Label          _titleLabel;
    [Export] private VBoxContainer  _sectionsContainer;
    [Export] private Button         _cancelButton;
    [Export] private Button         _actionButton;

    private DatabaseService _db;
    private int             _campaignId;
    private Mode            _mode;
    private ExportPackage   _importPackage; // Import mode only

    // Checkbox bookkeeping for cascade logic
    private CheckBox                           _selectAllCheckbox;
    private CheckBox                           _includeImagesCheckbox;
    private readonly List<SectionState>        _sections = new();
    private bool                               _updating;

    // ─── Setup ───────────────────────────────────────────────────────────────

    public void SetupExport(int campaignId, DatabaseService db)
    {
        _campaignId = campaignId;
        _db         = db;
        _mode       = Mode.Export;
        _titleLabel.Text   = "Export Campaign Data";
        _actionButton.Text = "Export";
        BuildSections();
    }

    public void SetupImport(ExportPackage package, int campaignId, DatabaseService db)
    {
        _importPackage = package;
        _campaignId    = campaignId;
        _db            = db;
        _mode          = Mode.Import;
        _titleLabel.Text   = "Import Campaign Data";
        _actionButton.Text = "Import";
        BuildSections();
    }

    public override void _Ready()
    {
        _cancelButton.Pressed += () => Hide();
        _actionButton.Pressed += OnConfirm;
        CloseRequested        += () => Hide();
    }

    // ─── Section building ─────────────────────────────────────────────────────

    private void BuildSections()
    {
        // Clear any previous build
        foreach (Node child in _sectionsContainer.GetChildren())
            child.QueueFree();
        _sections.Clear();

        // Global "Select All" row
        _selectAllCheckbox       = new CheckBox { Text = "Select All", ButtonPressed = true };
        _selectAllCheckbox.Toggled += OnSelectAllToggled;
        _sectionsContainer.AddChild(_selectAllCheckbox);
        _sectionsContainer.AddChild(new HSeparator());

        // Include Images checkbox — export: always shown; import: only if package has images
        bool showImages = _mode == Mode.Export || (_importPackage?.Images.Count > 0);
        if (showImages)
        {
            string label = _mode == Mode.Export
                ? "Include Images"
                : $"Include Images ({_importPackage.Images.Count})";
            _includeImagesCheckbox = new CheckBox { Text = label, ButtonPressed = true };
            _sectionsContainer.AddChild(_includeImagesCheckbox);
            _sectionsContainer.AddChild(new HSeparator());
        }

        // Types section
        BuildTypesSection();

        // Entity sections
        if (_mode == Mode.Export)
        {
            BuildEntitySection("Factions",   _db.Factions.GetAll(_campaignId).Select(f => (f.Id, f.Name)).ToList());
            BuildEntitySection("NPCs",       _db.Npcs.GetAll(_campaignId).Select(n => (n.Id, n.Name)).ToList());
            BuildEntitySection("Locations",  _db.Locations.GetAll(_campaignId).Select(l => (l.Id, l.Name)).ToList());
            BuildEntitySection("Sessions",   _db.Sessions.GetAll(_campaignId).Select(s => (s.Id, string.IsNullOrEmpty(s.Title) ? "Untitled Session" : s.Title)).ToList());
            BuildEntitySection("Items",      _db.Items.GetAll(_campaignId).Select(i => (i.Id, i.Name)).ToList());
            BuildEntitySection("Quests",     _db.Quests.GetAll(_campaignId).Select(q => (q.Id, q.Name)).ToList());
            // System entities
            BuildEntitySection("Classes",    _db.Classes.GetAll(_campaignId).Select(c => (c.Id, c.Name)).ToList());
            BuildEntitySection("Subclasses", _db.Classes.GetAllSubclasses(_campaignId).Select(s => (s.Id, s.Name)).ToList());
            BuildEntitySection("Subspecies", _db.Subspecies.GetAll(_campaignId).Select(s => (s.Id, s.Name)).ToList());
        }
        else // Import — show only what's in the file
        {
            BuildEntitySection("Factions",   _importPackage.Factions.Select(f => (f.Id, f.Name)).ToList());
            BuildEntitySection("NPCs",       _importPackage.Npcs.Select(n => (n.Id, n.Name)).ToList());
            BuildEntitySection("Locations",  _importPackage.Locations.Select(l => (l.Id, l.Name)).ToList());
            BuildEntitySection("Sessions",   _importPackage.Sessions.Select(s => (s.Id, string.IsNullOrEmpty(s.Title) ? "Untitled Session" : s.Title)).ToList());
            BuildEntitySection("Items",      _importPackage.Items.Select(i => (i.Id, i.Name)).ToList());
            BuildEntitySection("Quests",     _importPackage.Quests.Select(q => (q.Id, q.Name)).ToList());
            // System entities
            BuildEntitySection("Classes",    _importPackage.Classes.Select(c => (c.Id, c.Name)).ToList());
            BuildEntitySection("Subclasses", _importPackage.Subclasses.Select(s => (s.Id, s.Name)).ToList());
            BuildEntitySection("Subspecies", _importPackage.Subspecies.Select(s => (s.Id, s.Name)).ToList());
        }
    }

    private void BuildTypesSection()
    {
        var typeRows = _mode == Mode.Export
            ? new List<(int id, string name)>
            {
                (0,  $"Species ({_db.Species.GetAll(_campaignId).Count})"),
                (1,  $"NPC Statuses ({_db.NpcStatuses.GetAll(_campaignId).Count})"),
                (2,  $"NPC Relationship Types ({_db.NpcRelationshipTypes.GetAll(_campaignId).Count})"),
                (3,  $"NPC Faction Roles ({_db.NpcFactionRoles.GetAll(_campaignId).Count})"),
                (4,  $"Character Relationship Types ({_db.CharacterRelationshipTypes.GetAll(_campaignId).Count})"),
                (5,  $"Location Faction Roles ({_db.LocationFactionRoles.GetAll(_campaignId).Count})"),
                (6,  $"Faction Relationship Types ({_db.FactionRelationshipTypes.GetAll(_campaignId).Count})"),
                (7,  $"Item Types ({_db.ItemTypes.GetAll(_campaignId).Count})"),
                (8,  $"Quest Statuses ({_db.QuestStatuses.GetAll(_campaignId).Count})"),
            }
            : new List<(int id, string name)>
            {
                (0,  $"Species ({_importPackage.Species.Count})"),
                (1,  $"NPC Statuses ({_importPackage.NpcStatuses.Count})"),
                (2,  $"NPC Relationship Types ({_importPackage.NpcRelationshipTypes.Count})"),
                (3,  $"NPC Faction Roles ({_importPackage.NpcFactionRoles.Count})"),
                (4,  $"Character Relationship Types ({_importPackage.CharacterRelationshipTypes.Count})"),
                (5,  $"Location Faction Roles ({_importPackage.LocationFactionRoles.Count})"),
                (6,  $"Faction Relationship Types ({_importPackage.FactionRelationshipTypes.Count})"),
                (7,  $"Item Types ({_importPackage.ItemTypes.Count})"),
                (8,  $"Quest Statuses ({_importPackage.QuestStatuses.Count})"),
            };

        // Hide rows with 0 items
        typeRows = typeRows.Where(r => !r.name.EndsWith("(0)")).ToList();

        BuildEntitySection("Types", typeRows, isTypesSection: true);
    }

    private void BuildEntitySection(string title, List<(int id, string name)> items, bool isTypesSection = false)
    {
        if (items.Count == 0) return;

        var folder = new FoldableContainer();
        folder.Title  = $"{title}  ({items.Count})";
        folder.Folded = true;

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 2);
        folder.AddChild(content);

        var section = new SectionState { Key = title, IsTypes = isTypesSection };

        // "All [Title]" checkbox
        var allCheck = new CheckBox { Text = $"All {title}", ButtonPressed = true };
        content.AddChild(allCheck);
        section.AllCheckbox = allCheck;

        // Individual item checkboxes
        foreach (var (id, name) in items)
        {
            var cb = new CheckBox { Text = name, ButtonPressed = true };
            content.AddChild(cb);
            section.Items.Add((cb, id));

            cb.Toggled += _ => OnItemToggled(section);
        }

        allCheck.Toggled += b => OnSectionAllToggled(section, b);

        _sectionsContainer.AddChild(folder);
        _sections.Add(section);

        UpdateSelectAll();
    }

    // ─── Cascade logic ────────────────────────────────────────────────────────

    private void OnSelectAllToggled(bool pressed)
    {
        if (_updating) return;
        _updating = true;
        foreach (var s in _sections)
        {
            s.AllCheckbox.ButtonPressed = pressed;
            foreach (var (cb, _) in s.Items) cb.ButtonPressed = pressed;
        }
        _updating = false;
    }

    private void OnSectionAllToggled(SectionState section, bool pressed)
    {
        if (_updating) return;
        _updating = true;
        foreach (var (cb, _) in section.Items) cb.ButtonPressed = pressed;
        _updating = false;
        UpdateSelectAll();
    }

    private void OnItemToggled(SectionState section)
    {
        if (_updating) return;
        _updating = true;
        bool allChecked = section.Items.All(i => i.checkbox.ButtonPressed);
        section.AllCheckbox.ButtonPressed = allChecked;
        _updating = false;
        UpdateSelectAll();
    }

    private void UpdateSelectAll()
    {
        if (_updating) return;
        _updating = true;
        bool allChecked = _sections.All(s =>
            s.AllCheckbox.ButtonPressed && s.Items.All(i => i.checkbox.ButtonPressed));
        _selectAllCheckbox.ButtonPressed = allChecked;
        _updating = false;
    }

    // ─── Gather selection ─────────────────────────────────────────────────────

    private ExportSelection GatherSelection()
    {
        var sel = new ExportSelection();

        foreach (var section in _sections)
        {
            if (section.IsTypes)
            {
                // Items in the types section use fixed IDs 0–7 mapped to type flags
                bool allTypes = section.AllCheckbox.ButtonPressed;
                sel.AllTypes = allTypes;
                foreach (var (cb, id) in section.Items)
                {
                    bool on = allTypes || cb.ButtonPressed;
                    switch (id)
                    {
                        case 0: sel.Species                    = on; break;
                        case 1: sel.NpcStatuses                = on; break;
                        case 2: sel.NpcRelationshipTypes       = on; break;
                        case 3: sel.NpcFactionRoles            = on; break;
                        case 4: sel.CharacterRelationshipTypes = on; break;
                        case 5: sel.LocationFactionRoles       = on; break;
                        case 6: sel.FactionRelationshipTypes   = on; break;
                        case 7: sel.ItemTypes                  = on; break;
                        case 8: sel.QuestStatuses              = on; break;
                    }
                }
            }
            else
            {
                bool allChecked = section.AllCheckbox.ButtonPressed;
                var ids = section.Items.Where(i => i.checkbox.ButtonPressed).Select(i => i.id).ToHashSet();
                switch (section.Key)
                {
                    case "Factions":   sel.AllFactions   = allChecked; sel.FactionIds    = ids; break;
                    case "NPCs":       sel.AllNpcs       = allChecked; sel.NpcIds        = ids; break;
                    case "Locations":  sel.AllLocations  = allChecked; sel.LocationIds   = ids; break;
                    case "Sessions":   sel.AllSessions   = allChecked; sel.SessionIds    = ids; break;
                    case "Items":      sel.AllItems      = allChecked; sel.ItemIds       = ids; break;
                    case "Quests":     sel.AllQuests     = allChecked; sel.QuestIds      = ids; break;
                    case "Classes":    sel.AllClasses    = allChecked; sel.ClassIds      = ids; break;
                    case "Subclasses": sel.AllSubclasses = allChecked; sel.SubclassIds   = ids; break;
                    case "Subspecies": sel.AllSubspecies = allChecked; sel.SubspeciesIds = ids; break;
                }
            }
        }

        sel.IncludeImages = _includeImagesCheckbox?.ButtonPressed ?? false;
        return sel;
    }

    // ─── Confirm ──────────────────────────────────────────────────────────────

    private void OnConfirm()
    {
        var sel = GatherSelection();
        Hide();
        Confirmed?.Invoke(sel);
    }

    // ─── Inner type ───────────────────────────────────────────────────────────

    private class SectionState
    {
        public string                        Key         { get; set; } = "";
        public bool                          IsTypes     { get; set; }
        public CheckBox                      AllCheckbox { get; set; }
        public List<(CheckBox checkbox, int id)> Items  { get; set; } = new();
    }
}
