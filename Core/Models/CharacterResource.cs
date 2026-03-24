namespace DndBuilder.Core.Models
{
    public class CharacterResource
    {
        public int    CharacterId    { get; set; }
        public int    ResourceTypeId { get; set; }
        public int    CurrentAmount  { get; set; } = 0;
        public int    MaximumAmount  { get; set; } = 0;
        public string ValueText      { get; set; } = "";
        public string Notes          { get; set; } = "";
    }
}
