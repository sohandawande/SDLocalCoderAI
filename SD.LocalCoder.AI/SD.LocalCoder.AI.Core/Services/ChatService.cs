using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Core.Services
{
    public class ChatService : IChatService
    {
        private readonly Kernel _kernel;
        private readonly IGitService _gitService;

        // Keep context within typical local-model windows
        private const int MaxContextChars = 80_000;
        private const int MaxFiles = 20;

        private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".sln", ".slnx",
            ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs",
            ".json", ".md", ".yml", ".yaml", ".xml",
            ".py", ".java", ".kt", ".go", ".rs",
            ".html", ".css", ".scss", ".sql",
            ".dockerfile", ".gitignore", ".env.example"
        };

        private static readonly string[] SkipPathSegments =
        {
            "bin/", "obj/", "node_modules/", ".git/", "packages/",
            "dist/", "build/", ".vs/", ".idea/", "coverage/",
            "__pycache__/", ".next/", "target/"
        };

        public ChatService(Kernel kernel, IGitService gitService)
        {
            _kernel = kernel;
            _gitService = gitService;
        }

        public async Task<(bool Success, string? Response, string? Error)> ChatAsync(
            ChatWithRepoRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return (false, null, "Prompt is required");

            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(BuildSystemMessage());

                var userMessage = await BuildUserMessageAsync(request, cancellationToken);
                chatHistory.AddUserMessage(userMessage);

                var result = await chatService.GetChatMessageContentAsync(
                    chatHistory,
                    cancellationToken: cancellationToken);

                return (true, result.Content, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private static string BuildSystemMessage()
        {
            return
                "You are SD LocalCoder AI — an expert offline coding assistant. " +
                "Generate clean, production-ready, well-structured code. " +
                "Always follow best practices, use meaningful names, and explain the code briefly. " +
                "When repository context is provided, match the existing architecture, naming, " +
                "folder layout, and coding style of that codebase. Prefer extending existing patterns " +
                "over inventing new ones.";
        }

        private async Task<string> BuildUserMessageAsync(
            ChatWithRepoRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RepoId))
                return request.Prompt;

            var context = await Task.Run(
                () => BuildRepositoryContext(request.RepoId!),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(context))
            {
                return
                    $"[Repository '{request.RepoId}' was requested but no usable files were found.]\n\n" +
                    request.Prompt;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== REPOSITORY CONTEXT ===");
            sb.AppendLine($"RepoId: {request.RepoId}");
            sb.AppendLine("Use the following files as the source of truth for style, architecture, and existing code.");
            sb.AppendLine();
            sb.AppendLine(context);
            sb.AppendLine("=== END REPOSITORY CONTEXT ===");
            sb.AppendLine();
            sb.AppendLine("=== USER TASK ===");
            sb.AppendLine(request.Prompt);

            return sb.ToString();
        }

        private string BuildRepositoryContext(string repoId)
        {
            var files = _gitService.GetFileList(repoId);
            if (files.Count == 0)
                return string.Empty;

            var selected = files
                .Where(IsUsefulSourceFile)
                .OrderBy(f => f)
                .Take(MaxFiles * 3) // candidate pool before size filter
                .ToList();

            var sb = new StringBuilder();
            var totalChars = 0;
            var included = 0;

            foreach (var relativePath in selected)
            {
                if (included >= MaxFiles || totalChars >= MaxContextChars)
                    break;

                var content = _gitService.ReadFileContent(repoId, relativePath);
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                // Skip huge single files
                if (content.Length > 25_000)
                    continue;

                var block =
                    $"--- FILE: {relativePath} ---\n{content.TrimEnd()}\n--- END FILE ---\n\n";

                if (totalChars + block.Length > MaxContextChars)
                    break;

                sb.Append(block);
                totalChars += block.Length;
                included++;
            }

            if (included == 0)
                return string.Empty;

            sb.Insert(0, $"Included {included} file(s) (approx. {totalChars} chars).\n\n");
            return sb.ToString();
        }

        private static bool IsUsefulSourceFile(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');

            foreach (var skip in SkipPathSegments)
            {
                if (normalized.Contains(skip, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var fileName = Path.GetFileName(normalized);
            if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ".gitignore", StringComparison.OrdinalIgnoreCase))
                return true;

            var ext = Path.GetExtension(normalized);
            return !string.IsNullOrEmpty(ext) && CodeExtensions.Contains(ext);
        }
    }
}
