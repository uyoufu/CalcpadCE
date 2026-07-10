using Calcpad.WebApi.Configs;
using Calcpad.WebApi.Services.AI.Interface;
using Calcpad.WebApi.Utils.Web.Service;

namespace Calcpad.WebApi.Services.AI
{
    /// <summary>
    /// Simple factory for selecting the configured AI chat client.
    /// </summary>
    public class AIChatClientFactory(
        AppSettings<AIConfig> aiConfig,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory
    ) : ISingletonService
    {
        private const string OpenAIProvider = "OpenAIChat";
        private const string IepcProvider = "IepcChat";

        /// <summary>
        /// Creates the configured AI chat client.
        /// </summary>
        /// <returns>The selected AI chat client.</returns>
        public IAIChatClient Create()
        {
            var provider = string.IsNullOrWhiteSpace(aiConfig.Value.Provider)
                ? OpenAIProvider
                : aiConfig.Value.Provider.Trim();

            if (string.Equals(provider, OpenAIProvider, StringComparison.OrdinalIgnoreCase))
            {
                if (
                    aiConfig.Value.OpenAIChat?.IsValid() == true
                    && aiConfig.Value.OpenAIChat.MaxTokenLenght > 0
                )
                {
                    return new OpenAIChatClient(aiConfig);
                }

                throw new InvalidOperationException(
                    $"{OpenAIProvider} chat provider is selected but not configured."
                );
            }

            if (string.Equals(provider, IepcProvider, StringComparison.OrdinalIgnoreCase))
            {
                if (
                    aiConfig.Value.IepcChat?.IsValid() == true
                    && aiConfig.Value.IepcChat.MaxTokenLenght > 0
                )
                {
                    return new IepcChatClient(
                        httpClientFactory.CreateClient(),
                        aiConfig,
                        loggerFactory.CreateLogger<IepcChatClient>()
                    );
                }

                throw new InvalidOperationException(
                    $"{IepcProvider} chat provider is selected but not configured."
                );
            }

            throw new InvalidOperationException($"Unknown AI chat provider: {provider}.");
        }
    }
}
