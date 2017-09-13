// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Management
{
    public class ZipFileSystem : VirtualFileSystemBase
    {
        public ZipFileSystem(WebHostSettings settings) : base(settings)
        {
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(HttpRequest request, FileSystemInfo info, string localFilePath)
        {
            throw new NotImplementedException();
        }

        protected override Task<HttpResponseMessage> CreateItemPutResponse(HttpRequest request, FileSystemInfo info, string localFilePath, bool itemExists)
        {
            throw new NotImplementedException();
        }
    }
}