// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Azure.WebJobs.Script.Workers.Rpc;

namespace Microsoft.Azure.WebJobs.Script.Workers
{
    public class WorkerMonitor : IDisposable
    {
        private readonly IRpcWorkerChannel _rpcWorkerChannel;
        private readonly List<WorkerStatus> _workerStatsHistory = new List<WorkerStatus>();

        internal const int DefaultSampleIntervalSeconds = 1;
        private bool _disposed = false;
        private Timer _timer;
        internal const int SampleHistorySize = 10;
        private object _syncLock = new object();

        public WorkerMonitor(IRpcWorkerChannel rpcWorkerChannel)
        {
            _rpcWorkerChannel = rpcWorkerChannel;
        }

        public virtual void Start()
        {
            _timer = new Timer(OnTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public virtual WorkerStats GetStats()
        {
            WorkerStats stats = null;
            lock (_syncLock)
            {
                stats = new WorkerStats
                {
                    LatencyHistory = _workerStatsHistory.Select(x => x.Latency)
                };
            }
            return stats;
        }

        private async void OnTimer(object state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                WorkerStatus workerStatus = await _rpcWorkerChannel.GetWorkerStatusAsync();
                AddSample(_workerStatsHistory, workerStatus);
            }
            catch
            {
                // don't allow background exceptions to escape
            }
        }

        private void AddSample(List<WorkerStatus> samples, WorkerStatus sample)
        {
            lock (_syncLock)
            {
                if (samples.Count == SampleHistorySize)
                {
                    samples.RemoveAt(0);
                }
                samples.Add(sample);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
