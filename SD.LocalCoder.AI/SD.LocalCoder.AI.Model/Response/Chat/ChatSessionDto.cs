namespace SD.LocalCoder.AI.Model.Response.Chat
{
    public class ChatSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string? RepoId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<string> DefaultPaths { get; set; } = new();
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = string.Empty; // user | assistant | system
        public string Content { get; set; } = string.Empty;
        public DateTime AtUtc { get; set; }
        public List<string>? IncludedPaths { get; set; }
    }
}
