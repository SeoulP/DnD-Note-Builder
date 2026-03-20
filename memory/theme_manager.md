---
name: ThemeManager system
description: How the runtime colour theming system works — palettes, persistence, which nodes need manual updates
type: project
---

`Core/ThemeManager.cs` is an autoloaded singleton (registered after DatabaseService in project.godot).

**Palette structure:** Each `ThemePalette` has Background (800), NavBar (900), Component (700), Hover (accent 400), Focus (accent 600). 18 palettes: Slate, Red, Orange, Amber, Yellow, Lime, Green, Emerald, Teal, Cyan, Sky, Blue, Indigo, Violet, Purple, Fuchsia, Pink, Rose.

**Why:** User wanted live Tailwind-based colour switching from a Settings → Theme submenu.

**How `ApplyTheme` works:**
1. `RenderingServer.SetDefaultClearColor(palette.Background)` — app clear colour
2. `ResourceLoader.Load<Theme>("res://theme.tres")` — gets cached theme, mutates StyleBoxFlat sub-resources in-place (row_hover, delete_hover, LineEdit/focus+normal, PopupPanel/panel) — Godot propagates Resource.changed to all controls automatically
3. Emits `ThemeChanged(string name)` signal for nodes that manage their own inline StyleBoxFlat

**Nodes that need manual updates (connect to ThemeChanged):**
- `NavBar.cs` — updates `_navBarStyle.BgColor = Current.NavBar`
- `ImageCarousel.cs` — updates `_bgStyle.BgColor = Current.Background`

**Nodes that update automatically** (read from theme.tres): EntityRow, TypeOptionButton, all LineEdit/TextEdit/Label/PopupPanel nodes.

**Persistence:** `SettingsRepository` — `app_settings (key TEXT PK, value TEXT)` table, no CampaignId (global). Key: `"theme"`. Accessed via `_db.Settings.Get/Set`.

**Delete hover is always Rose 500 (#f43f5e)** regardless of theme — signals danger.

**How to apply:** `ThemeManager.Instance.ApplyTheme("Blue")` from anywhere.
