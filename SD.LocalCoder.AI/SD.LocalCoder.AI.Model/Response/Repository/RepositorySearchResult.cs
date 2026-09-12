using SD.LocalCoder.AI.Model.Repository;

namespace SD.LocalCoder.AI.Model.Response.Repository;

public sealed class RepositorySearchResult
{
    public string Path { get; init; } = string.Empty;
    public string Kind { get; init; } = "File";
    public string Name { get; init; } = string.Empty;
    public string? Signature { get; init; }
    public string? Namespace { get; init; }
    public string ArchitectureRole { get; init; } = "Unknown";
    public int Score { get; init; }
}
