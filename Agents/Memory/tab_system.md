---
name: Tab system
description: How the detail pane tab system works — navigation priority, pin/close, drag reorder, save/restore
type: project
---

The tab system lives entirely in `Scenes/Panels/CampaignDashboard/CampaignDashboard.cs`. There is no separate tab component scene — tabs are built programmatically.

## Key Types

**`TabEntry`** (inner class on `CampaignDashboard`):
- `EntityType` (string), `EntityId` (int) — what the tab points to
- `IsPinned` (bool)
- `Widget` (Control) — the rendered tab button in the tab bar
- `StyleRef` (StyleBoxFlat) — mutated in-place on theme change (do not replace, mutate)
- `ActionBtn` (Button) — combined pin/close button on the tab widget

## Navigation Priority

When navigating to an entity (e.g. clicking a sidebar item or a WikiLink):

1. **Existing tab** — if the entity already has an open tab, switch to it. Done.
2. **Current unpinned tab** — if the active tab is not pinned, reuse it.
3. **Next unpinned tab** — find any unpinned tab and reuse it.
4. **New tab** — if all tabs are pinned (or no tabs exist), create a new one.

Pinned tabs are **never** reused by navigation. Only the explicit `×` close or right-click menu can close them.

## Tab Bar

- Rendered inside a `HBoxContainer` (`TabRow`) inside a `ScrollContainer` (`TabScroll`)
- `horizontal_scroll_mode = 3` hides the scrollbar — tabs are scrollable but the bar is invisible
- `+` button (`BuildAddTabWidget`) sits at the end of the tab list as a mini-tab; always creates a new tab
- Middle-click on a tab closes it (handled in `_Input`)
- Hover shows `×` on the `ActionBtn`; when pinned, shows `📌` instead

## Right-Click Context Menu

`PopupMenu` shown on right-click of any tab widget. Options:
- Close
- Close All
- Close All to Right
- Pin / Unpin (toggle label based on current state)

## Drag-to-Reorder

- `_draggingTab` (TabEntry?) tracks the in-progress drag
- A ghost `Control` (`_dragGhost`, `ZIndex=100`) is reparented to the root viewport during drag so it renders above everything
- On `MouseMotion` during drag: ghost follows cursor; tab order is updated by comparing cursor X to tab midpoints
- On `MouseUp`: ghost freed, `_draggingTab` cleared, tab bar redrawn

## Save / Restore

`SaveTabs()` serializes the full tab list to JSON (type, id, pinned, active index) and writes to `app_settings` key `"tabs_{campaignId}"` via `SettingsRepository`.

`RestoreTabs()` is called at the end of `_Ready()` after the campaign is loaded. Reads the JSON, recreates each tab in order, then activates the saved active index.

Controlled by the "Remember Tabs" checkable menu item in Settings (key: `"remember_tabs"`). When off, `SaveTabs()` still runs but `RestoreTabs()` skips. This means toggling it on will restore from the last saved state.

## Theme Updates

All tab `StyleBoxFlat` instances are stored in `TabEntry.StyleRef`. `OnTabThemeChanged` iterates all entries and mutates `StyleRef.BgColor` in-place — never replaces the StyleBox object. Godot propagates `Resource.changed` automatically to the Control, so no manual `queue_redraw` is needed.

## Adding a New Entity Type

If you add a new entity type that should be openable in a tab:
1. Add a case to `InstantiatePane(string entityType, int id)` returning the correct detail pane scene instance
2. Add a case to `GetEntityColor(string entityType)` returning the tab accent color
3. Add a case to `OnNameChanged` to update the tab label when the entity's name changes
4. Add a case to `OnEntityDeleted` to close the tab when the entity is deleted
