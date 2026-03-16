using Godot;

public partial class App : Control
{
	// The spot where panels are switched out
	[Export] private BoxContainer _appPanel;
	[Export] PackedScene _campaignListPanelScene;
	[Export] PackedScene _campaignDashboardScene;

	public override void _Ready()
	{
		ShowCampaignList();
	}

	private void ShowCampaignList()
	{
		ClearPanel();
		var panel = _campaignListPanelScene.Instantiate<CampaignListPanel>();
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.DelvePressed += ShowDashboard;
		_appPanel.AddChild(panel);
	}

	private void ShowDashboard(int campaignId)
	{
		ClearPanel();
		var dashboard = _campaignDashboardScene.Instantiate<CampaignDashboard>();
		dashboard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		dashboard.SizeFlagsVertical = SizeFlags.ExpandFill;
		dashboard.SetCampaign(campaignId);
		_appPanel.AddChild(dashboard);
	}

	private void ClearPanel()
	{
		foreach (Node child in _appPanel.GetChildren())
			child.QueueFree();
	}
}