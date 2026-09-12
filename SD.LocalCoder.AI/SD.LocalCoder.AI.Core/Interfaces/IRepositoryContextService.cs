using SD.LocalCoder.AI.Model.Response.Repository;

namespace SD.LocalCoder.AI.Core.Interfaces;

public interface IRepositoryContextService
{
    Task<RepositoryContextResult?> BuildContextAsync(
        string repoId,
        string prompt,
        IReadOnlyCollection<string>? explicitPaths = null,
        CancellationToken cancellationToken = default);
}
