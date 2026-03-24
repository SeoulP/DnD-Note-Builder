namespace DndBuilder.Core.Models
{
    public class AbilityChoice
    {
        public int    Id              { get; set; }
        public int    AbilityId       { get; set; }
        public string Name            { get; set; } = "";
        public string Description     { get; set; } = "";
        public int    SortOrder       { get; set; } = 0;
        public int?   LinkedAbilityId { get; set; } = null;
    }
}
