namespace SD.LocalCoder.AI.Git.Interfaces
{
    public interface IGitService
    {
        // Remote Git
        Task<(bool Success, string Message, string? RepoId)> CloneRepositoryAsync(string gitUrl);

        // Local Folder
        Task<(bool Success, string Message, string? RepoId)> AddLocalRepositoryAsync(string localPath);

        // Common methods
        List<string> GetFileList(string repoId);
        string? ReadFileContent(string repoId, string relativePath);
        string GetRepoPath(string repoId);

        /// <summary>List known repo ids under the Repos folder.</summary>
        List<string> ListRepoIds();

        /// <summary>
        /// Write or create a file inside the repo. Returns false if path is invalid/unsafe.
        /// </summary>
        (bool Success, string Message) WriteFileContent(string repoId, string relativePath, string content);
    }
}
