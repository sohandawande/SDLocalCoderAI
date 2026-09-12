using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Model.Request.Chat;
using SD.LocalCoder.AI.Model.Response.Chat;

namespace SD.LocalCoder.AI.Core.Services
{
    /// <summary>
    /// In-memory multi-turn sessions (offline-friendly). Restart clears history.
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly Kernel _kernel;
        private readonly IGitService _gitService;
        private readonly ConcurrentDictionary<string, SessionState> _sessions = new();

        private const int MaxContextChars = 80_000;
        private const int MaxFiles = 20;
        private const int MaxSingleFileChars = 25_000;
        private const int MaxHistoryMessages = 40;

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

        public SessionService(Kernel kernel, IGitService gitService)
        {
            _kernel = kernel;
            _gitService = gitService;
        }

        public ChatSessionDto Create(CreateSessionRequest request)
        {
            var id = Guid.NewGuid().ToString("N")[..12];
            var now = DateTime.UtcNow;
            var state = new SessionState
            {
                SessionId = id,
                RepoId = string.IsNullOrWhiteSpace(request.RepoId) ? null : request.RepoId.Trim(),
                Title = string.IsNullOrWhiteSpace(request.Title)
                    ? $"Session {id}"
                    : request.Title.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                DefaultPaths = request.Paths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new()
            };

            _sessions[id] = state;
            return ToDto(state);
        }

