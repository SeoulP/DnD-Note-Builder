namespace DndBuilder.Core.Models
{
    public class ClassLevel
    {
        public int    Id        { get; set; }
        public int    ClassId   { get; set; }
        public int    Level     { get; set; }
        public string Features  { get; set; } = "";
        public string ClassData { get; set; } = "";

        public int ProfBonus { get; set; } = 2;
    }
}
