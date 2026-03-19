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
    private FileDialog         _fileDialog;
    private ConfirmationDialog _restoreConfirmDialog;
    private string             _pendingRestorePath = "";
    private ExportSelection    _pendingExportSel;

    private enum FileDialogPurpose { Backup, Restore, ExportCampaign, ImportCampaign }
    private FileDialogPurpose _fileDialogPurpose;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _backButton.Pressed += () => EmitSignal(SignalName.BackPressed);

        // File dialog (shared — purpose set before each open)
        _fileDialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem };
        AddChild(_fileDialog);
        _fileDialog.FileSelected += OnFileSelected;

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
        _fileDialogPurpose       = FileDialogPurpose.Backup;
        _fileDialog.FileMode     = FileDialog.FileModeEnum.SaveFile;
        _fileDialog.Title        = "Backup Database";
        _fileDialog.Filters      = new[] { "*.db ; SQLite Database" };
        _fileDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OpenRestoreDialog()
    {
        _fileDialogPurpose       = FileDialogPurpose.Restore;
        _fileDialog.FileMode     = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.Title        = "Restore Database";
        _fileDialog.Filters      = new[] { "*.db ; SQLite Database" };
        _fileDialog.PopupCentered(new Vector2I(900, 600));
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
            _pendingExportSel    = sel;
            _fileDialogPurpose   = FileDialogPurpose.ExportCampaign;
            _fileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            _fileDialog.Title    = "Export Campaign Data";
            _fileDialog.Filters  = new[] { "*.dndx ; DnD Builder Export" };
            _fileDialog.PopupCentered(new Vector2I(900, 600));
        };
        modal.PopupCentered();
    }

    private void OpenImportCampaignDialog()
    {
        if (!_campaignId.HasValue) return;
        _fileDialogPurpose   = FileDialogPurpose.ImportCampaign;
        _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.Title    = "Import Campaign Data";
        _fileDialog.Filters  = new[] { "*.dndx ; DnD Builder Export" };
        _fileDialog.PopupCentered(new Vector2I(900, 600));
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

    // ─── Shared file dialog handler ───────────────────────────────────────────

    private void OnFileSelected(string path)
    {
        switch (_fileDialogPurpose)
        {
            case FileDialogPurpose.Backup:
                File.Copy(_db.DbPath, path, overwrite: true);
                break;

            case FileDialogPurpose.Restore:
                _pendingRestorePath = path;
                DialogHelper.Show(_restoreConfirmDialog, "Restore will replace ALL current data. This cannot be undone. Continue?");
                break;

            case FileDialogPurpose.ExportCampaign:
                WriteExportFile(path, _pendingExportSel);
                break;

            case FileDialogPurpose.ImportCampaign:
                ShowImportCampaignModal(path);
                break;
        }
    }
}
