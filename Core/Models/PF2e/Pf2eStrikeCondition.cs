namespace DndBuilder.Core.Models
{
    public class Pf2eStrikeCondition
    {
        public int Id { get; set; } = 0;
        public int AbilityId { get; set; } = 0;
        public int ConditionTypeId { get; set; } = 0;
        public int ConditionValue { get; set; } = 0;
        public bool IsOnCritOnly { get; set; } = false;
        public int? SaveTypeId { get; set; } = null;
        public int? SaveDc { get; set; } = null;
        public string Notes { get; set; } = "";
    }
}
