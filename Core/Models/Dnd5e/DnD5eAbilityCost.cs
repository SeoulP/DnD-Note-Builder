namespace DndBuilder.Core.Models
{
    public class DnD5eAbilityCost
    {
        public int AbilityId      { get; set; }
        public int ResourceTypeId { get; set; }
        public int Amount         { get; set; } = 1;
    }
}
