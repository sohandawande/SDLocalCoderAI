using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SD.LocalCoder.AI.Model.Request;

namespace SD.LocalCoder.AI.Api.Controllers.Chat
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly Kernel _kernel;

        public ChatController(Kernel kernel)
        {
            _kernel = kernel;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Prompt))
                return BadRequest(new { error = "Prompt is required" });

            try
            {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();

                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage(
                    "You are SD LocalCoder AI — an expert offline coding assistant. " +
                    "Generate clean, production-ready, well-structured code. " +
                    "Always follow best practices, use meaningful names, and explain the code briefly.");

                chatHistory.AddUserMessage(request.Prompt);

                var result = await chatService.GetChatMessageContentAsync(chatHistory);

                return Ok(new
                {
                    response = result.Content,
                    model = "qwen2.5-coder:14b"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}