using Godot;

public partial class NavBar : PanelContainer
{
	[Signal] public delegate void BackPressedEventHandler();

	[Export] private Button _backButton;

	public override void _Ready()
	{
		_backButton.Pressed += () => EmitSignal(SignalName.BackPressed);
	}

	public void ShowBack(bool show)
	{
		_backButton.Visible = show;
	}
}