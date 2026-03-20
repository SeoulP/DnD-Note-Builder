using System;
using System.IO;
using System.Text.Json;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public partial class NavBar : PanelContainer
{
    [Signal] public delegate void BackPressedEventHandler();
    [Signal] public delegate void DatabaseRestoredEventHandler();
    [Signal] public delegate void CampaignDataImportedEventHandler();

    [Export] private Button      _backButton;
    [Export] private MenuButton  _settingsButton;
    [Export] private PackedScene _importExportModalScene;

    private DatabaseService    _db;
    private int?               _campaignId;
    private ConfirmationDialog _restoreConfirmDialog;
    private string             _pendingRestorePath = "";

    private StyleBoxFlat _navBarStyle;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _backButton.Pressed += () => EmitSignal(SignalName.BackPressed);

        // Restore confirmation
        _restoreConfirmDialog = DialogHelper.Make("Restore Database");
        AddChild(_restoreConfirmDialog);
        _restoreConfirmDialog.Confirmed += DoRestore;

        // Settings menu
        var popup = _settingsButton.GetPopup();
        popup.AddItem("Backup Database",         0);
        popup.AddItem("Restore Database",        1);
        popup.AddSeparator();
        popup.AddItem("Export Campaign Data...", 2);
        popup.AddItem("Import Campaign Data...", 3);
        popup.AddSeparator();
        popup.AddItem("Appearance...",           4);
        popup.SetItemTooltip(0, "Copy the entire database file to a location you choose. Backs up all campaigns and their data.");
        popup.SetItemTooltip(1, "Replace the entire database with a previously backed-up file. Overwrites all current data.");
        popup.SetItemTooltip(3, "Selectively export NPCs, Locations, Factions, Sessions, Items, and types from this campaign to a .dndx file.");
        popup.SetItemTooltip(4, "Import entities and types from a .dndx file into this campaign.");
        popup.IdPressed += OnMenuItemPressed;

        // Navbar background — driven by ThemeManager so it updates live
        _navBarStyle = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.NavBar };
        AddThemeStyleboxOverride("panel", _navBarStyle);
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    }

    public void ShowBack(bool show) => _backButton.Visible = show;

    public void SetCampaign(int? campaignId)
    {
        _campaignId = campaignId;
        var popup = _settingsButton.GetPopup();
        popup.SetItemDisabled(popup.GetItemIndex(2), !campaignId.HasValue);
        popup.SetItemDisabled(popup.GetItemIndex(3), !campaignId.HasValue);
    }

    // ─── Theme ────────────────────────────────────────────────────────────────

    private void OnThemeChanged()
    {
        _navBarStyle.BgColor = ThemeManager.Instance.Current.NavBar;
    }

    private void OpenAppearancePopup()
    {
        var tm = ThemeManager.Instance;

        // ── popup shell ──────────────────────────────────────────────────────
        var panel = new PopupPanel();
        var outer = new VBoxContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("separation", 10);

        // inline padding via a MarginContainer
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 10);
        margin.AddChild(inner);
        outer.AddChild(margin);
        panel.AddChild(outer);

        // ── dark / light toggle ───────────────────────────────────────────────
        var modeRow = new HBoxContainer();
        var modeLabel = new Label { Text = "Dark Mode" };
        modeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var modeToggle = new CheckButton { ButtonPressed = tm.IsDark };
        modeRow.AddChild(modeLabel);
        modeRow.AddChild(modeToggle);
        inner.AddChild(modeRow);

        // ── hue slider ────────────────────────────────────────────────────────
        var hueRow = new HBoxContainer();
        hueRow.AddThemeConstantOverride("separation", 8);

        var hueLabel = new Label { Text = "Hue" };
        hueLabel.CustomMinimumSize = new Vector2(32, 0);

        var slider = new HSlider
        {
            MinValue            = 0,
            MaxValue            = 359,
            Step                = 1,
            Value               = tm.CurrentHue,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var hueValue = new Label { Text = $"{tm.CurrentHue:F0}°" };
        hueValue.CustomMinimumSize = new Vector2(40, 0);

        hueRow.AddChild(hueLabel);
        hueRow.AddChild(slider);
        hueRow.AddChild(hueValue);
        inner.AddChild(hueRow);

        // ── saturation slider ─────────────────────────────────────────────────
        var satRow = new HBoxContainer();
        satRow.AddThemeConstantOverride("separation", 8);

        var satLabel = new Label { Text = "Saturation" };
        satLabel.CustomMinimumSize = new Vector2(80, 0);

        var satSlider = new HSlider
        {
            MinValue            = 0,
            MaxValue            = ThemeManager.SatLabels.Length - 1,
            Step                = 1,
            Value               = tm.CurrentSat,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var satValue = new Label { Text = ThemeManager.SatLabels[tm.CurrentSat] };
        satValue.CustomMinimumSize = new Vector2(64, 0);

        satRow.AddChild(satLabel);
        satRow.AddChild(satSlider);
        satRow.AddChild(satValue);
        inner.AddChild(satRow);

        // ── wire live preview ─────────────────────────────────────────────────
        slider.ValueChanged += val =>
        {
            hueValue.Text = $"{val:F0}°";
            tm.ApplyHue((float)val, modeToggle.ButtonPressed, (int)satSlider.Value);
        };
        modeToggle.Toggled += on => tm.ApplyHue((float)slider.Value, on, (int)satSlider.Value);
        satSlider.ValueChanged += val =>
        {
            satValue.Text = ThemeManager.SatLabels[(int)val];
            tm.ApplyHue((float)slider.Value, modeToggle.ButtonPressed, (int)val);
        };

        // ── show popup below the settings button ──────────────────────────────
        AddChild(panel);
        var btnRect = _settingsButton.GetGlobalRect();
        panel.Popup(new Rect2I(
            (int)btnRect.Position.X,
            (int)(btnRect.Position.Y + btnRect.Size.Y),
            280, 145));

        panel.PopupHide += () => panel.QueueFree();
    }

    // ─── Menu dispatch ────────────────────────────────────────────────────────

    private void OnMenuItemPressed(long id)
    {
        switch (id)
        {
            case 0: OpenBackupDialog();         break;
            case 1: OpenRestoreDialog();        break;
            case 2: OpenExportCampaignModal();  break;
            case 3: OpenImportCampaignDialog(); break;
            case 4: OpenAppearancePopup();      break;
        }
    }

    // ─── Backup / Restore ─────────────────────────────────────────────────────

    private void OpenBackupDialog()
    {
        DisplayServer.FileDialogShow(
            "Backup Database",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "campaign_backup.db",
            false,
            DisplayServer.FileDialogMode.SaveFile,
            new[] { "*.db ; SQLite Database" },
            Callable.From((bool ok, string[] paths, long _) =>
            {
                if (ok && paths.Length > 0)
                    File.Copy(_db.DbPath, paths[0], overwrite: true);
            }));
    }

    private void OpenRestoreDialog()
    {
        DisplayServer.FileDialogShow(
            "Restore Database",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            new[] { "*.db ; SQLite Database" },
            Callable.From((bool ok, string[] paths, long _) =>
            {
                if (!ok || paths.Length == 0) return;
                _pendingRestorePath = paths[0];
                DialogHelper.Show(_restoreConfirmDialog, "Restore will replace ALL current data. This cannot be undone. Continue?");
            }));
    }

    private void DoRestore()
    {
        if (string.IsNullOrEmpty(_pendingRestorePath)) return;
        File.Copy(_pendingRestorePath, _db.DbPath, overwrite: true);
        _db.Reconnect();
        EmitSignal(SignalName.DatabaseRestored);
        _pendingRestorePath = "";
    }

    // ─── Export / Import campaign data ────────────────────────────────────────

    private void OpenExportCampaignModal()
    {
        if (!_campaignId.HasValue) return;
        var modal = _importExportModalScene.Instantiate<ImportExportModal>();
        GetTree().Root.AddChild(modal);
        modal.SetupExport(_campaignId.Value, _db);
        modal.Confirmed += sel =>
        {
            DisplayServer.FileDialogShow(
                "Export Campaign Data",
                OS.GetSystemDir(OS.SystemDir.Documents),
                "campaign_export.dndx",
                false,
                DisplayServer.FileDialogMode.SaveFile,
                new[] { "*.dndx ; DnD Builder Export" },
                Callable.From((bool ok, string[] paths, long _) =>
                {
                    if (ok && paths.Length > 0)
                        WriteExportFile(paths[0], sel);
                }));
        };
        modal.PopupCentered();
    }

    private void OpenImportCampaignDialog()
    {
        if (!_campaignId.HasValue) return;
        DisplayServer.FileDialogShow(
            "Import Campaign Data",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            new[] { "*.dndx ; DnD Builder Export" },
            Callable.From((bool ok, string[] paths, long _) =>
            {
                if (ok && paths.Length > 0)
                    ShowImportCampaignModal(paths[0]);
            }));
    }

    private void WriteExportFile(string path, ExportSelection sel)
    {
        var pkg  = ImportExportService.BuildPackage(_campaignId.Value, sel, _db);
        var json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void ShowImportCampaignModal(string path)
    {
        if (!_campaignId.HasValue) return;
        var json = File.ReadAllText(path);
        ExportPackage pkg;
        try { pkg = JsonSerializer.Deserialize<ExportPackage>(json); }
        catch { GD.PrintErr("ImportExportModal: failed to parse .dndx file"); return; }
        if (pkg == null) return;

        var modal = _importExportModalScene.Instantiate<ImportExportModal>();
        GetTree().Root.AddChild(modal);
        modal.SetupImport(pkg, _campaignId.Value, _db);
        modal.Confirmed += sel =>
        {
            ImportExportService.ApplyPackage(_campaignId.Value, pkg, sel, _db);
            EmitSignal(SignalName.CampaignDataImported);
        };
        modal.PopupCentered();
    }
}
