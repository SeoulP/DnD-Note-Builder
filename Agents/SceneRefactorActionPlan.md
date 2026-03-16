# TTRPG Companion App
## Scene Refactor — Action Plan
*March 2026 • v1.0*

---

### Overview

This plan refactors the current scene structure so that `App.tscn` acts as a permanent shell with a single swappable content area, rather than directly owning campaign-specific UI. Each step leaves the app in a working state and is designed to be completed independently.

> Steps are ordered by dependency. Steps 1–2 are safe isolated changes. Steps 3–5 are the main structural move. Steps 6–7 complete the navigation layer.

---

### Step 1 — Fix the CampaignCard NodePath Bug

**Status: Not done — bug confirmed in `campaign_card.tscn`**

In `campaign_card.tscn`, both `_editButton` and `_deleteButton` currently point to `DeleteButton`. Fix the export paths before touching anything structural — this is an isolated, zero-risk change.

**Fix (in `campaign_card.tscn` lines 15–16):**

```
_editButton  = NodePath("FullHBoxContainer/VBoxContainer/ButtonHBoxContainer/EditButton")
_deleteButton = NodePath("FullHBoxContainer/VBoxContainer/ButtonHBoxContainer/DeleteButton")
```

**Verify:** Open the scene in the Godot editor and confirm both `@export` NodePath fields resolve without errors.

---

### Step 2 — Add Signals to CampaignCard

**Status: Already done** — `CampaignCard.cs:6–7` and `_Ready()` wiring are in place.

Both signals are defined and buttons are connected:

```csharp
[Signal] public delegate void EditPressedEventHandler(int campaignId);
[Signal] public delegate void DeletePressedEventHandler(int campaignId);
```

Buttons emit the correct signal in `_Ready()`. No action needed.

---

### Step 3 — Create CampaignListPanel Scene

**Status: Not started**

This is the main structural move. Create a new scene at `res://Scenes/CampaignList/campaign_list_panel.tscn` that absorbs all campaign-list UI currently living in `App.tscn`.

**New scene tree:**

```
CampaignListPanel (VBoxContainer, fullscreen anchors)
├── TopMarginContainer        ← moved from App
├── HBoxContainer             ← moved from App
│   ├── LeftMarginContainer   ← moved from App
│   ├── VBoxContainer         ← moved from App
│   │   ├── ScrollContainer   ← moved from App
│   │   │   └── CampaignList  ← moved from App (keep as instance)
│   │   └── Button (Add Campaign) ← moved from App
│   └── RightMarginContainer  ← moved from App
└── BottomMarginContainer     ← moved from App
```

Also move `AddCampaignModal` here as a child of `CampaignListPanel`. Keep it `visible = false`.

**Update NodePath references:** The `_dialog` and `_addButton` NodePath references in `CampaignList` now resolve locally within the panel instead of reaching up into `App`. Update these paths accordingly.

**Move responsibilities:**
- Add button connection → `CampaignListPanel.cs`
- Modal show/hide logic → `CampaignListPanel.cs`
- Remove `_addCampaignModal` and `_addCampaignButton` fields from `App.cs` entirely

---

### Step 4 — Subscribe to CampaignCard Signals in CampaignList

**Status: Already done** — `CampaignList.cs:35–37` subscribes to both signals. Edit handler is a `GD.Print` stub, which is correct at this stage.

```csharp
card.EditPressed += OnEditPressed;
card.DeletePressed += OnDeletePressed;
```

No action needed until Step 7 replaces the stub.

> Remember to disconnect signals if cards are freed dynamically, to avoid dangling references.

---

### Step 5 — Rebuild App.tscn as a Shell

**Status: Not started**

Strip `App.tscn` down to just permanent chrome. Everything content-related has moved to `CampaignListPanel`.

**Scene tree after:**

```
App (Control, fullscreen anchors)
└── ContentArea (MarginContainer or Control, fullscreen anchors)
```

**Changes to `App.tscn`:**
- Remove all `ext_resource` references except `App.cs`
- Remove `AddCampaignModal` node
- Remove the entire `PanelContainer` subtree
- Add `ContentArea` as the only child

**Changes to `App.cs`:**
- Remove `_addCampaignModal` exported field
- Remove `_addCampaignButton` exported field
- Add `[Export] private Control _contentArea`

> After this step `App.tscn` has two nodes. That is the goal — a boring shell that knows nothing about campaigns.

---

### Step 6 — Implement NavigateTo in App.cs

**Status: Not started**

`App.cs` gets one meaningful method. All panel navigation flows through it.

**Create `BasePanel` base class first** — all content panels will extend it. Create at `Core/BasePanel.cs`:

```csharp
public abstract partial class BasePanel : Control
{
    public virtual void Initialize(Variant data = default) { }
}
```

**`NavigateTo` in `App.cs`:**

```csharp
public void NavigateTo(PackedScene scene, Variant data = default)
{
    foreach (Node child in _contentArea.GetChildren())
        child.QueueFree();

    var panel = scene.Instantiate<BasePanel>();
    _contentArea.AddChild(panel);
    panel.Initialize(data);
}
```

**In `App._Ready()`:**

```csharp
NavigateTo(GD.Load<PackedScene>("res://Scenes/CampaignList/campaign_list_panel.tscn"));
```

---

### Step 7 — Wire CampaignList → App Navigation

**Status: Not started**

Replace the `GD.Print` stub from Step 4 with real navigation. `CampaignList` emits upward rather than calling `App` directly.

**Add signal to `CampaignList.cs`:**

```csharp
[Signal] public delegate void CampaignSelectedEventHandler(int campaignId);
```

**Emit from the handler:**

```csharp
private void OnCampaignEditPressed(int id) => EmitSignal(SignalName.CampaignSelected, id);
```

**Connect in `CampaignListPanel.cs` — two options:**

- **Direct reference:** `GetTree().Root.GetNode<App>("App").NavigateTo(...)` — simple, works for now
- **Autoload Router singleton:** `Router.NavigateTo(...)` — cleaner long-term, panels stay fully decoupled

> The Router singleton is the recommended path once a second panel type exists. Either approach works for Step 7.

---

### Summary — What Moves Where

| Item | Before | After |
|---|---|---|
| `AddCampaignModal` | Child of App | Child of CampaignListPanel |
| Add Campaign Button | Child of App layout | Child of CampaignListPanel |
| All margin containers | Child of App | Child of CampaignListPanel |
| ScrollContainer + CampaignList | Child of App | Child of CampaignListPanel |
| `_addCampaignModal` field | On `App.cs` | On `CampaignListPanel.cs` |
| `_addCampaignButton` field | On `App.cs` | On `CampaignListPanel.cs` |
| `CampaignCardScene` export | On `CampaignList` | Unchanged |
| Edit/Delete logic | Implicit / missing | Signals on `CampaignCard` |

---

### End State

After Step 7 the architecture looks like this:

```
App.tscn                  ← 2 nodes, knows nothing about campaigns
└── ContentArea           ← swap target

CampaignListPanel         ← self-contained, owns its modal and button
└── CampaignList          ← data-driven, emits signals only
    └── CampaignCard      ← announces EditPressed / DeletePressed
```

Adding a `CampaignPanel` or `CharacterSheetPanel` in future phases requires: create the scene, extend `BasePanel`, call `NavigateTo`. Nothing in `App` changes.
