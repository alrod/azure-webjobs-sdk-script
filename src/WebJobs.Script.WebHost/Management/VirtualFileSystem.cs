// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Management
{
    public class VirtualFileSystem : VirtualFileSystemBase
    {
        public VirtualFileSystem(WebHostSettings settings)
            : base(settings)
        {
        }

        protected override Task<HttpResponseMessage> CreateDirectoryPutResponse(DirectoryInfo info, string localFilePath)
        {
            if (info != null && info.Exists)
            {
                // Return a conflict result
                return base.CreateDirectoryPutResponse(info, localFilePath);
            }

            try
            {
                info.Create();
            }
            catch (IOException ex)
            {
                // TODO: log ex
                HttpResponseMessage conflictDirectoryResponse = CreateResponse(HttpStatusCode.Conflict);
                return Task.FromResult(conflictDirectoryResponse);
            }

            // Return 201 Created response
            var successFileResponse = CreateResponse(HttpStatusCode.Created);
            return Task.FromResult(successFileResponse);
        }

        protected override Task<HttpResponseMessage> CreateItemGetResponse(HttpRequest request, FileSystemInfo info, string localFilePath)
        {
            // Get current etag
            var currentEtag = CreateEntityTag(info);
            var lastModified = info.LastWriteTimeUtc;

            // Check whether we have a range request (taking If-Range condition into account)
            bool isRangeRequest = IsRangeRequest(request, currentEtag);

            // Check whether we have a conditional If-None-Match request
            // Unless it is a range request (see RFC2616 sec 14.35.2 Range Retrieval Requests)
            if (!isRangeRequest && IsIfNoneMatchRequest(request, currentEtag))
            {
                var notModifiedResponse = CreateResponse(HttpStatusCode.NotModified);
                notModifiedResponse.SetEntityTagHeader(currentEtag, lastModified);
                return Task.FromResult(notModifiedResponse);
            }

            // Generate file response
            Stream fileStream = null;
            try
            {
                fileStream = GetFileReadStream(localFilePath);
                var mediaType = MediaTypeMap.GetMediaType(info.Extension);
                var successFileResponse = CreateResponse(isRangeRequest ? HttpStatusCode.PartialContent : HttpStatusCode.OK);

                if (isRangeRequest)
                {
                    var typedHeaders = request.GetTypedHeaders();
                    var rangeHeader = new RangeHeaderValue
                    {
                        Unit = typedHeaders.Range.Unit.Value
                    };

                    foreach (var range in typedHeaders.Range.Ranges)
                    {
                        rangeHeader.Ranges.Add(new RangeItemHeaderValue(range.From, range.To));
                    }

                    successFileResponse.Content = new ByteRangeStreamContent(fileStream, rangeHeader, mediaType, BufferSize);
                }
                else
                {
                    successFileResponse.Content = new StreamContent(fileStream, BufferSize);
                    successFileResponse.Content.Headers.ContentType = mediaType;
                }

                // Set etag for the file
                successFileResponse.SetEntityTagHeader(currentEtag, lastModified);
                return Task.FromResult(successFileResponse);
            }
            catch (InvalidByteRangeException invalidByteRangeException)
            {
                // The range request had no overlap with the current extend of the resource so generate a 416 (Requested Range Not Satisfiable)
                // including a Content-Range header with the current size.
                // TODO: log?
                var invalidByteRangeResponse = CreateResponse(HttpStatusCode.RequestedRangeNotSatisfiable, invalidByteRangeException);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(invalidByteRangeResponse);
            }
            catch (Exception ex)
            {
                // Could not read the file
                // TODO: log?
                var errorResponse = CreateResponse(HttpStatusCode.NotFound, ex);
                if (fileStream != null)
                {
                    fileStream.Close();
                }
                return Task.FromResult(errorResponse);
            }
        }

        protected override async Task<HttpResponseMessage> CreateItemPutResponse(HttpRequest request, FileSystemInfo info, string localFilePath, bool itemExists)
        {
            // Check that we have a matching conditional If-Match request for existing resources
            if (itemExists)
            {
                // Get current etag
                var currentEtag = CreateEntityTag(info);
                var typedHeaders = request.GetTypedHeaders();
                // Existing resources require an etag to be updated.
                if (typedHeaders.IfMatch == null)
                {
                    var missingIfMatchResponse = CreateResponse(HttpStatusCode.PreconditionFailed, "missing If-Match");
                    return missingIfMatchResponse;
                }

                bool isMatch = false;
                foreach (var etag in typedHeaders.IfMatch)
                {
                    if (currentEtag.Equals(etag) || etag == Microsoft.Net.Http.Headers.EntityTagHeaderValue.Any)
                    {
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                {
                    var conflictFileResponse = CreateResponse(HttpStatusCode.PreconditionFailed, "Etag mismatch");
                    conflictFileResponse.Headers.ETag = currentEtag;
                    return conflictFileResponse;
                }
            }

            // Save file
            try
            {
                using (Stream fileStream = GetFileWriteStream(localFilePath, fileExists: itemExists))
                {
                    try
                    {
                        await request.Body.CopyToAsync(fileStream);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log ex
                        var conflictResponse = CreateResponse(HttpStatusCode.Conflict, ex);
                        return conflictResponse;
                    }
                }

                // Return either 204 No Content or 201 Created response
                var successFileResponse = CreateResponse(itemExists ? HttpStatusCode.NoContent : HttpStatusCode.Created);

                // Set updated etag for the file
                info.Refresh();
                successFileResponse.SetEntityTagHeader(CreateEntityTag(info), info.LastWriteTimeUtc);
                return successFileResponse;
            }
            catch (Exception ex)
            {
                // TODO: log ex
                var errorResponse = CreateResponse(HttpStatusCode.Conflict, ex);
                return errorResponse;
            }
        }

        protected override Task<HttpResponseMessage> CreateFileDeleteResponse(HttpRequest request, FileInfo info)
        {
            // Existing resources require an etag to be updated.
            var typedHeaders = request.GetTypedHeaders();
            if (typedHeaders.IfMatch == null)
            {
                var conflictDirectoryResponse = CreateResponse(HttpStatusCode.PreconditionFailed, "Missing If-Match");
                return Task.FromResult(conflictDirectoryResponse);
            }

            // Get current etag
            var currentEtag = CreateEntityTag(info);
            var isMatch = typedHeaders.IfMatch.Any(etag => etag == Microsoft.Net.Http.Headers.EntityTagHeaderValue.Any || currentEtag.Equals(etag));

            if (!isMatch)
            {
                var conflictFileResponse = CreateResponse(HttpStatusCode.PreconditionFailed, "Etag mismatch");
                conflictFileResponse.Headers.ETag = currentEtag;
                return Task.FromResult(conflictFileResponse);
            }

            return base.CreateFileDeleteResponse(request, info);
        }

        /// <summary>
        /// Create unique etag based on the last modified UTC time
        /// </summary>
        private static EntityTagHeaderValue CreateEntityTag(FileSystemInfo sysInfo)
        {
            if (sysInfo == null)
            {
                throw new ArgumentNullException(nameof(sysInfo));
            }

            var etag = BitConverter.GetBytes(sysInfo.LastWriteTimeUtc.Ticks);

            var result = new StringBuilder(2 + (etag.Length * 2));
            result.Append("\"");
            foreach (byte b in etag)
            {
                result.AppendFormat("{0:x2}", b);
            }
            result.Append("\"");
            return new EntityTagHeaderValue(result.ToString());
        }
    }
}