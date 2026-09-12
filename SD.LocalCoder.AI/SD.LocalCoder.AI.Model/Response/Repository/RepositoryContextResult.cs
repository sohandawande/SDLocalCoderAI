using SD.LocalCoder.AI.Model.Repository;

namespace SD.LocalCoder.AI.Model.Response.Repository;

public sealed class RepositoryContextResult
{
    public string RepositoryId { get; init; } = string.Empty;
    public RepositoryArchitectureProfile? Architecture { get; init; }
    public IReadOnlyList<RepositorySearchResult> RelatedSymbols { get; init; } = [];
    public IReadOnlyList<string> IncludedPaths { get; init; } = [];
    public string Context { get; init; } = string.Empty;
}
