using System.Collections.Generic;
using System.IO;
using DndBuilder.Core.Models;
using Godot;

/// <summary>
/// Self-contained multi-image carousel for any entity.
/// Call Setup(entityType, entityId, db) from the parent detail pane's Load().
/// Base class is Control (not PanelContainer) so children can use free anchor positioning.
/// </summary>
public partial class ImageCarousel : Control
{
    private const int PanelSize  = 200;
    private const int BtnSize    = 32;
    private const int DeleteSize = 28;

    private DatabaseService   _db;
    private EntityType        _entityType;
    private int               _entityId;
    private List<EntityImage> _images = new();
    private int               _index    = 0;
    private bool              _hovering = false;

    private TextureRect _image;
    private Label       _emptyHint;
    private Button      _prevBtn;
    private Button      _nextBtn;
    private Button             _addBtn;
    private Button             _deleteBtn;
    private Label              _counter;
    private ConfirmationDialog _confirmDialog;

    private static readonly string LightboxPath = "res://Scenes/Components/ImageLightbox/ImageLightbox.tscn";

    public override void _Ready()
    {
        CustomMinimumSize   = new Vector2(PanelSize, PanelSize);
        SizeFlagsHorizontal = SizeFlags.Fill;  // don't expand — sibling ExpandFill takes remaining space
        SizeFlagsVertical   = SizeFlags.Fill;  // fixed square — CarouselSpacer below fills remaining height
        ClipContents        = true;

        // ── background ───────────────────────────────────────────────────────
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.12f, 0.12f, 0.12f);
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(2);
        style.BorderColor = new Color(0.3f, 0.3f, 0.3f);
        var bg = new Panel();
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.AddThemeStyleboxOverride("panel", style);
        bg.MouseFilter = MouseFilterEnum.Ignore;  // don't block input
        AddChild(bg);

        // ── image (receives clicks → opens lightbox) ──────────────────────────
        _image = new TextureRect
        {
            StretchMode            = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode             = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter            = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _image.GuiInput      += OnImageGuiInput;
        _image.MouseEntered  += () => { _hovering = true;  UpdateDeleteVisibility(); };
        _image.MouseExited   += () => { _hovering = false; UpdateDeleteVisibility(); };
        AddChild(_image);

        // ── empty hint ───────────────────────────────────────────────────────
        _emptyHint = new Label
        {
            Text                = "Drop image\nor click +",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            AutowrapMode        = TextServer.AutowrapMode.Word,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        _emptyHint.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _emptyHint.Modulate = new Color(0.5f, 0.5f, 0.5f);
        AddChild(_emptyHint);

        // ── add button — bottom-center ────────────────────────────────────────
        _addBtn = new Button { Text = "+" };
        _addBtn.CustomMinimumSize = new Vector2(BtnSize, BtnSize);
        _addBtn.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _addBtn.OffsetLeft   = -BtnSize / 2;
        _addBtn.OffsetRight  =  BtnSize / 2;
        _addBtn.OffsetTop    = -(BtnSize + 4);
        _addBtn.OffsetBottom = -4;
        _addBtn.Pressed += OnAddPressed;
        AddChild(_addBtn);

        // ── prev arrow — bottom-left of add button ────────────────────────────
        _prevBtn = new Button { Text = "‹" };
        _prevBtn.CustomMinimumSize = new Vector2(BtnSize, BtnSize);
        _prevBtn.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _prevBtn.OffsetLeft   = -(BtnSize / 2 + 4 + BtnSize);
        _prevBtn.OffsetRight  = -(BtnSize / 2 + 4);
        _prevBtn.OffsetTop    = -(BtnSize + 4);
        _prevBtn.OffsetBottom = -4;
        _prevBtn.Pressed += () => { _index = (_index - 1 + _images.Count) % _images.Count; Refresh(); };
        AddChild(_prevBtn);

        // ── next arrow — bottom-right of add button ───────────────────────────
        _nextBtn = new Button { Text = "›" };
        _nextBtn.CustomMinimumSize = new Vector2(BtnSize, BtnSize);
        _nextBtn.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _nextBtn.OffsetLeft   = BtnSize / 2 + 4;
        _nextBtn.OffsetRight  = BtnSize / 2 + 4 + BtnSize;
        _nextBtn.OffsetTop    = -(BtnSize + 4);
        _nextBtn.OffsetBottom = -4;
        _nextBtn.Pressed += () => { _index = (_index + 1) % _images.Count; Refresh(); };
        AddChild(_nextBtn);

        // ── delete button — top-right, visible when image exists ─────────────
        _deleteBtn = new Button();
        _deleteBtn.CustomMinimumSize = new Vector2(DeleteSize, DeleteSize);
        var trashIcon = GD.Load<Texture2D>("res://Scenes/Icons/Trashcan.png");
        if (trashIcon != null)
            _deleteBtn.Icon = trashIcon;
        else
            _deleteBtn.Text = "×";
        _deleteBtn.SetAnchorsPreset(LayoutPreset.TopRight);
        _deleteBtn.OffsetLeft   = -(DeleteSize + 4);
        _deleteBtn.OffsetRight  = -4;
        _deleteBtn.OffsetTop    = 4;
        _deleteBtn.OffsetBottom = 4 + DeleteSize;
        _deleteBtn.Visible       = false;
        _deleteBtn.Pressed       += ConfirmDelete;
        _deleteBtn.MouseEntered  += () => { _hovering = true;  UpdateDeleteVisibility(); };
        _deleteBtn.MouseExited   += () => { _hovering = false; UpdateDeleteVisibility(); };
        AddChild(_deleteBtn);

        // ── confirmation dialog ───────────────────────────────────────────────
        _confirmDialog = DialogHelper.Make(text: "Delete this image? This cannot be undone.");
        _confirmDialog.Confirmed += DeleteCurrentImage;
        AddChild(_confirmDialog);

        // ── counter — bottom-right ────────────────────────────────────────────
        _counter = new Label { HorizontalAlignment = HorizontalAlignment.Right, Visible = false };
        _counter.SetAnchorsPreset(LayoutPreset.BottomRight);
        _counter.OffsetLeft   = -52;
        _counter.OffsetRight  = -4;
        _counter.OffsetTop    = -20;
        _counter.OffsetBottom = -4;
        _counter.AddThemeConstantOverride("outline_size", 4);
        _counter.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(_counter);

        Refresh();
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void Setup(EntityType entityType, int entityId, DatabaseService db)
    {
        _db         = db;
        _entityType = entityType;
        _entityId   = entityId;
        _index      = 0;
        _images     = _db.EntityImages.GetAll(entityType, entityId);
        Refresh();
    }

    // ── drag-and-drop ─────────────────────────────────────────────────────────

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_db == null) return false;
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var dict = data.AsGodotDictionary();
        if (!dict.ContainsKey("files")) return false;
        foreach (var f in dict["files"].AsStringArray())
            if (IsImagePath(f)) return true;
        return false;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dict = data.AsGodotDictionary();
        foreach (var path in dict["files"].AsStringArray())
        {
            if (!IsImagePath(path)) continue;
            AddImage(path);
        }
    }

