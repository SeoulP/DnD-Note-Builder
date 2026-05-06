namespace DndBuilder.Core.Models
{
    public class Pf2eAncestryAbilityBoost
    {
        public int Id { get; set; } = 0;
        public int AncestryId { get; set; } = 0;
        public int AbilityScoreId { get; set; } = 0;
        public bool IsFlaw { get; set; } = false;
    }
}
