// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Profiling;
using StackExchange.Profiling.Storage;

namespace Microsoft.Azure.WebJobs.Script
{
    public class Profiler
    {
        private static MiniProfiler mp;
        private static object lockObject = new object();
        private static ConcurrentDictionary<string, Timing> dic = new ConcurrentDictionary<string, Timing>();

        private static MiniProfiler Instance
        {
            get
            {
                if (mp == null)
                {
                    lock (lockObject)
                    {
                        if (mp != null)
                        {
                            return mp;
                        }

                        // Configure Miniprofiler
                        string connectionString = Environment.GetEnvironmentVariable("MiniProfilerConnection");
                        var mpOptions = new MiniProfilerOptions();
                        var storage = new SqlServerStorage(connectionString,
                                "MPTest",
                                "MPTimingsTest",
                                "MPClientTimingsTest");
                        mpOptions.Storage = storage;
                        MiniProfiler.Configure(mpOptions);

                        mp = MiniProfiler.StartNew("MiniProfiler" + Guid.NewGuid().ToString());
                        new Thread(async () =>
                        {
                            do
                            {
                                Thread.CurrentThread.IsBackground = true;
                                await Task.Delay(60000);
                                await mp.StopAsync();
                                await MiniProfiler.Current.Options.Storage.SaveAsync(MiniProfiler.Current);
                                mp = MiniProfiler.StartNew("MiniProfiler" + Guid.NewGuid().ToString());
                            }
                            while (true);
                        }).Start();
                    }
                }
                return mp;
            }
        }

        public static Timing Step(string name)
        {
            return Instance.Step(name);
        }

        public static Timing StartTiming(string name)
        {
            Timing timing = Instance.Step(name);
            dic.GetOrAdd(name, timing);
            return timing;
        }

        public static void FinishTiming(string key)
        {
            if (dic.ContainsKey(key))
            {
                dic[key].Stop();
                dic.TryRemove(key, out Timing outValue);
            }
        }
    }
}
