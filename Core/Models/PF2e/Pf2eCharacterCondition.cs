namespace DndBuilder.Core.Models
{
    public class Pf2eCharacterCondition
    {
        public int Id { get; set; } = 0;
        public int CharacterId { get; set; } = 0;
        public int ConditionTypeId { get; set; } = 0;
        public int ConditionValue { get; set; } = 0;
    }
}
