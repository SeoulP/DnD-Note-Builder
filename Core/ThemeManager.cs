using Godot;

/// <summary>
/// Autoloaded singleton that manages the application colour theme.
/// Generates any palette from a hue angle (0–359°) and a dark/light flag using
/// Slate's HSV saturation/value profile as the template.
///
/// Call ApplyHue(hue, dark) to switch live. Preference is persisted to the database.
/// </summary>
public partial class ThemeManager : Node
{
    [Signal] public delegate void ThemeChangedEventHandler();

    public static ThemeManager Instance { get; private set; }

    // ── palette ───────────────────────────────────────────────────────────────

    public record ThemePalette(
        Color Background,    // app clear colour + carousel bg
        Color NavBar,        // navbar panel
        Color Component,     // inputs, popups
        Color Hover,         // row hover accent
        Color Focus,         // focus ring border
        Color FontColor,     // primary text
        Color FontPlaceholder // placeholder / muted text
    );

    private static readonly Color DarkFont         = H("#e0d7d6");
    private static readonly Color DarkPlaceholder  = H("#b19e9d");
    private static readonly Color LightFont        = H("#1e293b");
    private static readonly Color LightPlaceholder = H("#94a3b8");

    // Rose 500 — delete hover is always a danger signal, independent of theme.
    public static readonly Color DeleteHoverColor = H("#f43f5e");

    // ── runtime state ─────────────────────────────────────────────────────────

    // 4 saturation stops: Greyscale · Muted · Default · Vivid
    public static readonly string[] SatLabels      = { "Greyscale", "Muted", "Default", "Vivid" };
    private static readonly float[] SatMultipliers = { 0.20f, 0.55f, 1.00f, 1.50f };

    public ThemePalette Current    { get; private set; }
    public float        CurrentHue { get; private set; } = 215f;
    public bool         IsDark     { get; private set; } = true;
    public int          CurrentSat { get; private set; } = 2;

    private DatabaseService _db;

    public override void _Ready()
    {
        Instance = this;
        _db      = GetNode<DatabaseService>("/root/DatabaseService");

        float hue  = float.TryParse(_db.Settings.Get("theme_hue",  "215"), out var h)  ? h  : 215f;
        bool  dark = _db.Settings.Get("theme_dark", "true") != "false";
        int   sat  = int.TryParse(_db.Settings.Get("theme_sat",  "2"),   out var s)  ? s  : 2;
        ApplyHue(hue, dark, sat, persist: false);
    }

    /// <summary>
    /// Generate and apply a palette from the given hue (0–359°) and dark/light flag.
    /// Changes are visible immediately. Pass persist:false to skip saving.
    /// </summary>
    public void ApplyHue(float hue, bool dark, int sat = 2, bool persist = true)
    {
        CurrentHue = hue;
        IsDark     = dark;
        CurrentSat = Mathf.Clamp(sat, 0, SatMultipliers.Length - 1);
        float sm   = SatMultipliers[CurrentSat];

        // At exactly hue=215° dark at default saturation, use Slate's authentic Tailwind hex values.
        Current = (dark && Mathf.RoundToInt(hue) == 215 && CurrentSat == 2)
            ? new ThemePalette(H("#1e293b"), H("#0f172a"), H("#334155"), H("#475569"), H("#7c3aed"), DarkFont, DarkPlaceholder)
            : dark ? MakeDark(hue, sm) : MakeLight(hue, sm);

        // 1. App background clear colour
        RenderingServer.SetDefaultClearColor(Current.Background);

        // 2. Mutate shared theme.tres StyleBoxFlat objects in-place
        var theme = ResourceLoader.Load<Theme>("res://theme.tres");

        SetStyleboxColor(theme, "row_hover",    "DndBuilder", Current.Hover);
        SetStyleboxColor(theme, "delete_hover", "DndBuilder", DeleteHoverColor);
        SetInputStylebox(theme, Current.Component, Current.Focus);
        SetFontColors(theme, Current.FontColor, Current.FontPlaceholder);

        // 3. Persist
        if (persist)
        {
            _db.Settings.Set("theme_hue",  hue.ToString("F1"));
            _db.Settings.Set("theme_dark", dark ? "true" : "false");
            _db.Settings.Set("theme_sat",  CurrentSat.ToString());
        }

        // 4. Notify scene nodes that manage their own inline StyleBoxFlat
        EmitSignal(SignalName.ThemeChanged);
    }

    // ── palette builders ──────────────────────────────────────────────────────

    private static ThemePalette MakeDark(float hue, float sm) => new(
        Hsv(hue, S(0.492f, sm), 0.231f),  // Background — Slate 800 profile
        Hsv(hue, S(0.643f, sm), 0.165f),  // NavBar     — Slate 900 profile
        Hsv(hue, S(0.400f, sm), 0.333f),  // Component  — Slate 700 profile
        Hsv(hue, S(0.324f, sm), 0.412f),  // Hover      — Slate 600 profile
        H("#7c3aed"),                       // Focus      — Violet 700
        DarkFont,
        DarkPlaceholder
    );

    private static ThemePalette MakeLight(float hue, float sm) => new(
        Hsv(hue, S(0.10f, sm),  0.97f),   // Background — barely tinted white
        Hsv(hue, S(0.38f, sm),  0.78f),   // NavBar     — more saturated header
        Hsv(hue, S(0.08f, sm),  1.00f),   // Component  — near-white inputs
        Hsv(hue, S(0.28f, sm),  0.88f),   // Hover      — subtle tinted highlight
        H("#7c3aed"),                       // Focus      — Violet 700
        LightFont,
        LightPlaceholder
    );

    // Applies saturation multiplier, clamped to [0, 1].
    private static float S(float baseSat, float multiplier)
        => Mathf.Clamp(baseSat * multiplier, 0f, 1f);

    // ── theme.tres helpers ────────────────────────────────────────────────────

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

        if (theme.GetStylebox("panel", "PopupPanel") is StyleBoxFlat popupSb)
            popupSb.BgColor = bgColor;
    }

    private static void SetFontColors(Theme theme, Color font, Color placeholder)
    {
        theme.SetColor("font_color",             "Label",    font);
        theme.SetColor("font_color",             "LineEdit", font);
        theme.SetColor("font_placeholder_color", "LineEdit", placeholder);
        theme.SetColor("font_color",             "TextEdit", font);
        theme.SetColor("font_placeholder_color", "TextEdit", placeholder);
    }

    // ── colour utilities ──────────────────────────────────────────────────────

    private static Color Hsv(float hue, float sat, float val)
        => Color.FromHsv(hue / 360f, sat, val);

    private static Color H(string hex) => Color.FromHtml(hex);
}
