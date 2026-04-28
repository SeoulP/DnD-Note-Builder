using DndBuilder.Core.Models;
using Godot;

public partial class TrackerSidebar : VBoxContainer
{
    private int             _campaignId;
    private Campaign        _campaign;
    private DatabaseService _db;

    [Export] private Button        _addEncounterButton;
    [Export] private VBoxContainer _encountersContainer;

    [Signal] public delegate void EntitySelectedEventHandler(string entityType, int entityId);
    [Signal] public delegate void EntitySelectedNewTabEventHandler(string entityType, int entityId);

    internal static readonly Color EncounterColor = new(0.95f, 0.35f, 0.35f);

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _addEncounterButton.Pressed += () =>
        {
            string today    = System.DateTime.Now.ToString("yyyy-MM-dd");
            var    existing = _db.Encounters.GetAll(_campaignId);
            int    count    = 0;
            foreach (var e in existing)
                if (e.Name != null && e.Name.StartsWith(today)) count++;
            count++;
            var enc = new Encounter { CampaignId = _campaignId, Name = $"{today} #{count}", StartedAt = System.DateTime.UtcNow.ToString("o") };
            int eid = _db.Encounters.Add(enc);
            LoadEncounters();
            EmitSignal(SignalName.EntitySelected, "encounter", eid);
        };

        NotesSidebar.StyleAddButton(_addEncounterButton, EncounterColor);
        NotesSidebar.StyleAccordion(GetNode<Control>("EncountersPanel"), EncounterColor);
    }

    public void SetCampaign(int campaignId, Campaign campaign, SystemVocabulary vocab)
    {
        _campaignId = campaignId;
        _campaign   = campaign;
        ReloadAll();
    }

    public void ReloadAll() => LoadEncounters();

    public void Reload(string entityType)
    {
        if (entityType == "encounter") LoadEncounters();
    }

    public void UpdateButtonName(string entityType, int entityId, string name)
    {
        if (entityType != "encounter") return;
        foreach (Node child in _encountersContainer.GetChildren())
        {
            if (child is Button btn && btn.HasMeta("id") && btn.GetMeta("id").AsInt32() == entityId)
            { btn.Text = name; break; }
        }
    }

    private void LoadEncounters()
    {
        ClearItems(_encountersContainer, _addEncounterButton);
        foreach (var enc in _db.Encounters.GetAll(_campaignId))
        {
            int    id    = enc.Id;
            string label = string.IsNullOrEmpty(enc.Name) ? "New Encounter" : enc.Name;
            if (enc.IsResolved) label += " ✓";
            var btn = MakeSidebarButton(label, EncounterColor);
            btn.SetMeta("id", id);
            btn.Pressed += () => EmitSignal(SignalName.EntitySelected, "encounter", id);
            WireCtrlClick(btn, "encounter", id);
            _encountersContainer.AddChild(btn);
        }
    }

    private void WireCtrlClick(Button btn, string entityType, int entityId)
    {
        btn.GuiInput += (InputEvent e) =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && mb.CtrlPressed)
            { btn.AcceptEvent(); EmitSignal(SignalName.EntitySelectedNewTab, entityType, entityId); }
        };
    }

    private static void ClearItems(VBoxContainer container, Button keepButton)
    {
        foreach (Node child in container.GetChildren())
            if (child != keepButton) child.QueueFree();
    }

    private static Button MakeSidebarButton(string text, Color color)
    {
        var btn = new Button { Text = text, Flat = false, Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        NotesSidebar.ApplyButtonStyle(btn, color);
        return btn;
    }
}
