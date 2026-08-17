using Calcpad.WebApi.Configs.SubConfigs;

namespace Calcpad.WebApi.Configs
{
    public class AIConfig
    {
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Gets or sets the active AI chat provider.
        /// </summary>
        public string Provider { get; set; } = "OpenAIChat";

        /// <summary>
        /// chat config for openai chat api
        /// </summary>
        public OpenAIChat? OpenAIChat { get; set; }

        /// <summary>
        /// chat config for iEPC chat api
        /// </summary>
        public IepcChat? IepcChat { get; set; }

        /// <summary>
        /// Gets or sets the collection of AI prompt templates used by the application.
        /// </summary>
        public List<AIPrompts>? Prompts { get; set; }
    }
}
