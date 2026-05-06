namespace DndBuilder.Core.Models
{
    public class Pf2eCreature
    {
        public int Id { get; set; } = 0;
        public int CampaignId { get; set; } = 0;
        public string Name { get; set; } = "";
        public int CreatureTypeId { get; set; } = 0;
        public int Level { get; set; } = 0;
        public int SizeId { get; set; } = 0;
        public int StrMod { get; set; } = 0;
        public int DexMod { get; set; } = 0;
        public int ConMod { get; set; } = 0;
        public int IntMod { get; set; } = 0;
        public int WisMod { get; set; } = 0;
        public int ChaMod { get; set; } = 0;
        public int Ac { get; set; } = 0;
        public int MaxHp { get; set; } = 0;
        public int Fortitude { get; set; } = 0;
        public int Reflex { get; set; } = 0;
        public int Will { get; set; } = 0;
        public int Perception { get; set; } = 0;
        public string Source { get; set; } = "";
        public int? SourcePage { get; set; } = null;
        public string Notes        { get; set; } = "";
        public string Description  { get; set; } = "";
    }
}
