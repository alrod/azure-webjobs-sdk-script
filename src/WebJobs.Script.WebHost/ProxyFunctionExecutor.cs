// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;
using Microsoft.Azure.AppService.Proxy.Client.Contract;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Controllers;
using Microsoft.Azure.WebJobs.Script.WebHost.WebHooks;

namespace Microsoft.Azure.WebJobs.Script.Host
{
    public class ProxyFunctionExecutor : IFuncExecutor
    {
        private readonly WebScriptHostManager _scriptHostManager;
        private readonly IDependencyResolver _dependencyResolver;
        private FunctionsController _functionsController;

        internal ProxyFunctionExecutor(WebScriptHostManager scriptHostManager, IDependencyResolver dependencyResolver, FunctionsController functionsController)
        {
            _scriptHostManager = scriptHostManager;
            _dependencyResolver = dependencyResolver;
            _functionsController = functionsController;
        }

        public async Task ExecuteFuncAsync(string funcName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = arguments["MS_AzureFunctionsHttpRequest"] as HttpRequestMessage;
            var function = _scriptHostManager.GetHttpFunctionOrNull(request);
            if (function == null)
            {
                // request does not map to an HTTP function
                request.Properties["MS_AzureFunctionsHttpResponse"] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }
            request.SetProperty(ScriptConstants.AzureFunctionsHttpFunctionKey, function);

            var authorizationLevel = await FunctionsController.DetermineAuthorizationLevelAsync(request, function, _dependencyResolver);
            if (function.Metadata.IsExcluded ||
               (function.Metadata.IsDisabled && !(request.IsAuthDisabled() || authorizationLevel == AuthorizationLevel.Admin)))
            {
                // disabled functions are not publicly addressable w/o Admin level auth,
                // and excluded functions are also ignored here (though the check above will
                // already exclude them)
                request.Properties["MS_AzureFunctionsHttpResponse"] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> processRequestHandler = async (req, ct) =>
            {
                return await _functionsController.ProcessRequestAsync(req, function, ct);
            };

            var resp = await _scriptHostManager.HttpRequestManager.ProcessRequestAsync(request, processRequestHandler, cancellationToken);
            request.Properties["MS_AzureFunctionsHttpResponse"] = resp;
            return;
        }
    }
}
