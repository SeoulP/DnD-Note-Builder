namespace DndBuilder.Core.Models
{
    public class DnD5eAbilityChoiceProgression
    {
        public int Id            { get; set; }
        public int AbilityId     { get; set; }
        public int RequiredLevel { get; set; }
        public int ChoiceCount   { get; set; }
    }
}
