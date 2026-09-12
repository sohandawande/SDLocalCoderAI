using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Indexing.Services;
using SD.LocalCoder.AI.Model.Repository;
using SD.LocalCoder.AI.Model.Response.Repository;

namespace SD.LocalCoder.AI.Core.Services;

public sealed class ProjectIntelligenceService : IProjectIntelligenceService
{
    private readonly RepositoryIndexer _indexer;

    public ProjectIntelligenceService(RepositoryIndexer indexer)
    {
        _indexer = indexer;
    }

    public Task<RepositoryArchitectureProfile?> AnalyzeAsync(
        string repoId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = _indexer.BuildIndex(repoId);
            if (files.Count == 0)
                return null;

            var extensionGroups = files
                .GroupBy(x => x.Extension, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Count())
                .ToList();

            var language = extensionGroups.FirstOrDefault()?.Key switch
            {
                ".cs" or ".csproj" => "C#",
                ".ts" or ".tsx" => "TypeScript",
                ".js" or ".jsx" => "JavaScript",
                ".py" => "Python",
                ".java" => "Java",
                ".kt" => "Kotlin",
                ".go" => "Go",
                ".rs" => "Rust",
                ".php" => "PHP",
                ".sql" => "SQL",
                _ => "Unknown"
            };

            var roles = files
                .GroupBy(x => x.ArchitectureRole)
                .OrderByDescending(x => x.Count())
                .Select(x => x.Key)
                .Where(x => x != "Unknown")
                .Take(12)
                .ToList();

            var architecture = DetectArchitecture(files, roles);
            var frameworks = DetectFrameworks(files);
            var patterns = DetectPatterns(files, roles);
            var layers = DetectLayers(files);

            var rules = BuildRules(architecture, roles, patterns);

            return new RepositoryArchitectureProfile
            {
                RepositoryId = repoId,
                PrimaryLanguage = language,
                Architecture = architecture,
                Frameworks = frameworks,
                Layers = layers,
                Patterns = patterns,
                Rules = rules,
                FileCount = files.Count,
                SymbolCount = files.Sum(x => x.Symbols.Count)
            };
        }, cancellationToken);
    }

    public Task<IReadOnlyList<RepositorySearchResult>> SearchAsync(
        string repoId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RepositorySearchResult>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(query))
                return [];

            maxResults = Math.Clamp(maxResults, 1, 100);
            var files = _indexer.BuildIndex(repoId);
            var terms = query.Split(
                [' ', '\t', '\r', '\n', '/', '\\', '.', '-', '_'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var results = new List<RepositorySearchResult>();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file.Path);
                var normalizedPath = file.Path.ToLowerInvariant();

                foreach (var symbol in file.Symbols)
                {
                    var score = Score(terms, normalizedPath, fileName, symbol.Name, symbol.Namespace, file.ArchitectureRole);
                    if (score > 0)
                    {
                        results.Add(new RepositorySearchResult
                        {
                            Path = file.Path,
                            Kind = symbol.Kind,
                            Name = symbol.Name,
                            Signature = symbol.Signature,
                            Namespace = symbol.Namespace,
                            ArchitectureRole = file.ArchitectureRole,
                            Score = score
                        });
                    }
                }

                var fileScore = Score(terms, normalizedPath, fileName, null, null, file.ArchitectureRole);
                if (fileScore > 0)
                {
                    results.Add(new RepositorySearchResult
                    {
                        Path = file.Path,
                        Kind = "File",
                        Name = Path.GetFileName(file.Path),
                        ArchitectureRole = file.ArchitectureRole,
                        Score = fileScore
                    });
                }
            }

            return results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path)
                .ThenBy(x => x.Name)
                .Take(maxResults)
                .ToList();
        }, cancellationToken);
    }

    private static int Score(
        IEnumerable<string> terms,
        string path,
        string fileName,
        string? symbol,
        string? ns,
        string role)
    {
        var score = 0;
        var symbolName = symbol?.ToLowerInvariant() ?? string.Empty;
        var namespaceName = ns?.ToLowerInvariant() ?? string.Empty;
        var roleName = role.ToLowerInvariant();

        foreach (var term in terms.Select(x => x.ToLowerInvariant()).Distinct())
        {
            if (term.Length < 2) continue;
            if (symbolName.Equals(term)) score += 100;
            else if (symbolName.Contains(term)) score += 50;

            if (fileName.Equals(term)) score += 70;
            else if (fileName.Contains(term)) score += 35;

            if (path.Contains(term)) score += 20;
            if (namespaceName.Contains(term)) score += 15;
            if (roleName.Contains(term)) score += 10;
        }

        return score;
    }

    private static string DetectArchitecture(
        IReadOnlyList<RepositoryFileInfo> files,
        IReadOnlyList<string> roles)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roleSet.Contains("Controller") && roleSet.Contains("Service") && roleSet.Contains("Repository"))
            return "Layered / Controller-Service-Repository";

        if (roleSet.Contains("Controller") && roleSet.Contains("Service"))
            return "Layered / Controller-Service";

        if (roleSet.Contains("Handler") && (roleSet.Contains("Command") || roleSet.Contains("Query")))
            return "CQRS / Handler-based";

        if (roleSet.Contains("Command") && roleSet.Contains("Query"))
            return "CQRS";

        if (files.Any(x => x.Path.Contains("verticalslice", StringComparison.OrdinalIgnoreCase)))
            return "Vertical Slice";

        return "Conventional / Undetermined";
    }

    private static IReadOnlyList<string> DetectFrameworks(IReadOnlyList<RepositoryFileInfo> files)
    {
        var frameworks = new List<string>();

        var paths = files.Select(x => x.Path).ToList();
        if (paths.Any(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
            frameworks.Add(".NET");

        if (paths.Any(x => x.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                           x.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)))
            frameworks.Add("TypeScript");

        if (paths.Any(x => x.EndsWith(".py", StringComparison.OrdinalIgnoreCase)))
            frameworks.Add("Python");

        return frameworks;
    }

    private static IReadOnlyList<string> DetectPatterns(
        IReadOnlyList<RepositoryFileInfo> files,
        IReadOnlyList<string> roles)
    {
        var patterns = new List<string>();

        if (roles.Contains("Service")) patterns.Add("Service");
        if (roles.Contains("Repository")) patterns.Add("Repository");
        if (roles.Contains("Controller")) patterns.Add("Controller");
        if (roles.Contains("Handler")) patterns.Add("Handler");
        if (roles.Contains("Middleware")) patterns.Add("Middleware");
        if (roles.Contains("Test")) patterns.Add("Automated Tests");

        if (files.Any(x => x.Symbols.Any(s => s.Kind.Equals("interface", StringComparison.OrdinalIgnoreCase))))
            patterns.Add("Interface Abstraction");

        return patterns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> DetectLayers(IReadOnlyList<RepositoryFileInfo> files)
    {
        return files
            .Select(x => x.Path.Replace('\\', '/').Split('/'))
            .Where(x => x.Length > 1)
            .Select(x => x[0])
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .Select(x => x.Key)
            .Take(20)
            .ToList();
    }

    private static IReadOnlyList<string> BuildRules(
        string architecture,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> patterns)
    {
        var rules = new List<string>
        {
            "Prefer existing project patterns over creating new patterns.",
            "Reuse existing abstractions before introducing duplicate classes.",
            "Keep generated changes limited to the user's requested scope."
        };

        if (architecture.Contains("Controller", StringComparison.OrdinalIgnoreCase) &&
            roles.Contains("Service"))
            rules.Add("Keep business logic in services rather than controllers.");

        if (roles.Contains("Repository"))
            rules.Add("Use the existing repository/data-access pattern instead of accessing persistence directly from controllers.");

        if (patterns.Contains("Interface Abstraction"))
            rules.Add("Prefer dependency injection through existing interfaces.");

        return rules;
    }
}
