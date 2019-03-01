// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    /// <summary>
    /// Middleware registered early in the request pipeline to check host
    /// environment and delay requests as necessary.
    /// </summary>
    public partial class EnvironmentReadyCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public EnvironmentReadyCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, IScriptWebHostEnvironment webHostEnvironment)
        {
            using (Profiler.Step("EnvironmentReadyCheckMiddleware_Invoke_Main"))
            {
                if (webHostEnvironment.DelayRequestsEnabled)
                {
                    await webHostEnvironment.DelayCompletionTask;
                }
            }

            using (Profiler.Step("EnvironmentReadyCheckMiddleware_Invoke_next"))
            {
                await _next.Invoke(httpContext);
            }
        }
    }
}