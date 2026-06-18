namespace Calcpad.WebApi.Configs.SubConfigs
{
    /// <summary>
    /// iEPC Chat configuration
    /// </summary>
    public class IepcChat
    {
        public string Endpoint { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string WorkspaceName { get; set; } = "default";

        /// <summary>
        /// Gets or sets the maximum allowed length for a token.
        /// 100k
        /// </summary>
        public long MaxTokenLenght { get; set; } = 100000;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);
        }
    }
}
