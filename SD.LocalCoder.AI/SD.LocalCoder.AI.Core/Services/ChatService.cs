using System.Text;
using System.Text.RegularExpressions;
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
        private const int MaxSingleFileChars = 25_000;

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

        // Boost paths that usually define architecture / style
        private static readonly string[] ArchitectureHints =
        {
            "controller", "service", "interface", "program.cs",
            "dependencyinjection", "middleware", "extension",
            "request", "response", "model", "entity", "dto",
            "repository", "handler", "command", "query"
        };

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "to", "of", "in", "on", "for",
            "is", "are", "be", "with", "from", "that", "this", "it",
            "as", "at", "by", "into", "add", "create", "make", "write",
            "code", "file", "please", "can", "you", "me", "my", "new"
        };

        public ChatService(Kernel kernel, IGitService gitService)
        {
            _kernel = kernel;
            _gitService = gitService;
        }

        public async Task<(bool Success, string? Response, string? Error, IReadOnlyList<string>? IncludedPaths)> ChatAsync(
            ChatWithRepoRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return (false, null, "Prompt is required", null);

            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(BuildSystemMessage());

                var (userMessage, includedPaths) = await BuildUserMessageAsync(request, cancellationToken);
                chatHistory.AddUserMessage(userMessage);

                var result = await chatService.GetChatMessageContentAsync(
                    chatHistory,
                    cancellationToken: cancellationToken);

                return (true, result.Content, null, includedPaths);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message, null);
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

        private async Task<(string Message, IReadOnlyList<string> IncludedPaths)> BuildUserMessageAsync(
            ChatWithRepoRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RepoId))
                return (request.Prompt, Array.Empty<string>());

            var (context, includedPaths) = await Task.Run(
                () => BuildRepositoryContext(request.RepoId!, request.Prompt, request.Paths),
                cancellationToken);

            if (string.IsNullOrWhiteSpace(context))
            {
                var fallback =
                    $"[Repository '{request.RepoId}' was requested but no usable files were found.]\n\n" +
                    request.Prompt;
                return (fallback, Array.Empty<string>());
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

            return (sb.ToString(), includedPaths);
        }

        private (string Context, IReadOnlyList<string> IncludedPaths) BuildRepositoryContext(
            string repoId,
            string prompt,
            List<string>? explicitPaths)
        {
            var allFiles = _gitService.GetFileList(repoId);
            if (allFiles.Count == 0)
                return (string.Empty, Array.Empty<string>());

            var fileSet = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);
            var keywords = ExtractKeywords(prompt);

            // 1) Explicit paths first (user-pinned)
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (explicitPaths is { Count: > 0 })
            {
                foreach (var raw in explicitPaths)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    var path = raw.Replace('\\', '/').Trim().TrimStart('/');
                    if (!fileSet.Contains(path))
                    {
                        // try case-insensitive match against repo list
                        var match = allFiles.FirstOrDefault(f =>
                            string.Equals(f.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
                        if (match is null)
                            continue;
                        path = match;
                    }

                    if (seen.Add(path))
                        ordered.Add(path);
                }
            }

            // 2) Rank remaining useful source files by relevance
            var ranked = allFiles
                .Where(IsUsefulSourceFile)
                .Where(f => !seen.Contains(f))
                .Select(f => (Path: f, Score: ScorePath(f, keywords)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path)
                .Select(x => x.Path)
                .ToList();

            ordered.AddRange(ranked);

            var sb = new StringBuilder();
            var included = new List<string>();
            var totalChars = 0;

            foreach (var relativePath in ordered)
            {
                if (included.Count >= MaxFiles || totalChars >= MaxContextChars)
                    break;

                var content = _gitService.ReadFileContent(repoId, relativePath);
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.Length > MaxSingleFileChars)
                    continue;

                var block =
                    $"--- FILE: {relativePath} ---\n{content.TrimEnd()}\n--- END FILE ---\n\n";

                if (totalChars + block.Length > MaxContextChars)
                    break;

                sb.Append(block);
                totalChars += block.Length;
                included.Add(relativePath);
            }

            if (included.Count == 0)
                return (string.Empty, Array.Empty<string>());

            sb.Insert(0, $"Included {included.Count} file(s) (approx. {totalChars} chars).\n\n");
            return (sb.ToString(), included);
        }

        private static List<string> ExtractKeywords(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new List<string>();

            return Regex.Split(prompt.ToLowerInvariant(), @"[^a-z0-9_]+")
                .Where(t => t.Length >= 3 && !StopWords.Contains(t))
                .Distinct()
                .ToList();
        }

        private static int ScorePath(string relativePath, List<string> keywords)
        {
            var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
            var fileName = Path.GetFileNameWithoutExtension(normalized);
            var score = 0;

            foreach (var kw in keywords)
            {
                if (normalized.Contains(kw, StringComparison.Ordinal))
                    score += 10;
                if (fileName.Contains(kw, StringComparison.Ordinal))
                    score += 15;
            }

            foreach (var hint in ArchitectureHints)
            {
                if (normalized.Contains(hint, StringComparison.Ordinal))
                    score += 4;
            }

            // Slight preference for shallower paths (often more important entry points)
            var depth = normalized.Count(c => c == '/');
            score -= Math.Min(depth, 5);

            return score;
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
