namespace DndBuilder.Core.Models
{
    public class DnD5eItemMechanics
    {
        public int Id           { get; set; }
        public int SystemItemId { get; set; }  // FK -> SystemItem.Id
        // Full field list defined in Phase 2. Expected fields:
        // public string Rarity             { get; set; } = "";
        // public bool   RequiresAttunement { get; set; } = false;
        // public string DamageDice         { get; set; } = "";
        // public string DamageType         { get; set; } = "";
        // public float  Weight             { get; set; } = 0;
        // public int    BonusModifier      { get; set; } = 0;
        // public string Properties         { get; set; } = "";
    }
}