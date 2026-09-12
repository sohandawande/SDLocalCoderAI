namespace SD.LocalCoder.AI.Model.Request.Chat
{
    /// <summary>
    /// Sends a user message into an existing chat session.
    /// </summary>
    public class SessionMessageRequest
    {
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional override of paths for this turn only (otherwise session defaults apply).
        /// </summary>
        public List<string>? Paths { get; set; }
    }
}
