using SD.LocalCoder.AI.Model.Request.Chat;
using SD.LocalCoder.AI.Model.Response.Chat;

namespace SD.LocalCoder.AI.Core.Interfaces
{
    public interface ISessionService
    {
        ChatSessionDto Create(CreateSessionRequest request);
        ChatSessionDto? Get(string sessionId);
        IReadOnlyList<ChatSessionDto> List();
        bool Delete(string sessionId);

        Task<(bool Success, ChatSessionDto? Session, string? Error)> SendAsync(
            string sessionId,
            SessionMessageRequest request,
            CancellationToken cancellationToken = default);
    }
}
