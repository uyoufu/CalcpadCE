using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Calcpad.WebApi.Configs;
using Calcpad.WebApi.Configs.SubConfigs;
using Calcpad.WebApi.Services.AI.Interface;
using Microsoft.Extensions.AI;

namespace Calcpad.WebApi.Services.AI
{
    /// <summary>
    /// iEPC Chat Client wrapper for IChatClient interface
    /// </summary>
    public class IepcChatClient(
        HttpClient httpClient,
        AppSettings<AIConfig> config,
        ILogger<IepcChatClient> logger
    ) : IAIChatClient
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions =
            new(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

        private readonly IepcChat? _iepcChat = config.Value.IepcChat;

        public long MaxTokenLength { get; private set; } =
            config.Value.IepcChat?.MaxTokenLenght ?? 0;

        public bool IsAvailable => _iepcChat?.IsValid() == true && MaxTokenLength > 0;

        #region IChatClient
        public void Dispose()
        {
            httpClient.Dispose();
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            if (_iepcChat == null || !_iepcChat.IsValid())
            {
                throw new InvalidOperationException("IepcChatClient is not initialized.");
            }

            var requestBody = BuildRequestBody(messages, _iepcChat);
            var requestUrl = BuildRequestUrl(_iepcChat);
            var requestJson = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "iEPC chat request failed with status code {StatusCode}: {Response}",
                    response.StatusCode,
                    responseJson
                );
                response.EnsureSuccessStatusCode();
            }

            var iepcResponse =
                JsonSerializer.Deserialize<IepcChatResponse>(responseJson, _jsonSerializerOptions)
                ?? throw new InvalidOperationException("iEPC chat response is invalid.");
            if (!iepcResponse.Ok)
            {
                throw new InvalidOperationException(
                    $"iEPC chat response failed: {iepcResponse.Message ?? iepcResponse.Code.ToString()}"
                );
            }

            var responseText = iepcResponse
                .Data?.Parts?.LastOrDefault(x =>
                    string.Equals(x.Type, "text", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(x.Text)
                )
                ?.Text;

            if (responseText == null)
            {
                throw new InvalidOperationException(
                    "iEPC chat response does not contain text part."
                );
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText));
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType.IsInstanceOfType(this) ? this : null;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
                CancellationToken cancellationToken = default
        )
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }
        #endregion

        private static IepcChatRequest BuildRequestBody(
            IEnumerable<ChatMessage> messages,
            IepcChat iepcChat
        )
        {
            var messageList = messages.ToList();
            var systemPrompt = string.Join(
                "\n",
                messageList
                    .Where(x => x.Role == ChatRole.System)
                    .Select(x => x.Text)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
            );
            var userText = string.Join(
                "\n",
                messageList
                    .Where(x => x.Role != ChatRole.System)
                    .Select(x => x.Text)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
            );

            return new IepcChatRequest(
                iepcChat.WorkspaceName,
                new IepcChatRequestMessage(
                    systemPrompt,
                    [new IepcChatRequestPart("text", userText)]
                ),
                false
            );
        }

        private static Uri BuildRequestUrl(IepcChat iepcChat)
        {
            var endpoint = iepcChat.Endpoint.TrimEnd('/');
            if (!endpoint.EndsWith("/api/opencode/ask", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = $"{endpoint}/api/opencode/ask";
            }

            var uriBuilder = new UriBuilder(endpoint);
            var token = $"token={Uri.EscapeDataString(iepcChat.ApiKey)}";
            uriBuilder.Query = string.IsNullOrWhiteSpace(uriBuilder.Query)
                ? token
                : $"{uriBuilder.Query.TrimStart('?')}&{token}";
            return uriBuilder.Uri;
        }

        private sealed record IepcChatRequest(
            string WorkspaceName,
            IepcChatRequestMessage Message,
            bool KeepSession
        );

        private sealed record IepcChatRequestMessage(
            string System,
            List<IepcChatRequestPart> Parts
        );

        private sealed record IepcChatRequestPart(string Type, string Text);

        private sealed record IepcChatResponse(
            bool Ok,
            IepcChatResponseData? Data,
            string? Message,
            int Code
        );

        private sealed record IepcChatResponseData(List<IepcChatResponsePart>? Parts);

        private sealed record IepcChatResponsePart(string? Type, string? Text);
    }
}
