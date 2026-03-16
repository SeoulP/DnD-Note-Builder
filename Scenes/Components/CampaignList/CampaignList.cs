using Godot;

public partial class CampaignList : VBoxContainer
{
	[Signal] public delegate void EditPressedEventHandler(int campaignId);
	[Signal] public delegate void DelvePressedEventHandler(int campaignId);
	[Export] private PackedScene _campaignCardScene { get; set; }
	[Export] private VBoxContainer _cardsContainer { get; set; }

	public override void _Ready()
	{
		LoadCampaigns();
	}

	public void LoadCampaigns()
	{
		foreach (Node child in _cardsContainer.GetChildren())
			child.QueueFree();

		var db = GetNode<DatabaseService>("/root/DatabaseService");
		var campaigns = db.Campaigns.GetAll();

		if (campaigns.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No campaigns yet. Create one to get started.";
			_cardsContainer.AddChild(empty);
			return;
		}

		GD.Print($"Loaded {campaigns.Count} campaigns");
		foreach (var campaign in campaigns)
		{
			var card = _campaignCardScene.Instantiate<CampaignCard>();
			_cardsContainer.AddChild(card);
			card.Populate(campaign);
			card.EditPressed   += OnEditPressed;
			card.DeletePressed += OnDeletePressed;
			card.DelvePressed  += OnDelvePressed;
		}
	}

	private void OnEditPressed(int campaignId)
	{
		EmitSignal(SignalName.EditPressed, campaignId);
	}

	private void OnDelvePressed(int campaignId)
	{
		EmitSignal(SignalName.DelvePressed, campaignId);
	}
	
	private void OnDeletePressed(int campaignId)
	{
		// Show confirmation dialog
		var dialog = new ConfirmationDialog();
		dialog.DialogText = "Are you sure you want to delete this campaign? This cannot be undone.";
		dialog.Confirmed += () =>
		{
			var db = GetNode<DatabaseService>("/root/DatabaseService");
			db.Campaigns.Delete(campaignId);
			LoadCampaigns();
		};
		AddChild(dialog);
		dialog.PopupCentered();
	}
}