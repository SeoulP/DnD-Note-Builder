using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public partial class NavBar : PanelContainer
{
    [Signal] public delegate void BackPressedEventHandler();
    [Signal] public delegate void DatabaseRestoredEventHandler();
    [Signal] public delegate void CampaignDataImportedEventHandler();
    [Signal] public delegate void PanelSwitchedEventHandler(string panel);

    [Export] private Button      _backButton;
    [Export] private Button      _notesButton;
    [Export] private Button      _systemButton;
    [Export] private Button      _trackerButton;
    [Export] private MenuButton  _settingsButton;
    [Export] private PackedScene _importExportModalScene;

    private DatabaseService    _db;
    private int?               _campaignId;
    private ConfirmationDialog _restoreConfirmDialog;
    private ConfirmationDialog _clearLogDialog;
    private string             _pendingRestorePath = "";
    private string             _activePanel        = "notes";

    private StyleBoxFlat _navBarStyle;
    private StyleBoxFlat _panelActiveSb;
    private StyleBoxFlat _panelInactiveSb;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _backButton.Pressed  += () => EmitSignal(SignalName.BackPressed);
        _notesButton.Pressed   += () => SwitchPanel("notes");
        _systemButton.Pressed  += () => SwitchPanel("system");
        _trackerButton.Pressed += () => SwitchPanel("tracker");

        _panelActiveSb   = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.Hover };
        _panelActiveSb.SetCornerRadiusAll(4);
        _panelActiveSb.ContentMarginLeft = _panelActiveSb.ContentMarginRight = 8;
        _panelActiveSb.ContentMarginTop  = _panelActiveSb.ContentMarginBottom = 4;
        _panelInactiveSb = new StyleBoxFlat { BgColor = Colors.Transparent };
        _panelInactiveSb.ContentMarginLeft = _panelInactiveSb.ContentMarginRight = 8;
        _panelInactiveSb.ContentMarginTop  = _panelInactiveSb.ContentMarginBottom = 4;

        UpdatePanelButtons();

        // Restore confirmation
        _restoreConfirmDialog = DialogHelper.Make("Restore Database");
        AddChild(_restoreConfirmDialog);
        _restoreConfirmDialog.Confirmed += DoRestore;

        // Clear log confirmation
        _clearLogDialog = DialogHelper.Make("Clear App Log");
        AddChild(_clearLogDialog);
        _clearLogDialog.Confirmed += DoClearLog;

        // Settings menu
        var popup = _settingsButton.GetPopup();
        popup.AddItem("Backup Database",         0);
        popup.AddItem("Restore Database",        1);
        popup.AddSeparator();
        popup.AddItem("Export Campaign Data...", 2);
        popup.AddItem("Import Campaign Data...", 3);
        popup.AddSeparator();
        popup.HideOnCheckableItemSelection = false;
        popup.AddCheckItem("Remember Tabs",      5);
        popup.SetItemChecked(popup.GetItemIndex(5), _db.Settings.Get("remember_tabs", "false") == "true");
        popup.AddSeparator();
        popup.AddItem("Appearance...",           4);
        popup.AddSeparator();

        // Log level submenu
        var logLevelSubmenu = new PopupMenu { Name = "LogLevelSubmenu" };
        popup.AddChild(logLevelSubmenu);
        logLevelSubmenu.HideOnCheckableItemSelection = false;
        string[] levelNames = { "Trace", "Debug", "Info", "Warning", "Error" };
        for (int i = 0; i < levelNames.Length; i++)
            logLevelSubmenu.AddRadioCheckItem(levelNames[i], 10 + i);
        logLevelSubmenu.SetItemChecked((int)AppLogger.Instance.MinLevel, true);
        logLevelSubmenu.IdPressed += OnLogLevelSelected;
        popup.AddSubmenuItem("Log Level", "LogLevelSubmenu", 6);
        popup.AddItem("Clear App Log",           7);
        popup.SetItemTooltip(0, "Save a .zip containing the database and all images. Backs up all campaigns and their data.");
        popup.SetItemTooltip(1, "Restore from a .zip backup (or legacy .db file). Overwrites all current data.");
        popup.SetItemTooltip(3, "Selectively export NPCs, Locations, Factions, Sessions, Items, and types from this campaign to a .dndx file.");
        popup.SetItemTooltip(4, "Import entities and types from a .dndx file into this campaign.");
        popup.AboutToPopup += () =>
            popup.SetItemDisabled(popup.GetItemIndex(7), !AppLogger.Instance.LogExists());
        popup.SetItemDisabled(popup.GetItemIndex(7), !AppLogger.Instance.LogExists());
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
        bool hasCampaign = campaignId.HasValue;

        _notesButton.Visible   = hasCampaign;
        _systemButton.Visible  = hasCampaign;
        _trackerButton.Visible = hasCampaign;
        if (!hasCampaign)
            _activePanel = "notes";
        UpdatePanelButtons();

        var popup = _settingsButton.GetPopup();
        popup.SetItemDisabled(popup.GetItemIndex(2), !hasCampaign);
        popup.SetItemDisabled(popup.GetItemIndex(3), !hasCampaign);
    }

    private void SwitchPanel(string panel)
    {
        _activePanel = panel;
        UpdatePanelButtons();
        EmitSignal(SignalName.PanelSwitched, panel);
    }

    private void UpdatePanelButtons()
    {
        ApplyPanelButtonStyle(_notesButton,   _activePanel == "notes");
        ApplyPanelButtonStyle(_systemButton,  _activePanel == "system");
        ApplyPanelButtonStyle(_trackerButton, _activePanel == "tracker");
    }

    private void ApplyPanelButtonStyle(Button btn, bool active)
    {
        var sb = active ? _panelActiveSb : _panelInactiveSb;
        var hoverSb = new StyleBoxFlat { BgColor = active ? ThemeManager.Instance.Current.Hover : ThemeManager.Instance.Current.Component };
        hoverSb.SetCornerRadiusAll(4);
        hoverSb.ContentMarginLeft = hoverSb.ContentMarginRight = 8;
        hoverSb.ContentMarginTop  = hoverSb.ContentMarginBottom = 4;
        btn.AddThemeStyleboxOverride("normal",   sb);
        btn.AddThemeStyleboxOverride("hover",    hoverSb);
        btn.AddThemeStyleboxOverride("pressed",  sb);
        btn.AddThemeStyleboxOverride("focus",    sb);
    }

    public void SetActivePanel(string panel)
    {
        _activePanel = panel;
        UpdatePanelButtons();
    }

    // ─── Theme ────────────────────────────────────────────────────────────────

    private void OnThemeChanged()
    {
        _navBarStyle.BgColor     = ThemeManager.Instance.Current.NavBar;
        _panelActiveSb.BgColor   = ThemeManager.Instance.Current.Hover;
        UpdatePanelButtons();
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
            case 5:
                var p    = _settingsButton.GetPopup();
                int idx  = p.GetItemIndex(5);
                bool now = !p.IsItemChecked(idx);
                p.SetItemChecked(idx, now);
                _db.Settings.Set("remember_tabs", now ? "true" : "false");
                break;
            case 7: OpenClearLogDialog(); break;
        }
    }

    private void OnLogLevelSelected(long id)
    {
        var level = (LogLevel)(int)(id - 10);
        AppLogger.Instance.SetMinLevel(level);
        _db.Settings.Set("log_level", level.ToString());

        var submenu = _settingsButton.GetPopup().GetNode<PopupMenu>("LogLevelSubmenu");
        for (int i = 0; i < 5; i++)
            submenu.SetItemChecked(i, i == (int)level);
    }

    private void OpenClearLogDialog()
    {
        DialogHelper.Show(_clearLogDialog, "This will permanently delete the app log. Continue?");
    }

    private void DoClearLog()
    {
        AppLogger.Instance.ClearLog();
        _settingsButton.GetPopup().SetItemDisabled(
            _settingsButton.GetPopup().GetItemIndex(7),
            !AppLogger.Instance.LogExists());
    }

    // ─── Backup / Restore ─────────────────────────────────────────────────────

    private void OpenBackupDialog()
    {
        var win = new Window
        {
            Title            = "Backup",
            InitialPosition  = Window.WindowInitialPosition.CenterMainWindowScreen,
            Unresizable      = true,
            Transient        = true,
            Exclusive        = true,
        };

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 8);

        var margin = new MarginContainer();
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        var includeImages = new CheckBox { Text = "Include Images", ButtonPressed = true };
        margin.AddChild(includeImages);
        vbox.AddChild(margin);
        vbox.AddChild(new HSeparator());

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 8);
        var footerMargin = new MarginContainer();
        footerMargin.AddThemeConstantOverride("margin_left",   12);
        footerMargin.AddThemeConstantOverride("margin_right",  12);
        footerMargin.AddThemeConstantOverride("margin_bottom", 12);
        var cancelBtn = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(80, 0) };
        var spacer    = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var backupBtn = new Button { Text = "Backup", CustomMinimumSize = new Vector2(120, 0) };
        footer.AddChild(cancelBtn);
        footer.AddChild(spacer);
        footer.AddChild(backupBtn);
        footerMargin.AddChild(footer);
        vbox.AddChild(footerMargin);

        win.AddChild(vbox);
        AddChild(win);
        win.PopupCenteredClamped(new Vector2I(320, 120));

        cancelBtn.Pressed    += () => win.QueueFree();
        win.CloseRequested   += () => win.QueueFree();
        backupBtn.Pressed    += () =>
        {
            bool withImages = includeImages.ButtonPressed;
            win.QueueFree();
            DisplayServer.FileDialogShow(
                "Backup",
                OS.GetSystemDir(OS.SystemDir.Documents),
                "campaign_backup.zip",
                false,
                DisplayServer.FileDialogMode.SaveFile,
                new[] { "*.zip ; Backup Archive" },
                Callable.From((bool ok, string[] paths, long _) =>
                {
                    if (!ok || paths.Length == 0) return;
                    _db.Disconnect();
                    try
                    {
                        if (File.Exists(paths[0])) File.Delete(paths[0]);
                        using var zip = ZipFile.Open(paths[0], ZipArchiveMode.Create);
                        zip.CreateEntryFromFile(_db.DbPath, "campaign.db");
                        foreach (var ext in new[] { "-wal", "-shm" })
                        {
                            var sidecar = _db.DbPath + ext;
                            if (File.Exists(sidecar))
                                zip.CreateEntryFromFile(sidecar, "campaign.db" + ext);
                        }
                        if (withImages && Directory.Exists(_db.ImgDir))
                        {
                            foreach (var file in Directory.GetFiles(_db.ImgDir, "*", SearchOption.AllDirectories))
                            {
                                string entryName = "img/" + Path.GetRelativePath(_db.ImgDir, file).Replace('\\', '/');
                                zip.CreateEntryFromFile(file, entryName);
                            }
                        }
                        AppLogger.Instance.Info("Backup", $"Backup saved to {paths[0]}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Instance.Error("Backup", "Backup failed", ex);
                    }
                    finally
                    {
                        _db.Reconnect();
                    }
                }));
        };
    }

    private void OpenRestoreDialog()
    {
        DisplayServer.FileDialogShow(
            "Restore",
            OS.GetSystemDir(OS.SystemDir.Documents),
            "",
            false,
            DisplayServer.FileDialogMode.OpenFile,
            new[] { "*.zip,*.db ; Backup Files" },
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
        string appDir = Path.GetDirectoryName(_db.DbPath);

        try
        {
            AppLogger.Instance.Info("Restore", $"Restoring from {_pendingRestorePath}");
            if (Path.GetExtension(_pendingRestorePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                _db.Disconnect();
                try
                {
                    using var zip = ZipFile.OpenRead(_pendingRestorePath);
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry
                        string dest = Path.Combine(appDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        entry.ExtractToFile(dest, overwrite: true);
                    }
                }
                finally
                {
                    _db.Reconnect();
                }
            }
            else
            {
                // Legacy .db restore
                File.Copy(_pendingRestorePath, _db.DbPath, overwrite: true);
                _db.Reconnect();
            }

            AppLogger.Instance.Info("Restore", "Restore completed");
            EmitSignal(SignalName.DatabaseRestored);
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error("Restore", "Restore failed", ex);
        }
        finally
        {
            _pendingRestorePath = "";
        }
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
        modal.PopupCenteredClamped(new Vector2I(480, 300), 0.85f);
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
        try
        {
            var pkg  = ImportExportService.BuildPackage(_campaignId.Value, sel, _db);
            var json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            AppLogger.Instance.Info("ImportExport", $"Export saved to {path}");
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error("ImportExport", "Export failed", ex);
        }
    }

    private void ShowImportCampaignModal(string path)
    {
        if (!_campaignId.HasValue) return;
        ExportPackage pkg;
        try
        {
            var json = File.ReadAllText(path);
            pkg = JsonSerializer.Deserialize<ExportPackage>(json);
        }
        catch (Exception ex) { AppLogger.Instance.Error("ImportExport", "Failed to read .dndx file", ex); return; }
        if (pkg == null) return;

        var modal = _importExportModalScene.Instantiate<ImportExportModal>();
        GetTree().Root.AddChild(modal);
        modal.SetupImport(pkg, _campaignId.Value, _db);
        modal.Confirmed += sel =>
        {
            ImportExportService.ApplyPackage(_campaignId.Value, pkg, sel, _db);
            EmitSignal(SignalName.CampaignDataImported);
        };
        modal.PopupCenteredClamped(new Vector2I(480, 300), 0.85f);
    }
}
