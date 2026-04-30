namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterStrikeTrait
    {
        public int Id { get; set; } = 0;
        public int StrikeId { get; set; } = 0;
        public int TraitTypeId { get; set; } = 0;
        public int? TraitParameterDieId { get; set; } = null;
        public int? TraitParameterDamageId { get; set; } = null;
    }
}
