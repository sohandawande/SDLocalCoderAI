using Microsoft.AspNetCore.Mvc;
using SD.LocalCoder.AI.Core.Interfaces;
using SD.LocalCoder.AI.Model.Request.Chat;

namespace SD.LocalCoder.AI.Api.Controllers.Chat
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Chat with optional repository context.
        /// When RepoId is provided, relevant files from that repo are injected into the prompt
        /// so the model can follow your existing style and architecture.
        /// Optional Paths pins specific files first; remaining budget uses prompt-ranked selection.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatWithRepoRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return BadRequest(new { error = "Prompt is required" });

            var (success, response, error, includedPaths) =
                await _chatService.ChatAsync(request, cancellationToken);

            if (!success)
                return StatusCode(500, new { error = error ?? "Chat failed" });

            return Ok(new
            {
                response,
                model = "qwen2.5-coder:14b",
                repoId = request.RepoId,
                includedPaths = includedPaths ?? Array.Empty<string>()
            });
        }
    }
}
