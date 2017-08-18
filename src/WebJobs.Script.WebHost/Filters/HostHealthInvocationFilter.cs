// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.WebHost.Scale;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Filters
{
    internal class HostHealthInvocationFilter : IFunctionInvocationFilter
    {
        private readonly Action _restartHost;
        private readonly HostPerformanceManager _hostPerformanceManager;

        public HostHealthInvocationFilter(Action restartHost, HostPerformanceManager hostPerformanceManager)
        {
            _restartHost = restartHost;
            _hostPerformanceManager = hostPerformanceManager;
        }

        public Task OnExecutingAsync(FunctionExecutingContext executingContext, CancellationToken cancellationToken)
        {
            if (_hostPerformanceManager.IsUnderHighLoad())
            {
                _restartHost();
            }

            return Task.CompletedTask;
        }

        public Task OnExecutedAsync(FunctionExecutedContext executedContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}