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
        private WebHookReceiverManager _webHookReceiverManager;

        internal ProxyFunctionExecutor(WebScriptHostManager scriptHostManager, IDependencyResolver dependencyResolver, WebHookReceiverManager webHookReceiverManager)
        {
            _scriptHostManager = scriptHostManager;
            _webHookReceiverManager = webHookReceiverManager;
            _dependencyResolver = dependencyResolver;
        }

        public async Task ExecuteFuncAsync(string funcName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            HttpRequestMessage request = arguments[ScriptConstants.AzureFunctionsHttpRequestKey] as HttpRequestMessage;
            var function = _scriptHostManager.GetHttpFunctionOrNull(request);
            if (function == null)
            {
                // request does not map to an HTTP function
                request.Properties[ScriptConstants.AzureFunctionsHttpResponseKey] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }
            request.SetProperty(ScriptConstants.AzureFunctionsHttpFunctionKey, function);

            var authorizationLevel = await FunctionRequestInvoker.DetermineAuthorizationLevelAsync(request, function, _dependencyResolver);
            if (function.Metadata.IsExcluded ||
               (function.Metadata.IsDisabled && !(request.IsAuthDisabled() || authorizationLevel == AuthorizationLevel.Admin)))
            {
                // disabled functions are not publicly addressable w/o Admin level auth,
                // and excluded functions are also ignored here (though the check above will
                // already exclude them)
                request.Properties[ScriptConstants.AzureFunctionsHttpResponseKey] = new HttpResponseMessage(HttpStatusCode.NotFound);
                return;
            }

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> processRequestHandler = async (req, ct) =>
            {
                return await FunctionRequestInvoker.ProcessRequestAsync(req, function, ct, _scriptHostManager, _webHookReceiverManager);
            };

            var resp = await _scriptHostManager.HttpRequestManager.ProcessRequestAsync(request, processRequestHandler, cancellationToken);
            request.Properties[ScriptConstants.AzureFunctionsHttpResponseKey] = resp;
            return;
        }
    }
}
