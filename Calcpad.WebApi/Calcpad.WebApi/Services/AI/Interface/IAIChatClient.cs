using Microsoft.Extensions.AI;

namespace Calcpad.WebApi.Services.AI.Interface
{
    public interface IAIChatClient : IChatClient
    {
        /// <summary>
        /// 支持的最大 token 数
        /// </summary>
        long MaxTokenLength { get; }

        bool IsAvailable { get; }
    }
}
