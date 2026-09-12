namespace SD.LocalCoder.AI.Model.Repository;

public sealed class RepositorySymbol
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string? Namespace { get; init; }
    public string? Signature { get; init; }
}
