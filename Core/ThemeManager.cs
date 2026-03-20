using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Autoloaded singleton that manages the application colour theme.
/// Each theme is a Tailwind colour family: 900 for the navbar, 800 for the app
/// background, 700 for input/popup components, and a complementary 400/600 pair
/// for the hover/focus accent.
///
/// Call ApplyTheme(name) to switch live. The preference is persisted to the database
/// via DatabaseService.Settings.
/// </summary>
public partial class ThemeManager : Node
{
    [Signal] public delegate void ThemeChangedEventHandler(string name);

    public static ThemeManager Instance { get; private set; }

    // ── palette definition ────────────────────────────────────────────────────

    public record ThemePalette(
        string Name,
        Color  Background,  // Tailwind [color]-800  — app clear colour + carousel bg
        Color  NavBar,      // Tailwind [color]-900  — navbar panel
        Color  Component,   // Tailwind [color]-700  — inputs, popups
        Color  Hover,       // complementary 400     — row hover accent
        Color  Focus        // complementary 600     — focus ring
    );

    // Accent pairings: each base colour's accent is shifted ~2 steps around the
    // Tailwind hue order so it reads as complementary rather than identical.
    public static readonly IReadOnlyList<ThemePalette> Palettes = new List<ThemePalette>
    {
        // Slate stays at 800/900/700 — desaturated, so the lighter shades still read as dark.
        // All other families shift to 900/950/800 — their 800s are vivid; 900/950 is far more neutral.
        //           Name        Background   NavBar       Component    Hover (same-family 600/700)  Focus (kept as-is)
        new("Slate",   H("#1e293b"), H("#0f172a"), H("#334155"), H("#475569"), H("#7c3aed")),
        new("Red",     H("#7f1d1d"), H("#450a0a"), H("#991b1b"), H("#b91c1c"), H("#059669")),
        new("Orange",  H("#7c2d12"), H("#431407"), H("#9a3412"), H("#c2410c"), H("#2563eb")),
        new("Amber",   H("#78350f"), H("#451a03"), H("#92400e"), H("#b45309"), H("#4f46e5")),
        new("Yellow",  H("#713f12"), H("#422006"), H("#854d0e"), H("#a16207"), H("#9333ea")),
        new("Lime",    H("#365314"), H("#1a2e05"), H("#3f6212"), H("#4d7c0f"), H("#c026d3")),
        new("Green",   H("#14532d"), H("#052e16"), H("#166534"), H("#15803d"), H("#e11d48")),
        new("Emerald", H("#064e3b"), H("#022c22"), H("#065f46"), H("#047857"), H("#db2777")),
        new("Teal",    H("#134e4a"), H("#042f2e"), H("#115e59"), H("#0f766e"), H("#9333ea")),
        new("Cyan",    H("#164e63"), H("#083344"), H("#155e75"), H("#0e7490"), H("#e11d48")),
        new("Sky",     H("#0c4a6e"), H("#082f49"), H("#075985"), H("#0369a1"), H("#d97706")),
        new("Blue",    H("#1e3a8a"), H("#172554"), H("#1e40af"), H("#1d4ed8"), H("#d97706")),
        new("Indigo",  H("#312e81"), H("#1e1b4b"), H("#3730a3"), H("#4338ca"), H("#65a30d")),
        new("Violet",  H("#4c1d95"), H("#2e1065"), H("#5b21b6"), H("#6d28d9"), H("#059669")),
        new("Purple",  H("#581c87"), H("#3b0764"), H("#6b21a8"), H("#7e22ce"), H("#0d9488")),
        new("Fuchsia", H("#701a75"), H("#4a044e"), H("#86198f"), H("#a21caf"), H("#0891b2")),
        new("Pink",    H("#831843"), H("#500724"), H("#9d174d"), H("#be185d"), H("#0284c7")),
        new("Rose",    H("#881337"), H("#4c0519"), H("#9f1239"), H("#be123c"), H("#0d9488")),
    };

    // Rose 500 — used for the delete-hover accent regardless of current theme
    // so it always reads as a danger signal.
    public static readonly Color DeleteHoverColor = H("#f43f5e");

    // ── runtime state ─────────────────────────────────────────────────────────

    public ThemePalette Current { get; private set; }

    private DatabaseService _db;

    public override void _Ready()
    {
        Instance = this;
        _db      = GetNode<DatabaseService>("/root/DatabaseService");

        var saved = _db.Settings.Get("theme", "Slate");
        ApplyTheme(saved, persist: false);
    }

    /// <summary>
    /// Switch to the named palette immediately. Updates the Godot theme resource,
    /// the rendering clear colour, and optionally saves the choice to the database.
    /// </summary>
    public void ApplyTheme(string name, bool persist = true)
    {
        var palette = Palettes.FirstOrDefault(p => p.Name == name) ?? Palettes[0];
        Current = palette;

        // 1. App background clear colour
        RenderingServer.SetDefaultClearColor(palette.Background);

        // 2. Mutate the shared theme.tres StyleBoxFlat objects in-place.
        //    Because StyleBox extends Resource, changing a property emits `changed`,
        //    which Godot propagates through the Theme to all controls — no manual
        //    node traversal required.
        var theme = ResourceLoader.Load<Theme>("res://theme.tres");

        SetStyleboxColor(theme, "row_hover",    "DndBuilder",  palette.Hover);
        SetStyleboxColor(theme, "delete_hover", "DndBuilder",  DeleteHoverColor);

        // LineEdit and TextEdit share the same StyleBoxFlat sub-resources in theme.tres,
        // so updating LineEdit automatically updates TextEdit too.
        // PopupPanel uses the component colour and is also updated here.
        SetInputStylebox(theme, palette.Component, palette.Focus);

        // 3. Persist
        if (persist)
            _db.Settings.Set("theme", name);

        // 4. Notify scene nodes that need manual updates (NavBar bg, ImageCarousel bg)
        EmitSignal(SignalName.ThemeChanged, name);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void SetStyleboxColor(Theme theme, string styleName, string typeName, Color color)
    {
        if (theme.GetStylebox(styleName, typeName) is StyleBoxFlat sb)
            sb.BgColor = color;
    }

    private static void SetInputStylebox(Theme theme, Color bgColor, Color focusColor)
    {
        if (theme.GetStylebox("focus", "LineEdit") is StyleBoxFlat focusSb)
        {
            focusSb.BgColor     = bgColor;
            focusSb.BorderColor = focusColor;
        }
        if (theme.GetStylebox("normal", "LineEdit") is StyleBoxFlat normalSb)
            normalSb.BgColor = bgColor;

        // PopupPanel bg uses component colour
        if (theme.GetStylebox("panel", "PopupPanel") is StyleBoxFlat popupSb)
            popupSb.BgColor = bgColor;
    }

    private static Color H(string hex) => Color.FromHtml(hex);
}
