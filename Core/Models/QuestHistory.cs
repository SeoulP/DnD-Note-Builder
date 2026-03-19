namespace DndBuilder.Core.Models
{
    public class QuestHistory
    {
        public int    Id        { get; set; }
        public int    QuestId   { get; set; }
        public int?   SessionId { get; set; }  // optional — which session this happened in
        public string Note      { get; set; } = "";
    }
}
