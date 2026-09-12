using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SD.LocalCoder.AI.Model.Common.Options;

namespace SD.LocalCoder.AI.Api.Controllers.Provider
{
    [Route("api/provider")]
    [ApiController]
    public class ProviderController : ControllerBase
    {
        private readonly AiProviderOptions _options;

        public ProviderController(IOptions<AiProviderOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>Current AI provider configuration (no secrets beyond masked key).</summary>
        [HttpGet]
        public IActionResult Get()
        {
            var key = _options.ApiKey ?? string.Empty;
            var masked = key.Length <= 4
                ? "****"
                : key[..2] + new string('*', Math.Min(key.Length - 4, 8)) + key[^2..];

            return Ok(new
            {
                provider = _options.Provider,
                modelId = _options.ModelId,
                endpoint = _options.Endpoint,
                apiKeyMasked = masked,
                mode = _options.Endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                    || _options.Endpoint.Contains("127.0.0.1")
                    ? "offline"
                    : "online"
            });
        }
    }
}
