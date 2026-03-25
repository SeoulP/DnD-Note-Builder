using System;
using System.Collections.Generic;
using DndBuilder.Core.Models;
using Godot;

public partial class BackgroundPickerModal : Window
{
    private DatabaseService             _db;
    private int                         _campaignId;
    private int?                        _selectedId;
    private List<DnD5eBackground>       _backgrounds = new();

    public event Action<int?> Confirmed;

    [Export] private VBoxContainer _listContainer;
    [Export] private Label         _detailName;
    [Export] private Label         _detailSkills;
    [Export] private Control       _detailSkillsRow;
    [Export] private Label         _detailDesc;
    [Export] private Button        _confirmButton;
    [Export] private Button        _cancelButton;

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        CloseRequested         += OnCancel;
        _cancelButton.Pressed  += OnCancel;
        _confirmButton.Pressed += OnConfirm;
    }

    public void Open(int campaignId, int? currentBackgroundId)
    {
        _campaignId = campaignId;
        _selectedId = currentBackgroundId;
        _backgrounds = _db.DnD5eBackgrounds.GetAll(campaignId);

        BuildList();
        PopulateDetail(_selectedId.HasValue ? _backgrounds.Find(b => b.Id == _selectedId.Value) : null);
        PopupCentered();
    }

    private void BuildList()
    {
        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree();

        foreach (var bg in _backgrounds)
        {
            var captured = bg;
            var btn = new Button
            {
                Text                = bg.Name,
                ToggleMode          = true,
                ButtonPressed       = _selectedId == bg.Id,
                Alignment           = HorizontalAlignment.Left,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Flat                = true,
            };
            btn.Pressed += () =>
            {
                _selectedId = captured.Id;
                RefreshListSelection();
                PopulateDetail(captured);
            };
            _listContainer.AddChild(btn);
        }
    }

    private void RefreshListSelection()
    {
        var children = _listContainer.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is Button btn)
                btn.ButtonPressed = _backgrounds.Count > i && _backgrounds[i].Id == _selectedId;
        }
    }

    private void PopulateDetail(DnD5eBackground bg)
    {
        if (bg == null)
        {
            _detailName.Text          = "Select a background";
            _detailDesc.Text          = "";
            _detailSkillsRow.Visible  = false;
            return;
        }

        _detailName.Text         = bg.Name;
        _detailDesc.Text         = bg.Description;
        _detailSkillsRow.Visible = true;

        if (!string.IsNullOrEmpty(bg.SkillNames))
            _detailSkills.Text = bg.SkillNames.Replace(",", ", ");
        else
            _detailSkills.Text = $"{bg.SkillCount} of your choice";
    }

    private void OnConfirm()
    {
        Confirmed?.Invoke(_selectedId);
        Hide();
    }

    private void OnCancel() => Hide();
}
