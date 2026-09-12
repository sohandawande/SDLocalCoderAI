namespace SD.LocalCoder.AI.Model.Request.Chat
{
    /// <summary>
    /// Chat request that can optionally use a repository as context.
    /// </summary>
    public class ChatWithRepoRequest
    {
        /// <summary>
        /// User prompt / coding task.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional repository id returned by clone/local endpoints.
        /// When set, relevant files from that repo are injected into the prompt.
        /// </summary>
        public string? RepoId { get; set; }

        /// <summary>
        /// Optional explicit relative paths to include first (e.g. "Controllers/Chat/ChatController.cs").
        /// When provided, these files are preferred; remaining budget is filled by ranked auto-selection.
        /// </summary>
        public List<string>? Paths { get; set; }
    }
}
