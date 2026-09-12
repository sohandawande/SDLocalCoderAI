using SD.LocalCoder.AI.Model.Repository;
using SD.LocalCoder.AI.Model.Response.Repository;

namespace SD.LocalCoder.AI.Core.Interfaces;

public interface IProjectIntelligenceService
{
    Task<RepositoryArchitectureProfile?> AnalyzeAsync(
        string repoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositorySearchResult>> SearchAsync(
        string repoId,
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
