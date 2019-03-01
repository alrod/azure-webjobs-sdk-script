// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    public class PlaceholderSpecializationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IScriptWebHostEnvironment _webHostEnvironment;
        private readonly IStandbyManager _standbyManager;
        private readonly IEnvironment _environment;
        private RequestDelegate _invoke;
        private double _specialized = 0;

        public PlaceholderSpecializationMiddleware(RequestDelegate next, IScriptWebHostEnvironment webHostEnvironment,
            IStandbyManager standbyManager, IEnvironment environment)
        {
            _next = next;
            _invoke = InvokeSpecializationCheck;
            _webHostEnvironment = webHostEnvironment;
            _standbyManager = standbyManager;
            _environment = environment;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            await _invoke(httpContext);
        }

        private async Task InvokeSpecializationCheck(HttpContext httpContext)
        {
            if (!_webHostEnvironment.InStandbyMode && _environment.IsContainerReady())
            {
                using (Profiler.Step("PlaceholderSpecializationMiddleware_InvokeSpecializationCheck_standbyManager.SpecializeHostAsync"))
                {
                    await _standbyManager.SpecializeHostAsync();
                }

                using (Profiler.Step("PlaceholderSpecializationMiddleware_InvokeSpecializationCheck_Interlocked.CompareExchange"))
                {
                    if (Interlocked.CompareExchange(ref _specialized, 1, 0) == 0)
                    {
                        Interlocked.Exchange(ref _invoke, _next);
                    }
                }
            }

            using (Profiler.Step("PlaceholderSpecializationMiddleware_InvokeSpecializationCheck_next"))
            {
                await _next(httpContext);
            }
        }
    }
}
