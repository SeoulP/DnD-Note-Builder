using DndBuilder.Core.Models;
using Godot;

public partial class CampaignCard : PanelContainer
{
	[Signal] public delegate void EditPressedEventHandler(int campaignId);
	[Signal] public delegate void DeletePressedEventHandler(int campaignId);
	[Signal] public delegate void DelvePressedEventHandler(int campaignId);

	[Export] private Label  _nameLabel;
	[Export] private Label  _systemLabel;
	[Export] private Label  _descLabel;
	[Export] private Label  _dateLabel;
	[Export] private Button _editButton;
	[Export] private Button _deleteButton;
	[Export] private Button _delveButton;

	private int _campaignId;

	public override void _Ready()
	{
		_editButton.Pressed  += () => EmitSignalEditPressed(_campaignId);
		_deleteButton.Pressed += () => EmitSignalDeletePressed(_campaignId);
		_delveButton.Pressed  += () => EmitSignalDelvePressed(_campaignId);
	}

	public void Populate(Campaign campaign)
	{
		_campaignId      = campaign.Id;
		_nameLabel.Text  = campaign.Name;
		_systemLabel.Text = campaign.SystemLabel;
		_dateLabel.Text  = campaign.DateStarted;

		// Truncate description preview to ~80 chars
		var desc = campaign.Description ?? "";
		_descLabel.Text = desc.Length > 80 ? desc[..80] + "…" : desc;
	}
}
