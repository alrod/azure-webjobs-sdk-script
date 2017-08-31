// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dependencies;
using Microsoft.Azure.AppService.Proxy.Client.Contract;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.Host;
using Microsoft.Azure.WebJobs.Script.WebHost.Filters;
using Microsoft.Azure.WebJobs.Script.WebHost.WebHooks;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Controllers
{
    /// <summary>
    /// Controller responsible for handling all http function invocations.
    /// </summary>
    public class FunctionsController : ApiController
    {
        private readonly WebScriptHostManager _scriptHostManager;
        private WebHookReceiverManager _webHookReceiverManager;

        public FunctionsController(WebScriptHostManager scriptHostManager, WebHookReceiverManager webHookReceiverManager)
        {
            _scriptHostManager = scriptHostManager;
            _webHookReceiverManager = webHookReceiverManager;
        }

        public override async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            var request = controllerContext.Request;
            var function = _scriptHostManager.GetHttpFunctionOrNull(request);
            if (function == null)
            {
                // request does not map to an HTTP function
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            request.SetProperty(ScriptConstants.AzureFunctionsHttpFunctionKey, function);

            var authorizationLevel = await FunctionRequestInvoker.DetermineAuthorizationLevelAsync(request, function, controllerContext.Configuration.DependencyResolver);
            if (function.Metadata.IsExcluded ||
               (function.Metadata.IsDisabled && !(request.IsAuthDisabled() || authorizationLevel == AuthorizationLevel.Admin)))
            {
                // disabled functions are not publicly addressable w/o Admin level auth,
                // and excluded functions are also ignored here (though the check above will
                // already exclude them)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> processRequestHandler = async (req, ct) =>
            {
                return await FunctionRequestInvoker.ProcessRequestAsync(req, function, ct, _scriptHostManager, _webHookReceiverManager);
            };

            IFuncExecutor proxyFunctionExecutor = new ProxyFunctionExecutor(this._scriptHostManager, controllerContext.Configuration.DependencyResolver, _webHookReceiverManager);
            request.Properties.Add(ScriptConstants.AzureProxyFunctionExecutorKey, proxyFunctionExecutor);
            return await _scriptHostManager.HttpRequestManager.ProcessRequestAsync(request, processRequestHandler, cancellationToken);
        }
    }
}
