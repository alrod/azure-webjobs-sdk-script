// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Script.Workers.Rpc;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Workers
{
    public class WorkerHealthTrottlerProvider : IHostThrottleProvider
    {
        private const int MinSampleCount = 5;
        private const float _maxLatencyThresholdInTicks = 10000 * 30; // 10000 ticks = 1 ms
        private readonly IFunctionInvocationDispatcherFactory _functionInvocationDispatcherFactory;

        public WorkerHealthTrottlerProvider(IFunctionInvocationDispatcherFactory functionInvocationDispatcherFactory)
        {
            _functionInvocationDispatcherFactory = functionInvocationDispatcherFactory;
        }

        public HostThrottleResult GetStatus(ILogger logger = null)
        {
            ThrottleState throttleState = ThrottleState.Unknown;

            IFunctionInvocationDispatcher functionInvocationDispatcher = _functionInvocationDispatcherFactory.GetFunctionDispatcher();
            // TODO: add a new member to IFunctionInvocationDispatcherFactory to get channels?
            RpcFunctionInvocationDispatcher rpcFunctionInvocationDispatcher = functionInvocationDispatcher as RpcFunctionInvocationDispatcher;

            if (rpcFunctionInvocationDispatcher != null)
            {
                // TODO: male the method async
                var channels = rpcFunctionInvocationDispatcher.GetInitializedWorkerChannelsAsync().GetAwaiter().GetResult();
                foreach (IRpcWorkerChannel workerChannel in channels)
                {
                    var workerChannelStats = workerChannel.GetStats();
                    var latencyStatsCount = workerChannelStats.LatencyHistory.Count();
                    if (latencyStatsCount > MinSampleCount)
                    {
                        var samples = workerChannelStats.LatencyHistory.Skip(latencyStatsCount - MinSampleCount).Take(MinSampleCount).Select(x => x.Ticks);
                        var latecyAverage = samples.Average();

                        if (logger != null)
                        {
                            string formattedLoadHistory = string.Join(",", samples);
                            logger?.LogInformation($"Latency Stats: (Avg. {latecyAverage}) {formattedLoadHistory}");
                        }

                        if (latecyAverage >= _maxLatencyThresholdInTicks)
                        {
                            throttleState = ThrottleState.Enabled;
                        }
                        else
                        {
                            throttleState = ThrottleState.Disabled;
                        }
                    }
                }
            }
            var result = new HostThrottleResult
            {
                ThrottleState = throttleState
            };

            return result;
        }
    }
}
