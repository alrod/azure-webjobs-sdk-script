// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dependencies;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Script.Description;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Filters;
using Microsoft.Azure.WebJobs.Script.WebHost.WebHooks;

namespace Microsoft.Azure.WebJobs.Script.Host
{
    public static class FunctionRequestInvoker
    {
        public static async Task<AuthorizationLevel> DetermineAuthorizationLevelAsync(HttpRequestMessage request, FunctionDescriptor function, IDependencyResolver resolver)
        {
            var secretManager = resolver.GetService<ISecretManager>();
            var authorizationResult = await AuthorizationLevelAttribute.GetAuthorizationResultAsync(request, secretManager, functionName: function.Name);
            var authorizationLevel = authorizationResult.AuthorizationLevel;
            request.SetAuthorizationLevel(authorizationLevel);
            request.SetProperty(ScriptConstants.AzureFunctionsHttpRequestKeyNameKey, authorizationResult.KeyName);

            return authorizationLevel;
        }

        public static async Task<HttpResponseMessage> ProcessRequestAsync(HttpRequestMessage request, FunctionDescriptor function, CancellationToken cancellationToken, WebScriptHostManager scriptHostManager, WebHookReceiverManager webHookReceiverManager)
        {
            var httpTrigger = function.GetTriggerAttributeOrNull<HttpTriggerAttribute>();
            bool isWebHook = !string.IsNullOrEmpty(httpTrigger.WebHookType);
            var authorizationLevel = request.GetAuthorizationLevel();
            HttpResponseMessage response = null;

            if (isWebHook)
            {
                if (request.HasAuthorizationLevel(AuthorizationLevel.Admin))
                {
                    // Admin level requests bypass the WebHook auth pipeline
                    response = await scriptHostManager.HandleRequestAsync(function, request, cancellationToken);
                }
                else
                {
                    // This is a WebHook request so define a delegate for the user function.
                    // The WebHook Receiver pipeline will first validate the request fully
                    // then invoke this callback.
                    Func<HttpRequestMessage, Task<HttpResponseMessage>> invokeFunction = async (req) =>
                    {
                        // Reset the content stream before passing the request down to the function
                        Stream stream = await req.Content.ReadAsStreamAsync();
                        stream.Seek(0, SeekOrigin.Begin);

                        return await scriptHostManager.HandleRequestAsync(function, req, cancellationToken);
                    };
                    response = await webHookReceiverManager.HandleRequestAsync(function, request, invokeFunction);
                }
            }
            else
            {
                // Authorize
                if (!request.HasAuthorizationLevel(httpTrigger.AuthLevel))
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                // Not a WebHook request so dispatch directly
                response = await scriptHostManager.HandleRequestAsync(function, request, cancellationToken);
            }

            return response;
        }
    }
}
