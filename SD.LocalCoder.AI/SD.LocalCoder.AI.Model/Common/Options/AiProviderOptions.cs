namespace SD.LocalCoder.AI.Model.Common.Options
{
    /// <summary>
    /// Offline (Ollama) or online (OpenAI / any OpenAI-compatible endpoint).
    /// </summary>
    public class AiProviderOptions
    {
        public const string SectionName = "AiProvider";

        /// <summary>Display name: Ollama | OpenAI | Custom</summary>
        public string Provider { get; set; } = "Ollama";

        public string ModelId { get; set; } = "qwen2.5-coder:14b";

        /// <summary>Any non-empty string works for Ollama; real key for cloud providers.</summary>
        public string ApiKey { get; set; } = "ollama";

        /// <summary>OpenAI-compatible base URL, e.g. http://localhost:11434/v1 or https://api.openai.com/v1</summary>
        public string Endpoint { get; set; } = "http://localhost:11434/v1";
    }
}
