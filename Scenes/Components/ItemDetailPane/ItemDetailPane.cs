using DndBuilder.Core.Models;
using Godot;


public partial class ItemDetailPane : ScrollContainer
{
    private DatabaseService    _db;
    private Item               _item;
    private ConfirmationDialog _confirmDialog;

    [Signal] public delegate void NavigateToEventHandler(string entityType, int entityId);
    [Signal] public delegate void NameChangedEventHandler(string entityType, int entityId, string displayText);
    [Signal] public delegate void DeletedEventHandler(string entityType, int entityId);

    [Export] private LineEdit         _nameInput;
    [Export] private TypeOptionButton _typeInput;
    [Export] private CheckBox         _isUniqueInput;
    [Export] private TextEdit         _descInput;
    [Export] private WikiNotes _notes;
    [Export] private Button           _deleteButton;
    [Export] private ImageCarousel    _imageCarousel;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _nameInput.TextChanged   += name => { Save(); EmitSignal(SignalName.NameChanged, "item", _item?.Id ?? 0, string.IsNullOrEmpty(name) ? "New Item" : name); };
        _nameInput.FocusExited   += () => { if (_nameInput.Text == "") _nameInput.Text = "New Item"; };
        _nameInput.FocusEntered  += () => _nameInput.CallDeferred(LineEdit.MethodName.SelectAll);
        _typeInput.TypeSelected  += _ => Save();
        _isUniqueInput.Toggled   += _ => Save();
        _descInput.TextChanged   += () => Save();
        _notes.TextChanged += () => Save();
        _notes.NavigateTo  += (type, id) => EmitSignal(SignalName.NavigateTo, type, id);

        _confirmDialog = DialogHelper.Make("Delete Item");
        AddChild(_confirmDialog);
        _confirmDialog.Confirmed += () => EmitSignal(SignalName.Deleted, "item", _item?.Id ?? 0);
        _deleteButton.Pressed += () =>
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_item?.Name}\"? This cannot be undone.");
        };
    }

    public void Load(Item item)
    {
        _item = item;

        _typeInput.NoneText = "(none)";
        _typeInput.Setup(
            () => _db.ItemTypes.GetAll(item.CampaignId).ConvertAll(t => (t.Id, t.Name)),
            name => { _db.ItemTypes.Add(new ItemType { CampaignId = item.CampaignId, Name = name, Description = "" }); },
            id   => _db.ItemTypes.Delete(id));
        _typeInput.SelectById(item.TypeId);

        _imageCarousel?.Setup(EntityType.Item, item.Id, _db);

        _nameInput.Text              = string.IsNullOrEmpty(item.Name) ? "New Item" : item.Name;
        _isUniqueInput.ButtonPressed = item.IsUnique;
        _descInput.Text              = item.Description;
        _notes.Setup(item.CampaignId, _db);
        _notes.Text = item.Notes;
    }

    private void Save()
    {
        if (_item == null) return;
        _item.Name        = string.IsNullOrEmpty(_nameInput.Text) ? "New Item" : _nameInput.Text;
        _item.TypeId      = _typeInput.SelectedId;
        _item.IsUnique    = _isUniqueInput.ButtonPressed;
        _item.Description = _descInput.Text;
        _item.Notes       = _notes.Text;
        _db.Items.Edit(_item);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_item == null) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Delete)
        {
            DialogHelper.Show(_confirmDialog, $"Delete \"{_item.Name}\"? This cannot be undone.");
            AcceptEvent();
        }
    }

}
