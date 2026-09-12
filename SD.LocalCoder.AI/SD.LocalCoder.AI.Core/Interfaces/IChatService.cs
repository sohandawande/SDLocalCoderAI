using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Core.Interfaces
{
    public interface IChatService
    {
        /// <summary>
        /// Runs a chat completion. When <paramref name="request"/>.RepoId is set,
        /// relevant files from that repository are injected as context.
        /// </summary>
        Task<(bool Success, string? Response, string? Error)> ChatAsync(ChatWithRepoRequest request, CancellationToken cancellationToken = default);
    }
}
