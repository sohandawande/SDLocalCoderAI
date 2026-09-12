namespace SD.LocalCoder.AI.Model.Repository;

public sealed class RepositoryArchitectureProfile
{
    public string RepositoryId { get; init; } = string.Empty;
    public string PrimaryLanguage { get; init; } = "Unknown";
    public string Architecture { get; init; } = "Unknown";
    public IReadOnlyList<string> Frameworks { get; init; } = [];
    public IReadOnlyList<string> Layers { get; init; } = [];
    public IReadOnlyList<string> Patterns { get; init; } = [];
    public IReadOnlyList<string> Rules { get; init; } = [];
    public int FileCount { get; init; }
    public int SymbolCount { get; init; }
}
