namespace DndBuilder.Core.Models
{
    public class NpcFaction
    {
        public int  NpcId     { get; set; }
        public int  FactionId { get; set; }
        public int? RoleId    { get; set; }  // FK -> NpcFactionRole.Id
    }
}
