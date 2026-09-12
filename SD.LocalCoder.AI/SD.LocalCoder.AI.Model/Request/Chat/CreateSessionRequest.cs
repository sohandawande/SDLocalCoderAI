namespace SD.LocalCoder.AI.Model.Request.Chat
{
    /// <summary>
    /// Creates a multi-turn coding session, optionally bound to a repository.
    /// </summary>
    public class CreateSessionRequest
    {
        /// <summary>Optional repo from clone/local endpoints.</summary>
        public string? RepoId { get; set; }

        /// <summary>Optional title shown in the UI.</summary>
        public string? Title { get; set; }

        /// <summary>Optional explicit paths preferred as context for this session.</summary>
        public List<string>? Paths { get; set; }
    }
}
