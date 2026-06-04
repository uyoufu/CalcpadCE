using System.Net.Http.Headers;
using System.Text;
using Calcpad.WebApi.Configs;
using Calcpad.WebApi.Utils.Json;
using Calcpad.WebApi.Utils.Web.Service;
using Newtonsoft.Json.Linq;

namespace Calcpad.WebApi.Api.SignalR
{
    public class HttpApiService(
        HttpClient httpClient,
        AppSettings<SignalRApiConfig> config,
        SignalRApiTokenCache tokenCache,
        ILogger<HttpApiService> logger
    ) : ISingletonService
    {
        private const string SendPath = "/api/v1/signal-rproxy/send";

        public async Task SendCalculationProgressAsync(
            string uniqueId,
            double value,
            string message,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(config.Value.BaseUrl))
                return;

            if (string.IsNullOrWhiteSpace(uniqueId))
                return;

            var token = await tokenCache.GetTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return;

            var signalRMessage = new SignalRMessage()
                .ToRooms(uniqueId)
                .WithMethod("calcpadProgressChanged")
                .Command("updateProgress")
                .PayloadsData(new JObject() { ["value"] = value, ["message"] = message });

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildSendUrl())
            {
                Content = new StringContent(
                    signalRMessage.ToJson(),
                    Encoding.UTF8,
                    "application/json"
                )
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "SignalR progress notification failed with status code {StatusCode}.",
                    response.StatusCode
                );
            }
        }

        private string BuildSendUrl()
        {
            return $"{config.Value.BaseUrl.TrimEnd('/')}{SendPath}";
        }
    }
}
