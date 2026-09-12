using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Model.Common.Options;
using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Api.Controllers.Chat
{
    [Route("api/sessions")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessions;
        private readonly AiProviderOptions _ai;

        public SessionController(ISessionService sessions, IOptions<AiProviderOptions> ai)
        {
            _sessions = sessions;
            _ai = ai.Value;
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateSessionRequest request)
        {
            var session = _sessions.Create(request ?? new CreateSessionRequest());
            return Ok(session);
        }

        [HttpGet]
        public IActionResult List() => Ok(_sessions.List());

        [HttpGet("{sessionId}")]
        public IActionResult Get(string sessionId)
        {
            var session = _sessions.Get(sessionId);
            if (session is null)
                return NotFound(new { error = "Session not found" });
            return Ok(session);
        }

        /// <summary>Non-streaming message (waits for full reply).</summary>
        [HttpPost("{sessionId}/messages")]
        public async Task<IActionResult> Send(
            string sessionId,
            [FromBody] SessionMessageRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return BadRequest(new { error = "Prompt is required" });

            var (success, session, error) = await _sessions.SendAsync(sessionId, request, cancellationToken);

            if (!success)
            {
                if (error == "Session not found")
                    return NotFound(new { error });
                return StatusCode(500, new { error = error ?? "Chat failed" });
            }

            return Ok(new
            {
                session,
                model = _ai.ModelId
            });
        }

        /// <summary>Streaming message via Server-Sent Events (token, context, done, error).</summary>
        [HttpPost("{sessionId}/messages/stream")]
        public async Task Stream(
            string sessionId,
            [FromBody] SessionMessageRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsJsonAsync(new { error = "Prompt is required" }, cancellationToken);
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            await foreach (var (evt, data) in _sessions.SendStreamAsync(sessionId, request, cancellationToken))
            {
                var payload = $"event: {evt}\ndata: {EscapeSseData(data)}\n\n";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                if (evt is "done" or "error")
                    break;
            }
        }

        [HttpDelete("{sessionId}")]
        public IActionResult Delete(string sessionId)
        {
            if (!_sessions.Delete(sessionId))
                return NotFound(new { error = "Session not found" });
            return NoContent();
        }

        private static string EscapeSseData(string data)
        {
            // SSE data lines cannot contain raw newlines without repeating "data:"
            return data.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\ndata: ");
        }
    }
}