    // ── private ───────────────────────────────────────────────────────────────

    private void OnImageGuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (_images.Count > 0 && _image.Texture != null)
                OpenLightbox();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        if (_image == null) return;

        bool hasImages     = _images.Count > 0;
        bool multiImage    = _images.Count > 1;
        _emptyHint.Visible = !hasImages;
        _counter.Visible    = multiImage;
        _prevBtn.Visible    = multiImage;
        _nextBtn.Visible    = multiImage;

        if (!hasImages) { _image.Texture = null; _counter.Text = ""; return; }

        _index         = Mathf.Clamp(_index, 0, _images.Count - 1);
        _counter.Text  = $"{_index + 1} / {_images.Count}";
        _image.Texture = LoadTexture(_images[_index].Path);
        UpdateDeleteVisibility();
    }

    private void OpenLightbox()
    {
        var scene = GD.Load<PackedScene>(LightboxPath);
        if (scene == null) return;
        var lightbox = scene.Instantiate<ImageLightbox>();
        lightbox.Setup(_images, _index);
        GetViewport().GuiReleaseFocus();
        GetTree().Root.AddChild(lightbox);
    }

    private void OnAddPressed()
    {
        if (_db == null) return;
        DisplayServer.FileDialogShow(
            "Select Image",
            OS.GetSystemDir(OS.SystemDir.Pictures),
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            new[] { "*.png,*.jpg,*.jpeg,*.webp ; Image Files" },
            Callable.From((bool ok, string[] paths, long _) =>
            {
                if (ok && paths.Length > 0)
                    AddImage(paths[0]);
            }));
    }

    private void ConfirmDelete() => DialogHelper.Show(_confirmDialog);

    private void UpdateDeleteVisibility()
    {
        if (_deleteBtn != null)
            _deleteBtn.Visible = _hovering && _images.Count > 0;
    }

    private void DeleteCurrentImage()
    {
        if (_db == null || _images.Count == 0) return;
        _db.EntityImages.Delete(_images[_index].Id);
        _images.RemoveAt(_index);
        _index = Mathf.Clamp(_index, 0, Mathf.Max(0, _images.Count - 1));
        Refresh();
    }

    private void AddImage(string path)
    {
        var img = new EntityImage
        {
            EntityType = _entityType,
            EntityId   = _entityId,
            Path       = path,
            SortOrder  = _images.Count,
        };
        img.Id = _db.EntityImages.Add(img);
        _images.Add(img);
        _index = _images.Count - 1;
        Refresh();
    }

    private static ImageTexture LoadTexture(string path)
    {
        if (!File.Exists(path)) return null;
        var img = new Image();
        if (img.Load(path) != Error.Ok) return null;
        return ImageTexture.CreateFromImage(img);
    }

    private static bool IsImagePath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp";
    }
}
