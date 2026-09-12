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
    }
}
