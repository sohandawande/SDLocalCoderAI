using System.Text;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Model.Response.Repository;

namespace SD.LocalCoder.AI.Core.Services;

public sealed class RepositoryContextService : IRepositoryContextService
{
    private const int MaxContextChars = 100_000;
    private const int MaxFiles = 28;
    private const int MaxSingleFileChars = 30_000;

    private readonly IGitService _gitService;
    private readonly IProjectIntelligenceService _intelligence;

    public RepositoryContextService(
        IGitService gitService,
        IProjectIntelligenceService intelligence)
    {
        _gitService = gitService;
        _intelligence = intelligence;
    }

    public async Task<RepositoryContextResult?> BuildContextAsync(
        string repoId,
        string prompt,
        IReadOnlyCollection<string>? explicitPaths = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repoId) || string.IsNullOrWhiteSpace(prompt))
            return null;

        var architecture = await _intelligence.AnalyzeAsync(repoId, cancellationToken);
        var related = await _intelligence.SearchAsync(repoId, prompt, 40, cancellationToken);

        var allFiles = _gitService.GetFileList(repoId);
        if (allFiles.Count == 0)
            return null;

        var normalizedLookup = allFiles.ToDictionary(
            x => x.Replace('\\', '/'),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var selected = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddExplicitPaths(explicitPaths, normalizedLookup, selected, seen);
        AddRelatedPaths(related, normalizedLookup, selected, seen);

        // Architecture files are useful when the user asks for a new feature,
        // because they tell the model where new code belongs.
        AddArchitectureFiles(allFiles, selected, seen);

        // Fill remaining context with files connected to the discovered patterns.
        foreach (var file in allFiles)
        {
            if (selected.Count >= MaxFiles)
                break;

            if (seen.Contains(file))
                continue;

            if (!IsContextCandidate(file))
                continue;

            selected.Add(file);
            seen.Add(file);
        }

        var context = BuildFileContext(repoId, selected, cancellationToken);
        if (string.IsNullOrWhiteSpace(context))
            return null;

        return new RepositoryContextResult
        {
            RepositoryId = repoId,
            Architecture = architecture,
            RelatedSymbols = related.Take(20).ToList(),
            IncludedPaths = selected,
            Context = context
        };
    }

    private void AddExplicitPaths(
        IReadOnlyCollection<string>? explicitPaths,
        IReadOnlyDictionary<string, string> lookup,
        List<string> selected,
        HashSet<string> seen)
    {
        if (explicitPaths is null)
            return;

        foreach (var raw in explicitPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var normalized = raw.Replace('\\', '/').Trim().TrimStart('/');
            if (lookup.TryGetValue(normalized, out var actual) && seen.Add(actual))
                selected.Add(actual);
        }
    }

    private static void AddRelatedPaths(
        IReadOnlyList<SD.LocalCoder.AI.Model.Response.Repository.RepositorySearchResult> related,
        IReadOnlyDictionary<string, string> lookup,
        List<string> selected,
        HashSet<string> seen)
    {
        foreach (var result in related.OrderByDescending(x => x.Score))
        {
            if (lookup.TryGetValue(result.Path.Replace('\\', '/'), out var actual) &&
                seen.Add(actual))
            {
                selected.Add(actual);
            }
        }
    }

    private static void AddArchitectureFiles(
        IReadOnlyList<string> allFiles,
        List<string> selected,
        HashSet<string> seen)
    {
        var preferred = allFiles
            .Where(IsArchitectureFile)
            .OrderBy(PathDepth)
            .ThenBy(x => x)
            .Take(12);

        foreach (var file in preferred)
        {
            if (selected.Count >= MaxFiles)
                break;

            if (seen.Add(file))
                selected.Add(file);
        }
    }

    private string BuildFileContext(
        string repoId,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var total = 0;

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sb.Length >= MaxContextChars)
                break;

            var content = _gitService.ReadFileContent(repoId, path);
            if (string.IsNullOrWhiteSpace(content) || content.Length > MaxSingleFileChars)
                continue;

            var block =
                $"--- FILE: {path} ---\n" +
                content.TrimEnd() +
                "\n--- END FILE ---\n\n";

            if (total + block.Length > MaxContextChars)
                break;

            sb.Append(block);
            total += block.Length;
        }

        return sb.ToString();
    }

    private static bool IsArchitectureFile(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();
        var name = Path.GetFileName(p);

        return p.Contains("/controller") ||
               p.Contains("/service") ||
               p.Contains("/repository") ||
               p.Contains("/handler") ||
               p.Contains("/middleware") ||
               p.Contains("/dependencyinjection") ||
               p.Contains("/configuration") ||
               name is "program.cs" or "startup.cs" or "package.json" or "angular.json";
    }

    private static bool IsContextCandidate(string path)
    {
        var p = path.Replace('\\', '/').ToLowerInvariant();

        if (p.Contains("/bin/") || p.Contains("/obj/") ||
            p.Contains("/node_modules/") || p.Contains("/.git/") ||
            p.Contains("/dist/") || p.Contains("/build/"))
            return false;

        return new[]
        {
            ".cs", ".csproj", ".ts", ".tsx", ".js", ".jsx",
            ".py", ".java", ".kt", ".go", ".rs", ".php",
            ".sql", ".html", ".css", ".scss", ".json", ".xml"
        }.Contains(Path.GetExtension(p));
    }

    private static int PathDepth(string path) =>
        path.Replace('\\', '/').Count(c => c == '/');
}
