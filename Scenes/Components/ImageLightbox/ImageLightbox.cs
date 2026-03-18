using System.Collections.Generic;
using System.IO;
using DndBuilder.Core.Models;
using Godot;

/// <summary>
/// Full-screen pan/zoom image viewer overlay.
/// Instantiated by ImageCarousel; call Setup() before adding to the tree.
/// Navigate with ‹/› buttons or Left/Right arrow keys.
/// Close: click backdrop, click ×, or press Escape.
/// </summary>
public partial class ImageLightbox : CanvasLayer
{
    private TextureRect       _imageDisplay;
    private Button            _closeBtn;
    private Button            _prevBtn;
    private Button            _nextBtn;
    private List<EntityImage> _images = new();
    private int               _index  = 0;

    private Vector2      _dispSize  = Vector2.Zero;
    private bool         _dragging  = false;
    private Vector2      _dragStart = Vector2.Zero;
    private Vector2      _posStart  = Vector2.Zero;
    private float        _zoom      = 1.0f;
    private const float  ZoomMin      = 0.25f;
    private const float  ZoomMax      = 4.0f;
    private const float  ZoomStep     = 0.15f;
    private const float  CloseBtnSize = 28f;
    private const float  NavBtnSize   = 40f;

    // Stored before _Ready if Setup() is called early
    private List<EntityImage> _pendingImages;
    private int               _pendingIndex;

    public override void _Ready()
    {
        Layer = 100;

        // ── backdrop ─────────────────────────────────────────────────────────
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += OnBackdropInput;
        AddChild(backdrop);

        // ── image ─────────────────────────────────────────────────────────────
        _imageDisplay = new TextureRect
        {
            StretchMode             = TextureRect.StretchModeEnum.KeepAspect,
            ExpandMode              = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter             = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.Move,
        };
        _imageDisplay.GuiInput += OnImageInput;
        AddChild(_imageDisplay);

        // ── close button — top-right corner of image ──────────────────────────
        _closeBtn = new Button { Text = "×" };
        _closeBtn.CustomMinimumSize = new Vector2(CloseBtnSize, CloseBtnSize);
        _closeBtn.Pressed += Close;
        AddChild(_closeBtn);

        // ── nav buttons — flanking the image, repositioned with it ───────────
        _prevBtn = new Button { Text = "‹" };
        _prevBtn.CustomMinimumSize = new Vector2(NavBtnSize, NavBtnSize);
        _prevBtn.Pressed += () => Navigate(-1);
        AddChild(_prevBtn);

        _nextBtn = new Button { Text = "›" };
        _nextBtn.CustomMinimumSize = new Vector2(NavBtnSize, NavBtnSize);
        _nextBtn.Pressed += () => Navigate(1);
        AddChild(_nextBtn);

        // ── apply pending setup ───────────────────────────────────────────────
        if (_pendingImages != null)
        {
            _images = _pendingImages;
            _index  = _pendingIndex;
            _pendingImages = null;
            LoadCurrent();
        }

        UpdateNavVisibility();
    }

    /// <summary>Called by ImageCarousel before AddChild.</summary>
    public void Setup(List<EntityImage> images, int index)
    {
        if (_imageDisplay != null)
        {
            _images = images;
            _index  = index;
            LoadCurrent();
            UpdateNavVisibility();
        }
        else
        {
            _pendingImages = images;
            _pendingIndex  = index;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
            if (key.Keycode == Key.Left)   { Navigate(-1); GetViewport().SetInputAsHandled(); }
            if (key.Keycode == Key.Right)  { Navigate(1);  GetViewport().SetInputAsHandled(); }
        }
    }

    // ── input handling ────────────────────────────────────────────────────────

    private void OnBackdropInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            Close();
    }

    private void OnImageInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                _dragging = mb.Pressed;
                if (_dragging)
                {
                    _dragStart = mb.GlobalPosition;
                    _posStart  = _imageDisplay.Position;
                }
                break;

            case InputEventMouseMotion mm when _dragging:
                _imageDisplay.Position = _posStart + (mm.GlobalPosition - _dragStart);
                PositionCloseButton();
                PositionNavButtons();
                break;

            case InputEventMouseButton mb when mb.Pressed &&
                (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown):
                ApplyZoom(mb.ButtonIndex == MouseButton.WheelUp ? ZoomStep : -ZoomStep, mb.GlobalPosition);
                break;
        }
    }

    // ── navigation ────────────────────────────────────────────────────────────

    private void Navigate(int dir)
    {
        if (_images.Count < 2) return;
        _index = (_index + dir + _images.Count) % _images.Count;
        _zoom  = 1.0f;
        LoadCurrent();
    }

    private void LoadCurrent()
    {
        if (_images.Count == 0 || _imageDisplay == null) return;
        var path = _images[_index].Path;
        if (!File.Exists(path)) return;
        var img = new Image();
        if (img.Load(path) != Error.Ok) return;
        ApplyTexture(ImageTexture.CreateFromImage(img));
    }

    private void UpdateNavVisibility()
    {
        if (_prevBtn == null || _nextBtn == null) return;
        bool multi = _images.Count > 1;
        _prevBtn.Visible = multi;
        _nextBtn.Visible = multi;
    }

    // ── zoom / texture ────────────────────────────────────────────────────────

    private void ApplyZoom(float delta, Vector2 pivot)
    {
        float newZoom = Mathf.Clamp(_zoom + delta, ZoomMin, ZoomMax);
        float ratio   = newZoom / _zoom;
        _zoom         = newZoom;

        Vector2 offset = (_imageDisplay.Position - pivot) * ratio + pivot;
        _imageDisplay.Scale    = new Vector2(_zoom, _zoom);
        _imageDisplay.Position = offset;
        PositionCloseButton();
        PositionNavButtons();
    }

    private void ApplyTexture(ImageTexture texture)
    {
        _imageDisplay.Texture = texture;
        if (texture == null) return;

        var   viewport = GetViewport().GetVisibleRect().Size;
        var   texSize  = new Vector2(texture.GetWidth(), texture.GetHeight());
        float scale    = Mathf.Min(Mathf.Min(viewport.X * 0.85f / texSize.X,
                                             viewport.Y * 0.85f / texSize.Y), 1f);
        _dispSize = texSize * scale;

        _imageDisplay.Scale    = new Vector2(1f, 1f);
        _imageDisplay.Size     = _dispSize;
        _imageDisplay.Position = (viewport - _dispSize) / 2f;
        PositionCloseButton();
        PositionNavButtons();
    }

    private void PositionCloseButton()
    {
        if (_closeBtn == null || _imageDisplay == null) return;
        _closeBtn.Position = _imageDisplay.Position + new Vector2(
            _dispSize.X * _zoom - CloseBtnSize - 4f,
            4f);
    }

    private void PositionNavButtons()
    {
        if (_prevBtn == null || _nextBtn == null || _imageDisplay == null) return;
        float imgW  = _dispSize.X * _zoom;
        float imgH  = _dispSize.Y * _zoom;
        float midY  = _imageDisplay.Position.Y + (imgH - NavBtnSize) / 2f;
        _prevBtn.Position = new Vector2(_imageDisplay.Position.X - NavBtnSize - 8f, midY);
        _nextBtn.Position = new Vector2(_imageDisplay.Position.X + imgW + 8f,       midY);
    }

    private void Close() => QueueFree();
}
