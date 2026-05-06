namespace DndBuilder.Core.Models
{
    public class QuestReward
    {
        public int    Id          { get; set; }
        public int    QuestId     { get; set; }
        public int    SortOrder   { get; set; }
        public string Description { get; set; } = "";
        public bool   IsClaimed   { get; set; }
    }
}
