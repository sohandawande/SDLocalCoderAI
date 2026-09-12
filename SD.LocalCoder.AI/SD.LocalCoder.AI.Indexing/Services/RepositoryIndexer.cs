using System.Text;
using System.Text.RegularExpressions;
using SD.LocalCoder.AI.Git.Interfaces;
using SD.LocalCoder.AI.Model.Repository;

namespace SD.LocalCoder.AI.Indexing.Services;

public sealed class RepositoryIndexer
{
    private readonly IGitService _gitService;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".ts", ".tsx", ".js", ".jsx", ".py", ".java", ".kt",
        ".go", ".rs", ".php", ".sql", ".html", ".css", ".scss", ".json", ".xml", ".md"
    };

    private static readonly string[] IgnoredSegments =
    [
        "/bin/", "/obj/", "/node_modules/", "/.git/", "/dist/", "/build/",
        "/coverage/", "/.vs/", "/.idea/", "/__pycache__/", "/target/"
    ];

    private static readonly Regex CSharpNamespace =
        new(@"^\s*namespace\s+([A-Za-z0-9_.]+)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex CSharpSymbols =
        new(@"\b(public|internal|protected|private)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+)?\b(class|interface|record|struct|enum|delegate)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

    private static readonly Regex TypeScriptSymbols =
        new(@"\b(export\s+)?(class|interface|type|enum|function)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

    public RepositoryIndexer(IGitService gitService)
    {
        _gitService = gitService;
    }

    public IReadOnlyList<RepositoryFileInfo> BuildIndex(string repoId)
    {
        var result = new List<RepositoryFileInfo>();

        foreach (var path in _gitService.GetFileList(repoId))
        {
            if (!IsSupported(path))
                continue;

            var content = _gitService.ReadFileContent(repoId, path);
            if (content is null)
                continue;

            var symbols = ExtractSymbols(path, content);
            result.Add(new RepositoryFileInfo
            {
                Path = path,
                Extension = Path.GetExtension(path),
                Size = Encoding.UTF8.GetByteCount(content),
                ArchitectureRole = DetectArchitectureRole(path),
                Symbols = symbols
            });
        }

        return result;
    }

    private static IReadOnlyList<RepositorySymbol> ExtractSymbols(string path, string content)
    {
        var list = new List<RepositorySymbol>();
        var extension = Path.GetExtension(path);

        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var ns = CSharpNamespace.Match(content).Groups[1].Value;
            foreach (Match match in CSharpSymbols.Matches(content))
            {
                list.Add(new RepositorySymbol
                {
                    Name = match.Groups[3].Value,
                    Kind = match.Groups[2].Value,
                    FilePath = path,
                    Namespace = string.IsNullOrWhiteSpace(ns) ? null : ns,
                    Signature = match.Value.Trim()
                });
            }
        }
        else if (extension is ".ts" or ".tsx" or ".js" or ".jsx")
        {
            foreach (Match match in TypeScriptSymbols.Matches(content))
            {
                list.Add(new RepositorySymbol
                {
                    Name = match.Groups[3].Value,
                    Kind = match.Groups[2].Value,
                    FilePath = path,
                    Signature = match.Value.Trim()
                });
            }
        }

        return list;
    }

    public static string DetectArchitectureRole(string path)
    {
        var p = "/" + path.Replace('\\', '/').Trim('/').ToLowerInvariant() + "/";

        if (p.Contains("/controller/") || p.Contains("/controllers/")) return "Controller";
        if (p.Contains("/service/") || p.Contains("/services/")) return "Service";
        if (p.Contains("/repository/") || p.Contains("/repositories/")) return "Repository";
        if (p.Contains("/model/") || p.Contains("/models/") || p.Contains("/dto/")) return "Model";
        if (p.Contains("/entity/") || p.Contains("/entities/")) return "Entity";
        if (p.Contains("/handler/") || p.Contains("/handlers/")) return "Handler";
        if (p.Contains("/middleware/")) return "Middleware";
        if (p.Contains("/query/") || p.Contains("/queries/")) return "Query";
        if (p.Contains("/command/") || p.Contains("/commands/")) return "Command";
        if (p.Contains("/test/") || p.Contains("/tests/")) return "Test";
        if (p.Contains("/config/") || p.Contains("/configuration/")) return "Configuration";
        if (p.EndsWith("/program.cs")) return "ApplicationStartup";
        if (p.EndsWith("/startup.cs")) return "ApplicationStartup";
        if (p.EndsWith(".csproj") || p.EndsWith(".sln") || p.EndsWith(".slnx")) return "ProjectConfiguration";

        return "Unknown";
    }

    private static bool IsSupported(string path)
    {
        var normalized = "/" + path.Replace('\\', '/').Trim('/').ToLowerInvariant() + "/";
        if (IgnoredSegments.Any(normalized.Contains))
            return false;

        var fileName = Path.GetFileName(path);
        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
            return true;

        return SupportedExtensions.Contains(Path.GetExtension(path));
    }
}
