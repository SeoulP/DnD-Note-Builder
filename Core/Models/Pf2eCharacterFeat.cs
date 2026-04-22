namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterFeat
    {
        public int Id { get; set; } = 0;
        public int CharacterId { get; set; } = 0;
        public int FeatId { get; set; } = 0;
        public int LevelTaken { get; set; } = 1;
    }
}
