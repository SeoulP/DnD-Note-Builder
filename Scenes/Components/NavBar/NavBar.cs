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
        popup.SetItemTooltip(0, "Copy the entire database file to a location you choose. Backs up all campaigns and their data.");
        popup.SetItemTooltip(1, "Replace the entire database with a previously backed-up file. Overwrites all current data.");
        // index 2 is the separator
        popup.SetItemTooltip(3, "Selectively export NPCs, Locations, Factions, Sessions, Items, and types from this campaign to a .dndx file.");
        popup.SetItemTooltip(4, "Import entities and types from a .dndx file into this campaign.");
        popup.IdPressed += OnMenuItemPressed;
    }

    public void ShowBack(bool show) => _backButton.Visible = show;

    public void SetCampaign(int? campaignId)
    {
        _campaignId = campaignId;
        var popup = _settingsButton.GetPopup();
        popup.SetItemDisabled(popup.GetItemIndex(2), !campaignId.HasValue);
        popup.SetItemDisabled(popup.GetItemIndex(3), !campaignId.HasValue);
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
