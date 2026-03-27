namespace DndBuilder.Core.Models
{
    public class AbilityUsageProgression
    {
        public int Id            { get; set; }
        public int AbilityId     { get; set; }
        public int RequiredLevel { get; set; }
        public int Usages        { get; set; }
    }
}
