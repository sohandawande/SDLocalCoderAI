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
    }
}
