using Godot;

public partial class CampaignListPanel : VBoxContainer
{
	[Signal] public delegate void DelvePressedEventHandler(int campaignId);

	[Export] private PackedScene _addCampaignModalScene;
	[Export] private Button _addCampaignButton;
	[Export] private CampaignList _campaignList;

	private AddCampaignModal _addCampaignModal;
	
	private DatabaseService _databaseService;

	public override void _Ready()
	{
		_addCampaignModal = _addCampaignModalScene.Instantiate<AddCampaignModal>();
		AddChild(_addCampaignModal);
		_addCampaignModal.Hide();

		_addCampaignModal.CampaignCreated += _ => _campaignList.LoadCampaigns();
		_addCampaignModal.CampaignEdited += _ => _campaignList.LoadCampaigns();
		_addCampaignButton.Pressed += () => _addCampaignModal.OpenForNew();
		
		_campaignList.EditPressed  += OnCampaignEdited;
		_campaignList.DelvePressed += (id) => EmitSignal(SignalName.DelvePressed, id);
		
		_databaseService = GetNode<DatabaseService>("/root/DatabaseService");
	}

	private void OnCampaignCreated(int newId)
	{
		_campaignList.LoadCampaigns();
	}

	private void OnCampaignEdited(int id)
	{
		var campaign = _databaseService.Campaigns.Get(id);
		if (campaign == null) return;
		
		_addCampaignModal.OpenForEdit(campaign);
	}
}
