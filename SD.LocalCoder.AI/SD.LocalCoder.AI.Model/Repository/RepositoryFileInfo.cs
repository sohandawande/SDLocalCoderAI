namespace SD.LocalCoder.AI.Model.Repository;

public sealed class RepositoryFileInfo
{
    public string Path { get; init; } = string.Empty;
    public string Extension { get; init; } = string.Empty;
    public long Size { get; init; }
    public string ArchitectureRole { get; init; } = "Unknown";
    public IReadOnlyList<RepositorySymbol> Symbols { get; init; } = [];
}
