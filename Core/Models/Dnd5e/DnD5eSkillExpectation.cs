namespace DndBuilder.Core.Models
{
    public class DnD5eSkillExpectation
    {
        public string Source        { get; set; } = "";  // "class" | "background" | "feat"
        public int    SourceId      { get; set; }
        public string SourceName    { get; set; } = "";
        public int    ExpectedCount { get; set; }
    }
}
