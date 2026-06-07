namespace AgenticBrowserAI.Models
{
    public class UserPromptInput
    {
        public bool IsTask { get; set; }
        public string Query { get; set; }
        public string Rules { get; set; }
        public string Data { get; set; }
        public bool IsMockedData { get; set; }
        public string AdditionalSections { get; set; }
        public string DataType { get; set; }
        public string FileName { get; set; }
    }

    public class DataType
    {
        public const string HTML = "HTML";
        public const string LINK = "Link(s)";
        public const string SCREENSHOT = "Screenshot(s)";
        public const string Unknown = "Unknown";
    }
}
