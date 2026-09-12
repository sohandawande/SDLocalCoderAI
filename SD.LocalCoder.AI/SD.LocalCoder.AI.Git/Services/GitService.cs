using LibGit2Sharp;
using SD.LocalCoder.AI.Git.Interfaces;
using System.Text;

namespace SD.LocalCoder.AI.Git.Services
{
    public class GitService : IGitService
    {
        private readonly string _reposBasePath;

        public GitService()
        {
            _reposBasePath = Path.Combine(Directory.GetCurrentDirectory(), "Repos");

            if (!Directory.Exists(_reposBasePath))
                Directory.CreateDirectory(_reposBasePath);
        }

        public async Task<(bool Success, string Message, string? RepoId)> CloneRepositoryAsync(string gitUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gitUrl))
                    return (false, "Git URL is required", null);

                var repoName = Path.GetFileNameWithoutExtension(gitUrl.TrimEnd('/').Split('/').Last());
                var repoId = $"{repoName}_{Guid.NewGuid().ToString()[..8]}";
                var localPath = Path.Combine(_reposBasePath, repoId);

                await Task.Run(() => Repository.Clone(gitUrl, localPath));

                return (true, "Repository cloned successfully", repoId);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to clone repository: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, string? RepoId)> AddLocalRepositoryAsync(string localPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localPath))
                    return (false, "Local path is required", null);

                if (!Directory.Exists(localPath))
                    return (false, "Local path does not exist", null);

                var repoName = new DirectoryInfo(localPath).Name;
                var repoId = $"{repoName}_{Guid.NewGuid().ToString()[..8]}";
                var targetPath = Path.Combine(_reposBasePath, repoId);

                await Task.Run(() => CopyDirectory(localPath, targetPath));

                return (true, "Local repository added successfully", repoId);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to add local repository: {ex.Message}", null);
            }
        }

        public List<string> GetFileList(string repoId)
        {
            var repoPath = Path.Combine(_reposBasePath, repoId);

            if (!Directory.Exists(repoPath))
                return new List<string>();

            return Directory.GetFiles(repoPath, "*.*", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(repoPath, f).Replace("\\", "/"))
                            .Where(f => !f.StartsWith(".git/"))
                            .OrderBy(f => f)
                            .ToList();
        }

        public string? ReadFileContent(string repoId, string relativePath)
        {
            if (!TryResolveSafePath(repoId, relativePath, out var fullPath))
                return null;

            if (!System.IO.File.Exists(fullPath))
                return null;

            return System.IO.File.ReadAllText(fullPath, Encoding.UTF8);
        }

        public (bool Success, string Message) WriteFileContent(string repoId, string relativePath, string content)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                return (false, "RepoId is required");

            if (string.IsNullOrWhiteSpace(relativePath))
                return (false, "Path is required");

            var repoPath = Path.Combine(_reposBasePath, repoId);
            if (!Directory.Exists(repoPath))
                return (false, "Repository not found");

            if (!TryResolveSafePath(repoId, relativePath, out var fullPath))
                return (false, "Invalid or unsafe path");

            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                System.IO.File.WriteAllText(fullPath, content ?? string.Empty, Encoding.UTF8);
                return (true, "File written successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to write file: {ex.Message}");
            }
        }

        public string GetRepoPath(string repoId)
        {
            return Path.Combine(_reposBasePath, repoId);
        }

        public List<string> ListRepoIds()
        {
            if (!Directory.Exists(_reposBasePath))
                return new List<string>();

            return Directory.GetDirectories(_reposBasePath)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n)
                .ToList()!;
        }

        private bool TryResolveSafePath(string repoId, string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            var repoPath = Path.GetFullPath(Path.Combine(_reposBasePath, repoId));

            var normalized = relativePath.Replace('\\', '/').Trim().TrimStart('/');
            if (normalized.Contains("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(relativePath))
                return false;

            var candidate = Path.GetFullPath(Path.Combine(repoPath, normalized));
            if (!candidate.StartsWith(repoPath, StringComparison.OrdinalIgnoreCase))
                return false;

            fullPath = candidate;
            return true;
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                if (subDir.Name.Equals(".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
