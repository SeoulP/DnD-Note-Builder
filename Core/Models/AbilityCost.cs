namespace DndBuilder.Core.Models
{
    public class AbilityCost
    {
        public int AbilityId      { get; set; }
        public int ResourceTypeId { get; set; }
        public int Amount         { get; set; } = 1;
    }
}
