﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Azure.WebJobs.Script.Eventing.Rpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Profiling;

namespace Microsoft.Azure.WebJobs.Script.Rpc
{
    // Implementation for the grpc service
    // TODO: move to WebJobs.Script.Grpc package and provide event stream abstraction
    internal class FunctionRpcService : FunctionRpc.FunctionRpcBase
    {
        private readonly IScriptEventManager _eventManager;
        private readonly ILogger _logger;
        private SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        public FunctionRpcService(IScriptEventManager eventManager, ILoggerFactory loggerFactory)
        {
            _eventManager = eventManager;
            _logger = loggerFactory.CreateLogger(ScriptConstants.LogCategoryFunctionRpcService);
        }

        public override async Task EventStream(IAsyncStreamReader<StreamingMessage> requestStream, IServerStreamWriter<StreamingMessage> responseStream, ServerCallContext context)
        {
            var cancelSource = new TaskCompletionSource<bool>();
            IDisposable outboundEventSubscription = null;
            try
            {
                context.CancellationToken.Register(() => cancelSource.TrySetResult(false));

                Func<Task<bool>> messageAvailable = async () =>
                {
                    // GRPC does not accept cancellation tokens for individual reads, hence wrapper
                    var requestTask = requestStream.MoveNext(CancellationToken.None);
                    var completed = await Task.WhenAny(cancelSource.Task, requestTask);
                    return completed.Result;
                };

                if (await messageAvailable())
                {
                    string workerId = requestStream.Current.StartStream.WorkerId;
                    outboundEventSubscription = _eventManager.OfType<OutboundEvent>()
                        .Where(evt => evt.WorkerId == workerId)
                        .ObserveOn(NewThreadScheduler.Default)
                        .Subscribe(async evt =>
                        //.SubscribeOn(NewThreadScheduler.Default)
                        //.ObserveOn(CurrentThreadScheduler.Instance)
                        //.ObserveOn(CurrentThreadScheduler.Instance)
                        //.ObserveOn(TaskPoolScheduler.Default.DisableOptimizations())
                        //.ObserveOn(SynchronizationContext.Current) // failed
                        //.ObserveOn(Scheduler.ThreadPool)
                        {
                            try
                            {
                                string invocationId = string.Empty;
                                if (evt.Message.InvocationRequest != null)
                                {
                                    invocationId = evt.Message.InvocationRequest.InvocationId;
                                }
                                else if (evt.Message.InvocationResponse != null)
                                {
                                    invocationId = evt.Message.InvocationRequest.InvocationId;
                                }

                                if (!string.IsNullOrEmpty(invocationId))
                                {
                                    WorkerLanguageInvoker.Dic[invocationId].Stop();
                                    Timing outValue;
                                    WorkerLanguageInvoker.Dic.TryRemove(invocationId, out outValue);
                                }

                                using (WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})EventStream1:" + evt.Message.ToString()))
                                {
                                    // WriteAsync only allows one pending write at a time
                                    // For each responseStream subscription, observe as a blocking write, in series, on a new thread
                                    // Alternatives - could wrap responseStream.WriteAsync with a SemaphoreSlim to control concurrent access

                                    using (WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})EventStream1: await slim "))
                                    {
                                        await _writeLock.WaitAsync();
                                    }
                                    using (WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})EventStream1: write async "))
                                    {
                                        try
                                        {
                                            if (!string.IsNullOrEmpty(invocationId))
                                            {
                                                WorkerLanguageInvoker.Dic.TryAdd(invocationId, WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})WorkerTime:"));
                                            }

                                            await responseStream.WriteAsync(evt.Message);
                                        }
                                        finally
                                        {
                                            _writeLock.Release();
                                        }
                                    }
                                }
                            }
                            catch (Exception subscribeEventEx)
                            {
                                _logger.LogError(subscribeEventEx, "Error reading message from Rpc channel");
                            }
                        });

                    do
                    {
                        string invocationId = string.Empty;
                        if (requestStream.Current.InvocationRequest != null)
                        {
                            invocationId = requestStream.Current.InvocationRequest.InvocationId;
                        }
                        else if (requestStream.Current.InvocationResponse != null)
                        {
                            invocationId = requestStream.Current.InvocationResponse.InvocationId;
                        }
                        using (WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})EventStream2:" + requestStream.Current.ToString()))
                        {
                        }

                        if (!string.IsNullOrEmpty(invocationId))
                        {
                            WorkerLanguageInvoker.Dic[invocationId].Stop();
                            Timing outValue;
                            WorkerLanguageInvoker.Dic.TryRemove(invocationId, out outValue);
                        }
                        using (WorkerLanguageInvoker.MPInstance().Step($"({Thread.CurrentThread.ManagedThreadId}===={invocationId})EventStream2:" + requestStream.Current.ToString()))
                        {
                            _eventManager.Publish(new InboundEvent(workerId, requestStream.Current));
                        }
                    }
                    while (await messageAvailable());
                }
            }
            finally
            {
                outboundEventSubscription?.Dispose();

                // ensure cancellationSource task completes
                cancelSource.TrySetResult(false);
            }
        }
    }
}
