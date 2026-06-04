using System.Security.Claims;
using Calcpad.WebApi.Configs;
using Calcpad.WebApi.Utils.Web.Service;
using Calcpad.WebApi.Utils.Web.Token;

namespace Calcpad.WebApi.Api.SignalR
{
    public class SignalRApiTokenCache(
        AppSettings<SignalRApiConfig> config,
        ILogger<SignalRApiTokenCache> logger
    ) : ISingletonService
    {
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(1);
        private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(5);
        private static readonly List<Claim> Claims =
        [
            new("userId", "dotnetApi")
        ];

        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private string _token = string.Empty;
        private DateTime _expiresAtUtc = DateTime.MinValue;

        public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!ShouldRefresh())
                return _token;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!ShouldRefresh())
                    return _token;

                var secret = config.Value.Secret;
                if (string.IsNullOrWhiteSpace(secret))
                {
                    logger.LogWarning(
                        "SignalRApi:Secret is empty. SignalR API token was not generated."
                    );
                    _token = string.Empty;
                    _expiresAtUtc = DateTime.MinValue;
                    return _token;
                }

                _expiresAtUtc = DateTime.UtcNow.Add(TokenLifetime);
                _token = new TokenParamsConfig
                {
                    Secret = secret,
                    ExpireDate = _expiresAtUtc,
                    Issuer = "iepc-dotnet",
                    Audience = "iepc-dotnet"
                }.CreateToken(Claims);

                return _token;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private bool ShouldRefresh()
        {
            return string.IsNullOrWhiteSpace(_token)
                || DateTime.UtcNow >= _expiresAtUtc.Subtract(RefreshBeforeExpiry);
        }
    }
}
