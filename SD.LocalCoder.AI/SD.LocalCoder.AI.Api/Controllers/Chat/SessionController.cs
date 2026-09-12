using Microsoft.AspNetCore.Mvc;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Api.Controllers.Chat
{
    [Route("api/sessions")]
    [ApiController]
    public class SessionController : ControllerBase
    {
        private readonly ISessionService _sessions;

        public SessionController(ISessionService sessions)
        {
            _sessions = sessions;
        }

        /// <summary>Create a multi-turn coding session (optional repo binding).</summary>
        [HttpPost]
        public IActionResult Create([FromBody] CreateSessionRequest request)
        {
            var session = _sessions.Create(request ?? new CreateSessionRequest());
            return Ok(session);
        }

        /// <summary>List all sessions (newest first).</summary>
        [HttpGet]
        public IActionResult List() => Ok(_sessions.List());

        /// <summary>Get one session with full message history.</summary>
        [HttpGet("{sessionId}")]
        public IActionResult Get(string sessionId)
        {
            var session = _sessions.Get(sessionId);
            if (session is null)
                return NotFound(new { error = "Session not found" });
            return Ok(session);
        }

        /// <summary>Send a message in the session (multi-turn + optional repo context).</summary>
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
                model = "qwen2.5-coder:14b"
            });
        }

        /// <summary>Delete a session.</summary>
        [HttpDelete("{sessionId}")]
        public IActionResult Delete(string sessionId)
        {
            if (!_sessions.Delete(sessionId))
                return NotFound(new { error = "Session not found" });
            return NoContent();
        }
    }
}
