// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.Binding;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Management;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    public class VirtualFileSystemMiddleware : IMiddleware
    {
        private readonly VirtualFileSystemBase _vfs;
        private readonly VirtualFileSystemBase _zip;

        public VirtualFileSystemMiddleware(VirtualFileSystem vfs, ZipFileSystem zip)
        {
            _vfs = vfs;
            _zip = zip;
        }

        public static bool IsVirtualFileSystemRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments("/admin/vfs") ||
                context.Request.Path.StartsWithSegments("/admin/zip");
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate _)
        {
            var handler = context.Request.Path.StartsWithSegments("/admin/vfs") ? _vfs : _zip;
            HttpResponseMessage response;
            switch (context.Request.Method.ToLowerInvariant())
            {
                case "get":
                    response = await handler.GetItem(context.Request);
                    break;

                case "put":
                    response = await handler.PutItem(context.Request);
                    break;

                case "delete":
                    response = await handler.DeleteItem(context.Request);
                    break;

                default:
                    response = new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
                    break;
            }

            context.Response.StatusCode = (int)response.StatusCode;

            var content = response.Content == null ? null : await response.Content.ReadAsStreamAsync();

            // write response headers
            context.Response.Headers.AddRange(response.Headers.ToCoreHeaders());

            if (response.Content != null)
            {
                context.Response.Headers.AddRange(response.Content.Headers.ToCoreHeaders());
                await response.Content.CopyToAsync(context.Response.Body);
            }
        }
    }
}