using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Core.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Runs a chat completion. When <paramref name="request"/>.RepoId is set,
        /// relevant files from that repository are injected as context.
        /// </summary>
        /// <returns>
        /// Success flag, model response text, error message if any, and the list of
        /// repository paths that were included in the prompt context.
        /// </returns>
        Task<(bool Success, string? Response, string? Error, IReadOnlyList<string>? IncludedPaths)> ChatAsync(
            ChatWithRepoRequest request,
            CancellationToken cancellationToken = default);
    }
}
