using Godot;

public partial class App : Control
{
	// The spot where panels are switched out
	[Export] private BoxContainer _appPanel;
	[Export] private NavBar _navBar;
	[Export] PackedScene _campaignListPanelScene;
	[Export] PackedScene _campaignDashboardScene;

	public override void _Ready()
	{
		_navBar.BackPressed += ShowCampaignList;
		ShowCampaignList();
	}

	private void ShowCampaignList()
	{
		_navBar.ShowBack(false);
		SetMargins(200, 200, 50, 50);
		ClearPanel();
		var panel = _campaignListPanelScene.Instantiate<CampaignListPanel>();
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.DelvePressed += ShowDashboard;
		_appPanel.AddChild(panel);
	}

	private void ShowDashboard(int campaignId)
	{
		_navBar.ShowBack(true);
		SetMargins(0, 0, 0, 0);
		ClearPanel();
		var dashboard = _campaignDashboardScene.Instantiate<CampaignDashboard>();
		dashboard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		dashboard.SizeFlagsVertical = SizeFlags.ExpandFill;
		dashboard.SetCampaign(campaignId);
		_appPanel.AddChild(dashboard);
	}

	private void SetMargins(int left, int right, int top, int bottom)
	{
		var margin = _appPanel.GetParent<MarginContainer>();
		if (margin == null) return;
		margin.AddThemeConstantOverride("margin_left",   left);
		margin.AddThemeConstantOverride("margin_right",  right);
		margin.AddThemeConstantOverride("margin_top",    top);
		margin.AddThemeConstantOverride("margin_bottom", bottom);
	}

	private void ClearPanel()
	{
		foreach (Node child in _appPanel.GetChildren())
			child.QueueFree();
	}
}