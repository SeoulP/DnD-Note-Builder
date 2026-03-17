namespace DndBuilder.Core.Models
{
    public class LocationFaction
    {
        public int  LocationId { get; set; }
        public int  FactionId  { get; set; }
        public int? RoleId     { get; set; }  // FK -> LocationFactionRole.Id
    }
}