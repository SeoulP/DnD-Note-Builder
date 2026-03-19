using Godot;

public partial class App : Control
{
	// The spot where panels are switched out
	[Export] private BoxContainer _appPanel;
	[Export] private NavBar _navBar;
	[Export] PackedScene _campaignListPanelScene;
	[Export] PackedScene _campaignDashboardScene;

	private CampaignDashboard _currentDashboard;

	public override void _Ready()
	{
		_navBar.BackPressed           += ShowCampaignList;
		_navBar.DatabaseRestored      += ShowCampaignList;
		_navBar.CampaignDataImported  += () => _currentDashboard?.ReloadSidebar();
		ShowCampaignList();
	}

	private void ShowCampaignList()
	{
		_currentDashboard = null;
		_navBar.ShowBack(false);
		_navBar.SetCampaign(null);
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
		_navBar.SetCampaign(campaignId);
		SetMargins(0, 0, 0, 0);
		ClearPanel();
		_currentDashboard = _campaignDashboardScene.Instantiate<CampaignDashboard>();
		_currentDashboard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_currentDashboard.SizeFlagsVertical   = SizeFlags.ExpandFill;
		_currentDashboard.SetCampaign(campaignId);
		_appPanel.AddChild(_currentDashboard);
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