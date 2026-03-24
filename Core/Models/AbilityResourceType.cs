namespace DndBuilder.Core.Models
{
    public class AbilityResourceType
    {
        public int    Id              { get; set; }
        public int    CampaignId      { get; set; }
        public string Name            { get; set; } = "";
        public string Description     { get; set; } = "";
        public string ResourceKind       { get; set; } = ""; // Uses, Dice, Points, Slots
        public string RecoveryType       { get; set; } = ""; // ShortRest, LongRest, ShortOrLongRest, None
        public string RecoveryAmount     { get; set; } = "All"; // All, Fixed
        public int    RecoveryFixedCount { get; set; } = 0;
        public bool   TracksValueText    { get; set; } = false;
    }
}