        public ChatSessionDto? Get(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var state) ? ToDto(state) : null;
        }

        public IReadOnlyList<ChatSessionDto> List()
        {
            return _sessions.Values
                .OrderByDescending(s => s.UpdatedAtUtc)
                .Select(ToDto)
                .ToList();
        }

        public bool Delete(string sessionId) => _sessions.TryRemove(sessionId, out _);

        public async Task<(bool Success, ChatSessionDto? Session, string? Error)> SendAsync(
            string sessionId,
            SessionMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
                return (false, null, "Session not found");

            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return (false, null, "Prompt is required");

            try
            {
                var paths = request.Paths?.Count > 0 ? request.Paths : state.DefaultPaths;
                var (contextBlock, includedPaths) = BuildRepositoryContext(state.RepoId, request.Prompt, paths);

                var userContent = string.IsNullOrWhiteSpace(contextBlock)
                    ? request.Prompt
                    : BuildUserMessageWithContext(state.RepoId, contextBlock, request.Prompt);

                state.Messages.Add(new ChatMessageDto
                {
                    Role = "user",
                    Content = request.Prompt,
                    AtUtc = DateTime.UtcNow,
                    IncludedPaths = includedPaths.Count > 0 ? includedPaths.ToList() : null
                });

                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                var history = BuildChatHistory(state, userContent);

                var result = await chatService.GetChatMessageContentAsync(
                    history,
                    cancellationToken: cancellationToken);

                var assistantText = result.Content ?? string.Empty;

                state.Messages.Add(new ChatMessageDto
                {
                    Role = "assistant",
                    Content = assistantText,
                    AtUtc = DateTime.UtcNow,
                    IncludedPaths = includedPaths.Count > 0 ? includedPaths.ToList() : null
                });

                // Keep memory bounded
                while (state.Messages.Count > MaxHistoryMessages)
                    state.Messages.RemoveAt(0);

                state.UpdatedAtUtc = DateTime.UtcNow;
                return (true, ToDto(state), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private ChatHistory BuildChatHistory(SessionState state, string latestUserContentWithContext)
        {
            var history = new ChatHistory();
            history.AddSystemMessage(
                "You are SD LocalCoder AI — an expert offline/online coding assistant. " +
                "Generate clean, production-ready code in any language the user asks for. " +
                "Follow best practices, meaningful names, and match repository style when context is provided. " +
                "When suggesting file changes, clearly mark the target path so the user can apply them.");

            // Prior turns (stored prompts only for user side to avoid re-injecting huge context every time)
            var prior = state.Messages.Take(state.Messages.Count - 1).ToList();
            foreach (var msg in prior)
            {
                if (msg.Role == "user")
                    history.AddUserMessage(msg.Content);
                else if (msg.Role == "assistant")
                    history.AddAssistantMessage(msg.Content);
            }

            // Latest user turn may include fresh repo context
            history.AddUserMessage(latestUserContentWithContext);
            return history;
        }

        private static string BuildUserMessageWithContext(string? repoId, string context, string prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== REPOSITORY CONTEXT ===");
            if (!string.IsNullOrWhiteSpace(repoId))
                sb.AppendLine($"RepoId: {repoId}");
            sb.AppendLine("Use these files as the source of truth for style and architecture.");
            sb.AppendLine();
            sb.AppendLine(context);
            sb.AppendLine("=== END REPOSITORY CONTEXT ===");
            sb.AppendLine();
            sb.AppendLine("=== USER TASK ===");
            sb.AppendLine(prompt);
            return sb.ToString();
        }

        private (string Context, IReadOnlyList<string> IncludedPaths) BuildRepositoryContext(
            string? repoId,
            string prompt,
            List<string>? explicitPaths)
        {
            if (string.IsNullOrWhiteSpace(repoId))
                return (string.Empty, Array.Empty<string>());

            var allFiles = _gitService.GetFileList(repoId);
            if (allFiles.Count == 0)
                return (string.Empty, Array.Empty<string>());

            var fileSet = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);
            var keywords = ExtractKeywords(prompt);
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (explicitPaths is { Count: > 0 })
            {
                foreach (var raw in explicitPaths)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var path = raw.Replace('\\', '/').Trim().TrimStart('/');
                    if (!fileSet.Contains(path))
                    {
                        var match = allFiles.FirstOrDefault(f =>
                            string.Equals(f.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
                        if (match is null) continue;
                        path = match;
                    }
                    if (seen.Add(path)) ordered.Add(path);
                }
            }

            var ranked = allFiles
                .Where(IsUsefulSourceFile)
                .Where(f => !seen.Contains(f))
                .Select(f => (Path: f, Score: ScorePath(f, keywords)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path)
                .Select(x => x.Path);

            ordered.AddRange(ranked);

            var sb = new StringBuilder();
            var included = new List<string>();
            var totalChars = 0;

            foreach (var relativePath in ordered)
            {
                if (included.Count >= MaxFiles || totalChars >= MaxContextChars) break;

                var content = _gitService.ReadFileContent(repoId, relativePath);
                if (string.IsNullOrWhiteSpace(content) || content.Length > MaxSingleFileChars) continue;

                var block = $"--- FILE: {relativePath} ---\n{content.TrimEnd()}\n--- END FILE ---\n\n";
                if (totalChars + block.Length > MaxContextChars) break;

                sb.Append(block);
                totalChars += block.Length;
                included.Add(relativePath);
            }

            if (included.Count == 0) return (string.Empty, Array.Empty<string>());

            sb.Insert(0, $"Included {included.Count} file(s) (approx. {totalChars} chars).\n\n");
            return (sb.ToString(), included);
        }

        private static List<string> ExtractKeywords(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return new List<string>();
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
                if (normalized.Contains(kw, StringComparison.Ordinal)) score += 10;
                if (fileName.Contains(kw, StringComparison.Ordinal)) score += 15;
            }

            foreach (var hint in ArchitectureHints)
            {
                if (normalized.Contains(hint, StringComparison.Ordinal)) score += 4;
            }

            score -= Math.Min(normalized.Count(c => c == '/'), 5);
            return score;
        }

        private static bool IsUsefulSourceFile(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            foreach (var skip in SkipPathSegments)
            {
                if (normalized.Contains(skip, StringComparison.OrdinalIgnoreCase)) return false;
            }

            var fileName = Path.GetFileName(normalized);
            if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, ".gitignore", StringComparison.OrdinalIgnoreCase))
                return true;

            var ext = Path.GetExtension(normalized);
            return !string.IsNullOrEmpty(ext) && CodeExtensions.Contains(ext);
        }

        private static ChatSessionDto ToDto(SessionState s) => new()
        {
            SessionId = s.SessionId,
            RepoId = s.RepoId,
            Title = s.Title,
            CreatedAtUtc = s.CreatedAtUtc,
            UpdatedAtUtc = s.UpdatedAtUtc,
            DefaultPaths = s.DefaultPaths.ToList(),
            Messages = s.Messages.Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                AtUtc = m.AtUtc,
                IncludedPaths = m.IncludedPaths?.ToList()
            }).ToList()
        };

        private sealed class SessionState
        {
            public string SessionId { get; set; } = string.Empty;
            public string? RepoId { get; set; }
            public string Title { get; set; } = string.Empty;
            public DateTime CreatedAtUtc { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
            public List<string> DefaultPaths { get; set; } = new();
            public List<ChatMessageDto> Messages { get; set; } = new();
        }
    }
}
