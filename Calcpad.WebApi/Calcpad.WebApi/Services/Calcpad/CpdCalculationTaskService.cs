using System.Collections.Concurrent;
using Calcpad.Document;
using Calcpad.WebApi.Api.SignalR;
using Calcpad.WebApi.Utils.Web.Service;

namespace Calcpad.WebApi.Services.Calcpad
{
    /// <summary>
    /// 管理正在执行的 Calcpad 计算任务
    /// </summary>
    /// <param name="httpApiService"></param>
    public class CpdCalculationTaskService(HttpApiService httpApiService) : ISingletonService
    {
        private readonly ConcurrentDictionary<string, CpdExecutor> _runningExecutors = new();

        /// <summary>
        /// 注册正在执行的计算任务
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="executor"></param>
        public void RegisterCalculationTask(string uniqueId, CpdExecutor executor)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                return;

            // 同一个文件只保留一个运行任务，新任务开始前终止旧任务
            if (_runningExecutors.TryGetValue(uniqueId, out var oldExecutor))
                oldExecutor.CancelCalculation();

            _runningExecutors[uniqueId] = executor;
        }

        /// <summary>
        /// 移除已经结束的计算任务
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="executor"></param>
        public async Task RemoveCalculationTaskAsync(string uniqueId, CpdExecutor executor)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                return;

            if (_runningExecutors.TryGetValue(uniqueId, out var currentExecutor)
                && ReferenceEquals(currentExecutor, executor))
            {
                _runningExecutors.TryRemove(uniqueId, out _);
                if (executor.IsCancellationRequested)
                    await httpApiService.SendCalculationProgressAsync(uniqueId, 1, "calculation task cancelled");
            }
        }

        /// <summary>
        /// 取消指定计算任务
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <returns></returns>
        public async Task<bool> CancelCalculationTaskAsync(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                return false;

            if (!_runningExecutors.TryGetValue(uniqueId, out var executor))
                return false;

            executor.CancelCalculation();
            return true;
        }
    }
}
