using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace WebJobs.Script.Tests.Perf.Dashboard
{
    public static class RunPerfNightlyTimer
    {
        [FunctionName("RunPerfNightlyTimer")]
        public static async Task Run([TimerTrigger("0 0 14 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            await AppVeyorClient.StartPerf(log);
        }
    }
}
