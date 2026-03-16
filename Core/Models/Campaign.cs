namespace DndBuilder.Core.Models;

public class Campaign
{
	public int    Id          { get; set; }
	public string Name        { get; set; }
	public string System      { get; set; }
	public string Description { get; set; }
	public string DateStarted { get; set; }

	// Human-readable system name for display
	public string SystemLabel => System switch
	{
		"dnd5e_2024"   => "D&D 5.5e (2024)",
		"pathfinder2e" => "Pathfinder 2e",
		_              => System
	};
}