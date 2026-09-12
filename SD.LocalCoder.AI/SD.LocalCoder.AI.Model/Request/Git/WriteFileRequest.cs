namespace SD.LocalCoder.AI.Model.Request.Git
{
    /// <summary>
    /// Writes or overwrites a file inside a registered repository (apply AI-generated code).
    /// </summary>
    public class WriteFileRequest
    {
        /// <summary>Relative path inside the repo, e.g. Controllers/Chat/ChatController.cs</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Full file content to write (UTF-8).</summary>
        public string Content { get; set; } = string.Empty;
    }
}
